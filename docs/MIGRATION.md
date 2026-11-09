# Moving from ShareX

SunShot 0.1.1 and newer import ShareX 21.0 and 22.0 through 22.4. SunShot 0.1.0 imports ShareX 21.0 only.

1. Close ShareX.
2. Open SunShot and choose **Import ShareX settings**.
3. Select the folder containing `ApplicationConfig.json`, usually `Documents\ShareX`.
4. Keep **Import capture history** selected if you want old records. Select **Save new captures in the existing ShareX screenshot folder** if needed.
5. Read the report, then choose **Import and restart**.
6. Review the settings. Enable hotkeys from the tray menu and configure upload destinations before uploading.

## Imported

- Common capture settings, workflows, hotkeys and upload destinations.
- ShareX 21 FFmpeg options and device names.
- Capture history, with duplicate records skipped.
- An optional existing screenshot folder for new captures.

## Not imported

- External config paths, startup settings, file associations and `PersonalPath.cfg`.
- ShareX 22 native recording devices, themes, newer tools and unsupported image options.
- Unsupported providers and filters. These workflows stay blocked until configured.
- Credentials that Windows cannot decrypt for the current account.

Hotkeys, uploads, automatic actions, deletion, AI tasks and watch folders start disabled. The report lists settings that need review. New captures use the SunShot folder unless you select the existing ShareX folder.

## Restore a backup

Import backups are stored in the SunShot profile under `Backup`. Close SunShot, copy the current profile somewhere safe, then restore the backed-up files. If an import stops partway through, reopen SunShot and let its recovery finish before changing files.

The importer reads the selected profile and does not write to ShareX. History keeps its original file paths, so keep those capture folders available.
