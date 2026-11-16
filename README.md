# BlueTube

Shows tech YouTube videos and Shorts from channels you select. An AI filter hides low-quality videos.

Runs on **Android** (phone app) and on **Windows and Linux** (desktop app). No server.

Stack: .NET 10, Blazor, SQLite. Android: MAUI Blazor Hybrid. Desktop: local Blazor Server app in a browser app window.

Spec and rules: [CLAUDE.md](CLAUDE.md). How-to for Claude sessions: [docs/AGENT-GUIDE.md](docs/AGENT-GUIDE.md).

## Folders

| Folder | Contents |
|---|---|
| `src/BlueTube.Core` | Logic: models, YouTube API, SQLite, quality filter, settings file. No UI. |
| `src/BlueTube.UI` | Shared Blazor pages, components, CSS, JS. |
| `src/BlueTube.App` | Android host (MAUI). |
| `src/BlueTube.Desktop` | Windows and Linux host (ASP.NET Core). |
| `tests/BlueTube.Tests` | NUnit tests for Core. |

## Test

```bash
dotnet test tests/BlueTube.Tests
```

## Desktop app (Windows, Linux)

### Get it

Download `BlueTube-win-x64` or `BlueTube-linux-x64` from the latest **desktop** run under GitHub **Actions**. Unzip it anywhere. No .NET install needed.

Or build it:

```bash
dotnet publish src/BlueTube.Desktop -c Release -r win-x64 --self-contained -o artifacts/desktop/win-x64
```

```bash
dotnet publish src/BlueTube.Desktop -c Release -r linux-x64 --self-contained -o artifacts/desktop/linux-x64
```

### Start it

- Windows: double-click `BlueTube.exe`. For a desktop icon: right-click it → **Send to → Desktop (create shortcut)**.
- Linux: `chmod +x BlueTube` once, then `./BlueTube`. For a menu entry with icon, create `~/.local/share/applications/bluetube.desktop` (replace `/path/to` with the folder):

```ini
[Desktop Entry]
Type=Application
Name=BlueTube
Exec=/path/to/BlueTube
Icon=/path/to/bluetube.png
Terminal=false
Categories=AudioVideo;Video;
```

BlueTube opens in an app window (Edge or Chrome app mode). If neither is installed, it opens in your default browser.

- No console window. The server runs hidden in the background.
- Address: `http://127.0.0.1:5217`. Only your own computer can open it.
- It stops 30 s after you close the last BlueTube window. To stop it at once: Task Manager → BlueTube → End task.
- Start and error messages go to `bluetube.log` in the data folder.
- Start it again while it runs: it only opens the window.

### Data

| OS | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\BlueTube` |
| Linux | `~/.local/share/BlueTube` |

`bluetube.db` keeps channels and videos. `settings.json` keeps API keys and settings as **plain text** (Linux: file mode 600). Do not share this file.

Developer options: `--no-window` (do not open a window), `--data <folder>` (use another data folder).

## Android app

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
2. Open **Settings**. Enter the key.
3. Open **Channels**. Add channels by `@handle`, `UC...` ID or channel URL.
4. Open **Feed**. Tap ⟳ (phone: or pull down).

## Copy channels between phone and PC

1. On one device: **Channels → Export / import channels → Copy**.
2. Send the text to the other device (email, chat, notes).
3. On the other device: paste it in the same box. Tap **Import**.

Import adds channels and tags. It does not copy videos, watch history or API keys. It uses no YouTube quota.

## License

MIT. See [LICENSE](LICENSE).
