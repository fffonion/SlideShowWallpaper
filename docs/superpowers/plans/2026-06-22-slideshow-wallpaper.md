# SlideShow Wallpaper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WinUI 3 desktop wallpaper player with one process, per-monitor playback windows, tray controls, autostart, HEIC image support, GitHub Actions release, and single-file publishing.

**Architecture:** The app uses an unpackaged WinUI 3 executable. The settings window owns configuration, while per-monitor borderless player windows render images behind desktop icons. Core scheduling, file scanning, config, and geometry logic lives in testable C# services.

**Tech Stack:** C# WinUI 3, Windows App SDK, xUnit, Win32 interop, Windows Forms tray icon, Windows Imaging Component via WinUI image decoding, GitHub Actions on `windows-latest`.

---

### Task 1: Scaffold and Project Shape

**Files:**
- Create: `SlideShowWallpaper.csproj`
- Create: `SlideShowWallpaper.Tests/SlideShowWallpaper.Tests.csproj`
- Create: `SlideShowWallpaper.sln`
- Create: `.github/workflows/release.yml`
- Create: `Properties/launchSettings.json`
- Create: `app.manifest`

- [ ] Generate the WinUI app in the workspace root with project name `SlideShowWallpaper`.
- [ ] Convert the project to unpackaged output with `WindowsPackageType=None`, x64, explicit compile items, and single-file publish target copied from the local reference style.
- [ ] Add an xUnit project that references the app project.
- [ ] Add a release workflow that restores, builds, publishes `artifacts/release/win-x64/SlideShowWallpaper.exe`, computes SHA256, and uploads both files.
- [ ] Verify `dotnet restore` succeeds.

### Task 2: Core Models and Tests

**Files:**
- Create: `Models/WallpaperConfig.cs`
- Create: `Models/MonitorProfile.cs`
- Create: `Models/ImagePlaybackItem.cs`
- Create: `Services/ImageLibrary.cs`
- Create: `Services/PlaybackQueue.cs`
- Test: `SlideShowWallpaper.Tests/ImageLibraryTests.cs`
- Test: `SlideShowWallpaper.Tests/PlaybackQueueTests.cs`

- [ ] Write tests proving supported image extensions are `jpg`, `jpeg`, `png`, `bmp`, `webp`, `heic`, and `heif`.
- [ ] Write tests proving GIF and video files are ignored.
- [ ] Write tests proving sequential queue ordering and loop behavior.
- [ ] Implement the minimum model and service code for those tests.
- [ ] Run `dotnet test`.

### Task 3: Monitor Detection and Window Placement

**Files:**
- Create: `Services/MonitorService.cs`
- Create: `Services/DesktopHostService.cs`
- Create: `Interop/NativeMethods.cs`
- Test: `SlideShowWallpaper.Tests/MonitorProfileTests.cs`

- [ ] Add testable monitor DTOs and mapping helpers.
- [ ] Enumerate screens through Windows Forms `Screen.AllScreens`.
- [ ] Create Win32 helper methods to find the WorkerW desktop host, set parent windows, and place player windows.
- [ ] Handle Explorer restart by rebuilding hosted windows.

### Task 4: Playback Windows

**Files:**
- Create: `Windows/WallpaperWindow.xaml`
- Create: `Windows/WallpaperWindow.xaml.cs`
- Create: `Services/WallpaperPlaybackCoordinator.cs`

- [ ] Create one borderless WinUI window per monitor.
- [ ] Render images with Fit, Stretch, and Original modes.
- [ ] Apply horizontal and vertical offsets.
- [ ] Add Fade, Slide, and None transitions with configurable duration.
- [ ] Add per-monitor pause/resume and next-image commands.

### Task 5: Settings UI

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`
- Create: `ViewModels/MainViewModel.cs`
- Create: `ViewModels/MonitorSettingsViewModel.cs`
- Create: `Services/FolderPickerService.cs`
- Create: `Services/SettingsStore.cs`

- [ ] Build a WinUI settings window with one tab per current monitor.
- [ ] Add folder selection, scale mode, offsets, playback mode, interval, transition, duration, pause, and next controls.
- [ ] Persist JSON settings under `%AppData%\SlideShowWallpaper\settings.json`.
- [ ] Reload settings on launch and bind each monitor tab to its saved profile.

### Task 6: Tray and Autostart

**Files:**
- Create: `Services/TrayIconService.cs`
- Create: `Services/AutostartService.cs`
- Modify: `App.xaml.cs`
- Modify: `MainWindow.xaml.cs`

- [ ] Add Windows Forms tray icon in the same process.
- [ ] Left-click opens the settings window.
- [ ] Right-click shows global open/exit plus per-monitor pause/resume and next commands.
- [ ] Closing the settings window hides it to tray unless exit is requested.
- [ ] Toggle autostart using `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

### Task 7: Verification

**Files:**
- Modify as needed after build feedback.

- [ ] Run `dotnet test`.
- [ ] Run `dotnet build .\SlideShowWallpaper.csproj -p:Platform=x64`.
- [ ] Run `dotnet msbuild .\SlideShowWallpaper.csproj -t:BuildSingleFile -p:Configuration=Release -p:Platform=x64 -v minimal`.
- [ ] Launch the built debug app and confirm a top-level settings window appears.
- [ ] Leave the final app instance running.

## Self-Review

- Requirements covered: per-monitor tabs, folders, Fit/Stretch/Original, offsets, ordering, loop, interval, transitions, tray controls, autostart, HEIC, one executable, Actions, single-file publish.
- Scope limits: no system wallpaper API, no GIF playback, no video playback.
- Risk areas: desktop WorkerW hosting can vary after Explorer restarts; the coordinator will rehost windows on display and shell changes.
