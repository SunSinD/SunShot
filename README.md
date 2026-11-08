<p align="center"><img src="assets/sunshot.png" width="96" alt="SunShot logo"></p>

# SunShot

A clean screen capture and recording app.

Capture your screen, add annotations, and copy, save or share the result. SunShot builds on ShareX 21.0.0 with familiar menus, fewer distractions, and its own settings and updates.

## Download

Get the [latest release](https://github.com/SunSinD/SunShot/releases/latest). Requires **Windows 11 22H2 or newer, x64**. The .NET runtime is included.

- **Installer:** run `SunShot-0.1.0-setup-x64.exe`.
- **Portable:** extract `SunShot-0.1.0-portable-x64.zip` to a writable folder and open `SunShot.exe`. Keep the whole folder together.

Version 0.1.0 is an early beta. Downloads are unsigned, so Windows may show a publisher warning.

## Make it yours

- Capture regions, windows, full screens, and scrolling pages.
- Record video or GIFs. SunShot offers to download FFmpeg on first use.
- Annotate images with arrows, text, shapes, blur, and other editor tools.
- Use the compact capture, copy, save, edit, and upload toolbar.
- Keep capture history, custom shortcuts, workflows, and your preferred upload destination.
- Use the existing light and dark themes and translations, including French.

New profiles start in English. You can choose another language in Application settings.

New profiles save and copy screenshots locally. Uploading is optional. Configure your destination before using it; services needing application keys are not preconfigured. Custom uploaders and services with your own credentials remain available.

## Moving from ShareX

Choose **Import ShareX settings** in the main window and select a ShareX **21.0** profile. SunShot copies the settings, backs up its current profile, and restarts. Uploading, hotkeys, external actions, and watch folders stay disabled until you review them. Your ShareX files are preserved.

Close ShareX before enabling the same global shortcuts in SunShot. See [migration](docs/MIGRATION.md) for details and limits.

## Build and checks

Use the .NET 10 SDK on Windows:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
./build/Package.ps1
```

Packaging also needs Inno Setup 6.5 or newer, available through `-IsccPath`. See [testing](docs/TESTING.md), [privacy](docs/PRIVACY.md), and [changes](CHANGELOG.md).

Development is AI-assisted. Feedback and C#/WinForms contributions are welcome.

## Credits and license

Based on [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0), by the ShareX team and contributors. SunShot is an independent project by SunSinD. Original copyright notices, translations, and third-party licenses are preserved.

Licensed under [GPL-3.0](LICENSE.txt). See [third-party software](docs/THIRD-PARTY.md) for bundled components and source links.
