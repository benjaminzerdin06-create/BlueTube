namespace BlueTube.Core.Services;

/// <summary>
/// Opens a web link outside BlueTube (system browser or YouTube app).
/// Android: MAUI Launcher. Desktop: default browser.
/// Do not use &lt;a target="_blank"&gt;: the Android WebView ignores it.
/// </summary>
public interface ILinkOpener
{
    Task OpenAsync(Uri url, CancellationToken ct = default);
}
