using BlueTube.Core;
using BlueTube.Core.Models;
using BlueTube.Core.Services;
using BlueTube.Tests.Fakes;

namespace BlueTube.Tests;

public class LearningLibraryTests
{
    [Test]
    public void Library_EntriesAreValid()
    {
        var entries = LearningLibrary.Channels;
        Assert.That(entries, Has.Count.GreaterThanOrEqualTo(40));
        Assert.That(entries.Select(e => e.Handle.ToLowerInvariant()), Is.Unique, "Duplicate handle.");
        Assert.That(entries.Where(e => e.Id is not null).Select(e => e.Id), Is.Unique, "Duplicate ID.");
        foreach (var e in entries)
        {
            Assert.That(e.Handle, Does.StartWith("@"), e.Name);
            Assert.That(LearningLibrary.Topics, Does.Contain(e.Topic), e.Name);
            Assert.That(TagHelper.Parse(e.Tags), Is.Not.Empty, e.Name);
            Assert.That(e.Why, Is.Not.Empty, e.Name);
            // Every entry must be verified once (see AGENT-GUIDE "Learning library") and carry its ID.
            Assert.That(e.Id, Is.Not.Null.And.Length.EqualTo(24).And.StartsWith("UC"), $"{e.Name}: add the verified ID.");
        }
    }

    [Test]
    public void Library_TagsAreAllowedAiTags()
    {
        var unknown = LearningLibrary.Channels
            .SelectMany(e => TagHelper.Parse(e.Tags))
            .Distinct()
            .Except(LlmQualityFilter.AllowedTags)
            .ToList();
        Assert.That(unknown, Is.Empty, "Add these to LlmQualityFilter.AllowedTags so feed chips merge.");
    }

    [TestCase("StatQuest with Josh Starmer", "StatQuest with Josh Starmer", true)]
    [TestCase("Game Maker's Toolkit", "Game Maker’s Toolkit", true)]
    [TestCase("GDC", "GDC 2025", true)]
    [TestCase("Fireship", "Fireship Clips", true)]
    [TestCase("Andrej Karpathy", "Karpathy Fan Edits", false)]
    [TestCase("Ben Eater", "", false)]
    public void NameMatches(string expected, string actual, bool match) =>
        Assert.That(LearningLibrary.NameMatches(expected, actual), Is.EqualTo(match));

    private const string IdA = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private const string IdB = "UCbbbbbbbbbbbbbbbbbbbbbb";

    [Test]
    public async Task AddLibrary_AddsVerified_RefusesMismatch_ReportsNotFound()
    {
        var repo = new FakeVideoRepository();
        var yt = new FakeYouTubeService();
        yt.Channels["@good"] = new Channel { Id = IdA, Title = "Good Channel", ThumbnailUrl = "g.jpg" };
        yt.Channels["@fake"] = new Channel { Id = IdB, Title = "Totally Different" };
        var feed = new FeedService(yt, repo, new FakeQualityFilter());

        var result = await feed.AddLibraryAsync([
            new LibraryChannel("T", "@good", "Good Channel", "ai-ml", "w"),
            new LibraryChannel("T", "@fake", "Real Name", "x", "w"),
            new LibraryChannel("T", "@nobody", "Nobody", "x", "w"),
        ]);

        Assert.That(result.Added, Is.EqualTo(new[] { "Good Channel" }));
        Assert.That(result.Mismatch, Has.Count.EqualTo(1).And.Some.Contains("Totally Different"));
        Assert.That(result.NotFound, Has.Count.EqualTo(1).And.Some.Contains("@nobody"));
        Assert.That(repo.Channels.Keys, Is.EquivalentTo(new[] { IdA }));
        Assert.That(repo.Channels[IdA].TagList, Is.EqualTo(new[] { "ai-ml" }));
        Assert.That(repo.Channels[IdA].ThumbnailUrl, Is.EqualTo("g.jpg"));
    }

    [Test]
    public async Task AddLibrary_WithId_NoApiCall_AndMergesTagsForExisting()
    {
        var repo = new FakeVideoRepository();
        var yt = new FakeYouTubeService();
        repo.Channels[IdA] = new Channel { Id = IdA, Title = "Mine", Tags = "unity" };
        var feed = new FeedService(yt, repo, new FakeQualityFilter());

        var result = await feed.AddLibraryAsync([
            new LibraryChannel("T", "@a", "Mine", "csharp", "w", IdA),
            new LibraryChannel("T", "@b", "New One", "robotics", "w", IdB),
        ]);

        Assert.That(result.Added, Is.EqualTo(new[] { "New One" }));
        Assert.That(result.AlreadyThere, Is.EqualTo(new[] { "Mine" }));
        Assert.That(repo.Channels[IdA].TagList, Is.EqualTo(new[] { "unity", "csharp" }));
        Assert.That(repo.Channels[IdB].ThumbnailUrl, Is.Empty, "Filled on next refresh.");
        Assert.That(yt.Calls, Is.Empty);
    }
}
