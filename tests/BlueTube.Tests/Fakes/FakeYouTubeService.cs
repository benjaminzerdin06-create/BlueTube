using BlueTube.Core.Models;
using BlueTube.Core.Services;

namespace BlueTube.Tests.Fakes;

public sealed class FakeYouTubeService : IYouTubeService
{
    public Dictionary<string, Channel> Channels { get; } = [];
    /// <summary>All videos per channel ID. GetNewVideosAsync returns those after "since".</summary>
    public Dictionary<string, List<Video>> Videos { get; } = [];
    /// <summary>Throw this for the given channel ID.</summary>
    public Dictionary<string, YouTubeApiException> Errors { get; } = [];
    public List<(string ChannelId, DateTime? Since)> Calls { get; } = [];

    public Task<Channel?> ResolveChannelAsync(string idOrHandle, CancellationToken ct = default) =>
        Task.FromResult(Channels.TryGetValue(idOrHandle, out var c)
            ? new Channel { Id = c.Id, Title = c.Title, ThumbnailUrl = c.ThumbnailUrl }
            : null);

    public Task<IReadOnlyList<Video>> GetNewVideosAsync(Channel channel, DateTime? since, CancellationToken ct = default)
    {
        Calls.Add((channel.Id, since));
        if (Errors.TryGetValue(channel.Id, out var e)) throw e;
        IReadOnlyList<Video> result = Videos.GetValueOrDefault(channel.Id, [])
            .Where(v => since is null || v.PublishedAt > since)
            .Select(Clone)
            .ToList();
        return Task.FromResult(result);
    }

    private static Video Clone(Video v) => new()
    {
        Id = v.Id, ChannelId = v.ChannelId, Title = v.Title, PublishedAt = v.PublishedAt,
        IsShort = v.IsShort, DurationSeconds = v.DurationSeconds,
    };
}
