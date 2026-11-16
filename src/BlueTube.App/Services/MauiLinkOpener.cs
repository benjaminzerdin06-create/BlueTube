using BlueTube.Core.Services;

namespace BlueTube.App.Services;

/// <summary>Opens links in the YouTube app or browser on the phone.</summary>
public sealed class MauiLinkOpener : ILinkOpener
{
    public async Task OpenAsync(Uri url, CancellationToken ct = default)
    {
        try
        {
            await Launcher.Default.OpenAsync(url);
        }
        catch (Exception)
        {
            // No app can open the link. Nothing to do.
        }
    }
}
