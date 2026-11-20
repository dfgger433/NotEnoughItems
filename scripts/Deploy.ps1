#Requires -Version 7.0

<#
.SYNOPSIS
Build, test and install the plugin in a local game installation.
.DESCRIPTION
Existing resource overrides are preserved by the project's Install target.
-WhatIf prints the planned commands and destination without building or installing.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [string]$GameRoot,
    [string]$CUCoreLibPath,
    [switch]$SkipTests
)

. (Join-Path $PSScriptRoot 'Common.ps1')

function Get-PhysicalPath {
    param([string]$Path)

    # Resolve directory junctions and symlinked files anywhere in the path.
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
            # FileSystemInfo.ResolveLinkTarget was added after PowerShell 7.0.
            $target = [string](@($item.Target)[0])
            if (-not [IO.Path]::IsPathFullyQualified($target)) {
                $target = Join-Path (Split-Path -Parent $physicalPath) $target
            }
            $physicalPath = Get-PhysicalPath ([IO.Path]::GetFullPath($target))
        }
    }
    return $physicalPath
}

function Assert-GameStopped {
    param([string]$Root)

    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $expectedExe = Join-Path (Get-PhysicalPath $Root) 'CasualtiesUnknown.exe'
    if (Test-Path -LiteralPath $expectedExe -PathType Leaf) {
        $expectedExe = Get-PhysicalPath $expectedExe
    }
    foreach ($process in (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue)) {
        $runningExe = $null
        try {
            $runningExe = $process.Path
            if ($runningExe) { $runningExe = Get-PhysicalPath $runningExe }
        } catch {
            $runningExe = $null
        }
        if (-not $runningExe) {
            if ($process.HasExited) { continue }
            throw "Cannot verify the executable path of CasualtiesUnknown (PID $($process.Id)). Close it before deploying."
        }
        if ([string]::Equals($runningExe, $expectedExe, $comparison)) {
            throw "The game is running from '$Root' (PID $($process.Id)). Close it before deploying."
        }
    }
}

function Assert-InstallSpace {
    param([string]$Destination, [long]$DllBytes)

    # If Install has not yet created the directory, check its existing parent.
    $destinationItem = Get-Item -LiteralPath $Destination -ErrorAction SilentlyContinue
    if ($null -ne $destinationItem -and -not $destinationItem.PSIsContainer) {
        throw "Plugin destination is not a directory: '$Destination'."
    }
    $existingDirectory = if ($null -eq $destinationItem) { Split-Path -Parent $Destination } else { $Destination }
    $dll = Get-Item -LiteralPath (Join-Path $Destination 'NotEnoughItems.dll') -ErrorAction SilentlyContinue
    if ($null -ne $dll -and $dll.PSIsContainer) { throw "Plugin DLL destination is a directory: '$($dll.FullName)'." }
    if ($null -ne $dll -and $dll.IsReadOnly) { throw "Plugin DLL destination is read-only: '$($dll.FullName)'." }

    $required = $DllBytes
    foreach ($file in Get-ProjectAssets) {
        if ($file.Extension -notin @('.png', '.json', '.mcmeta')) { continue }
        $relative = [IO.Path]::GetRelativePath($script:AssetRoot, $file.FullName)
        $assetDestination = Join-Path $Destination 'assets/notenoughitems' $relative
        $assetItem = Get-Item -LiteralPath $assetDestination -ErrorAction SilentlyContinue
        if ($null -ne $assetItem -and $assetItem.PSIsContainer) {
            throw "Asset destination is a directory, not a file: '$assetDestination'."
        }
        if ($null -eq $assetItem) { $required += $file.Length }
    }
    # Temporary allocations and filesystem overhead (also allows for legacy asset migration).
    Assert-FreeSpace (Get-PhysicalPath $existingDirectory) ($required + 1MB)
}

$game = Get-GameSettings $GameRoot $CUCoreLibPath
$buildArguments = Get-OfflineBuildArguments $game
$installArguments = @('build', $script:ProjectFile, '-c', $Configuration, '--no-restore', '-t:Install') + $buildArguments
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
$buildOptions = @{
    Configuration = $Configuration
    GameRoot = $game.Root
    CUCoreLibPath = $game.CoreLib
    SkipTests = $SkipTests
}
$builtDll = Join-Path $script:ProjectRoot "bin/$Configuration/netstandard2.1/NotEnoughItems.dll"
$dllItem = Get-Item -LiteralPath $builtDll -ErrorAction SilentlyContinue
$dllBytes = if ($null -eq $dllItem -or $dllItem.PSIsContainer) { 1MB } else { $dllItem.Length }
Assert-InstallSpace $game.PluginDir $dllBytes
Write-Host "Game root: $($game.Root)"
Write-Host "Plugin destination: $($game.PluginDir)"
if ($WhatIfPreference) {
    Write-Host "Build: $buildScript (configuration: $Configuration; skip tests: $([bool]$SkipTests))"
    Write-Host ('Install: ' + (Format-DotNetCommand $installArguments))
}
if (-not $PSCmdlet.ShouldProcess($game.PluginDir, 'Build and install NotEnoughItems, then verify the DLL SHA256')) { return }

Assert-GameStopped $game.Root
& $buildScript @buildOptions
$build = Get-ProjectBuildInfo $Configuration $buildArguments
Assert-InstallSpace $game.PluginDir (Get-Item -LiteralPath $build.TargetPath).Length
Assert-GameStopped $game.Root
Invoke-DotNetCommand $installArguments

$installedDll = Join-Path $game.PluginDir ([IO.Path]::GetFileName($build.TargetPath))
$null = Get-RequiredItem $installedDll 'Installed plugin DLL'
$sourceHash = (Get-FileHash -LiteralPath $build.TargetPath -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) {
    throw "Install verification failed for '$installedDll'. Expected SHA256 $sourceHash, found $installedHash."
}
Write-Host "Installed: $installedDll"
Write-Host "Verified SHA256: $installedHash"
