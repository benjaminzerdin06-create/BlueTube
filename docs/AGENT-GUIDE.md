# BlueTube: guide for Claude sessions

Read [CLAUDE.md](../CLAUDE.md) first. It has the stack rules. This file tells you **how** to do common tasks: which file to change, which part, and what else to update.

Write all docs, comments and commit messages in ASD-STE100 Simplified Technical English. Use the fewest words possible.

---

## 0. Before you start

| Check | Command or action |
|---|---|
| Tests pass | `dotnet test tests/BlueTube.Tests` |
| Phone is connected | `"$LOCALAPPDATA/Android/Sdk/platform-tools/adb" devices` shows `RF8TB08YSDP device` |
| Git is clean | `git status` |

The owner's phone is a **Samsung Galaxy A13 (SM-A137F)**. It runs **32-bit-only Android 14** (`armeabi-v7a`). Screen: 1080 x 2408.

Paths on this machine:

| Tool | Path |
|---|---|
| Android SDK | `%LOCALAPPDATA%\Android\Sdk` |
| adb | `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` |
| JDK 21 | `%LOCALAPPDATA%\Microsoft\jdk` |

Claude's shell can see these paths. The owner's own terminal could **not** find `adb.exe` (sandbox difference). Do not tell the owner to run adb commands. Run them yourself.

---

## 1. Build, install, test on the phone

Use PowerShell for these. Bash quoting of Windows paths breaks.

**Core tests** (run before every commit):

```powershell
dotnet test tests/BlueTube.Tests
```

**Build the APK:**

```powershell
dotnet publish src/BlueTube.App -f net10.0-android -c Release "-p:JavaSdkDirectory=$env:LOCALAPPDATA\Microsoft\jdk"
```

Output: `src/BlueTube.App/bin/Release/net10.0-android/publish/com.bluetube.app-Signed.apk`.

**Install and start** (keeps app data: channels, videos, keys):

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r src\BlueTube.App\bin\Release\net10.0-android\publish\com.bluetube.app-Signed.apk
& $adb shell am force-stop com.bluetube.app
& $adb shell monkey -p com.bluetube.app -c android.intent.category.LAUNCHER 1
```

**Screenshot** (then use the Read tool on the PNG):

```powershell
& $adb shell screencap -p /sdcard/bt.png; & $adb pull /sdcard/bt.png "<scratchpad>\phone.png"; & $adb shell rm /sdcard/bt.png
```

**Logs:**

```powershell
& $adb logcat -c                     # clear before the test
& $adb logcat -d -v brief | Select-String "chromium.*Console|Exception|FATAL"
```

Ignore `BufferQueueDebug`, `ConnectivityManager StackLog` and `OpenGLRenderer` lines. They are Samsung system noise.

The APK is signed with this PC's debug key. A build from a different PC cannot update the installed app. Uninstall first in that case. Uninstall deletes the owner's channels and keys, so ask first.

---

## 2. Add channels to the owner's phone (runtime, no code change)

The YouTube API key is already saved on the phone. Check: Settings shows "Key saved. Enter a new key to replace it."

### 2.1 Find the correct handle

The app resolves `@handle`, `UC...` ID (24 characters) or a channel URL. A wrong handle gives **"Channel not found."** Handles are not always the channel name. Example: Game Maker's Toolkit is `@GMTK`, not `@GameMakersToolkit`.

If you are not sure of a handle, ask the owner for the channel URL, or use web search to find `youtube.com/@...`.

Handles that work (already added):

| Channel | Handle | Tags |
|---|---|---|
| Code Monkey | `@CodeMonkeyUnity` | unity, csharp |
| Game Maker's Toolkit | `@GMTK` | gamedev |
| Sebastian Lague | `@SebastianLague` | unity, graphics |
| Nick Chapsas | `@nickchapsas` | csharp, dotnet |
| Fireship | `@Fireship` | programming |
| Two Minute Papers | `@TwoMinutePapers` | ai-ml |
| Gamers Nexus | `@GamersNexus` | hardware |

### 2.2 Tap positions (Galaxy A13, original pixels)

| Target | x, y |
|---|---|
| Tab: Feed | 132, 2184 |
| Tab: Shorts | 336, 2184 |
| Tab: Channels | 672, 2184 |
| Tab: Settings | 942, 2184 |
| Channels: handle box | 540, 290 |
| Channels: tags box | 540, 421 |
| Channels: Add channel button | 540, 550 |
| Feed: refresh button | 980, 150 |
| Feed: first video card | 538, 860 |

Screenshots you Read are scaled to 897 x 2000. Multiply screenshot coordinates by 1.20 to get the tap position.

### 2.3 Script

Use this exact script. **Do not remove the sleeps.** Without them, `adb input text` arrives before the WebView finishes the last field, and all handles mix into one box.

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
function Clear-Field($y) {
  & $adb shell input tap 540 $y; Start-Sleep -Milliseconds 600
  & $adb shell input keyevent 123                       # move to end
  & $adb shell input keyevent $(@(67)*120)              # 120 x backspace
  Start-Sleep -Milliseconds 800 }
function Add-Channel($handle, $tags) {
  & $adb shell input tap 672 2184; Start-Sleep -Seconds 2   # Channels tab
  Clear-Field 290; & $adb shell input text $handle; Start-Sleep -Seconds 1
  Clear-Field 421; & $adb shell input text $tags;   Start-Sleep -Seconds 1
  & $adb shell input tap 540 550; Start-Sleep -Seconds 5 }  # Add channel

Add-Channel "@SomeHandle" "tag1,tag2"
```

