# SunShot

A screen capture and recording app for Windows, based on ShareX 21.0.0.

**[Download the latest release](https://github.com/SunSinD/SunShot/releases/latest)**
Windows 10 version 2004 (build 19041) or newer, x64. The .NET runtime is included.

## Features

- Capture regions, windows, full screens and scrolling pages.
- Record video or GIFs with FFmpeg.
- Annotate with text, shapes, arrows and blur.
- Use hotkeys, capture history and task settings.
- Choose light or dark themes, with French translation.

New profiles copy and save captures locally. Uploading and automatic update checks start off.

## Download

- **Installer:** run the `.exe`, then open SunShot.
- **Portable:** extract the `.zip` and open `SunShot.exe`. Keep the folder together.

Downloads are unsigned. Windows may show a publisher warning.

## Moving from ShareX

1. Close ShareX.
2. Choose **Import ShareX settings** in SunShot.
3. Select the ShareX profile folder, usually `Documents\ShareX`.
4. Review the report, choose the history and capture-folder options, then import.

SunShot backs up its current profile and restarts. ShareX 21.0 and 22.0 through 22.4 are supported. See the [migration guide](docs/MIGRATION.md) for details.

## Build

Use the .NET 10 SDK on Windows:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
```

Package a clean checkout with `./build/Package.ps1`. See [testing](docs/TESTING.md) and [third-party software](docs/THIRD-PARTY.md).

## Credits and license

Based on [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0), by the ShareX team and contributors. SunShot is an independent project by **SunSinD**. Original notices and licenses are preserved.

Licensed under [GPL-3.0](LICENSE.txt).
