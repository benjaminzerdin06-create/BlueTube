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
    else Console.WriteLine($"Port {Port} is in use. BlueTube may already run.");
    return;
}

// Default: Windows %LOCALAPPDATA%\BlueTube, Linux ~/.local/share/BlueTube.
var dataDir = dataArg >= 0 && dataArg + 1 < args.Length
    ? Path.GetFullPath(args[dataArg + 1])
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "BlueTube");
Directory.CreateDirectory(dataDir);

var builder = WebApplication.CreateBuilder(args);
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
    Console.WriteLine($"BlueTube runs at {url}");
    Console.WriteLine($"Data: {dataDir}");
    Console.WriteLine("It stops 30 s after you close the last BlueTube window.");
    if (openWindow) AppWindow.Open(url);
});

app.Run();
