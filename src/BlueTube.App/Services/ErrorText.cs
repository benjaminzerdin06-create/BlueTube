using BlueTube.Core.Services;

namespace BlueTube.App.Services;

/// <summary>Short user messages for errors.</summary>
public static class ErrorText
{
    public static string For(Exception e) => e switch
    {
        YouTubeApiException { Error: YouTubeError.MissingKey } => "No YouTube API key. Add it in Settings.",
        YouTubeApiException { Error: YouTubeError.InvalidKey } => "YouTube API key is not valid. Check it in Settings.",
        YouTubeApiException { Error: YouTubeError.QuotaExceeded } => "Daily YouTube quota is used. Try again tomorrow.",
        YouTubeApiException { Error: YouTubeError.Network } => "No connection. Showing saved videos.",
        HttpRequestException => "No connection.",
        OperationCanceledException => "Stopped.",
        _ => $"Error: {e.Message}",
    };
}
