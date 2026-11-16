using BlueTube.Core.Models;
using BlueTube.Core.Services;

namespace BlueTube.Tests;

public class SqliteVideoRepositoryTests
{
    private string _path = "";
    private SqliteVideoRepository _repo = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"bluetube-{Guid.NewGuid():N}.db");
        _repo = new SqliteVideoRepository(_path);
        await _repo.InitAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _repo.DisposeAsync();
        File.Delete(_path);
    }

    private static Video V(string id, string channel = "UC1", int day = 1, bool isShort = false) => new()
    {
        Id = id,
        ChannelId = channel,
        Title = $"Video {id}",
        PublishedAt = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc),
        IsShort = isShort,
    };

    [Test]
    public async Task Channel_Upsert_Get_Delete()
    {
        await _repo.UpsertChannelAsync(new Channel { Id = "UC1", Title = "A", TagList = ["Unity", "csharp"] });
        await _repo.UpsertChannelAsync(new Channel { Id = "UC1", Title = "B", Tags = "unity,csharp" });

        var channels = await _repo.GetChannelsAsync();
        Assert.That(channels, Has.Count.EqualTo(1));
        Assert.That(channels[0].Title, Is.EqualTo("B"));
        Assert.That(channels[0].TagList, Is.EqualTo(new[] { "unity", "csharp" }));

        await _repo.InsertNewVideosAsync([V("v1")]);
        await _repo.DeleteChannelAsync("UC1");

        Assert.That(await _repo.GetChannelAsync("UC1"), Is.Null);
        Assert.That(await _repo.GetVideoAsync("v1"), Is.Null);
    }

    [Test]
    public async Task InsertNewVideos_SkipsExisting_KeepsFlags()
    {
        Assert.That(await _repo.InsertNewVideosAsync([V("v1"), V("v2")]), Is.EqualTo(2));

        var v1 = (await _repo.GetVideoAsync("v1"))!;
        v1.Favorite = true;
        v1.QualityScore = 80;
        await _repo.UpdateVideoAsync(v1);

        var dup = V("v1");
        dup.Title = "changed";
        Assert.That(await _repo.InsertNewVideosAsync([dup, V("v3")]), Is.EqualTo(1));

        var stored = (await _repo.GetVideoAsync("v1"))!;
        Assert.That(stored.Title, Is.EqualTo("Video v1"));
        Assert.That(stored.Favorite, Is.True);
        Assert.That(stored.QualityScore, Is.EqualTo(80));
    }

    [Test]
    public async Task GetVideos_FiltersAndSortsNewestFirst()
    {
        var watched = V("v2", day: 2);
        watched.Watched = true;
        await _repo.InsertNewVideosAsync([V("v1", day: 1), watched, V("v3", day: 3), V("s1", day: 4, isShort: true)]);

        var longs = await _repo.GetVideosAsync(new VideoQuery(IsShort: false));
        Assert.That(longs.Select(v => v.Id), Is.EqualTo(new[] { "v3", "v2", "v1" }));

        var unwatched = await _repo.GetVideosAsync(new VideoQuery(IsShort: false, Watched: false));
        Assert.That(unwatched.Select(v => v.Id), Is.EqualTo(new[] { "v3", "v1" }));

        var shorts = await _repo.GetVideosAsync(new VideoQuery(IsShort: true));
        Assert.That(shorts.Select(v => v.Id), Is.EqualTo(new[] { "s1" }));
    }

    [Test]
    public async Task GetLatestPublishedAt_PerChannel()
    {
        Assert.That(await _repo.GetLatestPublishedAtAsync("UC1"), Is.Null);
        await _repo.InsertNewVideosAsync([V("a", "UC1", 5), V("b", "UC1", 9), V("c", "UC2", 20)]);

        Assert.That(await _repo.GetLatestPublishedAtAsync("UC1"), Is.EqualTo(new DateTime(2026, 1, 9)));
    }

    [Test]
    public async Task GetUnscored_ReturnsOnlyNullScores()
    {
        var scored = V("a");
        scored.QualityScore = 50;
        await _repo.InsertNewVideosAsync([scored, V("b")]);

        var unscored = await _repo.GetUnscoredVideosAsync(10);
        Assert.That(unscored.Select(v => v.Id), Is.EqualTo(new[] { "b" }));
    }
}
