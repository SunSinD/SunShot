[CmdletBinding()]
param(
    [string]$DotnetPath = 'dotnet',
    [string]$IsccPath,
    [string]$OutputPath,
    [string]$GitPath = 'git'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

function Assert-CleanSource {
    $changes = @(& $GitPath -C $repoRoot status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Could not check the source tree.' }
    if ($changes.Count -ne 0) { throw 'Commit source changes before packaging so the source archive matches the application.' }
}

function New-ZipArchive([string]$Path, [string]$Root, [string[]]$Files, [string]$Prefix) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path }
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in ($Files | Sort-Object)) {
            $name = $Prefix + $file.Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $Root $file), $name, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $archive.Dispose() }
    $check = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $bad = @($check.Entries | Where-Object { $_.FullName.Contains('\') -or $_.FullName.StartsWith('/') -or $_.FullName.Split('/') -contains '..' })
        if ($bad.Count -gt 0 -or $check.Entries.Count -ne $Files.Count) { throw "Invalid archive entries: $Path" }
    }
    finally { $check.Dispose() }
}
if (!$OutputPath) { $OutputPath = Join-Path $repoRoot 'Output' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.build.props')
$version = [string]$props.Project.PropertyGroup[0].Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid app version.' }
Assert-CleanSource
$sourceCommit = & $GitPath -C $repoRoot rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the source commit.' }

if (!$IsccPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $IsccPath = $compiler.Source }
    foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")) {
        if (!$IsccPath -and (Test-Path -LiteralPath $candidate)) { $IsccPath = $candidate }
    }
}
if (!$IsccPath -or !(Test-Path -LiteralPath $IsccPath)) { throw 'Install Inno Setup 6.5+ or 7, or pass -IsccPath.' }

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$stage = Join-Path $OutputPath ('.stage-' + [guid]::NewGuid().ToString('N'))
foreach ($stale in @('SHA256SUMS.txt', "SunShot-$version-setup-x64.exe", "SunShot-$version-portable-x64.zip", "SunShot-$version-source.zip")) {
    $stalePath = Join-Path $OutputPath $stale
    if (Test-Path -LiteralPath $stalePath) { Remove-Item -LiteralPath $stalePath }
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Push-Location $repoRoot
try {
    $projects = @('SunShot/ShareX.csproj', 'SunShot.ImageEditor.App/ShareX.ImageEditor.App.csproj', 'SunShot.NativeMessagingHost/ShareX.NativeMessagingHost.csproj')
    # Avalonia can reuse existing PDBs while rewriting XAML assemblies, even when symbols are disabled.
    foreach ($project in $projects) {
        & $DotnetPath clean $project -c Release -p:Platform=x64 --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Clean failed: $project" }
    }
    foreach ($project in $projects) {
        & $DotnetPath publish $project -c Release -p:Platform=x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false --source https://api.nuget.org/v3/index.json -o $stage
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
    }
    $privatePaths = @($repoRoot, [Environment]::GetFolderPath('UserProfile')) | Where-Object { $_ }
    $ownAssemblies = [IO.Directory]::EnumerateFiles($stage, '*', [IO.SearchOption]::AllDirectories) | Where-Object {
        [IO.Path]::GetFileName($_) -match '^(SunShot|ShareX[._]).*\.(dll|exe)$'
    }
    foreach ($assembly in $ownAssemblies) {
        $bytes = [IO.File]::ReadAllBytes($assembly)
        foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
            $content = $encoding.GetString($bytes)
            foreach ($privatePath in $privatePaths) {
                if ($content.IndexOf($privatePath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "Local build path embedded in release assembly: $([IO.Path]::GetFileName($assembly))"
                }
            }
        }
    }
    foreach ($symbols in [IO.Directory]::EnumerateFiles($stage, '*.pdb', [IO.SearchOption]::AllDirectories)) {
        [IO.File]::Delete($symbols)
    }
    $debugLibrary = Join-Path $stage 'DirectML.Debug.dll'
    if (Test-Path -LiteralPath $debugLibrary) { Remove-Item -LiteralPath $debugLibrary }
    foreach ($developmentFile in [IO.Directory]::EnumerateFiles($stage, '*.lib', [IO.SearchOption]::AllDirectories)) {
        [IO.File]::Delete($developmentFile)
    }
    foreach ($file in @('SunShot.exe', 'SunShot.deps.json', 'SunShot.runtimeconfig.json', 'ShareX.ImageEditor.App.exe', 'ShareX_NativeMessagingHost.exe', 'e_sqlite3.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $stage $file))) { throw "Missing release file: $file" }
    }
    if (Test-Path -LiteralPath (Join-Path $stage 'ShareX.exe')) { throw 'Unexpected upstream executable in staging.' }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.txt') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'NOTICE.txt') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'Licenses') -Destination $stage -Recurse
    & (Join-Path $PSScriptRoot 'CollectLicenses.ps1') -PayloadDirectory $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'assets') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'SunShot/ShareX_File_Icon.ico') -Destination $stage
    foreach ($manifest in @('host-manifest-chrome.json', 'host-manifest-firefox.json')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "SunShot/$manifest") -Destination $stage
    }

    & $IsccPath "/DPayloadDir=$stage" "/DPackageDir=$OutputPath" "/DAppVersion=$version" (Join-Path $PSScriptRoot 'SunShot.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    [IO.File]::WriteAllText((Join-Path $stage 'Portable'), '')
    $stageFiles = @([IO.Directory]::EnumerateFiles($stage, '*', [IO.SearchOption]::AllDirectories) | ForEach-Object { $_.Substring($stage.Length + 1) })
    New-ZipArchive (Join-Path $OutputPath "SunShot-$version-portable-x64.zip") $stage $stageFiles ''
    Assert-CleanSource
    $currentCommit = & $GitPath -C $repoRoot rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0 -or $currentCommit -ne $sourceCommit) { throw 'The source commit changed during packaging.' }
    $sourceZip = Join-Path $OutputPath "SunShot-$version-source.zip"
    & $GitPath -C $repoRoot archive --format=zip "--prefix=SunShot-$version/" "--output=$sourceZip" $sourceCommit
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the committed source archive.' }
    $assets = @("SunShot-$version-setup-x64.exe", "SunShot-$version-portable-x64.zip", "SunShot-$version-source.zip")
    $sums = $assets | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $OutputPath $_) -Algorithm SHA256).Hash.ToLowerInvariant(), $_ }
    [IO.File]::WriteAllText((Join-Path $OutputPath 'SHA256SUMS.txt'), (($sums -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "Packages: $OutputPath"
    Write-Host "Clean staging directory retained for verification: $stage"
}
finally { Pop-Location }
