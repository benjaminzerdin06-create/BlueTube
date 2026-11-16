using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

public interface IYouTubeService
{
    /// <summary>Finds a channel by "UC..." ID, "@handle" or channel URL. Returns null if not found.</summary>
    Task<Channel?> ResolveChannelAsync(string idOrHandle, CancellationToken ct = default);

    /// <summary>Gets long videos and Shorts published after <paramref name="since"/>. Null: get the latest page only.</summary>
    Task<IReadOnlyList<Video>> GetNewVideosAsync(Channel channel, DateTime? since, CancellationToken ct = default);
}

public enum YouTubeError
{
    Unknown,
    MissingKey,
    InvalidKey,
    QuotaExceeded,
    NotFound,
    Network,
}

public sealed class YouTubeApiException(YouTubeError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public YouTubeError Error { get; } = error;
}
