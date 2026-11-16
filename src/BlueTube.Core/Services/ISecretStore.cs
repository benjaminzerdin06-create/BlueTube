namespace BlueTube.Core.Services;

/// <summary>Keeps secrets on the device. The app uses MAUI SecureStorage. Tests use a fake.</summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string name, CancellationToken ct = default);
    /// <summary>A null or empty value removes the secret.</summary>
    Task SetAsync(string name, string? value, CancellationToken ct = default);
}

public static class SecretNames
{
    public const string YouTubeApiKey = "youtube_api_key";
    public const string LlmApiKey = "llm_api_key";
}
