#Requires -Version 7.0

<#
.SYNOPSIS
Build and test NotEnoughItems without installing it.
.DESCRIPTION
Build artifacts go to the repository's dist directory. Use -Package to create
an atomic versioned ZIP and -SkipTests to omit regression tests. The installed
.NET SDK and targeting packs are used offline; no dependencies are downloaded.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [string]$GameRoot,
    [string]$CUCoreLibPath,
    [switch]$SkipTests,
    [switch]$Package
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$script:ProjectFile = Join-Path $script:ProjectRoot 'src/NotEnoughItems/NotEnoughItems.csproj'
$script:TestProjectFile = Join-Path $script:ProjectRoot 'tests/NotEnoughItems.Tests/NotEnoughItems.Tests.csproj'
$script:DistDirectory = Join-Path $script:ProjectRoot 'dist'
$script:ArtifactDll = Join-Path $script:DistDirectory 'NotEnoughItems.dll'
$script:AssetRelativePath = 'assets/notenoughitems'

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

function ConvertTo-MSBuildPropertyValue {
    param([string]$Value)
    return $Value.Replace('%', '%25').Replace(',', '%2C').Replace(';', '%3B')
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
    return @{
        Root = $rootPath
        CoreLib = $coreLibPath
        Properties = @(
            ('-p:GameRoot=' + (ConvertTo-MSBuildPropertyValue $rootPath))
            ('-p:CUCoreLibPath=' + (ConvertTo-MSBuildPropertyValue $coreLibPath))
        )
    }
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
    $paths = @('dist', 'dist/NotEnoughItems.dll', 'dist/assets', 'dist/assets/notenoughitems',
        'src/NotEnoughItems/bin', 'src/NotEnoughItems/obj', 'src/NotEnoughItems/obj/Debug',
        'src/NotEnoughItems/obj/Release', 'src/NotEnoughItems/obj/Debug/netstandard2.1',
        'src/NotEnoughItems/obj/Release/netstandard2.1', 'tests/NotEnoughItems.Tests/bin',
        'tests/NotEnoughItems.Tests/obj', 'tests/NotEnoughItems.Tests/bin/Debug/net8.0',
        'tests/NotEnoughItems.Tests/bin/Release/net8.0', 'tests/NotEnoughItems.Tests/bin/Debug/core-only/net8.0',
        'tests/NotEnoughItems.Tests/bin/Release/core-only/net8.0', 'tests/NotEnoughItems.Tests/obj/Debug/net8.0',
        'tests/NotEnoughItems.Tests/obj/Release/net8.0', 'tests/NotEnoughItems.Tests/obj/Debug/core-only/net8.0',
        'tests/NotEnoughItems.Tests/obj/Release/core-only/net8.0')
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

function Format-DotNetCommand {
    param([string[]]$Arguments)
    $display = foreach ($argument in $Arguments) {
        if ($argument -match '^[A-Za-z0-9_./:\\=-]+$') { $argument } else { "'" + $argument.Replace("'", "''") + "'" }
    }
    return 'dotnet ' + ($display -join ' ')
}

function Invoke-DotNetCommand {
    param([string[]]$Arguments, [switch]$CaptureOutput)
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $dotnet) { throw 'dotnet SDK not found on PATH.' }
    $command = Format-DotNetCommand $Arguments
    Write-Host "> $command"
    $previousConsoleEncoding = [Console]::OutputEncoding
    $previousForceUtf8 = [Environment]::GetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', 'Process')
    Push-Location -LiteralPath $script:ProjectRoot
    try {
        [Environment]::SetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', '1', 'Process')
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        if ($CaptureOutput) { $output = & $dotnet.Source @Arguments } else { & $dotnet.Source @Arguments | Out-Host }
        if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $command" }
        if ($CaptureOutput) { return $output }
    } finally {
        [Console]::OutputEncoding = $previousConsoleEncoding
        [Environment]::SetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', $previousForceUtf8, 'Process')
        Pop-Location
    }
}

