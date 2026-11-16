using BlueTube.Core.Models;
using BlueTube.Core.Services;
using BlueTube.Tests.Fakes;

namespace BlueTube.Tests;

public class JsonFileSettingsStoreTests
{
    private string _path = "";

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"bluetube-{Guid.NewGuid():N}", "settings.json");

    [TearDown]
    public void TearDown()
    {
        var dir = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Test]
    public async Task Secrets_And_Settings_SurviveRestart()
    {
        var store = new JsonFileSettingsStore(_path);
        await store.SetAsync(SecretNames.YouTubeApiKey, "  yt-key  ");
        store.MinScore = 70;
        store.LlmModel = "claude-haiku-5-5";

        var reopened = new JsonFileSettingsStore(_path);

        Assert.That(await reopened.GetAsync(SecretNames.YouTubeApiKey), Is.EqualTo("yt-key"));
        Assert.That(reopened.MinScore, Is.EqualTo(70));
        Assert.That(reopened.LlmModel, Is.EqualTo("claude-haiku-5-5"));
    }

    [Test]
    public async Task Defaults_WhenNoFile()
    {
        var store = new JsonFileSettingsStore(_path);
        Assert.That(await store.GetAsync(SecretNames.LlmApiKey), Is.Null);
        Assert.That(store.MinScore, Is.EqualTo(40));
        Assert.That(store.LlmModel, Is.EqualTo(LlmQualityFilter.DefaultModel));
    }

    [Test]
    public async Task EmptyValue_RemovesSecret_AndScoreIsClamped()
    {
        var store = new JsonFileSettingsStore(_path);
        await store.SetAsync(SecretNames.YouTubeApiKey, "k");
        await store.SetAsync(SecretNames.YouTubeApiKey, "");
        store.MinScore = 500;

        Assert.That(await store.GetAsync(SecretNames.YouTubeApiKey), Is.Null);
        Assert.That(store.MinScore, Is.EqualTo(100));
    }

    [Test]
    public void BrokenFile_StartsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not json");
        Assert.That(new JsonFileSettingsStore(_path).MinScore, Is.EqualTo(40));
    }

    [Test]
    public async Task OnLinux_FileIsOwnerOnly()
    {
        if (OperatingSystem.IsWindows()) Assert.Ignore("Unix file mode only.");
        await new JsonFileSettingsStore(_path).SetAsync(SecretNames.YouTubeApiKey, "k");
        Assert.That(File.GetUnixFileMode(_path), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }
}

public class ChannelTransferTests
{
    private const string IdA = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private const string IdB = "UCbbbbbbbbbbbbbbbbbbbbbb";

    [Test]
    public void Export_Then_Import_RoundTrips()
    {
        var json = ChannelTransfer.Export([new Channel { Id = IdA, Title = "A", ThumbnailUrl = "a.jpg", Tags = "unity,csharp" }]);

        var back = ChannelTransfer.Import(json).Single();

        Assert.That(back.Id, Is.EqualTo(IdA));
        Assert.That(back.Title, Is.EqualTo("A"));
        Assert.That(back.ThumbnailUrl, Is.EqualTo("a.jpg"));
        Assert.That(back.TagList, Is.EqualTo(new[] { "unity", "csharp" }));
        Assert.That(json, Does.Not.Contain("LastFetchedAt"));
    }

    [Test]
    public void Import_SkipsBadIds_AndDuplicates()
    {
        var json = $$"""{"bluetube_channels":1,"channels":[{"id":"{{IdA}}"},{"id":"{{IdA}}"},{"id":"@handle"},{"id":"UCshort"}]}""";
        var result = ChannelTransfer.Import(json);
        Assert.That(result.Select(c => c.Id), Is.EqualTo(new[] { IdA }));
        Assert.That(result[0].Title, Is.EqualTo(IdA), "Missing title falls back to ID.");
    }

    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("[]")]
    public void Import_RejectsOtherText(string text) =>
        Assert.Throws<FormatException>(() => ChannelTransfer.Import(text));

    [Test]
    public async Task FeedService_Import_AddsNew_MergesTags_NoApiCalls()
    {
        var repo = new FakeVideoRepository();
        var yt = new FakeYouTubeService();
        repo.Channels[IdA] = new Channel { Id = IdA, Title = "A", Tags = "unity" };
        var feed = new FeedService(yt, repo, new FakeQualityFilter());
        var json = ChannelTransfer.Export([
            new Channel { Id = IdA, Title = "A", Tags = "csharp" },
            new Channel { Id = IdB, Title = "B", Tags = "gamedev" },
        ]);

        var added = await feed.ImportChannelsAsync(json);

        Assert.That(added, Is.EqualTo(1));
        Assert.That(repo.Channels[IdA].TagList, Is.EqualTo(new[] { "unity", "csharp" }));
        Assert.That(repo.Channels[IdB].Title, Is.EqualTo("B"));
        Assert.That(yt.Calls, Is.Empty);
    }

    [Test]
    public async Task Refresh_FillsMissingThumbnail_Once()
    {
        var repo = new FakeVideoRepository();
        var yt = new FakeYouTubeService();
        repo.Channels[IdB] = new Channel { Id = IdB, Title = "Old", ThumbnailUrl = "" };
        yt.Channels[IdB] = new Channel { Id = IdB, Title = "Real B", ThumbnailUrl = "b.jpg" };
        var feed = new FeedService(yt, repo, new FakeQualityFilter());

        await feed.RefreshAsync();

        Assert.That(repo.Channels[IdB].Title, Is.EqualTo("Real B"));
        Assert.That(repo.Channels[IdB].ThumbnailUrl, Is.EqualTo("b.jpg"));
    }
}
