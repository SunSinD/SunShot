<img src="assets/sunshot.png" width="64" alt="SunShot logo">

# SunShot

A clean screen capture and recording app for Windows.

**[Download the latest release](https://github.com/SunSinD/SunShot/releases/latest)**<br><sub>Windows 10 version 2004 or newer, x64. The .NET runtime is included.</sub>

Coming from ShareX? [Bring your settings with you](#moving-from-sharex).

Capture your screen, annotate it, then copy, save or share it. SunShot is built on **ShareX 21.0.0**, with the same menus and tools, a compact toolbar and its own settings and updates.

## Download

| Package | Getting started |
| --- | --- |
| **Installer** (`.exe`) | Run it, then open SunShot. |
| **Portable** (`.zip`) | Extract it and open `SunShot.exe`. Keep the folder together. |

See [what's new](CHANGELOG.md).

> SunShot is an early beta. Downloads are unsigned, so Windows may show a publisher warning.

## Features

**Capture** regions, windows, full screens and scrolling pages.

**Record** video or GIFs. FFmpeg downloads the first time you need it.

**Edit** with arrows, text, shapes, blur and more.

**Keep your workflow** with hotkeys, capture history and task settings.

**Choose your look** with light and dark themes, in English or French.

New profiles save captures locally. Uploading and automatic update checks are off until you turn them on. See the [privacy notes](docs/PRIVACY.md).

## Moving from ShareX

1. Close ShareX.
2. In SunShot, choose **Import ShareX settings**.
3. Select your ShareX profile folder, usually `Documents\ShareX`.
4. Read the report, then import. SunShot backs up its profile and restarts.

Works with ShareX 21.0 and 22.0 to 22.4. You can import your capture history and keep saving to your ShareX screenshot folder. Hotkeys, uploads and watch folders start off so you can review them first.

See the [migration guide](docs/MIGRATION.md) for what carries over.

## Contributing

Bug reports, testing and C#/WinForms contributions are welcome. [Open an issue](https://github.com/SunSinD/SunShot/issues) for feedback. Development is assisted by AI.

<details>
<summary><b>Build from source</b></summary>

Use the .NET 10 SDK on Windows:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
```

To package a clean, committed checkout, run `./build/Package.ps1`. It needs Inno Setup 6.5 or newer (or pass `-IsccPath`).

See [testing](docs/TESTING.md) and [third party software](docs/THIRD-PARTY.md).

</details>

## Credits and license

Based on [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0) by the ShareX team and contributors. SunShot is an independent project by **SunSinD** and is not affiliated with ShareX. Original notices, translations and third party licenses are kept.

Licensed under [GPL 3.0](LICENSE.txt).
