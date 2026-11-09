# Testing

Build and test on Windows with the .NET 10 SDK:

```powershell
dotnet build SunShot/ShareX.csproj -c Release -p:Platform=x64 --self-contained true --source https://api.nuget.org/v3/index.json
dotnet list SunShot/ShareX.csproj package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json
dotnet run --project tests/Regression/Regression.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
dotnet run --project tests/SunShot.CoreChecks/SunShot.CoreChecks.csproj -c Release -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
./build/Package.ps1
```

Packaging requires a clean, committed checkout. Run the regression harness with the pinned FFmpeg archive path to include the valid-download check.

Before release, check first launch, capture and editor flows, recording, upload consent, ShareX import and rollback, history, watch folders, English/French labels, installer contents, portable contents and checksums.

Use a synthetic window and an isolated portable copy. Do not use a personal ShareX profile or real upload destination.

## Recent release checks

- 0.1.0: release build, dependency audit, regression, CoreChecks and portable smoke tests passed.
- 0.1.1: release build, dependency audit, regression with the pinned archive, CoreChecks and import UI checks passed.
- 0.1.2: release build, dependency audit, regression and CoreChecks passed. Packaging produced all four release files.
