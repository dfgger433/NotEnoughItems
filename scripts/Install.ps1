param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\NotEnoughItems'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$languageFiles = @('Lang\en_us.json', 'Lang\zh_cn.json')
# Require every notice and license from the source docs before copying any files.
$docs = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($projectRoot, $_.FullName) })
# Ignore the old documentation path in case dist has not been cleaned since the move.
$assets = @(Get-ChildItem -LiteralPath (Join-Path $sourceDir 'assets') -File -Recurse |
    ForEach-Object { [IO.Path]::GetRelativePath($sourceDir, $_.FullName).Replace('\', '/') } |
    Where-Object { $_ -notlike 'assets/notenoughitems/docs/*' })
$files = @('NotEnoughItems.dll', 'LICENSE', 'COPYING') + $languageFiles + $docs + $assets
foreach ($file in $files) {
    $source = Join-Path $sourceDir $file
    if (!(Test-Path $source -PathType Leaf)) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
foreach ($file in $files) {
    $destination = Join-Path $destinationDir $file
    # Existing language files may have been edited by the player.
    if ($file -in $languageFiles -and (Test-Path -LiteralPath $destination -PathType Leaf)) { continue }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination $destination -Force
}
Write-Output "Installed in $destinationDir"
