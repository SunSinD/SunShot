# SunShot core checks

Windows 11 and the .NET 10 SDK are required. From the repository root:

```powershell
dotnet run --project tests/SunShot.CoreChecks/SunShot.CoreChecks.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
```

The regression harness also checks upload consent, private temp cleanup, native SQLite, watched files, and FFmpeg download validation:

```powershell
dotnet run --project tests/Regression/Regression.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Pass the path to the pinned FFmpeg ZIP after `--` to include successful installation verification. The invalid-download rejection test runs without it.

Upload consent checks exercise the production filter, BMP-to-PNG processing, and alternate-destination retry paths. Real services are cancelled before creating uploaders; approved retry tests use memory-only uploaders. They verify cancellation, separate consent for a fallback destination, and consent reuse only within the same task. Nothing is uploaded. Private temp cleanup runs in a child process with both `TEMP` and `TMP` redirected to guarded synthetic fixtures, proving that SunShot is cleaned and the sibling ShareX directory remains unchanged.

When using `-p:Platform=x64`, the application used by these checks is under `SunShot/bin/Release/win-x64`. Use that output for subsequent native testing and compare DLL hashes after copying it.

This console harness builds and tests the actual application assembly. It exits with a nonzero code on failure. It creates synthetic profiles under `%TEMP%\sunshot-core-checks-*`; it never starts SunShot, opens a real profile, registers hotkeys, changes startup settings, uploads files, or writes integration registry keys. Synthetic fixtures remain for inspection, and their directory is printed at the end.

Checks cover settings sanitization, encrypted credentials, workflows, source immutability, backup and rollback, interrupted-import recovery, locked files, unsupported versions, invalid JSON and enums, missing optional files, machine-specific uploader configs, oversized files, folder overlap, Windows junctions, application/mutex/pipe isolation, updater channels and exact release asset names, English/French import-form layout, and suppression of queued settings saves after importing. Symbolic-link creation is also tested when the Windows token permits it; junction rejection is always checked.

The harness uses reflection only to access internal production classes. All import and updater behavior comes from the built application; there is no copied implementation or external test framework. It does not perform desktop automation or replace the later Windows interaction test pass.