Rules:

- Tags: lowercase, comma-separated, **no spaces** (`adb input text` cannot type a space; use `%s` if you must).
- Prefer tags from the AI tag list in section 5.4, so the Feed tag chips merge.
- **Never send keyevent 111 (Escape) or 4 (Back) to hide the keyboard.** Both act as Back and leave the page.
- After the script, take a screenshot. Each new channel shows in the list with its avatar. A red "Channel not found." means a wrong handle.
- Each add costs 1 YouTube quota unit. The daily quota is 10,000.

### 2.4 Get the videos

Tap Feed (132, 2184), then refresh (980, 150). Wait about 15 s. The banner shows "N new videos."

### 2.5 Remove a channel

Channels tab: tap the ✕ at the right of the row, then tap "Remove?". This deletes the channel **and its videos** (watched/favorite flags too). Ask the owner before you remove a channel.

### 2.6 Secrets: what you must not do

- Do not type API keys into the app or any form, even if the owner asks. The owner pastes keys on the phone. You may open the key page for them: `adb shell am start -a android.intent.action.VIEW -d "https://console.cloud.google.com/apis/credentials?project=bluetube-514916"`.
- Do not put keys in source, config, tests or commits. The repo is **public**.

---

## 3. Code map

```
src/BlueTube.Core/                 all logic, no MAUI
  Models/Channel.cs                SQLite table Channel
  Models/Video.cs                  SQLite table Video
  Models/TagHelper.cs              comma-separated tag columns
  Formatting.cs                    Duration "4:05", Age "3d"
  Services/IYouTubeService.cs      interface + YouTubeError + YouTubeApiException
  Services/YouTubeService.cs       YouTube Data API v3 over HttpClient
  Services/YouTubeJson.cs          JSON DTO records (only fields we use)
  Services/IVideoRepository.cs     interface + VideoQuery
  Services/SqliteVideoRepository.cs
  Services/FeedService.cs          refresh, add channel, feed filters, scoring
  Services/IQualityFilter.cs       interface + IAppSettings + errors
  Services/LlmQualityFilter.cs     Claude Messages API, raw HTTP
  Services/ISecretStore.cs         interface + SecretNames
  Services/JsonFileSettingsStore.cs  desktop secrets + settings (plain JSON file)
  Services/ChannelTransfer.cs      channel list export/import (JSON text)
src/BlueTube.UI/                   shared Blazor UI (Razor class library) — used by App AND Desktop
  Components/Routes.razor
  Components/Layout/MainLayout.razor   DB init, ErrorBoundary, bottom tab bar
  Components/Pages/Feed.razor      "/"
  Components/Pages/Watch.razor     "/watch/{Id}"
  Components/Pages/Shorts.razor    "/shorts"
  Components/Pages/Channels.razor  "/channels" (+ Export / import box)
  Components/Pages/Settings.razor  "/settings"
  Components/Shared/VideoCard.razor, ShortPlayer.razor, TagChip.razor, KeyField.razor
  Services/ErrorText.cs            user messages for errors
  wwwroot/app.css                  global styles + color tokens (:root) → _content/BlueTube.UI/app.css
  wwwroot/app.js                   pull-to-refresh, Shorts observer, player commands
src/BlueTube.App/                  Android host (MAUI) — no pages here
  MauiProgram.cs                   DI registrations (Android)
  MainPage.xaml(.cs)               BlazorWebView, Android back button, fullscreen client
  Platforms/Android/FullscreenChromeClient.cs
  Services/SecureStorageSecretStore.cs, PreferencesAppSettings.cs
  wwwroot/index.html               HTML shell (Android)
src/BlueTube.Desktop/              Windows/Linux host (ASP.NET Core Blazor Server)
  Program.cs                       DI registrations (desktop), port 5217, data folder
  Components/App.razor             HTML shell (desktop)
  AppWindow.cs                     opens Edge/Chrome --app window, port check
  AutoShutdown.cs                  stops 30 s after last window closes
  wwwroot/manifest.webmanifest, icon.svg
tests/BlueTube.Tests/              NUnit, Core only
  Fakes/                           FakeHttpHandler, FakeYouTubeService, FakeVideoRepository,
                                   FakeQualityFilter (+ FakeAppSettings), FakeSecretStore
```

