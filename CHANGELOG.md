# Changes

## 0.1.1 (unreleased)

### Added

- Import ShareX 22.0 through 22.4 profiles with explicit settings conversions and a compatibility report before applying changes. Unsupported settings are reported; unavailable upload destinations stay blocked until configured.
- Offer capture history import and optional reuse of the existing ShareX screenshots folder. History is merged without moving images, and repeated imports skip duplicates.
- Give the README a clearer layout, download guide and real application screenshot.

### Fixed

- Use the same destination confirmation for History and Image history uploads as the main window.
- Wait for locked, preallocated watched files to finish copying, even when their size stays unchanged for more than five seconds.
- Poll pending watched files asynchronously so locked files do not occupy background threads while waiting.
- Skip inaccessible watched files and directories instead of allowing an access error to close the app.
- Correct remaining recording tooltips and capture, effects, input and output window titles to use SunShot.
- Update the editor's Debug-only developer tools setup for the installed Avalonia version.
- Name exported backups after SunShot, report success accurately and explain manual restoration.
- Preserve blocked uploader selections through default/inherited runtime tasks and prevent inherited extension filters from bypassing them.
- Correct imported preset labels, report skipped external settings paths and clear FFmpeg overrides pointing inside the source profile.
- Clarify manual restoration of import backups and test converted settings with the production loader.
- Clean Release outputs before publishing, package source directly from the clean build commit and check both application DLLs and EXEs for local build paths.

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
