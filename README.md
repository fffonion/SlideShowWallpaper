# SlideShow Wallpaper

WinUI 3 desktop app for running per-monitor wallpaper slideshows on Windows.

![SlideShow Wallpaper main window](docs/screenshots/main-window-en-screen.png)

The screenshot above uses images from `C:\Windows\Web\Screen`.

## Features

- Per-monitor folders, playback controls, status, and preview lists.
- Lazy thumbnail loading with JPEG thumbnail cache under `%TEMP%`.
- Image and video playback in the same folder, with media filter options for images, videos, or both.
- NDF playback with on-demand materialization for full media and in-memory thumbnail extraction.
- Random, single-loop, name, and modified-date playback order options, plus shuffle for random order.
- Cover, fit, stretch, and original scale modes with per-monitor offsets and transitions.
- Folder watching with collapsed rescans, so playlist updates are picked up after changes.
- Video options for looping, sound, and pausing video while another app is maximized or fullscreen.
- Per-display tray actions, close-to-tray, quiet startup via `/q`, and single-instance activation.
- App settings saved to `SlideShowWallpaper.ini` next to the executable.
- Light, dark, and system theme modes with localized UI resources.

## Build

```powershell
$Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { $env:PROCESSOR_ARCHITECTURE }
dotnet build .\SlideShowWallpaper.csproj -c Debug -p:Platform=$Platform
```

## Test

```powershell
$Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { $env:PROCESSOR_ARCHITECTURE }
dotnet test .\SlideShowWallpaper.Tests\SlideShowWallpaper.Tests.csproj -c Debug -p:Platform=$Platform
```

## Single-File Release

```powershell
$Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { $env:PROCESSOR_ARCHITECTURE }
dotnet build .\SlideShowWallpaper.csproj -c Release -p:Platform=$Platform -t:BuildSingleFile
```

The executable is published to:

```text
artifacts\release\win-x64\SlideShowWallpaper.exe
```

## Quiet Startup

Use `/q` to start directly in the tray:

```powershell
.\artifacts\release\win-x64\SlideShowWallpaper.exe /q
```
