using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BlueTube.Desktop;

/// <summary>Opens BlueTube in a browser "app" window (no tabs, no address bar).</summary>
public static class AppWindow
{
    /// <summary>True if another BlueTube (or other program) already listens on the port.</summary>
    public static bool IsPortInUse(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    /// <summary>Tries Edge/Chrome/Chromium in app mode. Falls back to the default browser.</summary>
    public static void Open(string url)
    {
        var args = $"--app={url} --window-size=1280,860";
        foreach (var browser in AppModeBrowsers())
        {
            try
            {
                Process.Start(new ProcessStartInfo(browser, args) { UseShellExecute = false });
                return;
            }
            catch (Win32Exception)
            {
                // Not installed. Try the next one.
            }
        }

        try
        {
            // Windows: default browser. Linux: xdg-open.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            Console.WriteLine($"Open {url} in your browser.");
        }
    }

    private static IEnumerable<string> AppModeBrowsers()
    {
        if (OperatingSystem.IsWindows())
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            };
            var relative = new[]
            {
                @"Microsoft\Edge\Application\msedge.exe",
                @"Google\Chrome\Application\chrome.exe",
            };
            return relative
                .SelectMany(r => roots.Select(root => Path.Combine(root, r)))
                .Where(File.Exists);
        }

        // Linux: names on PATH. Process.Start throws Win32Exception if missing.
        return ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge"];
    }
}
