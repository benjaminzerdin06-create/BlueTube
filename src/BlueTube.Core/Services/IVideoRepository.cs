using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

public interface IVideoRepository
{
    Task InitAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken ct = default);
    Task<Channel?> GetChannelAsync(string id, CancellationToken ct = default);
    Task UpsertChannelAsync(Channel channel, CancellationToken ct = default);
    /// <summary>Deletes the channel and its videos.</summary>
    Task DeleteChannelAsync(string id, CancellationToken ct = default);

    Task<Video?> GetVideoAsync(string id, CancellationToken ct = default);
    /// <summary>Adds only videos with a new Id. Does not change stored rows.</summary>
    /// <returns>Number of added videos.</returns>
    Task<int> InsertNewVideosAsync(IEnumerable<Video> videos, CancellationToken ct = default);
    Task UpdateVideoAsync(Video video, CancellationToken ct = default);
    Task<IReadOnlyList<Video>> GetVideosAsync(VideoQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<Video>> GetUnscoredVideosAsync(int limit, CancellationToken ct = default);
    Task<DateTime?> GetLatestPublishedAtAsync(string channelId, CancellationToken ct = default);
}

/// <summary>Newest first. A null flag means: do not filter on it.</summary>
public record VideoQuery(
    bool IsShort,
    bool? Watched = null,
    bool? WatchLater = null,
    bool? Favorite = null,
    int Limit = 200);
