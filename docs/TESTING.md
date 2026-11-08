# Testing

Build on Windows with the .NET 10 SDK:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
dotnet list SunShot/ShareX.csproj package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json
dotnet run --project tests/Regression/Regression.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
dotnet run --project tests/SunShot.CoreChecks/SunShot.CoreChecks.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
./build/Package.ps1
```

Packaging requires a clean, committed checkout so the source ZIP describes the application being built.

Use an isolated portable copy for manual checks. Test captures against a synthetic window rather than private desktop content. Keep uploads disabled unless testing against a local receiver. Do not use a real ShareX profile for destructive or failure-injection tests.

Before a release, check:

- First launch, local profile creation, settings persistence, and independent instance handling.
- Region capture, copy, save as, editor annotations, history, and restart.
- Recording, cancellation, FFmpeg checksum verification, and a playable output file.
- Explicit upload confirmation, disabled upload state, custom uploaders, and a local upload receiver.
- Import success, invalid files, missing optional files, credentials, backups, rollback, and unchanged source files.
- Slow watched files and disabling a watcher with pending work.
- Light/dark themes, English/French labels, narrow windows, and keyboard access.
- Installer and portable contents, separate identity, uninstall behavior, checksums, and clean-source build.

Hardware encoders, every upload provider, multi-monitor HDR/DPI combinations, scrolling capture on every app, and Windows versions other than the test host require wider beta testing.

## 0.1.0 checks

- Release x64 publish and installer compilation succeeded on Windows 11.
- The dependency audit reported no known vulnerable packages at the time of the check.
- The regression harness passed 30 assertions covering native SQLite, valid/invalid FFmpeg archives, an eight-second file write, empty files, queued watcher callbacks after disposal or replacement, filtered upload destinations and cancellation, and isolated temporary-folder cleanup. The valid-archive check uses the optional archive path argument.
- A one-off UI harness passed 32 logic assertions and rendered the real MainForm in English/French and light/dark themes at widths from 650 to 1200 pixels. These were offscreen checks at 100% scaling; the temporary harness is not part of the source archive.
- A fresh isolated portable profile was tested with real Windows interaction: English startup, narrow toolbar, synthetic region capture, byte-preserving Save as, modern editor annotations and selection switching, and legacy editor editing and saving. Earlier desktop results from an old output copy were discarded.
- Recording a synthetic window region produced an H.264 MP4 at 580 × 290 and 30 fps. The visible pause, resume, and stop controls worked, the timer stayed still while paused, and FFmpeg decoded both completed recordings without errors. The legacy editor's Save as dialog was not verified by automation.
- A real import of a synthetic ShareX 21.0 profile displayed the result, restarted the portable app, preserved the source hash, created a backup, removed its journal, and loaded the expected local capture tasks with uploads, hotkeys, and watch folders disabled. The tray menu opened and offered Enable hotkeys.
- CoreChecks passed 60 checks using production import/update/settings code and synthetic profiles, including complete serialized ShareX profiles, image-effect type checks, source immutability, rollback, locked files, malformed settings, Windows junctions, English defaults, and queued saves. Symbolic-link creation was skipped because the test token lacked that privilege.

## 0.1.1 candidate checks

- Release x64 CoreChecks passed 359 checks, including the complete existing suite and ShareX 22.0 through 22.4 conversion checks. Symbolic-link creation was skipped for token permissions; Windows junction rejection passed. The original importer-only subset passed 276 checks before the final runtime cases were added. The full 359-check run includes 57 checks of blocked default and inherited tasks, actual custom-uploader selection and the production upload resolver with consent canceled before any network access. This includes workflows that have their own destinations but inherit filters from a blocked default.
- Regression passed 33 assertions with the pinned valid FFmpeg archive, including the new locked, preallocated copy scenario.
- Packaging cleans Release outputs before publishing because Avalonia can reuse older PDB files when rewriting editor assemblies. The privacy gate rejects leaked build paths before archives are created.
- The standalone editor's Debug build passed after updating its developer-tools initialization. Release builds passed, and the dependency audit reported no known vulnerable packages at check time.
- A separate temporary harness used the actual import and review forms in English and French. Labels and review buttons fit at the default size, the compatibility report was read-only with vertical scrolling, and Cancel left the destination uncreated. These checks used synthetic settings and did not apply an import.
- Read-only preparation of a preserved ShareX 22.4 profile succeeded with three hotkey definitions. All inspected source JSON hashes stayed unchanged, and no destination was created. That profile was not applied to the user's installed SunShot.
- The checked-in 22.x fixtures are synthetic JSON with property and enum inventories traced to each upstream release commit. They are not runtime exports from upstream ShareX. Real provider logins, every nested editor option and all combinations of 22.x preferences still need wider testing.
