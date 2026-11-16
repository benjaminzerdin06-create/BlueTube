using BlueTube.Core;
using BlueTube.Core.Models;
using BlueTube.Core.Services;
using BlueTube.Tests.Fakes;

namespace BlueTube.Tests;

public class FeedServiceTests
{
    private FakeYouTubeService _yt = null!;
    private FakeVideoRepository _repo = null!;
    private FeedService _feed = null!;

    [SetUp]
    public void SetUp()
    {
        _yt = new FakeYouTubeService();
        _repo = new FakeVideoRepository();
        _feed = new FeedService(_yt, _repo);
    }

    private static Video V(string id, int day, string channel = "UC1", int? score = null, string aiTags = "", bool isShort = false) => new()
    {
        Id = id,
        ChannelId = channel,
        PublishedAt = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc),
        QualityScore = score,
        AiTags = aiTags,
        IsShort = isShort,
    };

    [Test]
    public async Task Refresh_FirstFetch_StoresAll_ThenIncrementalUsesLatestDate()
    {
        _repo.Channels["UC1"] = new Channel { Id = "UC1", Title = "One" };
        _yt.Videos["UC1"] = [V("a", 1), V("b", 2)];

        var first = await _feed.RefreshAsync();
        Assert.That(first.Added, Is.EqualTo(2));
        Assert.That(_yt.Calls[0].Since, Is.Null);
        Assert.That(_repo.Channels["UC1"].LastFetchedAt, Is.Not.Null);
        Assert.That(_repo.Videos["a"].ChannelTitle, Is.EqualTo("One"));

        _yt.Videos["UC1"].Add(V("c", 3));
        var second = await _feed.RefreshAsync();

        Assert.That(second.Added, Is.EqualTo(1));
        Assert.That(_yt.Calls[1].Since, Is.EqualTo(new DateTime(2026, 1, 2)));
        Assert.That(_repo.Videos.Keys, Is.EquivalentTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public async Task Refresh_DuplicateVideo_KeepsUserFlags()
    {
        _repo.Channels["UC1"] = new Channel { Id = "UC1", Title = "One" };
        var stored = V("a", 5);
        stored.Favorite = true;
        _repo.Videos["a"] = stored;
        // API returns the same video again (same date as latest -> service should not re-add).
        _yt.Videos["UC1"] = [V("a", 5), V("b", 6)];

        var result = await _feed.RefreshAsync();

        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(_repo.Videos["a"].Favorite, Is.True);
    }

    [Test]
    public async Task Refresh_ChannelNotFound_ContinuesWithOthers()
    {
        _repo.Channels["UC1"] = new Channel { Id = "UC1", Title = "Gone" };
        _repo.Channels["UC2"] = new Channel { Id = "UC2", Title = "Ok" };
        _yt.Errors["UC1"] = new YouTubeApiException(YouTubeError.NotFound, "gone");
        _yt.Videos["UC2"] = [V("x", 1, "UC2")];

        var result = await _feed.RefreshAsync();

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(result.ChannelErrors, Has.Count.EqualTo(1).And.Some.Contains("Gone"));
    }

    [Test]
    public async Task Refresh_QuotaExceeded_StopsAndReports()
    {
        _repo.Channels["UC1"] = new Channel { Id = "UC1", Title = "A" };
        _repo.Channels["UC2"] = new Channel { Id = "UC2", Title = "B" };
        _yt.Errors["UC1"] = new YouTubeApiException(YouTubeError.QuotaExceeded, "quota");

        var result = await _feed.RefreshAsync();

        Assert.That(result.Error!.Error, Is.EqualTo(YouTubeError.QuotaExceeded));
        Assert.That(_yt.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddChannel_ResolvesAndStoresTags()
    {
        _yt.Channels["@fireship"] = new Channel { Id = "UCx", Title = "Fireship" };

        var added = await _feed.AddChannelAsync("@fireship", ["WebDev", " js "]);

        Assert.That(added!.Id, Is.EqualTo("UCx"));
        Assert.That(_repo.Channels["UCx"].TagList, Is.EqualTo(new[] { "webdev", "js" }));
        Assert.That(await _feed.AddChannelAsync("@nobody", []), Is.Null);
    }

    [Test]
    public void Apply_Threshold_HidesLowScores_KeepsUnscored()
    {
        var videos = new[] { V("low", 1, score: 30), V("high", 2, score: 80), V("none", 3) };

        var result = FeedService.Apply(videos, new FeedFilter(false, MinScore: 50), new Dictionary<string, IReadOnlyList<string>>());

        Assert.That(result.Select(v => v.Id), Is.EquivalentTo(new[] { "high", "none" }));
    }

    [Test]
    public void Apply_Threshold_IgnoredForSavedLists()
    {
        var videos = new[] { V("low", 1, score: 10) };

        var result = FeedService.Apply(videos, new FeedFilter(false, MinScore: 50, List: FeedList.Favorites), new Dictionary<string, IReadOnlyList<string>>());

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_Tag_MatchesAiTagsOrChannelTags()
    {
        var videos = new[] { V("ai", 1, "UC1", aiTags: "unity,csharp"), V("chan", 2, "UC2"), V("other", 3, "UC3") };
        var channelTags = new Dictionary<string, IReadOnlyList<string>> { ["UC2"] = ["unity"], ["UC3"] = ["hardware"] };

        var result = FeedService.Apply(videos, new FeedFilter(false, Tag: "Unity"), channelTags);

        Assert.That(result.Select(v => v.Id), Is.EquivalentTo(new[] { "ai", "chan" }));
    }

    [Test]
    public async Task GetFeed_SeparatesShortsAndHidesWatched()
    {
        var watched = V("w", 1);
        watched.Watched = true;
        _repo.Videos["w"] = watched;
        _repo.Videos["l"] = V("l", 2);
        _repo.Videos["s"] = V("s", 3, isShort: true);

        var longs = await _feed.GetFeedAsync(new FeedFilter(IsShort: false, HideWatched: true));
        var shorts = await _feed.GetFeedAsync(new FeedFilter(IsShort: true));

        Assert.That(longs.Select(v => v.Id), Is.EqualTo(new[] { "l" }));
        Assert.That(shorts.Select(v => v.Id), Is.EqualTo(new[] { "s" }));
    }

    [TestCase(0, "")]
    [TestCase(65, "1:05")]
    [TestCase(3723, "1:02:03")]
    public void Formatting_Duration(int s, string expected) => Assert.That(Formatting.Duration(s), Is.EqualTo(expected));

    [TestCase(0.5, "now")]
    [TestCase(90, "1h")]
    [TestCase(60 * 24 * 3, "3d")]
    [TestCase(60 * 24 * 30, "4w")]
    [TestCase(60 * 24 * 800, "2y")]
    public void Formatting_Age(double minutesAgo, string expected)
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(Formatting.Age(now.AddMinutes(-minutesAgo), now), Is.EqualTo(expected));
    }
}
