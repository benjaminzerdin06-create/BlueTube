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

/// <param name="NotFound">Handle unknown to YouTube.</param>
/// <param name="Mismatch">Handle exists, but the title is not the expected channel. Not added.</param>
public record LibraryResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> AlreadyThere,
    IReadOnlyList<string> NotFound,
    IReadOnlyList<string> Mismatch);

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

    /// <summary>
    /// Adds library channels. Entry with a verified ID: no API call. Else: resolve the handle (1 quota unit)
    /// and refuse it if the returned title does not match the expected name (impostor or wrong handle).
    /// Channels already stored keep their data; library tags are merged.
    /// </summary>
    public async Task<LibraryResult> AddLibraryAsync(IEnumerable<LibraryChannel> entries, CancellationToken ct = default)
    {
        var added = new List<string>();
        var already = new List<string>();
        var notFound = new List<string>();
        var mismatch = new List<string>();
        var stored = (await repo.GetChannelsAsync(ct)).ToDictionary(c => c.Id);

        foreach (var e in entries)
        {
            Channel channel;
            if (e.Id is not null)
            {
                channel = new Channel { Id = e.Id, Title = e.Name };
            }
            else
            {
                var resolved = await youTube.ResolveChannelAsync(e.Handle, ct);
                if (resolved is null)
                {
                    notFound.Add($"{e.Name} ({e.Handle})");
                    continue;
                }
                if (!LearningLibrary.NameMatches(e.Name, resolved.Title))
                {
                    mismatch.Add($"{e.Handle}: expected \"{e.Name}\", YouTube says \"{resolved.Title}\"");
                    continue;
                }
                channel = resolved;
            }

            if (stored.TryGetValue(channel.Id, out var existing))
            {
                existing.TagList = existing.TagList.Concat(TagHelper.Parse(e.Tags)).ToList();
                await repo.UpsertChannelAsync(existing, ct);
                already.Add(existing.Title);
                continue;
            }

            channel.TagList = TagHelper.Parse(e.Tags);
            await repo.UpsertChannelAsync(channel, ct);
            stored[channel.Id] = channel;
            added.Add(channel.Title);
        }
        return new LibraryResult(added, already, notFound, mismatch);
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

    /// <summary>Max videos the feed shows. Filters run before this limit, so a tag finds older videos too.</summary>
    public const int FeedLimit = 200;

    /// <summary>Rows read before score/tag filtering. Large: the library has thousands of videos.</summary>
    private const int FilterWindow = 20_000;

    public async Task<IReadOnlyList<Video>> GetFeedAsync(FeedFilter filter, CancellationToken ct = default)
    {
        var query = new VideoQuery(
            IsShort: filter.IsShort,
            Watched: filter.HideWatched ? false : null,
            WatchLater: filter.List == FeedList.WatchLater ? true : null,
            Favorite: filter.List == FeedList.Favorites ? true : null,
            Limit: FilterWindow);

        var videos = await repo.GetVideosAsync(query, ct);
        var channelTags = (await repo.GetChannelsAsync(ct)).ToDictionary(c => c.Id, c => c.TagList);
        return Apply(videos, filter, channelTags).Take(FeedLimit).ToList();
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
