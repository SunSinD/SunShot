# Changes

## 0.1.0

First SunShot release, based on ShareX 21.0.0. Early beta for Windows 11 x64.

### Changed

- Add SunShot branding, a sun logo, separate settings, and SunShot-only updates.
- Add a compact toolbar for capture, recording, copying, saving, editing, and uploading.
- Hide AI analysis and unrelated utilities from the main menus and task pickers.
- Start new profiles in English with automatic update checks off.
- Add an explicit ShareX 21.0 settings import with backups and safe defaults.
- Move to .NET 10 and update SSH.NET and SQLite dependencies.

### Fixed

- Let watched files finish growing beyond five seconds before processing them.
- Stop pending watch-folder callbacks after the watcher is disabled.
- Keep editor commands in sync when selecting another annotation with a right click.
- Confirm uploads of selected files from the main window.
