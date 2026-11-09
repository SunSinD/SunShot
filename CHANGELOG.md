# Changes

## 0.1.2 (2026-11-09)

- Install and run on Windows 10 version 2004 or newer, not just Windows 11.
- Skip the Windows 11 rounded-corner setting on Windows 10.
- Import report now explains how to keep using an existing ffmpeg.exe. ShareX 21 recording devices carry over.
- Shorter, clearer import dialog, messages and docs.

## 0.1.1 (2026-11-08)

### Added

- Import settings from ShareX 22.0 to 22.4, with a compatibility report before anything changes.
- Import capture history and optionally keep saving to the ShareX screenshot folder. Duplicates are skipped.
- New README layout with a screenshot.

### Fixed

- Crash when the main window closes during startup.
- History and Image history uploads now ask for the same confirmation as the main window.
- Watched files that stay locked or keep the same size for more than five seconds are now processed when they finish copying.
- Watched files no longer tie up background threads while waiting.
- An inaccessible watched file or folder no longer closes the app.
- Remaining "ShareX" titles and tooltips renamed to SunShot.
- Editor developer tools setup in Debug builds.
- Exported backups are named after SunShot.
- Imported workflows with an unsupported upload destination stay blocked, including inherited ones.
- Imported preset labels and FFmpeg paths inside the ShareX profile.
- Release packages no longer contain local build paths.

## 0.1.0

First SunShot release, based on ShareX 21.0.0.

### Changed

- SunShot branding, sun logo, separate settings and SunShot-only updates.
- Compact toolbar for capture, recording, copying, saving, editing and uploading.
- AI analysis and unrelated tools hidden from the main menus.
- New profiles start in English with update checks off.
- ShareX 21.0 settings import with backups.
- Moved to .NET 10 and updated SSH.NET and SQLite.

### Fixed

- Watched files that take more than five seconds to finish.
- Watch-folder callbacks after the watcher is turned off.
- Editor commands after right-clicking another annotation.
- Uploading selected files from the main window now asks first.
