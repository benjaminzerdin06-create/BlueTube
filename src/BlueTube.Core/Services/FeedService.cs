using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

public enum FeedList { All, WatchLater, Favorites }

/// <param name="MinScore">Hide scored videos below this. Unscored videos stay visible.</param>
/// <param name="Tag">Show only videos with this AI tag or channel tag. Null: all.</param>
public record FeedFilter(
    bool IsShort,
    int MinScore = 0,
    string? Tag = null,
    bool HideWatched = false,
    FeedList List = FeedList.All);

/// <param name="Error">Set if the refresh stopped early (no key, bad key, quota, offline).</param>
public record RefreshResult(int Added, IReadOnlyList<string> ChannelErrors, YouTubeApiException? Error);

/// <summary>Fetch, dedupe, store. Also reads the feed with filters.</summary>
public sealed class FeedService(IYouTubeService youTube, IVideoRepository repo)
{
    public async Task<RefreshResult> RefreshAsync(CancellationToken ct = default)
    {
        var added = 0;
        var channelErrors = new List<string>();

        foreach (var channel in await repo.GetChannelsAsync(ct))
        {
            try
            {
                var since = await repo.GetLatestPublishedAtAsync(channel.Id, ct);
                var videos = await youTube.GetNewVideosAsync(channel, since, ct);
                foreach (var v in videos)
                {
                    v.ChannelId = channel.Id;
                    if (v.ChannelTitle.Length == 0) v.ChannelTitle = channel.Title;
                }
                added += await repo.InsertNewVideosAsync(videos, ct);

                channel.LastFetchedAt = DateTime.UtcNow;
                await repo.UpsertChannelAsync(channel, ct);
            }
            catch (YouTubeApiException e) when (e.Error is YouTubeError.NotFound or YouTubeError.Unknown)
            {
                // One bad channel must not stop the others.
                channelErrors.Add($"{channel.Title}: {e.Message}");
            }
            catch (YouTubeApiException e)
            {
                return new RefreshResult(added, channelErrors, e);
            }
        }
        return new RefreshResult(added, channelErrors, null);
    }

    /// <summary>Finds the channel on YouTube and stores it. Returns null if not found.</summary>
    public async Task<Channel?> AddChannelAsync(string idOrHandle, IEnumerable<string> tags, CancellationToken ct = default)
    {
        var channel = await youTube.ResolveChannelAsync(idOrHandle, ct);
        if (channel is null) return null;

        var existing = await repo.GetChannelAsync(channel.Id, ct);
        channel.LastFetchedAt = existing?.LastFetchedAt;
        channel.TagList = tags.ToList();
        await repo.UpsertChannelAsync(channel, ct);
        return channel;
    }

    public async Task<IReadOnlyList<Video>> GetFeedAsync(FeedFilter filter, CancellationToken ct = default)
    {
        var query = new VideoQuery(
            IsShort: filter.IsShort,
            Watched: filter.HideWatched ? false : null,
            WatchLater: filter.List == FeedList.WatchLater ? true : null,
            Favorite: filter.List == FeedList.Favorites ? true : null);

        var videos = await repo.GetVideosAsync(query, ct);
        var channelTags = (await repo.GetChannelsAsync(ct)).ToDictionary(c => c.Id, c => c.TagList);
        return Apply(videos, filter, channelTags);
    }

    /// <summary>Score threshold and tag filter. Saved lists (watch later, favorites) ignore the threshold.</summary>
    public static IReadOnlyList<Video> Apply(
        IEnumerable<Video> videos,
        FeedFilter filter,
        IReadOnlyDictionary<string, IReadOnlyList<string>> channelTags)
    {
        var tag = filter.Tag?.Trim().ToLowerInvariant();
        return videos
            .Where(v => filter.List != FeedList.All || v.QualityScore is null || v.QualityScore >= filter.MinScore)
            .Where(v => string.IsNullOrEmpty(tag)
                        || v.AiTagList.Contains(tag)
                        || (channelTags.TryGetValue(v.ChannelId, out var ct) && ct.Contains(tag)))
            .ToList();
    }

    /// <summary>All tags in use: AI tags of the videos plus channel tags. Sorted.</summary>
    public async Task<IReadOnlyList<string>> GetTagsAsync(bool isShort, CancellationToken ct = default)
    {
        var videos = await repo.GetVideosAsync(new VideoQuery(isShort, Limit: 1000), ct);
        var channels = await repo.GetChannelsAsync(ct);
        return videos.SelectMany(v => v.AiTagList)
            .Concat(channels.SelectMany(c => c.TagList))
            .Distinct()
            .Order()
            .ToList();
    }
}
