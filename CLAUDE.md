# CLAUDE.md — BlueTube

Personal app for watching **curated, high-quality tech YouTube videos and Shorts**. Single user. Runs on the owner's phone (Android, sideloaded APK) and on Windows/Linux PCs (desktop app). No backend server.

Claude Code writes most of this project. Follow this file strictly. Ask before deviating from the stack or structure.

How-to for common tasks (add channels on the phone, add fields, change Videos/Shorts, build, install, test): [docs/AGENT-GUIDE.md](docs/AGENT-GUIDE.md).

---

## Owner context

- Strong in **C#** (Unity background). New to MAUI, Blazor, HTML/CSS, SQLite.
- Wants the **fewest tools possible** while staying professional.
- Explain new concepts briefly in PR/commit messages or comments only where non-obvious. Keep responses concise.

---

## Stack (fixed — do not add to it without asking)

| Need | Use |
|---|---|
| UI | **Blazor** (`.razor`, HTML, CSS) in a shared Razor class library |
| Android host | **.NET 10 MAUI Blazor Hybrid** |
| Desktop host (Windows, Linux) | **ASP.NET Core Blazor Server** on `127.0.0.1`, shown in a browser app window (shared framework, no extra NuGet) |
| Storage | **SQLite** via `sqlite-net-pcl` |
| HTTP / JSON | Built-in `HttpClient` + `System.Text.Json` (no Google SDK) |
| Secrets | Android: MAUI `SecureStorage`. Desktop: plain JSON file in the user data folder (owner's choice; Linux mode 600) |
| Tests | **NUnit** |
| CI | GitHub Actions |

**Do NOT add:** Google.Apis.*, Moq/NSubstitute, bUnit, CSS frameworks (Tailwind/Bootstrap), MVVM toolkits, DI containers beyond the built-in one, any JS framework. Plain CSS only. Hand-written fakes for tests.

Allowed NuGet packages: `sqlite-net-pcl`, `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Components.Web` (BlueTube.UI only; same version MAUI uses). Anything else → ask first.

---

## Solution structure

```
BlueTube.sln
src/
  BlueTube.Core/        # class library (net10.0) — ALL logic, no MAUI references
    Models/             # Video, Channel, Tag, ...
    Services/           # interfaces + implementations
      IYouTubeService.cs / YouTubeService.cs
      IVideoRepository.cs / SqliteVideoRepository.cs
      IQualityFilter.cs / LlmQualityFilter.cs
      FeedService.cs    # orchestrates fetch → dedupe → store → score
      JsonFileSettingsStore.cs  # desktop ISecretStore + IAppSettings
      ChannelTransfer.cs        # channel list export/import (JSON text)
  BlueTube.UI/          # Razor class library (net10.0) — ALL pages/components, shared by both hosts
    Components/
      Routes.razor
      Layout/           # MainLayout.razor (DB init, ErrorBoundary, tab bar)
      Pages/            # Feed.razor, Shorts.razor, Watch.razor, Channels.razor, Settings.razor
      Shared/           # VideoCard.razor, ShortPlayer.razor, TagChip.razor, KeyField.razor
    Services/ErrorText.cs
    wwwroot/            # app.css, app.js → served as _content/BlueTube.UI/...
  BlueTube.App/         # MAUI Android host only: MauiProgram, MainPage, Platforms/Android, SecureStorage/Preferences
  BlueTube.Desktop/     # Windows/Linux host: Program.cs, App.razor (HTML shell), AppWindow, AutoShutdown
tests/
  BlueTube.Tests/       # NUnit, references Core only
    Fakes/              # FakeYouTubeService, FakeVideoRepository, FakeQualityFilter
.github/workflows/tests.yml    # Core tests
.github/workflows/desktop.yml  # desktop publish (win-x64, linux-x64) + Linux smoke test + artifacts
```

**Rules**
- `BlueTube.Core` and `BlueTube.UI` must never reference MAUI. Both build on Linux.
- No MAUI APIs (`Launcher`, `FileSystem`, `Preferences`, ...) in `BlueTube.UI`. Put platform code behind a Core interface, implement it in each host.
- `.razor` files contain UI and minimal glue. Business logic goes in Core services.
- Every service has an interface. Register it in **both** `src/BlueTube.App/MauiProgram.cs` and `src/BlueTube.Desktop/Program.cs`.
- Each component may have a scoped `Component.razor.css`. Global styles in `BlueTube.UI/wwwroot/app.css`.
- UI must work at phone width and on a wide desktop window (see `.grid` in Feed, Shorts max-width).

---

## YouTube integration

- Use **YouTube Data API v3** REST endpoints directly with `HttpClient`.
- **Never use `search.list`** for routine refreshes (100 quota units). Daily quota is 10,000.
- Fetch channel uploads via `playlistItems.list` (1 unit):
  - Channel ID `UC...` → long videos playlist `UULF...`, Shorts playlist `UUSH...` (replace the `UC` prefix). Unofficial but reliable; fall back to `UU...` + duration check (`videos.list`, ≤ 180 s and vertical = Short) if a playlist 404s.
- Batch `videos.list` calls (up to 50 IDs per call) for duration/stats.
- Cache everything in SQLite. Only fetch items newer than the last stored `publishedAt` per channel.
- **Playback:** official YouTube IFrame embed only, ads included. No ad-blocking, no downloading, no scraping — stay within YouTube API ToS.
  - If the embed shows error 152/153 in the WebView, fix by supplying a valid `origin` / referrer for the iframe — do not switch to unofficial players.
- Shorts screen: full-screen vertical swipe using CSS `scroll-snap-type: y mandatory`; only the visible item has a live player.

---

## AI quality filter

- `IQualityFilter.ScoreAsync(Video)` → score 0–100 + tags (e.g. `unity`, `ai-ml`, `csharp`, `hardware`, `gamedev`).
- Input: title, description, channel name (transcripts optional, later).
- LLM provider + API key configurable in Settings, stored in `SecureStorage`. Keep provider-specific code behind the interface.
- Score each video **once**; store result. Feed hides videos below a user-set threshold.

---

## Data model (starting point)

- `Channel`: Id, Title, ThumbnailUrl, Tags, LastFetchedAt
- `Video`: Id, ChannelId, Title, Description, ThumbnailUrl, PublishedAt, DurationSeconds, IsShort, QualityScore (nullable), AiTags, Watched, WatchLater, Favorite
- Use `sqlite-net-pcl` attributes (`[PrimaryKey]`, `[Indexed]`). DB file in `FileSystem.AppDataDirectory`.

---

## Secrets

- **Never commit API keys.** No keys in source, config files, or tests.
- Keys entered by the user in Settings → `SecureStorage`.
- `.gitignore` must include standard .NET/VS ignores plus `*.local.json`.

---

## Testing

- NUnit tests in `tests/BlueTube.Tests`, Core only.
- Use hand-written fakes in `Fakes/`. No network calls in tests.
- SQLite tests use a temp file, deleted in `[TearDown]`.
- Cover: API JSON parsing, Short vs long classification, dedupe/incremental fetch, repository CRUD, filter threshold logic.
- Run `dotnet test tests/BlueTube.Tests` before every commit; all must pass.

## CI

`.github/workflows/tests.yml`: on push + PR, `ubuntu-latest`, setup .NET 10, `dotnet test tests/BlueTube.Tests`. Do not build the MAUI app in CI for now.

`.github/workflows/desktop.yml`: publish `BlueTube.Desktop` for `linux-x64` and `win-x64` (self-contained), start the Linux build and check it serves `/`, `blazor.web.js` and the `_content/BlueTube.UI` files, upload both folders as artifacts.

---

## Build phases (do in order, one at a time)

1. **Scaffold** — solution, three projects, `.gitignore`, README, CI workflow, one passing placeholder test.
2. **Core models + repository** — SQLite repo with tests.
3. **YouTube service** — playlistItems + videos fetching, parsing, tests with fake HTTP responses (use a fake `HttpMessageHandler`).
4. **Feed page** — list of long videos from stored channels, `VideoCard` component, pull-to-refresh.
5. **Watch page** — embedded player, mark watched / watch later / favorite.
6. **Shorts page** — vertical swipe player.
7. **Channels + Settings pages** — add/remove channels (by ID or handle), tags, API keys.
8. **AI quality filter** — scoring, tags, threshold slider, filter chips on Feed.
9. **Polish** — dark theme, loading/empty/error states, app icon.

Finish a phase (code + tests green + short commit) before starting the next. Summarize what was done and what's next at the end of each phase.

---

## Conventions

- C#: file-scoped namespaces, nullable enabled, `async`/`await` all the way, `CancellationToken` on public async methods.
- Naming: PascalCase types/methods, `_camelCase` private fields.
- Small, focused commits: `feat: ...`, `fix: ...`, `test: ...`, `chore: ...`. Add a short body: what changed, why.
- Docs, comments, commit messages: ASD-STE100 Simplified Technical English. Fewest words possible.
- Repo is public. Never commit keys or personal data.
- Handle errors gracefully in UI (offline, quota exceeded, invalid key) — never crash.
- Keep it simple. No premature abstractions, no features outside this file without asking.
