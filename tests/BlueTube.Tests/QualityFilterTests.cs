using System.Net;
using System.Text.Json.Nodes;
using BlueTube.Core.Models;
using BlueTube.Core.Services;
using BlueTube.Tests.Fakes;

namespace BlueTube.Tests;

public class QualityFilterTests
{
    private FakeHttpHandler _http = null!;
    private FakeSecretStore _secrets = null!;
    private FakeAppSettings _settings = null!;
    private LlmQualityFilter _filter = null!;

    private static readonly Video Sample = new()
    {
        Id = "v1", Title = "ECS in Unity 6", ChannelTitle = "Dev Channel",
        Description = "Deep dive into DOTS.", DurationSeconds = 1200,
    };

    [SetUp]
    public void SetUp()
    {
        _http = new FakeHttpHandler();
        _secrets = new FakeSecretStore();
        _secrets.Values[SecretNames.LlmApiKey] = "sk-test";
        _settings = new FakeAppSettings { LlmModel = "claude-opus-5-5" };
        _filter = new LlmQualityFilter(new HttpClient(_http), _secrets, _settings);
    }

    private static string Reply(string text, string stop = "end_turn") =>
        new JsonObject
        {
            ["stop_reason"] = stop,
            ["content"] = new JsonArray(
                new JsonObject { ["type"] = "thinking", ["thinking"] = "" },
                new JsonObject { ["type"] = "text", ["text"] = text }),
        }.ToJsonString();

    [Test]
    public async Task Score_ParsesStructuredReply_AndSendsCorrectRequest()
    {
        _http.On("api.anthropic.com", Reply("""{"score": 87, "tags": ["unity", "Deep-Dive", "made-up"]}"""));

        var result = await _filter.ScoreAsync(Sample);

        Assert.That(result.Score, Is.EqualTo(87));
        Assert.That(result.Tags, Is.EqualTo(new[] { "unity", "deep-dive" }), "Unknown tags are dropped, case is normalized.");

        var req = _http.LastRequest!;
        Assert.That(req.Headers.GetValues("x-api-key").Single(), Is.EqualTo("sk-test"));
        Assert.That(req.Headers.GetValues("anthropic-version").Single(), Is.EqualTo("2026-10-21"));

        var body = JsonNode.Parse(_http.Bodies.Single())!;
        Assert.That(body["model"]!.GetValue<string>(), Is.EqualTo("claude-opus-5-5"));
        Assert.That(body["output_config"]!["effort"]!.GetValue<string>(), Is.EqualTo("low"));
        Assert.That(body["output_config"]!["format"]!["type"]!.GetValue<string>(), Is.EqualTo("json_schema"));
        Assert.That(body["thinking"], Is.Null, "Opus 5.5 rejects disabled thinking; leave it out.");
        Assert.That(body["messages"]![0]!["content"]!.GetValue<string>(), Does.Contain("ECS in Unity 6").And.Contain("Deep dive into DOTS."));
    }

    [Test]
    public async Task Score_ClampsOutOfRange()
    {
        _http.On("api.anthropic.com", Reply("""{"score": 140, "tags": []}"""));
        Assert.That((await _filter.ScoreAsync(Sample)).Score, Is.EqualTo(100));
    }

    [Test]
    public async Task Score_EmptyModelSetting_UsesDefault()
    {
        _settings.LlmModel = "";
        _http.On("api.anthropic.com", Reply("""{"score": 10, "tags": []}"""));
        await _filter.ScoreAsync(Sample);
        Assert.That(JsonNode.Parse(_http.Bodies.Single())!["model"]!.GetValue<string>(), Is.EqualTo(LlmQualityFilter.DefaultModel));
    }

    [Test]
    public async Task Score_Refusal_GivesNeutralScore()
    {
        _http.On("api.anthropic.com", """{"stop_reason":"refusal","content":[]}""");
        var result = await _filter.ScoreAsync(Sample);
        Assert.That(result.Score, Is.EqualTo(LlmQualityFilter.DeclinedScore));
        Assert.That(result.Tags, Is.Empty);
    }

