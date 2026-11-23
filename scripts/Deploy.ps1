#Requires -Version 7.0

<#
.SYNOPSIS
Build, test and install the plugin in a local game installation.
.DESCRIPTION
Install.ps1 preserves overrides and verifies the installed DLL's SHA-256.
-WhatIf prints the plan without building, testing or installing.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [string]$GameRoot,
    [string]$CUCoreLibPath,
    [switch]$SkipTests
)

. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-SafeDistPath
$game = Get-GameSettings $GameRoot $CUCoreLibPath
$null = Get-GameExecutable $game.Root
$assets = @(Get-ProjectAssets)
$dll = Get-PathItem $script:ArtifactDll
$dllBytes = if ($null -eq $dll -or $dll.PSIsContainer) { 1MB } else { $dll.Length }
$null = Get-InstallPlan $game.Root $game.PluginDir $dllBytes $assets

$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
$installScript = Join-Path $PSScriptRoot 'Install.ps1'
Write-Host "Game root: $($game.Root)"
Write-Host "Plugin destination: $($game.PluginDir)"
if ($WhatIfPreference) {
    Write-Host "Build: $buildScript (configuration: $Configuration; skip tests: $([bool]$SkipTests))"
    Write-Host "Install: $installScript (artifact: $script:DistDirectory)"
}
if (-not $PSCmdlet.ShouldProcess($game.PluginDir, 'Build and install NotEnoughItems, then verify the DLL SHA-256')) { return }

Assert-GameStopped $game.Root
& $buildScript -Configuration $Configuration -GameRoot $game.Root -CUCoreLibPath $game.CoreLib -SkipTests:$SkipTests
# Revalidates the completed artifact, space, and process state; verifies the copied DLL hash.
& $installScript -GameRoot $game.Root -CUCoreLibPath $game.CoreLib -Confirm:$false
