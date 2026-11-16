using System.Diagnostics;
using BlueTube.Core.Services;

namespace BlueTube.Desktop;

/// <summary>Opens links in the default browser (Windows shell, Linux xdg-open). Not in the BlueTube window.</summary>
public sealed class ShellLinkOpener : ILinkOpener
{
    public Task OpenAsync(Uri url, CancellationToken ct = default)
    {
        if (url.Scheme is "https" or "http")
        {
            try
            {
                Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // No browser found. Nothing to do.
            }
        }
        return Task.CompletedTask;
    }
}
