using BlueTube.Core.Services;

namespace BlueTube.UI.Services;

/// <summary>Short user messages for errors.</summary>
public static class ErrorText
{
    public static string For(Exception e) => e switch
    {
        YouTubeApiException { Error: YouTubeError.MissingKey } => "No YouTube API key. Add it in Settings.",
        YouTubeApiException { Error: YouTubeError.InvalidKey } => "YouTube API key is not valid. Check it in Settings.",
        YouTubeApiException { Error: YouTubeError.QuotaExceeded } => "Daily YouTube quota is used. Try again tomorrow.",
        YouTubeApiException { Error: YouTubeError.Network } => "No connection. Showing saved videos.",
        QualityFilterException { Error: QualityFilterError.InvalidKey } => "Claude API key is not valid. Check it in Settings.",
        QualityFilterException { Error: QualityFilterError.RateLimited } => "Claude rate limit. Scoring continues on next refresh.",
        QualityFilterException { Error: QualityFilterError.Unavailable } => "Claude API is busy. Scoring continues on next refresh.",
        QualityFilterException { Error: QualityFilterError.Network } => "No connection to Claude. Scoring continues on next refresh.",
        QualityFilterException e2 => $"Scoring error: {e2.Message}",
        HttpRequestException => "No connection.",
        OperationCanceledException => "Stopped.",
        _ => $"Error: {e.Message}",
    };
}
