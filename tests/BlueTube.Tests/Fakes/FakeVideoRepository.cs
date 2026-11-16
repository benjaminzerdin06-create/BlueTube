using BlueTube.Core.Models;
using BlueTube.Core.Services;

namespace BlueTube.Tests.Fakes;

/// <summary>In-memory repository. Same rules as the SQLite one.</summary>
public sealed class FakeVideoRepository : IVideoRepository
{
    public Dictionary<string, Channel> Channels { get; } = [];
    public Dictionary<string, Video> Videos { get; } = [];

    public Task InitAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Channel>>(Channels.Values.OrderBy(c => c.Title).ToList());

    public Task<Channel?> GetChannelAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Channels.GetValueOrDefault(id));

    public Task UpsertChannelAsync(Channel channel, CancellationToken ct = default)
    {
        Channels[channel.Id] = channel;
        return Task.CompletedTask;
    }

    public Task DeleteChannelAsync(string id, CancellationToken ct = default)
    {
        Channels.Remove(id);
        foreach (var v in Videos.Values.Where(v => v.ChannelId == id).ToList()) Videos.Remove(v.Id);
        return Task.CompletedTask;
    }

    public Task<Video?> GetVideoAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Videos.GetValueOrDefault(id));

    public Task<int> InsertNewVideosAsync(IEnumerable<Video> videos, CancellationToken ct = default) =>
        Task.FromResult(videos.Count(v => Videos.TryAdd(v.Id, v)));

    public Task UpdateVideoAsync(Video video, CancellationToken ct = default)
    {
        // Scoring calls this in parallel.
        lock (Videos) Videos[video.Id] = video;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Video>> GetVideosAsync(VideoQuery q, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Video>>(Videos.Values
            .Where(v => v.IsShort == q.IsShort)
            .Where(v => q.Watched is null || v.Watched == q.Watched)
            .Where(v => q.WatchLater is null || v.WatchLater == q.WatchLater)
            .Where(v => q.Favorite is null || v.Favorite == q.Favorite)
            .OrderByDescending(v => v.PublishedAt)
            .Take(q.Limit)
            .ToList());

    public Task<IReadOnlyList<Video>> GetUnscoredVideosAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Video>>(Videos.Values
            .Where(v => v.QualityScore is null)
            .OrderByDescending(v => v.PublishedAt)
            .Take(limit)
            .ToList());

    public Task<DateTime?> GetLatestPublishedAtAsync(string channelId, CancellationToken ct = default) =>
        Task.FromResult(Videos.Values.Where(v => v.ChannelId == channelId)
            .Select(v => (DateTime?)v.PublishedAt)
            .Max());
}