    [Test]
    public void Score_BadJson_ThrowsUnknown()
    {
        _http.On("api.anthropic.com", Reply("not json"));
        var e = Assert.ThrowsAsync<QualityFilterException>(() => _filter.ScoreAsync(Sample));
        Assert.That(e!.Error, Is.EqualTo(QualityFilterError.Unknown));
    }

    [Test]
    public void Score_NoKey_ThrowsMissingKey_NoRequest()
    {
        _secrets.Values.Clear();
        var e = Assert.ThrowsAsync<QualityFilterException>(() => _filter.ScoreAsync(Sample));
        Assert.That(e!.Error, Is.EqualTo(QualityFilterError.MissingKey));
        Assert.That(_http.Requests, Is.Empty);
    }

    [TestCase(HttpStatusCode.Unauthorized, QualityFilterError.InvalidKey)]
    [TestCase(HttpStatusCode.TooManyRequests, QualityFilterError.RateLimited)]
    [TestCase((HttpStatusCode)529, QualityFilterError.Unavailable)]
    [TestCase(HttpStatusCode.BadRequest, QualityFilterError.Unknown)]
    public void Score_HttpErrors_AreMapped(HttpStatusCode status, QualityFilterError expected)
    {
        _http.On("api.anthropic.com", """{"type":"error","error":{"type":"x","message":"nope"}}""", status);
        var e = Assert.ThrowsAsync<QualityFilterException>(() => _filter.ScoreAsync(Sample));
        Assert.That(e!.Error, Is.EqualTo(expected));
        Assert.That(e.Message, Is.EqualTo("nope"));
    }
}

public class ScoringTests
{
    private FakeVideoRepository _repo = null!;
    private FakeQualityFilter _quality = null!;
    private FeedService _feed = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new FakeVideoRepository();
        _quality = new FakeQualityFilter();
        _feed = new FeedService(new FakeYouTubeService(), _repo, _quality);
        for (var i = 0; i < 10; i++)
            _repo.Videos[$"v{i}"] = new Video { Id = $"v{i}", PublishedAt = new DateTime(2026, 1, i + 1) };
    }

    [Test]
    public async Task ScoreNew_ScoresEachVideoOnce()
    {
        _quality.Results["v3"] = new QualityResult(12, ["news"]);

        var first = await _feed.ScoreNewAsync();
        var second = await _feed.ScoreNewAsync();

        Assert.That(first.Scored, Is.EqualTo(10));
        Assert.That(second.Scored, Is.Zero);
        Assert.That(_quality.Calls, Has.Count.EqualTo(10));
        Assert.That(_repo.Videos["v3"].QualityScore, Is.EqualTo(12));
        Assert.That(_repo.Videos["v3"].AiTagList, Is.EqualTo(new[] { "news" }));
    }

    [Test]
    public async Task ScoreNew_NoKey_IsSkippedNotError()
    {
        _quality.ThrowAll = new QualityFilterException(QualityFilterError.MissingKey, "no key");

        var result = await _feed.ScoreNewAsync();

        Assert.That(result.Skipped, Is.True);
        Assert.That(result.Error, Is.Null);
        Assert.That(_repo.Videos.Values.All(v => v.QualityScore is null));
    }

    [Test]
    public async Task ScoreNew_BadKey_StopsAndReports()
    {
        _quality.ThrowAll = new QualityFilterException(QualityFilterError.InvalidKey, "bad");

        var result = await _feed.ScoreNewAsync();

        Assert.That(result.Error!.Error, Is.EqualTo(QualityFilterError.InvalidKey));
        // At most one call per parallel worker (4). No new calls after the first fatal error.
        Assert.That(_quality.Calls, Has.Count.LessThanOrEqualTo(4), "Must stop early, not call for every video.");
    }

    [Test]
    public async Task ScoreNew_OneBadReply_OthersStillScored()
    {
        _quality.Errors["v5"] = new QualityFilterException(QualityFilterError.Unknown, "bad json");

        var result = await _feed.ScoreNewAsync();

        Assert.That(result.Scored, Is.EqualTo(9));
        Assert.That(result.Error, Is.Null);
        Assert.That(_repo.Videos["v5"].QualityScore, Is.Null);
    }
}
