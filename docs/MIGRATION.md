# Moving from ShareX

SunShot 0.1.1 and newer import ShareX 21.0 and 22.0 to 22.4. SunShot 0.1.0 imports ShareX 21.0 only.

1. Close ShareX.
2. Open SunShot and choose **Import ShareX settings**.
3. Select the folder containing `ApplicationConfig.json`, usually `Documents\ShareX`.
4. Keep **Import capture history** checked to bring your old captures into History. Check **Save new captures in the existing ShareX screenshot folder** to keep using that folder.
5. Read the report, then choose **Import and restart**.
6. Turn hotkeys on from the tray menu (**Enable hotkeys**). To upload, clear **DisableUpload** in **Application settings > Advanced**.

## What carries over

- Capture settings, workflows, hotkeys and upload destinations, including saved logins.
- ShareX 21 recording settings, including video and audio devices.
- Capture history. Images stay where they are, and duplicates are skipped.
- Your ShareX screenshot folder, if you choose to keep using it.

## What doesn't

- An `ffmpeg.exe` inside the ShareX profile folder. Select it again in SunShot's recording settings, or let SunShot download its own.
- ShareX 22's built-in recorder devices (microphone, camera, system audio). Choose them again in SunShot's recording settings.
- ShareX 22 themes, newer tools and image formats SunShot doesn't have.
- Upload services SunShot doesn't support. Those workflows won't upload until you pick a new destination.
- Saved logins from another Windows account.
- Startup settings, file associations, custom config folders and `PersonalPath.cfg`.

Automatic actions, deletion, AI tasks and watch folders also start off. The report lists anything that needs a look.

## Restore a backup

Each import saves a backup in the `Backup` folder of your SunShot profile. Backups can contain saved logins.

1. Close SunShot and copy your current profile somewhere safe.
2. For each `.original` file in the backup (`ApplicationConfig.json`, `HotkeysConfig.json`, `UploadersConfig.json`, `History.db`), copy it into the profile and remove `.original` from its name.
3. If the backup has an `.absent` file instead, delete that file from the profile. It didn't exist before the import.
4. Don't copy `.new` or `.restore` files. Don't restore `History.db` while a `History.db-wal`, `-shm` or `-journal` file is next to it.

If an import was interrupted, just reopen SunShot. It rolls the import back on its own.
