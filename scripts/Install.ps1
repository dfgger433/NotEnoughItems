param(
    [string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$sourceDir = Join-Path $projectRoot 'dist\NotEnoughItems'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\NotEnoughItems'
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) { throw 'Invalid game directory.' }
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll') -PathType Leaf)) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
if ($Build) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -GameRoot $GameRoot
    if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
}
$languageFiles = @('Lang\en_us.json', 'Lang\zh_cn.json')
# Require every notice and license from the source docs before copying any files.
$docs = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($projectRoot, $_.FullName) })
$requiredFiles = @('NotEnoughItems.dll', 'LICENSE', 'COPYING', 'README.md', 'assets\notenoughitems\atlases\nei_gui.json') + $languageFiles + $docs
foreach ($file in $requiredFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $sourceDir $file) -PathType Leaf)) { throw "Missing package file: $file. Run Build.ps1 first." }
}
# Install the same tree as the ZIP, keeping the root folder at exactly one level.
$files = Get-ChildItem -LiteralPath $sourceDir -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($sourceDir, $_.FullName) }
foreach ($file in $files) {
    $destination = Join-Path $destinationDir $file
    # Existing language files may have been edited by the player.
    if ($file -in $languageFiles -and (Test-Path -LiteralPath $destination -PathType Leaf)) { continue }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination $destination -Force
}
Write-Output "Installed in $destinationDir"
