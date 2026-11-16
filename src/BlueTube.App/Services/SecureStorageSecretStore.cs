using BlueTube.Core.Services;

namespace BlueTube.App.Services;

/// <summary>ISecretStore on MAUI SecureStorage (Android Keystore).</summary>
public sealed class SecureStorageSecretStore : ISecretStore
{
    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(name);
        }
        catch (Exception)
        {
            // Keystore can fail after an OS restore. Treat as "no key". User enters it again.
            SecureStorage.Default.Remove(name);
            return null;
        }
    }

    public async Task SetAsync(string name, string? value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            SecureStorage.Default.Remove(name);
        else
            await SecureStorage.Default.SetAsync(name, value.Trim());
    }
}
