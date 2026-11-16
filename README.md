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

2. Install the Android SDK and JDK (once). This accepts the Android SDK license:

```bash
dotnet build src/BlueTube.App -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory="$LOCALAPPDATA/Android/Sdk" -p:JavaSdkDirectory="$LOCALAPPDATA/Microsoft/jdk" -p:AcceptAndroidSDKLicenses=True
```

3. Build:

```bash
dotnet publish src/BlueTube.App -f net10.0-android -c Release -p:JavaSdkDirectory="$LOCALAPPDATA/Microsoft/jdk"
```

4. Install `src/BlueTube.App/bin/Release/net10.0-android/publish/com.bluetube.app-Signed.apk` on the phone. USB with debugging on:

```bash
"$LOCALAPPDATA/Android/Sdk/platform-tools/adb" install -r src/BlueTube.App/bin/Release/net10.0-android/publish/com.bluetube.app-Signed.apk
```

The APK uses the debug signing key. Keep the same machine for updates, or uninstall first.

## First start

1. Get a YouTube Data API v3 key from Google Cloud Console.
2. Open **Settings**. Enter the key. The app keeps keys in Android SecureStorage, not in source.
3. Open **Channels**. Add channels by `@handle` or `UC...` ID.
4. Open **Feed**. Pull down to refresh.

## License

MIT. See [LICENSE](LICENSE).