Data flow: `Feed.razor` → `FeedService.RefreshAsync` → `IYouTubeService.GetNewVideosAsync` → `IVideoRepository.InsertNewVideosAsync` → `FeedService.ScoreNewAsync` → `IQualityFilter.ScoreAsync`.

---

## 4. Channels: code changes

### 4.1 Add a field to Channel (example: `SubscriberCount`)

1. `src/BlueTube.Core/Models/Channel.cs`: add `public long SubscriberCount { get; set; }`.
   - sqlite-net `CreateTableAsync` adds new columns to an existing DB at start. It does **not** rename or drop columns. Never rename a stored property; add a new one.
   - Use a type with a safe default (number, `string = ""`, nullable). Old rows get the default.
2. `src/BlueTube.Core/Services/YouTubeJson.cs`: add the JSON field to the DTO. For statistics, add a `statistics` record to `ChannelDto`.
3. `src/BlueTube.Core/Services/YouTubeService.cs` → `ResolveChannelAsync`: add the part to the URL (`channels?part=snippet,statistics&...`). Same cost: 1 unit. Map the value into `new Channel { ... }`.
4. `src/BlueTube.Core/Services/FeedService.cs` → `AddChannelAsync`: it copies only `LastFetchedAt` from the stored row. If the new field is user-editable, copy it there too.
5. `tests/BlueTube.Tests/Fakes/FakeYouTubeService.cs` → `ResolveChannelAsync`: copy the new field in the `new Channel { ... }`.
6. Tests: extend `YouTubeServiceTests.ResolveChannel_ByHandle` JSON and asserts.
7. UI: `Components/Pages/Channels.razor`, inside the `<li>` loop. Styles in `Channels.razor.css`.

### 4.2 Accept a new input format (example: `youtube.com/c/Name`)

1. `YouTubeService.NormalizeChannelInput`: add the case. Legacy `/c/` and `/user/` names need `channels?forUsername=` (only for `/user/`); `/c/` names have no direct API lookup. Prefer telling the user to paste the `@handle` URL.
2. Add `[TestCase]` rows to `YouTubeServiceTests.NormalizeChannelInput`.
3. Update the placeholder text in `Channels.razor` (`@@` writes a literal `@` in Razor).

### 4.3 Built-in starter channel list

Not built yet. If the owner asks for it:

1. Add `src/BlueTube.Core/StarterChannels.cs`: `public static IReadOnlyList<(string Handle, string Tags)> All`.
2. Add `FeedService.AddStarterChannelsAsync`: loop, call `AddChannelAsync`, skip nulls, return count. Test it with `FakeYouTubeService`.
3. `Channels.razor`: show an "Add starter channels" button only when the list is empty.

---

## 5. Videos: code changes

### 5.1 Add a field to Video (example: `ViewCount`)

