param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\NotEnoughItems'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$files = @('NotEnoughItems.dll') + @(Get-ChildItem (Join-Path $sourceDir 'assets') -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($sourceDir, $_.FullName) })
foreach ($file in $files) {
    $source = Join-Path $sourceDir $file
    if (!(Test-Path $source -PathType Leaf)) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
foreach ($file in $files) {
    $destination = Join-Path $destinationDir $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination $destination -Force
}
Write-Output "Installed in $destinationDir"
