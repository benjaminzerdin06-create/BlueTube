using BlueTube.App.Services;
// Pages and components live in BlueTube.UI (shared with BlueTube.Desktop).
using BlueTube.Core.Services;

namespace BlueTube.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // One HttpClient for the app lifetime (avoids socket exhaustion).
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
        builder.Services.AddSingleton<ISecretStore, SecureStorageSecretStore>();
        builder.Services.AddSingleton<IAppSettings, PreferencesAppSettings>();
        builder.Services.AddSingleton<IQualityFilter, LlmQualityFilter>();
        builder.Services.AddSingleton<IVideoRepository>(
            new SqliteVideoRepository(Path.Combine(FileSystem.AppDataDirectory, "bluetube.db")));
        builder.Services.AddSingleton<IYouTubeService, YouTubeService>();
        builder.Services.AddSingleton<FeedService>();

        return builder.Build();
    }
}