1. `src/BlueTube.Core/Models/Video.cs`: add `public long ViewCount { get; set; }`. Same column rules as 4.1.
2. `YouTubeJson.cs`: add a `statistics` record (`viewCount` is a **string** in the API JSON; parse with `long.TryParse`) and add it to `VideoDto`.
3. `YouTubeService.GetVideoDetailsAsync`: add `statistics` to `parts`. Same cost: 1 unit per 50 videos.
4. `YouTubeService.ToVideo`: map the value.
5. `tests/.../Fakes/FakeYouTubeService.cs` → `Clone`: copy the new field. **This is easy to forget.** Feed tests then lose the value.
6. Tests: extend `VideoJson(...)` in `YouTubeServiceTests` and assert the value.
7. Note: `InsertNewVideosAsync` uses `OR IGNORE`. Stored videos are **never updated** by refresh. Old videos keep the default value. If the field must stay current, add a new repository method (and fake) that updates only that column, and call it from `FeedService.RefreshAsync`.
8. UI: `Components/Shared/VideoCard.razor` (`.sub` line) and/or `Watch.razor`.

### 5.2 Add a feed list (example: "Unwatched")

1. `FeedService.cs`: add the value to `enum FeedList`.
2. `FeedService.GetFeedAsync`: map it to a `VideoQuery` (for Unwatched: `Watched: false`).
3. `FeedService.Apply`: the score threshold applies only when `List == FeedList.All`. Decide if the new list uses the threshold, then change that `Where`.
4. Test in `FeedServiceTests` (use `FakeVideoRepository`).
5. `Feed.razor`: add a `<TagChip Tag="Unwatched" Active=... OnClick="() => SelectListAsync(FeedList.Unwatched)" />` in the first `.chips` div.

### 5.3 Add a per-video action (example: "Not interested")

1. `Video.cs`: add `public bool Hidden { get; set; }`.
2. `VideoQuery` (`IVideoRepository.cs`): add `bool? Hidden = null`.
3. `SqliteVideoRepository.GetVideosAsync` **and** `FakeVideoRepository.GetVideosAsync`: add the `Where`.
4. `FeedService.GetFeedAsync`: pass `Hidden: false` for normal lists.
5. Tests for repository and feed.
6. `Watch.razor` → `.actions` div: add a button that calls `ToggleAsync(v => v.Hidden = !v.Hidden)`.

### 5.4 AI tags and scoring

- Allowed tags: `LlmQualityFilter.AllowedTags`. The model can return only these (JSON schema `enum`). Add a tag there; also list it in the table in 2.1 if channels use it.
- Prompt: `LlmQualityFilter.SystemPrompt`.
- Model choices: `Settings.razor` `<select>` options; default `LlmQualityFilter.DefaultModel`.
- Each video is scored **once**. To re-score after a prompt change, add a repository method that sets `QualityScore = NULL` (and a fake), plus a Settings button. Ask the owner first: it costs API money.
- Tests: `QualityFilterTests` (HTTP contract) and `ScoringTests` (FeedService).

### 5.5 Fetch more history per channel

`YouTubeService.FirstFetchPages` (now 1 page = 50 items per playlist). More pages cost 1 unit each, per channel, per playlist (long + Shorts). Incremental refresh stops at the last stored date, up to `IncrementalMaxPages`.

---

## 6. Shorts: code changes

### 6.1 How Shorts are found

`YouTubeService.GetNewVideosAsync`:

- Channel `UCxxxx` → playlist `UUSHxxxx` = Shorts, `UULFxxxx` = long videos. These IDs are unofficial.
- If either playlist gives 404: fallback to `UUxxxx` (all uploads), then `IsShort(duration, width, height)`: 1–180 s and vertical (`player` part with `maxHeight=1000` gives the embed size).
- Change the limit: `YouTubeService.MaxShortSeconds`. Update `IsShort_Classification` test cases.

### 6.2 Shorts page parts

