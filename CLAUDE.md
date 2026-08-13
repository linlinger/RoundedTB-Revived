# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

RoundedTB Revived — a community-maintained continuation of RoundedTB (originally by torchgm, GPL v3). It lets users add margins, rounded corners, and "segments" (separate rounded app-list / tray / widgets sections) to the Windows 10 & 11 taskbars. It works by attaching a **window region** (`SetWindowRgn` + `CreateRoundRectRgn`) to the actual taskbar HWNDs owned by Explorer.

Note: the user-visible name is "RoundedTB Revived" (window title, tray tooltip, About); the internal assembly/namespace/exe name is still `RoundedTB` and must not be renamed.

The repo is a C#/.NET **WPF** app — there is no Minecraft/Fabric code here. The git root is `RoundedTB/` (this folder), not the parent `roundedtb-revived/`.

## Maintenance goals

This is a community-maintained continuation of the original project (see `ANALYSIS-R3.1-to-HEAD.md` for what changed between R3.1 and the last upstream commit; `PROGRESS.md` is the work log). When changing code:

- **Compatibility first**: support Windows 10 (1607+) through Windows 11 24H2. Win11 22H2+ depends on the UIA-based geometry in `Taskbar.cs` (`GetTrueTaskbarContentBounds` + centred detection).
- **Preserve original design behaviour**: align behaviour with the R3.1 baseline (e.g. tray icon theme mapping, close-button-hides-window). The internal assembly/exe/namespace stay `RoundedTB`; only the user-visible name is "RoundedTB Revived".
- **Bugs and stability before new features.** New features (e.g. per-segment hiding) are deferred/TODO.
- i18n further work is TODO (the JSON `Strings/` infrastructure is in place, see the Localization section).
- All changes go through git commits with `fix:` / `feat:` / `docs:` prefixes.

## Build & run

Windows-only; requires Visual Studio / MSBuild (VS 2022 Build Tools work) and NuGet. There is **no test project** and no linter — do not invent test commands.

`build.bat` at the repo root builds the main app with whichever tool is on PATH (`msbuild` if present, else `dotnet`):

```bash
build.bat
# equivalent manual commands:
dotnet build RoundedTB/RoundedTB.csproj -c Release
# or (matches CI): msbuild -restore -property:Configuration=Release -t:RoundedTB
```

The project targets **`net8.0-windows10.0.19041`**. Output lands in `RoundedTB/bin/Release/net8.0-windows10.0.19041/RoundedTB.exe` (the launcher) plus `RoundedTB.dll` (the real app). Requires only the .NET SDK (a WPF build works with `dotnet build` — the legacy `IWshRuntimeLibrary` COM reference was removed and `EnableStartup()` now uses late-bound `WScript.Shell` via `dynamic`, so no .NET-Framework-only MSBuild is needed). CI (`.github/workflows/ci.yml`) builds on `windows-2022` and uploads the Release output as `rtb-artifacts`.

MSIX packaging lives in `PackagingProject/RoundedTB.Package.wapproj` (Desktop Bridge / WAP) — it references the main csproj and is NOT part of the sln's build targets. It still requires Visual Studio tooling and is not built by `build.bat`.

## Architecture

Everything lives in one project (`RoundedTB/RoundedTB/`), namespace `RoundedTB`, with a single main window. The core loop:

1. **`MainWindow` (MainWindow.xaml.cs)** — the app's only real window; all orchestration starts here. On startup it: checks the OS build number from the registry (`>= 21996` ⇒ Windows 11) and branches Win11 vs Win10 behavior; enforces single-instance (a second launch renames an existing window to `RoundedTB_SettingsRequest` to ask it to show itself); loads/saves `Types.Settings` via `Interaction.ReadJSON/WriteJSON`; then calls `Taskbar.GenerateTaskbarInfo()` and applies the initial regions. `ApplyButton_Click` is the central "commit settings to taskbars" path.
2. **`Taskbar.cs`** (static) — finds taskbars via `FindWindowExA`: `Shell_TrayWnd` (main) + `Shell_SecondaryTrayWnd` (per-monitor), then each one's tray (`TrayNotifyWnd`; `Windows.UI.Composition.DesktopWindowContentBridge` on Win11) and app-list (`MSTaskSwWClass` / `MSTaskListWClass`). `UpdateSimpleTaskbar` / `UpdateDynamicTaskbar` build the regions. **All geometry is physical pixels** — every margin/radius is multiplied by `taskbar.ScaleFactor` (= `GetDpiForWindow(hwnd) / 96`).
   - **Windows 11 22H2+ compatibility (important)**: on modern Windows the taskbar content is rendered by a XAML island and the legacy `MSTaskSwWClass` window rect no longer tracks the real icon positions — it made dynamic mode clip running apps and leave the strip left of the Start button visible. `UpdateDynamicTaskbar` therefore derives the segment's horizontal span from **UI Automation** (`GetTrueTaskbarContentBounds`, using `System.Windows.Automation`), falling back to the legacy rect when UIA returns nothing. `Background.DoWork` re-queries these bounds every ~1 s (infrequent tick) and forces a redraw when they change. `CheckIfCentred()` also tolerates a missing `TaskbarAl` registry value (defaults to centred on Win11).
