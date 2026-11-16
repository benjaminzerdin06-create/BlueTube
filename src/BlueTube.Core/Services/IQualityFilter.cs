using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

/// <param name="Score">0-100. Higher: more useful, deeper, less clickbait.</param>
public record QualityResult(int Score, IReadOnlyList<string> Tags);

public interface IQualityFilter
{
    /// <summary>Scores one video. Provider details stay in the implementation.</summary>
    Task<QualityResult> ScoreAsync(Video video, CancellationToken ct = default);
}

public enum QualityFilterError
{
    Unknown,
    MissingKey,
    InvalidKey,
    RateLimited,
    Unavailable,
    Network,
}

public sealed class QualityFilterException(QualityFilterError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public QualityFilterError Error { get; } = error;
}

/// <summary>Non-secret user settings. The app keeps them in MAUI Preferences.</summary>
public interface IAppSettings
{
    /// <summary>Feed hides scored videos below this. 0: show all.</summary>
    int MinScore { get; set; }
    /// <summary>Model ID for the quality filter.</summary>
    string LlmModel { get; set; }
}
