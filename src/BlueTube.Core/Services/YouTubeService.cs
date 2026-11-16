using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

/// <summary>
/// YouTube Data API v3 over plain HTTP. Uses only 1-unit calls (no search.list).
/// Channel "UCxxx" has playlists "UULFxxx" (long videos) and "UUSHxxx" (Shorts).
/// These playlist IDs are unofficial. If one gives 404, the service uses "UUxxx" and classifies by duration and shape.
/// </summary>
public sealed class YouTubeService(HttpClient http, ISecretStore secrets) : IYouTubeService
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3/";
    public const int MaxShortSeconds = 180;
    private const int FirstFetchPages = 1;
    private const int IncrementalMaxPages = 10;

    public async Task<Channel?> ResolveChannelAsync(string idOrHandle, CancellationToken ct = default)
    {
        var input = NormalizeChannelInput(idOrHandle);
        if (input.Length == 0) return null;

        var filter = input.StartsWith("UC", StringComparison.Ordinal)
            ? $"id={Uri.EscapeDataString(input)}"
            : $"forHandle={Uri.EscapeDataString(input)}";

        var response = await GetAsync<ListResponse<ChannelDto>>($"channels?part=snippet&{filter}", ct);
        var dto = response?.Items?.FirstOrDefault();
        if (dto is null) return null;

        return new Channel
        {
            Id = dto.Id,
            Title = dto.Snippet.Title,
            ThumbnailUrl = BestThumbnail(dto.Snippet.Thumbnails),
        };
    }

    public async Task<IReadOnlyList<Video>> GetNewVideosAsync(Channel channel, DateTime? since, CancellationToken ct = default)
    {
        if (!channel.Id.StartsWith("UC", StringComparison.Ordinal))
            throw new ArgumentException("Channel ID must start with 'UC'.", nameof(channel));

        var suffix = channel.Id[2..];
        var longIds = await TryGetPlaylistVideoIdsAsync("UULF" + suffix, since, ct);
        var shortIds = await TryGetPlaylistVideoIdsAsync("UUSH" + suffix, since, ct);

        if (longIds is not null && shortIds is not null)
        {
            var shortSet = shortIds.ToHashSet();
            var longs = await GetVideoDetailsAsync(longIds.Except(shortSet), includePlayer: false, ct);
            var shorts = await GetVideoDetailsAsync(shortIds, includePlayer: false, ct);
            foreach (var v in shorts) v.IsShort = true;
            return [.. longs, .. shorts];
        }

        // Fallback: all uploads, classify each video.
        var allIds = await TryGetPlaylistVideoIdsAsync("UU" + suffix, since, ct)
                     ?? throw new YouTubeApiException(YouTubeError.NotFound, $"No uploads playlist for channel {channel.Id}.");
        var all = await GetVideoDetailsAsync(allIds, includePlayer: true, ct);
        return all;
    }

    /// <summary>Returns null if the playlist does not exist (404).</summary>
    private async Task<List<string>?> TryGetPlaylistVideoIdsAsync(string playlistId, DateTime? since, CancellationToken ct)
    {
        var ids = new List<string>();
        string? pageToken = null;
        var maxPages = since is null ? FirstFetchPages : IncrementalMaxPages;

        for (var page = 0; page < maxPages; page++)
        {
            var url = $"playlistItems?part=contentDetails&maxResults=50&playlistId={playlistId}"
                      + (pageToken is null ? "" : $"&pageToken={pageToken}");

            ListResponse<PlaylistItemDto>? response;
            try
            {
                response = await GetAsync<ListResponse<PlaylistItemDto>>(url, ct);
            }
            catch (YouTubeApiException e) when (e.Error == YouTubeError.NotFound)
            {
                return page == 0 ? null : ids;
            }

            var reachedKnown = false;
            foreach (var item in response?.Items ?? [])
            {
                var publishedAt = item.ContentDetails.VideoPublishedAt?.UtcDateTime;
                if (since is not null && publishedAt is not null && publishedAt <= since)
                {
                    reachedKnown = true;
                    break;
                }
                // Private or deleted videos have no publish date. Skip them.
                if (publishedAt is not null) ids.Add(item.ContentDetails.VideoId);
            }

            pageToken = response?.NextPageToken;
            if (reachedKnown || pageToken is null) break;
        }
        return ids;
    }

    private async Task<List<Video>> GetVideoDetailsAsync(IEnumerable<string> ids, bool includePlayer, CancellationToken ct)
    {
        var result = new List<Video>();
        // videos.list accepts max 50 IDs per call.
        foreach (var batch in ids.Distinct().Chunk(50))
        {
            var parts = includePlayer ? "snippet,contentDetails,player&maxHeight=1000" : "snippet,contentDetails";
            var url = $"videos?part={parts}&id={string.Join(',', batch)}";
            var response = await GetAsync<ListResponse<VideoDto>>(url, ct);
            foreach (var dto in response?.Items ?? [])
                result.Add(ToVideo(dto, classifyByShape: includePlayer));
        }
        return result;
    }

    private static Video ToVideo(VideoDto dto, bool classifyByShape)
    {
        var duration = ParseDurationSeconds(dto.ContentDetails?.Duration);
        return new Video
        {
            Id = dto.Id,
            ChannelId = dto.Snippet.ChannelId ?? "",
            ChannelTitle = dto.Snippet.ChannelTitle ?? "",
            Title = dto.Snippet.Title,
            Description = dto.Snippet.Description ?? "",
            ThumbnailUrl = BestThumbnail(dto.Snippet.Thumbnails),
            PublishedAt = dto.Snippet.PublishedAt.UtcDateTime,
            DurationSeconds = duration,
            IsShort = classifyByShape && IsShort(duration, dto.Player?.EmbedWidth, dto.Player?.EmbedHeight),
        };
    }

    /// <summary>Short: 1 to 180 s and vertical. Unknown shape counts as vertical.</summary>
    public static bool IsShort(int durationSeconds, long? width, long? height)
    {
        if (durationSeconds <= 0 || durationSeconds > MaxShortSeconds) return false;
        if (width is null || height is null) return true;
        return height > width;
    }

    /// <summary>ISO 8601 duration ("PT1H2M3S") to seconds. Bad or missing value gives 0.</summary>
    public static int ParseDurationSeconds(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return 0;
        try { return (int)XmlConvert.ToTimeSpan(iso).TotalSeconds; }
        catch (FormatException) { return 0; }
    }

    /// <summary>Accepts "UC...", "@handle", "handle" or a youtube.com channel URL.</summary>
    public static string NormalizeChannelInput(string raw)
    {
        var s = raw.Trim();
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Host.Contains("youtube.com"))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0] == "channel") return segments[1];
            if (segments.Length >= 1 && segments[0].StartsWith('@')) return segments[0];
            return "";
        }
        if (s.StartsWith("UC", StringComparison.Ordinal) && s.Length == 24) return s;
        return s.StartsWith('@') ? s : "@" + s;
    }

    private static string BestThumbnail(Dictionary<string, ThumbnailDto>? thumbs)
    {
        if (thumbs is null) return "";
        foreach (var key in new[] { "high", "medium", "standard", "default" })
            if (thumbs.TryGetValue(key, out var t)) return t.Url;
        return thumbs.Values.FirstOrDefault()?.Url ?? "";
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        var key = await secrets.GetAsync(SecretNames.YouTubeApiKey, ct);
        if (string.IsNullOrWhiteSpace(key))
            throw new YouTubeApiException(YouTubeError.MissingKey, "No YouTube API key. Add one in Settings.");

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"{BaseUrl}{relativeUrl}&key={Uri.EscapeDataString(key)}", ct);
        }
        catch (HttpRequestException e)
        {
            throw new YouTubeApiException(YouTubeError.Network, "No connection to YouTube.", e);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>(ct);

            throw await ToExceptionAsync(response, ct);
        }
    }

    private static async Task<YouTubeApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ErrorResponse? body = null;
        try { body = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct); }
        catch (JsonException) { }

        var reason = body?.Error?.Errors?.FirstOrDefault()?.Reason ?? "";
        var message = body?.Error?.Message ?? response.ReasonPhrase ?? "YouTube error.";

        var error = reason switch
        {
            "quotaExceeded" or "dailyLimitExceeded" or "rateLimitExceeded" => YouTubeError.QuotaExceeded,
            "keyInvalid" or "keyExpired" => YouTubeError.InvalidKey,
            // A bad key gives 400 "badRequest" with message "API key not valid".
            "badRequest" when message.Contains("API key", StringComparison.OrdinalIgnoreCase) => YouTubeError.InvalidKey,
            _ when response.StatusCode == HttpStatusCode.NotFound => YouTubeError.NotFound,
            _ => YouTubeError.Unknown,
        };
        return new YouTubeApiException(error, message);
    }
}
