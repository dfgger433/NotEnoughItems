#Requires -Version 7.0

<#
.SYNOPSIS
Install existing build artifacts, optionally building and testing first.
.DESCRIPTION
Use -Build for the complete build/test/install workflow. Use -WhatIf to validate
and preview without building or writing. Existing asset overrides are preserved;
legacy Textures backgrounds are migrated only when the canonical resource is absent.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$GameRoot,
    [string]$CUCoreLibPath,
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [switch]$Build,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$script:DistDirectory = Join-Path $script:ProjectRoot 'dist'
$script:ArtifactDll = Join-Path $script:DistDirectory 'NotEnoughItems.dll'
$script:AssetRelativePath = 'assets/notenoughitems'
$script:LegacyBackgrounds = @{
    'textures/gui/backgrounds/bookmarks.png' = 'Textures/background-bookmarks.png'
    'textures/gui/backgrounds/catalog.png' = 'Textures/background-catalog.png'
    'textures/gui/backgrounds/options.png' = 'Textures/background-options.png'
    'textures/gui/backgrounds/recipe.png' = 'Textures/background-recipe.png'
    'textures/gui/backgrounds/default.png' = 'Textures/background.png'
}

function Get-PathItem {
    param([string]$Path)
    try { Get-Item -LiteralPath $Path -Force -ErrorAction Stop }
    catch [System.Management.Automation.ItemNotFoundException] { $null }
}

function Get-RequiredItem {
    param([string]$Path, [string]$Label, [switch]$Directory)
    $item = Get-PathItem $Path
    if ($null -eq $item -or $item.PSProvider.Name -ne 'FileSystem' -or $item.PSIsContainer -ne $Directory.IsPresent) {
        $type = if ($Directory) { 'directory' } else { 'file' }
        throw "$Label $type not found or inaccessible: '$Path'."
    }
    return $item
}

function Get-PhysicalPath {
    param([string]$Path)
    $root = [IO.Path]::GetPathRoot($Path)
    $physicalPath = $root
    foreach ($segment in ($Path.Substring($root.Length) -split '[\\/]')) {
        if (-not $segment) { continue }
        $item = Get-Item -LiteralPath (Join-Path $physicalPath $segment) -Force
        $physicalPath = $item.FullName
        if ($null -ne $item.PSObject.Methods['ResolveLinkTarget']) {
            $target = $item.ResolveLinkTarget($true)
            if ($null -ne $target) { $physicalPath = $target.FullName }
        } elseif ($item.LinkType -in @('Junction', 'SymbolicLink')) {
            $target = [string](@($item.Target)[0])
            if (-not [IO.Path]::IsPathFullyQualified($target)) { $target = Join-Path (Split-Path -Parent $physicalPath) $target }
            $physicalPath = Get-PhysicalPath ([IO.Path]::GetFullPath($target))
        }
    }
    return $physicalPath
}

function Assert-NoReparsePoints {
    param([string]$Root, [string[]]$RelativePaths)
    $rootPath = Get-PhysicalPath ((Get-RequiredItem $Root 'Trusted path root' -Directory).FullName)
    foreach ($relative in $RelativePaths) {
        if ([IO.Path]::IsPathFullyQualified($relative) -or ($relative -split '[\\/]') -contains '..') {
            throw "Expected a relative path below '$rootPath', got '$relative'."
        }
        $path = $rootPath
        foreach ($segment in ($relative -split '[\\/]')) {
            if (-not $segment -or $segment -eq '.') { continue }
            $path = Join-Path $path $segment
            $item = Get-PathItem $path
            if ($null -eq $item) { break }
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing to use a linked or reparse path component: '$path'." }
        }
    }
}

function Test-SamePath {
    param([string]$Left, [string]$Right)
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    return [string]::Equals($Left, $Right, $comparison)
}

function Get-GameSettings {
    param([string]$Root, [string]$CoreLib)
    if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Join-Path $script:ProjectRoot '../..' }
    $rootPath = (Get-RequiredItem $Root 'Game root (set -GameRoot to the game installation)' -Directory).FullName
    $managed = Join-Path $rootPath 'CasualtiesUnknown_Data/Managed'
    $bepInEx = Join-Path $rootPath 'BepInEx'
    $plugins = (Get-RequiredItem (Join-Path $bepInEx 'plugins') 'BepInEx plugins' -Directory).FullName
    foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll')) { $null = Get-RequiredItem (Join-Path $managed $name) 'Game assembly' }
    foreach ($name in @('BepInEx.dll', '0Harmony.dll')) { $null = Get-RequiredItem (Join-Path $bepInEx "core/$name") 'BepInEx assembly' }
    if ([string]::IsNullOrWhiteSpace($CoreLib)) { $CoreLib = Join-Path $plugins 'CUCoreLib.dll' }
    $coreLibPath = (Get-RequiredItem $CoreLib 'CUCoreLib assembly (set -CUCoreLibPath if installed elsewhere)').FullName
    return @{ Root = $rootPath; CoreLib = $coreLibPath; PluginDir = Join-Path $plugins 'NotEnoughItems' }
}