3. **`Background.cs`** — the heart: a `BackgroundWorker` (`DoWork`) that polls **every ~100 ms**. It re-checks taskbar count/handles, cursor position (auto-hide fade animation, hover-to-reveal segments), maximized windows (the `FillOnMaximise` feature via `TaskbarShouldBeFilled`), and reapplies regions whenever any rect changed (`TaskbarRefreshRequired`).
4. **`LocalPInvoke.cs`** — large flat pile of `[DllImport]` declarations (user32/gdi32/shell32/dwmapi), structs (`RECT`, `POINT`, `WINDOWPLACEMENT`…), and constants/enums (`WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, `LWA_ALPHA`, `SetWindowPosFlags`, `AppBarStates`, `DWMWINDOWATTRIBUTE`…). All native interop goes through here.
5. **`Interaction.cs`** — misc helpers: JSON settings + log file I/O, top-level window enumeration, work-area manipulation for auto-hide, and **TranslucentTB compatibility** (sends the registered `TTB_ForceRefreshTaskbar` window message to `TTB_WorkerWindow`).
6. **`Types.cs`** — data model: `Types.Taskbar` (HWNDs + rects + scale factor + flags), `Types.Settings` (all user options, persisted as JSON), `Types.SegmentSettings` (per-segment corner radius + 4 margins), `Types.EffectiveRegion`.

Supporting files: `AppBars.cs` (SHAppBarMessage wrappers), `MonitorStuff.cs` (display enumeration), `IAppVisibility.cs` (COM interop for Start-menu/monitor visibility — appears legacy/unused), `TaskbarEffect.xaml(.cs)` (unused stub per its own comment), `AboutWindow.xaml` / `Infobox.xaml` (dialogs).

## Localization (i18n)

UI text lives in `Strings/*.json` (one file per language — `en.json`, `zh-Hans.json`, `zh-Hant.json`), copied to the output `Strings/` folder at build time. This is a lightweight, hand-editable alternative to `.resx` (like a Linux `.po` file): translators can edit the JSON directly, no rebuild needed. Full guide: `i18n-GUIDE.md` at the repo root.

- `Localization.Init()` (called in `App.OnStartup`, before any window) builds a **candidate list** from the OS UI culture and loads the first existing file: full name (`zh-CN`), underscore form (`zh_CN`), two-letter (`zh`), then script names for Chinese (`zh-Hans`/`zh_Hans` or `zh-Hant`/`zh_Hant`), finally falling back to `en`. Adding a language = dropping a `<lang>.json` in `Strings/`, no code change (as long as a candidate matches).
- If a candidate file **exists but fails JSON parsing**, `Localization` sets `HasLanguageError`/`ErrorFile` and falls back to English; `App.OnStartup` then shows a bilingual MessageBox ("language file is invalid").
- XAML strings use `{l:Loc Key}` (`xmlns:l="clr-namespace:RoundedTB"`); code strings use `Localization.Get("Key")`.
- Missing keys render as the key itself so gaps are visible.
- **No runtime hot-swap**: language is chosen at startup; changing it needs a restart.
- Key naming convention: `Main_` / `Menu_` / `Help_` / `About_` / `Info_` prefixes.
- JSON files must stay **UTF-8 without BOM**.

## Important quirks

- **`Interaction.AddLog` is a no-op** — its body is commented out. `%LocalAppData%\rtb.log` is created but nothing is ever written to it; debug output relies on `Debug.WriteLine`. Don't expect logs to explain behavior.
- **Settings file**: `%LocalAppData%\rtb.json` (plain machine-agnostic runs) or the app's `RoamingFolder` when running as an MSIX/UWP app (detected via `DesktopBridge.Helpers` / `IsRunningAsUWP()`).
- **Regions are applied to Explorer's taskbar windows**, not to RoundedTB's own window. On clean exit (`shouldReallyDieNoReally == true`), every taskbar is reset (region removed, layered/transparent styles cleared) so the taskbar returns to normal. Closing the window normally only hides it to the tray.
- `MainWindow.version` (int, `-1` = Canary) gates settings migration; build-specific.
- The codebase was migrated with the .NET Upgrade Assistant (see `upgrade-assistant.clef` / `AnalysisReport.sarif` at the repo root) and targets `net8.0-windows10.0.19041` (was `net6.0-windows10.0.19041`). Old code may still carry migrated-but-unused remnants (e.g. `AppBars.cs` largely duplicates `LocalPInvoke`'s appbar bits).
- **Config schema migration**: older builds saved `rtb.json` with a pre-segment-settings schema (`CornerRadius`/`MarginBasic`/`ShowTrayOnHover`). The current `Types.Settings` deserializes those as null, so `MainWindow` defaults any missing `*Layout` object at startup. A user upgrading from an old build should re-enable "Show segments only when hovered" once (`ShowTrayOnHover` does not map to `ShowSegmentsOnHover`).
- UI is styled with the `WPF-UI` (WPFUI) package; `App.OnStartup` starts its theme watcher. Notify icons via `Hardcodet.NotifyIcon.Wpf`.