| Part | File | Notes |
|---|---|---|
| Page, list, active index | `Components/Pages/Shorts.razor` | Loads `FeedFilter(IsShort: true, MinScore, HideWatched: true)`. `OnActive` marks the previous Short watched. |
| One Short | `Components/Shared/ShortPlayer.razor` | Live `<iframe>` only when `Active`. Else a thumbnail. |
| Swipe container | `Shorts.razor.css` | `scroll-snap-type: y mandatory`, height = viewport minus tab bar. |
| Item layout | `ShortPlayer.razor.css` | `.touch-layer` covers top 78%; bottom 22% stays free for YouTube controls and ad "Skip". |
| Visible-item detect | `src/BlueTube.UI/wwwroot/app.js` → `observeShorts` | IntersectionObserver, threshold 0.6, calls .NET `OnActive(index)`. |
| Pause / play | `src/BlueTube.UI/wwwroot/app.js` → `playerCommand` | postMessage to the iframe. Needs `enablejsapi=1` in the embed URL. |

### 6.3 Common Shorts changes

- **Show watched Shorts too:** `Shorts.razor` → remove `HideWatched: true`.
- **Do not mark watched on swipe:** `Shorts.razor` → `OnActive`, remove the `previous.Watched = true` block.
- **Embed parameters** (loop, controls, captions): `ShortPlayer.razor` → `EmbedUrl`. Keep `origin`, `widget_referrer`, `enablejsapi=1`, `playsinline=1`.
- **Open a Short full page:** add a button in `.side` that navigates to `watch/{Id}` with `NavigationManager`.
- **Known issue:** the title/buttons overlay overlaps YouTube's own controls while they show. Possible fix: hide `.info` for 3 s after a tap.

---

## 7. Playback rules (Watch and Shorts)

- Official YouTube IFrame embed only. Ads stay. No ad blocking, no download, no scraping.
- Every embed URL must have `origin=<app origin>` and `widget_referrer=<app origin>`. Get the origin from `new Uri(Nav.BaseUri).GetLeftPart(UriPartial.Authority)`.
- Every `<iframe>` must have `referrerpolicy="strict-origin-when-cross-origin"`. Both HTML shells (`src/BlueTube.App/wwwroot/index.html`, `src/BlueTube.Desktop/Components/App.razor`) have a referrer meta tag. Do not remove it.
- These prevent YouTube error 152/153 in the WebView. Tested OK on the A13.
- Fullscreen: `Platforms/Android/FullscreenChromeClient.cs`. Autoplay: `MainPage.xaml.cs` sets `MediaPlaybackRequiresUserGesture = false`.

---

## 8. New page or tab

1. `src/BlueTube.UI/Components/Pages/NewPage.razor` with `@page "/new"`. Optional `NewPage.razor.css`.
2. Inject services with `@inject`. **The property name must differ from the component class name.** `@inject FeedService Feed` in `Feed.razor` failed to build (CS0542). Use `FeedSvc`.
3. Tab: `Components/Layout/MainLayout.razor` → add a `<NavLink href="new">` with an inline 24 x 24 SVG and a `<span>`. Five tabs fit; more do not.
4. New service: interface + class in Core, register with `AddSingleton` in **both** `src/BlueTube.App/MauiProgram.cs` and `src/BlueTube.Desktop/Program.cs`.

## 9. New setting

- Not secret (number, choice): add a property to `IAppSettings` (`IQualityFilter.cs`). Implement it in **three** places: `src/BlueTube.App/Services/PreferencesAppSettings.cs` (MAUI `Preferences`), `src/BlueTube.Core/Services/JsonFileSettingsStore.cs` (desktop, add a key constant + property like `MinScore`), and `Fakes/FakeQualityFilter.cs` → `FakeAppSettings`. Add a test in `DesktopSupportTests.JsonFileSettingsStoreTests`. UI in `Settings.razor`.
- Secret (API key): add a name to `SecretNames` (`ISecretStore.cs`). UI: `<KeyField Name="@SecretNames.NewKey" />` in `Settings.razor`. Both stores accept any name; no other change.

---

## 9a. Desktop app (Windows, Linux)

Host: `src/BlueTube.Desktop`. It shows the **same** pages as the phone (from `BlueTube.UI`). A UI change applies to both apps. Test both.