function Get-ProjectBuildInfo {
    param([string]$Configuration, [string[]]$BuildArguments)
    $output = Invoke-DotNetCommand (@('msbuild', $script:ProjectFile, '-nologo', '-getProperty:TargetPath,Version', "-p:Configuration=$Configuration") + $BuildArguments) -CaptureOutput
    try {
        $properties = ($output -join "`n" | ConvertFrom-Json).Properties
        if (-not [IO.Path]::IsPathFullyQualified($properties.TargetPath) -or $properties.Version -notmatch '^\d[A-Za-z0-9.+-]*$') {
            throw 'MSBuild did not return an absolute DLL path and a filename-safe version.'
        }
    } catch { throw "Cannot read TargetPath and Version from MSBuild: $($_.Exception.Message)" }
    if (-not (Test-SamePath ([IO.Path]::GetFullPath($properties.TargetPath)) $script:ArtifactDll)) {
        throw "Unexpected MSBuild TargetPath '$($properties.TargetPath)'; expected '$script:ArtifactDll'."
    }
    return $properties
}

function Get-OfflineBuildArguments {
    param([hashtable]$Game)
    $source = ConvertTo-MSBuildPropertyValue $script:ProjectRoot
    return @('-warnaserror', "-p:RestoreSources=$source", '-p:NuGetAudit=false') + $Game.Properties
}

function New-PluginPackage {
    param([hashtable]$Artifact, [string]$Version)
    $packagePath = Join-Path $script:DistDirectory "NotEnoughItems-$Version.zip"
    $entries = [ordered]@{ ($Artifact.Dll.Name) = $Artifact.Dll.FullName }
    foreach ($file in $Artifact.Assets) { $entries["$script:AssetRelativePath/$($file.RelativePath)"] = $file.Source }
    foreach ($name in @('README.md', 'CHANGELOG.md')) {
        $path = Join-Path $script:ProjectRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { $entries[$name] = $path }
    }
    $tempPath = Join-Path $script:DistDirectory ('.NotEnoughItems-' + [guid]::NewGuid() + '.tmp')
    Assert-SafeDistPath
    $stream = [IO.File]::Open($tempPath, [IO.FileMode]::CreateNew)
    try {
        try {
            $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
            try { foreach ($entry in $entries.GetEnumerator()) { $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Value, $entry.Key) } }
            finally { $archive.Dispose() }
        } finally { $stream.Dispose() }
        [IO.File]::Move($tempPath, $packagePath, $true)
    } finally { if ([IO.File]::Exists($tempPath)) { [IO.File]::Delete($tempPath) } }
    Write-Host "Package: $packagePath"
}

$game = Get-GameSettings $GameRoot $CUCoreLibPath
$buildArguments = Get-OfflineBuildArguments $game
Assert-SafeDistPath
$null = Get-RequiredItem $script:ProjectFile 'Plugin project'
if (-not $SkipTests) { $null = Get-RequiredItem $script:TestProjectFile 'Test project' }
$null = Get-ProjectAssets
Write-Host "Game root: $($game.Root)"
Invoke-DotNetCommand (@('build', $script:ProjectFile, '-c', $Configuration) + $buildArguments)
$build = Get-ProjectBuildInfo $Configuration $buildArguments
$artifact = Get-BuildArtifact
Write-Host "Plugin DLL: $($artifact.Dll.FullName)"

if (-not $SkipTests) {
    $testArguments = $buildArguments + ('-p:NeiAssemblyPath=' + (ConvertTo-MSBuildPropertyValue $artifact.Dll.FullName))
    Invoke-DotNetCommand (@('build', $script:TestProjectFile, '-c', $Configuration) + $testArguments)
    Invoke-DotNetCommand @('run', '--project', $script:TestProjectFile, '-c', $Configuration, '--no-build')
} else { Write-Host 'Regression tests skipped (-SkipTests).' }
if ($Package) { New-PluginPackage $artifact $build.Version }
