using System.Net;
using BlueTube.Core.Models;
using BlueTube.Core.Services;
using BlueTube.Tests.Fakes;

namespace BlueTube.Tests;

public class YouTubeServiceTests
{
    private const string ChannelId = "UCabcdefghijklmnopqrstuv"; // 24 chars
    private const string Suffix = "abcdefghijklmnopqrstuv";

    private FakeHttpHandler _http = null!;
    private FakeSecretStore _secrets = null!;
    private YouTubeService _yt = null!;

    [SetUp]
    public void SetUp()
    {
        _http = new FakeHttpHandler();
        _secrets = new FakeSecretStore();
        _secrets.Values[SecretNames.YouTubeApiKey] = "test-key";
        _yt = new YouTubeService(new HttpClient(_http), _secrets);
    }

    private static string PlaylistJson(string? next, params (string Id, string Date)[] items) =>
        $$"""
        {
          {{(next is null ? "" : $"\"nextPageToken\": \"{next}\",")}}
          "items": [{{string.Join(",", items.Select(i =>
              $$$"""{"contentDetails":{"videoId":"{{{i.Id}}}","videoPublishedAt":"{{{i.Date}}}"}}"""))}}]
        }
        """;

    private static string VideoJson(string id, string duration, string date = "2026-03-01T10:00:00Z", int w = 0, int h = 0) =>
        $$"""
        {
          "id": "{{id}}",
          "snippet": {
            "title": "Title {{id}}", "description": "Desc", "channelId": "{{ChannelId}}", "channelTitle": "Chan",
            "publishedAt": "{{date}}",
            "thumbnails": { "default": {"url":"d.jpg"}, "high": {"url":"h.jpg"} }
          },
          "contentDetails": { "duration": "{{duration}}" }
          {{(w > 0 ? $$""", "player": {"embedWidth": {{w}}, "embedHeight": {{h}} }""" : "")}}
        }
        """;

    private static string VideosJson(params string[] videos) => $$"""{"items":[{{string.Join(",", videos)}}]}""";

    [TestCase("PT1H2M3S", 3723)]
    [TestCase("PT59S", 59)]
    [TestCase("PT3M", 180)]
    [TestCase("P0D", 0)]
    [TestCase("garbage", 0)]
    [TestCase(null, 0)]
    public void ParseDuration(string? iso, int expected) =>
        Assert.That(YouTubeService.ParseDurationSeconds(iso), Is.EqualTo(expected));

    [TestCase(30, 1080L, 1920L, true)]
    [TestCase(30, 1920L, 1080L, false)]
    [TestCase(180, null, null, true)]
    [TestCase(181, 1080L, 1920L, false)]
    [TestCase(0, 1080L, 1920L, false)]
    public void IsShort_Classification(int seconds, long? w, long? h, bool expected) =>
        Assert.That(YouTubeService.IsShort(seconds, w, h), Is.EqualTo(expected));

    [TestCase("UCabcdefghijklmnopqrstuv", "UCabcdefghijklmnopqrstuv")]
    [TestCase("@fireship", "@fireship")]
    [TestCase("fireship", "@fireship")]
    [TestCase("https://www.youtube.com/@fireship", "@fireship")]
    [TestCase("https://youtube.com/channel/UCabcdefghijklmnopqrstuv", "UCabcdefghijklmnopqrstuv")]
    [TestCase("  ", "@")]
    public void NormalizeChannelInput(string raw, string expected) =>
        Assert.That(YouTubeService.NormalizeChannelInput(raw), Is.EqualTo(expected));

    [Test]
    public async Task ResolveChannel_ByHandle()
    {
        _http.On("channels?", """{"items":[{"id":"CHANNEL","snippet":{"title":"Fireship","publishedAt":"2017-01-01T00:00:00Z","thumbnails":{"medium":{"url":"m.jpg"}}}}]}""".Replace("CHANNEL", ChannelId));

        var channel = await _yt.ResolveChannelAsync("@fireship");

        Assert.That(channel!.Id, Is.EqualTo(ChannelId));
        Assert.That(channel.Title, Is.EqualTo("Fireship"));
        Assert.That(channel.ThumbnailUrl, Is.EqualTo("m.jpg"));
        Assert.That(_http.Requests[0], Does.Contain("forHandle=%40fireship").And.Contain("key=test-key"));
    }

    [Test]
    public async Task ResolveChannel_NotFound_ReturnsNull()
    {
        _http.On("channels?", """{"items":[]}""");
        Assert.That(await _yt.ResolveChannelAsync("@nobody"), Is.Null);
    }

