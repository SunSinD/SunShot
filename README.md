<img src="assets/sunshot.png" width="64" alt="SunShot logo">

# SunShot

A clean screen capture and recording app for Windows.

**[Download the latest release](https://github.com/SunSinD/SunShot/releases/latest)**<br><sub>Windows 10 version 2004 (build 19041) or newer, x64. The .NET runtime is included.</sub>

Coming from ShareX? [Bring your settings with you](#moving-from-sharex).

Capture your screen, add annotations, and copy, save or share the result. Built on **ShareX 21.0.0**, SunShot keeps the familiar menus and tools, with a compact toolbar and its own settings and updates.

## Download

| Package | Getting started |
| --- | --- |
| **Installer** (`.exe`) | Run the installer, then open SunShot. |
| **Portable** (`.zip`) | Extract to a writable folder and open `SunShot.exe`. Keep the whole folder together. |

See [what's new](CHANGELOG.md) in each release.

> SunShot is an early beta. Downloads are unsigned, so Windows may show a publisher warning.

## Features

- **Capture** regions, windows, full screens and scrolling pages.
- **Record** video or GIFs. Download FFmpeg when you first need it.
- **Edit** with arrows, text, shapes, blur and other annotation tools.
- **Keep your workflow** with custom hotkeys, capture history and task settings.
- **Choose your look** with light and dark themes and translations, including French.

### Out of the box

New profiles start in English and copy and save screenshots locally. Automatic update checks start off. Uploading is optional: configure your destination first. Services needing application keys are not preconfigured; custom uploaders and your own credentials remain available.

Read the [privacy notes](docs/PRIVACY.md) for details.

## Moving from ShareX

1. Choose **Import ShareX settings** in the main window.
2. Select your ShareX profile folder, usually `Documents\ShareX`.
3. Review the compatibility report, then import.
4. SunShot backs up its current profile, copies the settings and restarts.

**SunShot 0.1.2 accepts ShareX 21.0 and 22.0 through 22.4 settings.** The older 0.1.0 release supports ShareX 21.0 only.

- Your ShareX files are preserved. Capture history can be merged while leaving images in place, and you can keep saving new captures in your existing ShareX screenshot folder.
- Common settings carry over. Newer tools, native recording and themes have limits explained in the report.
- Uploading, hotkeys, external actions and watch folders stay disabled until you review them. Close ShareX before enabling the same global shortcuts in SunShot.

The [migration guide](docs/MIGRATION.md) covers supported versions, what carries over and how to turn features back on.

## Contributing

Bug reports, testing and C#/WinForms contributions are welcome. [Open an issue](https://github.com/SunSinD/SunShot/issues) for feedback. Development is AI-assisted.

<details>
<summary><b>Build from source</b></summary>

Use the .NET 10 SDK on Windows:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
```

To package a clean, committed checkout, run `./build/Package.ps1`. Packaging also needs Inno Setup 6.5 or newer, available through `-IsccPath`.

See [testing](docs/TESTING.md) and [third-party software](docs/THIRD-PARTY.md).

</details>

## Credits and license

Based on [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0), by the ShareX team and contributors. SunShot is an independent project by **SunSinD**, unaffiliated with ShareX. Original copyright notices, translations and third-party licenses are preserved.

Licensed under [GPL-3.0](LICENSE.txt).