function Get-ProjectAssets {
    $assetRoot = $script:ProjectRoot
    foreach ($segment in @('Assets', 'NotEnoughItems')) {
        $matches = @(Get-ChildItem -LiteralPath $assetRoot -Directory -Force | Where-Object Name -EQ $segment)
        if ($matches.Count -ne 1) { throw "Expected exactly one asset directory named '$segment' in '$assetRoot'." }
        $assetRoot = $matches[0].FullName
    }
    $files = @(Get-ChildItem -LiteralPath $assetRoot -File -Recurse -Force | Sort-Object FullName)
    if ($files.Count -eq 0) { throw "The plugin asset namespace has no files: '$assetRoot'." }
    $paths = @('Assets/NotEnoughItems')
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($assetRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
        $paths += 'Assets/NotEnoughItems/' + $relative
    }
    Assert-NoReparsePoints $script:ProjectRoot $paths
    foreach ($file in $files) {
        [pscustomobject]@{
            RelativePath = [IO.Path]::GetRelativePath($assetRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
            Bytes = $file.Length
        }
    }
}

function Assert-SafeDistPath {
    $paths = @('dist', 'dist/NotEnoughItems.dll', 'dist/assets', 'dist/assets/notenoughitems')
    foreach ($asset in Get-ProjectAssets) { $paths += 'dist/assets/notenoughitems/' + $asset.RelativePath }
    Assert-NoReparsePoints $script:ProjectRoot $paths
}

function Get-BuildArtifact {
    $dll = Get-RequiredItem $script:ArtifactDll 'Build artifact (run Build.ps1 first)'
    if ($dll.Length -eq 0) { throw "Build artifact is empty: '$($dll.FullName)'." }
    $assetRoot = Join-Path $script:DistDirectory $script:AssetRelativePath
    $assets = foreach ($asset in Get-ProjectAssets) {
        $file = Get-RequiredItem (Join-Path $assetRoot $asset.RelativePath) 'Build artifact (run Build.ps1 first)'
        [pscustomobject]@{ RelativePath = $asset.RelativePath; Source = $file.FullName; Bytes = $file.Length }
    }
    return @{ Dll = $dll; Assets = @($assets) }
}

function Get-GameExecutable {
    param([string]$Root)
    $executable = Get-RequiredItem (Join-Path $Root 'CasualtiesUnknown.exe') 'Game executable'
    return Get-PhysicalPath $executable.FullName
}

function Assert-GameStopped {
    param([string]$Root)
    $expectedExe = Get-GameExecutable $Root
    foreach ($process in (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue)) {
        $runningExe = $null
        try { if ($process.Path) { $runningExe = Get-PhysicalPath $process.Path } } catch { $runningExe = $null }
        if (-not $runningExe) {
            if ($process.HasExited) { continue }
            throw "Cannot verify the executable path of CasualtiesUnknown (PID $($process.Id)). Close it before installing."
        }
        if (Test-SamePath $runningExe $expectedExe) { throw "The game is running from '$Root' (PID $($process.Id)). Close it before installing." }
    }
}

function Get-InstallPathItem {
    param([string]$PluginParent, [string]$RelativePath, [switch]$Directory)
    $path = $PluginParent
    $segments = $RelativePath -split '/'
    for ($i = 0; $i -lt $segments.Count; $i++) {
        $path = Join-Path $path $segments[$i]
        $item = Get-PathItem $path
        if ($null -eq $item) { return $null }
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link or reparse point in plugin path: '$path'." }
        $expectDirectory = ($i -lt ($segments.Count - 1)) -or $Directory.IsPresent
        if ($item.PSIsContainer -ne $expectDirectory) {
            $expectedType = if ($expectDirectory) { 'directory' } else { 'file' }
            throw "Expected a $expectedType at plugin path '$path'."
        }
    }
    return $item
}

function Assert-SafeInstallPath {
    param([string]$Root)
    $paths = @('BepInEx', 'BepInEx/plugins', 'BepInEx/plugins/NotEnoughItems', 'BepInEx/plugins/NotEnoughItems/NotEnoughItems.dll')
    foreach ($asset in Get-ProjectAssets) { $paths += 'BepInEx/plugins/NotEnoughItems/' + $script:AssetRelativePath + '/' + $asset.RelativePath }
    Assert-NoReparsePoints $Root $paths
}

function Assert-FreeSpace {
    param([string]$Directory, [long]$MinimumBytes)
    $pathRoot = [IO.Path]::GetPathRoot($Directory)
    try { $free = [System.IO.DriveInfo]::new($pathRoot).AvailableFreeSpace }
    catch { throw "Cannot determine free space for '$Directory': $($_.Exception.Message)" }
    if ($free -lt $MinimumBytes) { throw "Insufficient free space for '$Directory': need at least $MinimumBytes bytes; have $free." }
}

function Get-InstallPlan {
    param([string]$Root, [string]$Destination, [long]$DllBytes, [object[]]$Assets)
    Assert-SafeInstallPath $Root
    $pluginParent = Split-Path -Parent $Destination
    $pluginName = Split-Path -Leaf $Destination
    $pluginDir = Get-InstallPathItem $pluginParent $pluginName -Directory
    $dll = Get-InstallPathItem $pluginParent "$pluginName/NotEnoughItems.dll"
    if ($null -ne $dll -and $dll.IsReadOnly) { throw "Plugin DLL destination is read-only: '$($dll.FullName)'." }
    $required = $DllBytes
    $assetCopies = foreach ($asset in $Assets) {
        $relative = "$pluginName/$script:AssetRelativePath/$($asset.RelativePath)"
        $existing = Get-InstallPathItem $pluginParent $relative
        if ($null -ne $existing) { continue }
        $legacy = if ($script:LegacyBackgrounds.ContainsKey($asset.RelativePath)) {
            Get-InstallPathItem $pluginParent "$pluginName/$($script:LegacyBackgrounds[$asset.RelativePath])"
        }
        if ($null -ne $legacy) { $required += $legacy.Length; $source = $legacy.FullName }
        else {
            $required += $asset.Bytes
            $source = if ($asset.PSObject.Properties['Source']) { $asset.Source }
        }
        [pscustomobject]@{ Source = $source; Destination = Join-Path $pluginParent $relative }
    }
    $existingDirectory = if ($null -eq $pluginDir) { $pluginParent } else { $pluginDir.FullName }
    Assert-FreeSpace (Get-PhysicalPath $existingDirectory) ($required + 1MB)
    return @($assetCopies)
}

$game = Get-GameSettings $GameRoot $CUCoreLibPath
$null = Get-GameExecutable $game.Root
Assert-SafeDistPath
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
$assets = @(Get-ProjectAssets)
$artifact = $null
if (-not $Build) { $artifact = Get-BuildArtifact }
$dllBytes = if ($null -eq $artifact) { 1MB } else { $artifact.Dll.Length }
$plannedAssets = if ($null -eq $artifact) { $assets } else { $artifact.Assets }
$assetCopies = @(Get-InstallPlan $game.Root $game.PluginDir $dllBytes $plannedAssets)
if ($null -ne $artifact) { $sourceHash = (Get-FileHash -LiteralPath $artifact.Dll.FullName -Algorithm SHA256).Hash }

Write-Host "Game root: $($game.Root)"
Write-Host "Plugin destination: $($game.PluginDir)"
if ($WhatIfPreference) {
    if ($Build) { Write-Host "Build and tests: $buildScript (configuration: $Configuration; skip tests: $([bool]$SkipTests))" }
    else { Write-Host "Existing build artifacts: $script:DistDirectory" }
}
$verb = if ($Build) { 'Build, test and install' } else { 'Install existing artifacts' }
if (-not $PSCmdlet.ShouldProcess($game.PluginDir, "$verb NotEnoughItems and verify the DLL SHA-256")) { return }

if ($Build) {
    Assert-GameStopped $game.Root
    & $buildScript -Configuration $Configuration -GameRoot $game.Root -CUCoreLibPath $game.CoreLib -SkipTests:$SkipTests
    Assert-SafeDistPath
    $artifact = Get-BuildArtifact
    $assetCopies = @(Get-InstallPlan $game.Root $game.PluginDir $artifact.Dll.Length $artifact.Assets)
    $sourceHash = (Get-FileHash -LiteralPath $artifact.Dll.FullName -Algorithm SHA256).Hash
}

Assert-GameStopped $game.Root
Assert-SafeDistPath
Assert-SafeInstallPath $game.Root
# Re-evaluate the destination plan immediately before writing; source assets are preflighted above.
$assetCopies = @(Get-InstallPlan $game.Root $game.PluginDir $artifact.Dll.Length $artifact.Assets)
$installedDll = Join-Path $game.PluginDir 'NotEnoughItems.dll'
foreach ($copy in $assetCopies) {
    $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $copy.Destination))
    [IO.File]::Copy($copy.Source, $copy.Destination, $false)
}
$null = [IO.Directory]::CreateDirectory($game.PluginDir)
$tempDll = Join-Path $game.PluginDir ('.NotEnoughItems-' + [guid]::NewGuid() + '.tmp')
try {
    [IO.File]::Copy($artifact.Dll.FullName, $tempDll, $false)
    $tempHash = (Get-FileHash -LiteralPath $tempDll -Algorithm SHA256).Hash
    if ($sourceHash -ne $tempHash) { throw "DLL copy verification failed for '$tempDll'." }
    [IO.File]::Move($tempDll, $installedDll, $true)
} finally { if ([IO.File]::Exists($tempDll)) { [IO.File]::Delete($tempDll) } }
$installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) { throw "Install verification failed for '$installedDll'. Expected SHA-256 $sourceHash, found $installedHash." }
Write-Host "Installed: $installedDll"
Write-Host "Verified SHA-256: $installedHash"