    [Test]
    public async Task GetNewVideos_SplitsLongAndShorts()
    {
        _http.On("playlistId=UULF" + Suffix, PlaylistJson(null, ("L1", "2026-03-02T00:00:00Z")))
             .On("playlistId=UUSH" + Suffix, PlaylistJson(null, ("S1", "2026-03-03T00:00:00Z")))
             .On("id=L1", VideosJson(VideoJson("L1", "PT12M")))
             .On("id=S1", VideosJson(VideoJson("S1", "PT45S")));

        var videos = await _yt.GetNewVideosAsync(new Channel { Id = ChannelId }, since: null);

        var l1 = videos.Single(v => v.Id == "L1");
        var s1 = videos.Single(v => v.Id == "S1");
        Assert.That(l1.IsShort, Is.False);
        Assert.That(l1.DurationSeconds, Is.EqualTo(720));
        Assert.That(l1.ThumbnailUrl, Is.EqualTo("h.jpg"));
        Assert.That(l1.ChannelTitle, Is.EqualTo("Chan"));
        Assert.That(s1.IsShort, Is.True);
    }

    [Test]
    public async Task GetNewVideos_Incremental_StopsAtKnownDate()
    {
        var since = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        _http.On("playlistId=UULF" + Suffix, PlaylistJson("NEXT",
                ("new1", "2026-03-05T00:00:00Z"),
                ("old1", "2026-03-01T00:00:00Z"),
                ("old2", "2026-02-20T00:00:00Z")))
             .On("playlistId=UUSH" + Suffix, PlaylistJson(null))
             .On("id=new1", VideosJson(VideoJson("new1", "PT10M")));

        var videos = await _yt.GetNewVideosAsync(new Channel { Id = ChannelId }, since);

        Assert.That(videos.Select(v => v.Id), Is.EqualTo(new[] { "new1" }));
        Assert.That(_http.Requests, Has.None.Contains("pageToken=NEXT"), "Must not fetch more pages after a known video.");
    }

    [Test]
    public async Task GetNewVideos_Incremental_FollowsPages()
    {
        var since = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _http.On("pageToken=P2", PlaylistJson(null, ("b", "2026-02-01T00:00:00Z"), ("old", "2025-12-01T00:00:00Z")))
             .On("playlistId=UULF" + Suffix, PlaylistJson("P2", ("a", "2026-03-01T00:00:00Z")))
             .On("playlistId=UUSH" + Suffix, PlaylistJson(null))
             .On("videos?", VideosJson(VideoJson("a", "PT5M"), VideoJson("b", "PT6M")));

        var videos = await _yt.GetNewVideosAsync(new Channel { Id = ChannelId }, since);

        Assert.That(videos.Select(v => v.Id), Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task GetNewVideos_PlaylistMissing_FallsBackToUploadsAndShape()
    {
        // No UULF/UUSH routes -> 404. Fallback to UU.
        _http.On("playlistId=UU" + Suffix, PlaylistJson(null, ("v", "2026-03-01T00:00:00Z"), ("s", "2026-03-02T00:00:00Z")))
             .On("videos?", VideosJson(
                 VideoJson("v", "PT2M", w: 1920, h: 1080),
                 VideoJson("s", "PT50S", w: 1080, h: 1920)));

        var videos = await _yt.GetNewVideosAsync(new Channel { Id = ChannelId }, since: null);

        Assert.That(videos.Single(x => x.Id == "v").IsShort, Is.False);
        Assert.That(videos.Single(x => x.Id == "s").IsShort, Is.True);
        Assert.That(_http.Requests.Last(), Does.Contain("player"));
    }

    [Test]
    public async Task GetNewVideos_BatchesDetailsBy50()
    {
        var items = Enumerable.Range(0, 50).Select(i => ($"id{i}", "2026-03-01T00:00:00Z")).ToArray();
        _http.On("playlistId=UULF" + Suffix, PlaylistJson(null, items))
             .On("playlistId=UUSH" + Suffix, PlaylistJson(null, ("s0", "2026-03-01T00:00:00Z")))
             .On("videos?", VideosJson());

        await _yt.GetNewVideosAsync(new Channel { Id = ChannelId }, since: null);

        var videoCalls = _http.Requests.Where(r => r.Contains("/videos?")).ToList();
        Assert.That(videoCalls, Has.Count.EqualTo(2)); // 50 long in one call + 1 short in one call
    }

    [Test]
    public void MissingKey_Throws()
    {
        _secrets.Values.Clear();
        var e = Assert.ThrowsAsync<YouTubeApiException>(() => _yt.ResolveChannelAsync("@x"));
        Assert.That(e!.Error, Is.EqualTo(YouTubeError.MissingKey));
        Assert.That(_http.Requests, Is.Empty);
    }

    [TestCase("quotaExceeded", "Quota", HttpStatusCode.Forbidden, YouTubeError.QuotaExceeded)]
    [TestCase("badRequest", "API key not valid. Please pass a valid API key.", HttpStatusCode.BadRequest, YouTubeError.InvalidKey)]
    [TestCase("other", "x", HttpStatusCode.InternalServerError, YouTubeError.Unknown)]
    public void ApiErrors_AreMapped(string reason, string message, HttpStatusCode status, YouTubeError expected)
    {
        _http.On("channels?", $$$"""{"error":{"code":{{{(int)status}}},"message":"{{{message}}}","errors":[{"reason":"{{{reason}}}"}]}}""", status);
        var e = Assert.ThrowsAsync<YouTubeApiException>(() => _yt.ResolveChannelAsync("@x"));
        Assert.That(e!.Error, Is.EqualTo(expected));
    }
}
