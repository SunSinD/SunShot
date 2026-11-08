[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$PayloadDirectory = [IO.Path]::GetFullPath($PayloadDirectory)
$licenses = Join-Path $PayloadDirectory 'Licenses'

$cacheRoots = @()
if ($env:NUGET_PACKAGES) { $cacheRoots += $env:NUGET_PACKAGES }
$restored = @{}
foreach ($project in @('SunShot', 'SunShot.ImageEditor.App', 'SunShot.NativeMessagingHost')) {
    $assets = Get-Content -LiteralPath (Join-Path $repoRoot "$project/obj/project.assets.json") -Raw | ConvertFrom-Json
    $cacheRoots += $assets.packageFolders.psobject.Properties.Name
    foreach ($library in $assets.libraries.psobject.Properties) {
        if ($library.Value.type -eq 'package') { $restored[$library.Name.Split('/')[0].ToLowerInvariant()] = $library }
    }
}
$cacheRoots += Join-Path $env:USERPROFILE '.nuget/packages'
$cacheRoots = @($cacheRoots | Where-Object { $_ } | Select-Object -Unique)

$packages = [ordered]@{}
$references = [ordered]@{}
$listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$appHosts = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$depsFiles = @(Get-ChildItem -LiteralPath $PayloadDirectory -Filter '*.deps.json' -File)
if ($depsFiles.Count -lt 3) { throw 'Expected deps.json files for SunShot, the image editor, and the native messaging host.' }
foreach ($depsFile in $depsFiles) {
    [void]$appHosts.Add($depsFile.Name.Substring(0, $depsFile.Name.Length - '.deps.json'.Length) + '.exe')
    $deps = Get-Content -LiteralPath $depsFile.FullName -Raw | ConvertFrom-Json
    $target = $deps.targets.psobject.Properties[$deps.runtimeTarget.name].Value
    foreach ($entry in $target.psobject.Properties) {
        foreach ($section in @('runtime', 'native')) {
            $assetsSection = $entry.Value.psobject.Properties[$section]
            if ($assetsSection) {
                foreach ($asset in $assetsSection.Value.psobject.Properties) { [void]$listed.Add([IO.Path]::GetFileName($asset.Name)) }
            }
        }
        $library = $deps.libraries.psobject.Properties[$entry.Name].Value
        switch ($library.type) {
            'package' { $packages[$entry.Name] = $library.path }
            'runtimepack' {
                $name = $entry.Name.Substring('runtimepack.'.Length)
                $packages[$name] = $name.ToLowerInvariant()
            }
            'reference' { $references[$entry.Name] = $true }
        }
    }
}

$contentFiles = @{ 'DirectML.dll' = 'microsoft.ai.directml' }
foreach ($binary in Get-ChildItem -LiteralPath $PayloadDirectory -File | Where-Object { $_.Extension -in '.dll', '.exe' }) {
    if ($listed.Contains($binary.Name) -or $appHosts.Contains($binary.Name)) { continue }
    $owner = $contentFiles[$binary.Name]
    if (!$owner -or !$restored.ContainsKey($owner)) { throw "Shipped binary has no known dependency source: $($binary.Name)" }
    $packages[$restored[$owner].Name] = $restored[$owner].Value.path
}

$notices = [ordered]@{
    '^ImageListView/' = @('ImageListView_license.txt')
    '^Avalonia\.Fonts\.Inter/' = @('Avalonia_license.txt', 'Additional/Inter.txt')
    '^Avalonia(\.[^/]+)?/' = @('Avalonia_license.txt')
    '^MicroCom\.Runtime/' = @('Additional/MicroCom.txt')
    '^Microsoft\.Data\.Sqlite(\.Core)?/' = @('Additional/EntityFrameworkCore.txt')
    '^Microsoft\.IO\.RecyclableMemoryStream/' = @('Additional/Microsoft.IO.RecyclableMemoryStream.txt')
    '^Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64/' = @('Additional/WindowsDesktop.txt')
    '^Microsoft\.Windows\.SDK\.NET\.Ref/' = @('Additional/Microsoft.Windows.SDK.NET.txt')
    '^OpenTK(\.[^/]+)?/' = @('Additional/OpenTK.txt')
    '^SharpGen\.Runtime(\.COM)?/' = @('Additional/SharpGen.txt')
    '^SQLitePCLRaw\.[^/]+/' = @('Additional/SQLitePCLRaw.txt')
    '^SSH\.NET/' = @('Additional/SSH.NET.txt')
    '^Tmds\.DBus\.Protocol/' = @('Additional/Tmds.DBus.txt')
    '^Vortice\.Mathematics/' = @('Additional/Vortice.Mathematics.txt')
    '^Vortice\.[^/]+/' = @('Additional/Vortice.Windows.txt')
    '^ZXing\.Net[^/]*/' = @('ZXing.Net_license.txt')
}
function Get-MappedNotices([string]$id) {
    foreach ($pattern in $notices.Keys) {
        if ($id -match $pattern) {
            foreach ($file in $notices[$pattern]) {
                if (!(Test-Path -LiteralPath (Join-Path $licenses $file) -PathType Leaf)) { throw "Missing notice file Licenses/$file for $id" }
                "Licenses/$file"
            }
            return
        }
    }
}

$destination = Join-Path $licenses 'Packages'
if (Test-Path -LiteralPath $destination) {
    $resolvedDestination = (Get-Item -LiteralPath $destination).FullName
    if ($resolvedDestination -ne [IO.Path]::GetFullPath((Join-Path $PayloadDirectory 'Licenses/Packages')) -or
        ((Get-Item -LiteralPath $licenses).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        ((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to remove a redirected dependency-notice directory.'
    }
    Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$inventory = [Collections.Generic.List[string]]::new()
foreach ($id in ($packages.Keys | Sort-Object)) {
    $directory = $null
    foreach ($cacheRoot in $cacheRoots) {
        $candidate = Join-Path $cacheRoot $packages[$id]
        if (Test-Path -LiteralPath $candidate) { $directory = [IO.Path]::GetFullPath($candidate); break }
    }
    if (!$directory) { throw "Missing dependency metadata: $id" }
    $name = $id.Replace('/', '-')
    $target = Join-Path $destination $name
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $texts = @()
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        $isNotice = $file.Name -match '(?i)^(license|notice|third[-_. ]?party|copying|copyright)'
        if (!$isNotice -and $file.Extension -ne '.nuspec') { continue }
        $relative = $file.FullName.Substring($directory.Length).TrimStart('\', '/')
        $output = Join-Path $target $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $output
        if ($isNotice) {
            if ($file.Length -lt 64) { throw "License text is empty or truncated: $($file.FullName)" }
            $texts += "Licenses/Packages/$name/$($relative.Replace('\', '/'))"
        }
    }
    $texts += @(Get-MappedNotices $id)
    if ($texts.Count -eq 0) { throw "Shipped dependency has no license text: $id" }
    $inventory.Add("$id`t$($texts -join '; ')")
}
foreach ($id in ($references.Keys | Sort-Object)) {
    $texts = @(Get-MappedNotices $id)
    if ($texts.Count -eq 0) { throw "Shipped reference has no license text: $id" }
    $inventory.Add("$id`t$($texts -join '; ')")
}
$required = [ordered]@{
    'Microsoft.NETCore.App.Runtime.win-x64/' = @('/LICENSE.TXT', '/THIRD-PARTY-NOTICES.TXT')
    'Microsoft.WindowsDesktop.App.Runtime.win-x64/' = @('/LICENSE', 'Licenses/Additional/WindowsDesktop.txt')
    'SkiaSharp/' = @('/LICENSE.txt')
    'SkiaSharp.NativeAssets.Win32/' = @('/LICENSE.txt', '/THIRD-PARTY-NOTICES.txt')
    'HarfBuzzSharp.NativeAssets.Win32/' = @('/LICENSE.txt', '/THIRD-PARTY-NOTICES.txt')
    'Microsoft.ML.OnnxRuntime.DirectML/' = @('/LICENSE', '/ThirdPartyNotices.txt')
    'Microsoft.AI.DirectML/' = @('/LICENSE.txt', '/ThirdPartyNotices.txt')
    'Avalonia.Angle.Windows.Natives/' = @('/LICENSE')
    'OpenTK.redist.glfw/' = @('/COPYING.md')
    'SQLitePCLRaw.lib.e_sqlite3/' = @('Licenses/Additional/SQLitePCLRaw.txt')
    'Microsoft.Windows.SDK.NET.Ref/' = @('Licenses/Additional/Microsoft.Windows.SDK.NET.txt')
}
foreach ($prefix in $required.Keys) {
    $entry = @($inventory | Where-Object { $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })
    if ($entry.Count -eq 0) { throw "Required dependency notices were not collected: $prefix" }
    $collected = @($entry[0].Split("`t")[1] -split '; ')
    foreach ($expected in $required[$prefix]) {
        $match = @(if ($expected.StartsWith('/')) { $collected | Where-Object { $_.StartsWith('Licenses/Packages/') -and $_.EndsWith($expected, [StringComparison]::OrdinalIgnoreCase) } } else { $collected | Where-Object { $_ -eq $expected } })
        if ($match.Count -eq 0) { throw "Required notice text missing for ${prefix}: $expected" }
    }
}
[IO.File]::WriteAllText((Join-Path $destination 'PACKAGES.txt'), (($inventory -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