| Task | What to do |
|---|---|
| Run from source (test data, no window) | `dotnet run --project src/BlueTube.Desktop -- --no-window --data <scratch folder>`. Then open `http://127.0.0.1:5217` in the built-in browser pane. Stop: kill the process on port 5217. |
| Run with a window | `dotnet run --project src/BlueTube.Desktop`. Opens Edge/Chrome in `--app` mode. **Uses the owner's real desktop data** (`%LOCALAPPDATA%\BlueTube`). |
| Publish | `dotnet publish src/BlueTube.Desktop -c Release -r win-x64 --self-contained -o artifacts/desktop/win-x64` (also `linux-x64`). `artifacts/` is git-ignored. |
| Check a publish | Start `BlueTube.exe --no-window --data <folder>`. Get `/`, `/_framework/blazor.web.js`, `/_content/BlueTube.UI/app.css` → all 200. |
| Linux test | WSL cannot run here (virtualization off in firmware; do not change it). Push, then read the **desktop** workflow: it starts the Linux build and checks the files. `gh run list --workflow desktop.yml --limit 1`. |
| Change the port | `const int Port` in `Program.cs`. The YouTube embed origin changes with it. |
| Desktop-only service | Implement the Core interface in `BlueTube.Desktop` (or in Core if pure .NET and testable), register in `Program.cs`. |

Rules:

- **No MAUI API in `BlueTube.UI`.** `Launcher`, `FileSystem`, `Preferences`, `SecureStorage`, `Platform` do not exist on desktop. Example: "Open in YouTube" is a plain `<a target="_blank">`; both hosts open it outside the app.
- Pages run with `InteractiveServerRenderMode(prerender: false)` on desktop. Do not depend on prerender.
- Without `builder.WebHost.UseStaticWebAssets()` in `Program.cs`, `dotnet run` gives HTTP 500 for `_content/...` files. Keep it.
- Layout must work at phone width and at desktop width. Feed cards use `.grid` (auto columns, min 300 px). Shorts use a 9:16 max-width on screens ≥ 700 px.
- Desktop keys are **plain text** in `settings.json` (owner's choice). Never print or log them.

Channel export/import (`ChannelTransfer`): the JSON has `id`, `title`, `thumbnail`, `tags`. If you add a Channel field that must move between devices, add it to `ExportChannel` and to `Import`, and keep old exports readable (new fields optional).

---

## 10. Blazor pitfalls found in this project

| Problem | Cause | Fix |
|---|---|---|
| Button stays disabled after typing | `@bind` updates on `change` (blur). A tap on a disabled button does not blur the input. | Add `@bind:event="oninput"` to inputs that enable a button. |
| Razor error RZ10007 / CS1525 | Nested `"` inside an attribute lambda, e.g. `@oninput="e => ... ?? "0""` | Move the code to a method: `@oninput="OnSliderInput"`. |
| CS0542 | `@inject X Feed` inside `Feed.razor` | Use a different property name. |
| Header under status bar | WebView draws edge-to-edge | `SafeAreaEdges="All"` on `MainPage.xaml`. Keep it. |
| Desktop: `_content/...` gives 500 | Static web assets off outside Development | `builder.WebHost.UseStaticWebAssets()` in Desktop `Program.cs`. |
| Install fails `NO_MATCHING_ABIS` | Phone is 32-bit only | Keep `<RuntimeIdentifiers>android-arm;android-arm64</RuntimeIdentifiers>` in `BlueTube.App.csproj`. |
| Literal `@` in markup | Razor treats `@` as code | Write `@@`. |
| Raw string JSON in tests fails CS9007 | `}}` inside a `$$"""` string | Use `$$$"""` with `{{{x}}}`, or a plain `"""` string plus `.Replace`. |

---

## 11. Finish a change

1. `dotnet test tests/BlueTube.Tests`: all pass.
2. Build the APK (section 1) **and** the desktop app (`dotnet build src/BlueTube.Desktop`). No new warnings.
3. Install on the phone. Screenshot the changed screen. Read the logs. For UI changes also open the desktop app (section 9a) in the browser pane.
4. Commit. One focused commit per change. STE body. Example:

   ```
   feat: add view count to videos

   Get statistics with videos.list. Same quota cost.
   Show views on VideoCard. Old videos show no count.

   Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
   ```

5. `git push`. CI (`.github/workflows/tests.yml`) runs Core tests on Linux. Check with `gh run list --limit 3`.
6. Tell the owner what you tested on the phone and what you did not test.
