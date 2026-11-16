using BlueTube.Core.Services;
using BlueTube.Desktop;
using BlueTube.Desktop.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;

// Fixed port: the YouTube embed "origin" and the browser app window stay the same between runs.
const int Port = 5217;
var url = $"http://127.0.0.1:{Port}";

// Developer options: --no-window (do not open a window), --data <folder> (other data folder).
var openWindow = !args.Contains("--no-window");
var dataArg = Array.IndexOf(args, "--data");

// Already running: show the window and stop this copy.
if (AppWindow.IsPortInUse(Port))
{
    if (openWindow) AppWindow.Open(url);
    return;
}

// Default: Windows %LOCALAPPDATA%\BlueTube, Linux ~/.local/share/BlueTube.
var dataDir = dataArg >= 0 && dataArg + 1 < args.Length
    ? Path.GetFullPath(args[dataArg + 1])
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "BlueTube");
Directory.CreateDirectory(dataDir);

// No console window on Windows (WinExe). Startup messages and fatal errors go to bluetube.log.
var logPath = Path.Combine(dataDir, "bluetube.log");
void Log(string message) =>
    File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Find wwwroot next to the executable, also when a shortcut starts BlueTube from another folder.
    ContentRootPath = AppContext.BaseDirectory,
});
// Serve BlueTube.UI files (_content/...) also when started with "dotnet run" outside Development.
// Published builds copy the files to wwwroot; then this call does nothing.
builder.WebHost.UseStaticWebAssets();
// Loopback only: other computers on the network cannot open BlueTube.
builder.WebHost.UseUrls(url);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<CircuitHandler, AutoShutdown>();

// Same services as the phone app (src/BlueTube.App/MauiProgram.cs). Only storage differs.
var store = new JsonFileSettingsStore(Path.Combine(dataDir, "settings.json"));
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
builder.Services.AddSingleton<ISecretStore>(store);
builder.Services.AddSingleton<IAppSettings>(store);
builder.Services.AddSingleton<ILinkOpener, ShellLinkOpener>();
builder.Services.AddSingleton<IVideoRepository>(new SqliteVideoRepository(Path.Combine(dataDir, "bluetube.db")));
builder.Services.AddSingleton<IYouTubeService, YouTubeService>();
builder.Services.AddSingleton<IQualityFilter, LlmQualityFilter>();
builder.Services.AddSingleton<FeedService>();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(BlueTube.UI.Components.Routes).Assembly);

app.Lifetime.ApplicationStarted.Register(() =>
{
    Log($"Started at {url}. Stops 30 s after the last BlueTube window closes.");
    if (openWindow) AppWindow.Open(url);
});
app.Lifetime.ApplicationStopped.Register(() => Log("Stopped."));

try
{
    app.Run();
}
catch (Exception e)
{
    Log($"Fatal error: {e}");
    throw;
}
