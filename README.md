# BlueTube

Android app. Shows tech YouTube videos and Shorts from channels you select. An AI filter hides low-quality videos.

Stack: .NET 10 MAUI Blazor Hybrid, SQLite. No server.

Spec and rules: [CLAUDE.md](CLAUDE.md).

## Folders

| Folder | Contents |
|---|---|
| `src/BlueTube.Core` | Logic: models, YouTube API, SQLite, quality filter. No MAUI. |
| `src/BlueTube.App` | MAUI Blazor UI. |
| `tests/BlueTube.Tests` | NUnit tests for Core. |

## Test

```bash
dotnet test tests/BlueTube.Tests
```

## Build the APK

1. Install the MAUI Android workload:

```bash
dotnet workload install maui-android
```

2. Build:

```bash
dotnet publish src/BlueTube.App -f net10.0-android -c Release
```

3. Copy the `.apk` from `src/BlueTube.App/bin/Release/net10.0-android/publish/` to the phone. Install it.

## First start

1. Get a YouTube Data API v3 key from Google Cloud Console.
2. Open **Settings**. Enter the key. The app keeps keys in Android SecureStorage, not in source.
3. Open **Channels**. Add channels by `@handle` or `UC...` ID.
4. Open **Feed**. Pull down to refresh.

## License

MIT. See [LICENSE](LICENSE).
