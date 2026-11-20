#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$script:ProjectFile = Join-Path $script:ProjectRoot 'NotEnoughItems.csproj'
$script:TestProjectFile = Join-Path $script:ProjectRoot 'tests/Nei.Tests.csproj'
$script:AssetRoot = Join-Path $script:ProjectRoot 'assets/notenoughitems'

function Get-RequiredItem {
    param([string]$Path, [string]$Label, [switch]$Directory)

    $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $item -or $item.PSProvider.Name -ne 'FileSystem' -or
        $item.PSIsContainer -ne $Directory.IsPresent) {
        $type = if ($Directory) { 'directory' } else { 'file' }
        throw "$Label $type not found or inaccessible: '$Path'."
    }
    return $item
}

function ConvertTo-MSBuildPropertyValue {
    param([string]$Value)

    # MSBuild parses comma and semicolon as property-list delimiters.
    return $Value.Replace('%', '%25').Replace(',', '%2C').Replace(';', '%3B')
}

function Get-GameSettings {
    param([string]$Root, [string]$CoreLib)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        $Root = Join-Path $script:ProjectRoot '../..'
    }
    $rootItem = Get-RequiredItem $Root 'Game root (set -GameRoot to the game installation)' -Directory
    $rootPath = $rootItem.FullName
    $managed = Join-Path $rootPath 'CasualtiesUnknown_Data/Managed'
    $bepInEx = Join-Path $rootPath 'BepInEx'
    $plugins = (Get-RequiredItem (Join-Path $bepInEx 'plugins') 'BepInEx plugins' -Directory).FullName
    foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll')) {
        $null = Get-RequiredItem (Join-Path $managed $name) 'Game assembly'
    }
    foreach ($name in @('BepInEx.dll', '0Harmony.dll')) {
        $null = Get-RequiredItem (Join-Path $bepInEx "core/$name") 'BepInEx assembly'
    }
    if ([string]::IsNullOrWhiteSpace($CoreLib)) {
        $CoreLib = Join-Path $plugins 'CUCoreLib.dll'
    }
    $coreLibPath = (Get-RequiredItem $CoreLib 'CUCoreLib assembly (set -CUCoreLibPath if installed elsewhere)').FullName

    return @{
        Root = $rootPath
        CoreLib = $coreLibPath
        PluginDir = Join-Path $plugins 'NotEnoughItems'
        Properties = @(
            '-p:GameRoot=' + (ConvertTo-MSBuildPropertyValue $rootPath)
            '-p:CUCoreLibPath=' + (ConvertTo-MSBuildPropertyValue $coreLibPath)
        )
    }
}

function Get-ProjectAssets {
    $null = Get-RequiredItem $script:AssetRoot 'Plugin asset namespace' -Directory
    $files = @(Get-ChildItem -LiteralPath $script:AssetRoot -File -Recurse -Force)
    if ($files.Count -eq 0) {
        throw "The plugin asset namespace has no files: '$script:AssetRoot'."
    }
    return $files
}

function Format-DotNetCommand {
    param([string[]]$Arguments)

    $display = foreach ($argument in $Arguments) {
        if ($argument -match '^[A-Za-z0-9_./:\\=-]+$') { $argument }
        else { "'" + $argument.Replace("'", "''") + "'" }
    }
    return 'dotnet ' + ($display -join ' ')
}

function Invoke-DotNetCommand {
    param([string[]]$Arguments, [switch]$CaptureOutput)

    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $dotnet) { throw 'dotnet SDK not found on PATH.' }
    $command = Format-DotNetCommand $Arguments
    Write-Host "> $command"
    Push-Location -LiteralPath $script:ProjectRoot
    try {
        if ($CaptureOutput) {
            $output = & $dotnet.Source @Arguments
        } else {
            & $dotnet.Source @Arguments | Out-Host
        }
        if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $command" }
        if ($CaptureOutput) { return $output }
    } finally {
        Pop-Location
    }
}

function Get-ProjectBuildInfo {
    param([string]$Configuration, [string[]]$BuildArguments)

    $output = Invoke-DotNetCommand (@('msbuild', $script:ProjectFile, '-nologo',
        '-getProperty:TargetPath,Version', "-p:Configuration=$Configuration") + $BuildArguments) -CaptureOutput
    try {
        $properties = ($output -join "`n" | ConvertFrom-Json).Properties
        if (-not [IO.Path]::IsPathFullyQualified($properties.TargetPath) -or
            $properties.Version -notmatch '^\d[A-Za-z0-9.+-]*$') {
            throw 'MSBuild did not return an absolute DLL path and a filename-safe version.'
        }
    } catch {
        throw "Cannot read TargetPath and Version from MSBuild: $($_.Exception.Message)"
    }
    $null = Get-RequiredItem $properties.TargetPath 'Built plugin DLL'
    return $properties
}

function Get-OfflineBuildArguments {
    param([hashtable]$Game)

    # This project has no NuGet package dependencies. Restore from the project
    # directory only; required SDK/targeting packs must already be installed.
    $source = ConvertTo-MSBuildPropertyValue $script:ProjectRoot
    return @('-warnaserror', "-p:RestoreSources=$source", '-p:NuGetAudit=false') + $Game.Properties
}

function Assert-FreeSpace {
    param([string]$Directory, [long]$MinimumBytes)

    $pathRoot = [System.IO.Path]::GetPathRoot($Directory)
    try {
        $free = [System.IO.DriveInfo]::new($pathRoot).AvailableFreeSpace
    } catch {
        throw "Cannot determine free space for '$Directory': $($_.Exception.Message)"
    }
    if ($free -lt $MinimumBytes) {
        throw "Insufficient free space for '$Directory': need at least $MinimumBytes bytes; have $free."
    }
}
