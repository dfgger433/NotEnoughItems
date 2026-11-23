#Requires -Version 7.0

<#
.SYNOPSIS
Install an existing dist artifact without building the plugin.
.DESCRIPTION
The game must be closed. Existing assets are never overwritten. If a canonical
background is missing, a legacy Textures background takes precedence over the
bundled default. Use -WhatIf to validate and print the plan without installing.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$GameRoot,
    [string]$CUCoreLibPath
)

. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-SafeDistPath
$game = Get-GameSettings $GameRoot $CUCoreLibPath
$artifact = Get-BuildArtifact
$assetCopies = @(Get-InstallPlan $game.Root $game.PluginDir $artifact.Dll.Length $artifact.Assets)
$sourceHash = (Get-FileHash -LiteralPath $artifact.Dll.FullName -Algorithm SHA256).Hash
Assert-GameStopped $game.Root

$installedDll = Join-Path $game.PluginDir 'NotEnoughItems.dll'
$action = "Install NotEnoughItems from '$script:DistDirectory' ($($assetCopies.Count) missing assets), then verify the DLL SHA-256"
if (-not $PSCmdlet.ShouldProcess($game.PluginDir, $action)) { return }
Assert-GameStopped $game.Root
Assert-SafeInstallPath $game.Root

foreach ($copy in $assetCopies) {
    $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $copy.Destination))
    # Create the file without overwrite, even if an override appeared after preflight.
    [IO.File]::Copy($copy.Source, $copy.Destination, $false)
}

$null = [IO.Directory]::CreateDirectory($game.PluginDir)
# Verify a temporary copy before replacing the installed DLL.
$tempDll = Join-Path $game.PluginDir ('.NotEnoughItems-' + [guid]::NewGuid() + '.tmp')
try {
    [IO.File]::Copy($artifact.Dll.FullName, $tempDll, $false)
    $tempHash = (Get-FileHash -LiteralPath $tempDll -Algorithm SHA256).Hash
    if ($sourceHash -ne $tempHash) { throw "DLL copy verification failed for '$tempDll'." }
    [IO.File]::Move($tempDll, $installedDll, $true)
} finally {
    if ([IO.File]::Exists($tempDll)) { [IO.File]::Delete($tempDll) }
}

$installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) {
    throw "Install verification failed for '$installedDll'. Expected SHA-256 $sourceHash, found $installedHash."
}
Write-Host "Installed: $installedDll"
Write-Host "Verified SHA-256: $installedHash"
