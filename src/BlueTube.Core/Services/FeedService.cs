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

/// <param name="Skipped">True if no LLM key is set. Scoring is optional.</param>
/// <param name="Error">Set if scoring stopped early (bad key, rate limit, offline).</param>
public record ScoreResult(int Scored, bool Skipped, QualityFilterException? Error);

/// <summary>Fetch, dedupe, store, score. Also reads the feed with filters.</summary>
public sealed class FeedService(IYouTubeService youTube, IVideoRepository repo, IQualityFilter quality)
{
    private const int ScoreParallelism = 4;

    /// <summary>Scores unscored videos. Each video is scored once; the result is stored.</summary>
    public async Task<ScoreResult> ScoreNewAsync(int max = 100, CancellationToken ct = default)
    {
        var videos = await repo.GetUnscoredVideosAsync(max, ct);
        var scored = 0;
        QualityFilterException? fatal = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await Parallel.ForEachAsync(videos,
                new ParallelOptions { MaxDegreeOfParallelism = ScoreParallelism, CancellationToken = stop.Token },
                async (video, token) =>
                {
                    // A fatal error already happened: do not call the API again.
                    if (Volatile.Read(ref fatal) is not null) return;
                    try
                    {
                        var result = await quality.ScoreAsync(video, token);
                        video.QualityScore = result.Score;
                        video.AiTagList = result.Tags;
                        await repo.UpdateVideoAsync(video, token);
                        Interlocked.Increment(ref scored);
                    }
                    catch (QualityFilterException e) when (e.Error == QualityFilterError.Unknown)
                    {
                        // One bad reply. Video stays unscored and is tried again next time.
                    }
                    catch (QualityFilterException e)
                    {
                        // Key, rate limit or network problem: the next calls fail too. Stop.
                        Interlocked.CompareExchange(ref fatal, e, null);
                        await stop.CancelAsync();
                    }
                });
        }
        catch (OperationCanceledException) when (fatal is not null && !ct.IsCancellationRequested)
        {
        }

        return fatal is { Error: QualityFilterError.MissingKey }
            ? new ScoreResult(scored, Skipped: true, Error: null)
            : new ScoreResult(scored, Skipped: false, fatal);
    }

    public async Task<RefreshResult> RefreshAsync(CancellationToken ct = default)
    {
        var added = 0;
        var channelErrors = new List<string>();

        foreach (var channel in await repo.GetChannelsAsync(ct))
        {
            try
            {
                // Imported channels have no thumbnail. Get it (and the real title) once.
                if (channel.ThumbnailUrl.Length == 0
                    && await youTube.ResolveChannelAsync(channel.Id, ct) is { } info)
                {
                    channel.Title = info.Title;
                    channel.ThumbnailUrl = info.ThumbnailUrl;
                }

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

    public async Task<string> ExportChannelsAsync(CancellationToken ct = default) =>
        ChannelTransfer.Export(await repo.GetChannelsAsync(ct));

    /// <summary>
    /// Adds channels from export text. No YouTube calls (0 quota).
    /// Known channels: tags are merged. Next refresh gets their videos.
    /// </summary>
    /// <returns>Number of new channels.</returns>
    /// <exception cref="FormatException">Text is not a BlueTube export.</exception>
    public async Task<int> ImportChannelsAsync(string json, CancellationToken ct = default)
    {
        var added = 0;
        foreach (var channel in ChannelTransfer.Import(json))
        {
            var existing = await repo.GetChannelAsync(channel.Id, ct);
            if (existing is null)
            {
                await repo.UpsertChannelAsync(channel, ct);
                added++;
            }
            else
            {
                existing.TagList = existing.TagList.Concat(channel.TagList).ToList();
                await repo.UpsertChannelAsync(existing, ct);
            }
        }
        return added;
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
