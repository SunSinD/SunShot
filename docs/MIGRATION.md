# Moving from ShareX

SunShot uses `Documents\SunShot` for installed settings, captures, and history. Portable builds use the `SunShot` folder beside `SunShot.exe`. Neither mode automatically opens ShareX's profile or follows its `PersonalPath.cfg`.

1. Close ShareX so its settings are saved.
2. Open SunShot and choose **Import ShareX settings**.
3. Select the folder containing ShareX 21.0's `ApplicationConfig.json`. The usual location is `Documents\ShareX`; portable installations may use another folder.
4. Confirm the import, then let SunShot restart.
5. Review the imported settings. To enable hotkeys, choose **Enable hotkeys** in the tray menu. To enable uploading, clear **DisableUpload** in **Application settings > Advanced**. Close ShareX before enabling conflicting shortcuts.

The importer accepts ShareX 21.0 settings only. Newer versions have different settings and are not supported. It copies capture preferences, workflows, hotkey definitions, and upload destinations, including readable saved credentials. Missing hotkey or uploader files use defaults. Credentials protected by a different Windows account are cleared and reported.

Automatic upload, external actions, deletion tasks, AI tasks, watch folders, global hotkeys, and uploading are disabled after import. Screenshot and config paths are reset to SunShot's own folders. History, images, startup settings, file associations, external config files, and `PersonalPath.cfg` are excluded.

SunShot backs up its existing settings under its `Backup` folder before replacement. Backups may contain credentials. A failed or interrupted import is rolled back before settings load at the next startup. The source profile is read only.

To restore a SunShot backup manually, close SunShot and copy the backed-up JSON files into its profile folder. Keep a copy of the current files first. The ShareX importer accepts profile folders, not exported `.sxb` archives.

SunShot uses separate shortcuts, Windows integration, and update downloads. Existing ShareX browser extensions request ShareX's native host name; they do not automatically connect to SunShot. Provider logins using application keys are not configured in this independent build. You can use custom uploaders or configure a supported destination with your own account details.
