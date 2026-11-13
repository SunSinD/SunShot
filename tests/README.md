# SunShot checks

Windows 10 version 2004 (build 19041) or newer and the .NET 10 SDK are required.

```powershell
dotnet run --project tests/SunShot.CoreChecks/SunShot.CoreChecks.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
dotnet run --project tests/Regression/Regression.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json -- <path-to-pinned-ffmpeg-zip>
```

CoreChecks use the production assembly with synthetic profiles. They cover settings conversion, credentials, workflows, source immutability, backups, rollback, history, invalid input, isolation and the layout of the import form in English and French.

Regression checks upload consent, cleanup of the private temporary folder, SQLite, watched files and FFmpeg validation. The archive path turns on the check for a successful FFmpeg install. The checks for invalid downloads run without it.

The harness never starts SunShot, opens a personal profile, registers hotkeys, uploads files or writes integration registry keys. Use a separate portable copy for manual checks.
