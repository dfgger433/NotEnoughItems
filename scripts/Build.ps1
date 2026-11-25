param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$dist = Join-Path $projectRoot 'dist'
if ((Test-Path -LiteralPath $dist) -and ((Get-Item -LiteralPath $dist -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw "Refusing to build into a linked output directory: $dist"
}
$packageDir = Join-Path $dist 'NotEnoughItems'
$stagingDir = Join-Path $dist ('.staging-' + [guid]::NewGuid().ToString('N'))
$stagedPackage = Join-Path $stagingDir 'NotEnoughItems'
$stagedDll = Join-Path $stagedPackage 'NotEnoughItems.dll'

try {
    # A fresh output directory keeps stale assets out of both deliverables.
    & dotnet build (Join-Path $projectRoot 'src\NotEnoughItems\NotEnoughItems.csproj') -c Release -o $stagedPackage "-p:GameRoot=$GameRoot" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    & dotnet run --project (Join-Path $projectRoot 'tests\NotEnoughItems.Tests\NotEnoughItems.Tests.csproj') -c Release "-p:GameRoot=$GameRoot" "-p:NeiAssemblyPath=$stagedDll"
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }

    # Publish only the plugin DLL and its content, not SDK/debug sidecars.
    foreach ($relativePath in @('NotEnoughItems.deps.json', 'NotEnoughItems.pdb', 'NotEnoughItems.xml')) {
        $sidecar = Join-Path $stagedPackage $relativePath
        if (Test-Path -LiteralPath $sidecar -PathType Leaf) { Remove-Item -LiteralPath $sidecar -Force }
    }
    $version = (Get-Item -LiteralPath $stagedDll).VersionInfo.ProductVersion.Split('+')[0]
    $zipName = "NotEnoughItems-$version.zip"
    $stagedZip = Join-Path $stagingDir $zipName
    # Archive the directory itself, not its contents: drag it into BepInEx/plugins.
    Compress-Archive -LiteralPath $stagedPackage -DestinationPath $stagedZip

    if (Test-Path -LiteralPath $packageDir -PathType Container) {
        $previousPackage = Get-Item -LiteralPath $packageDir -Force
        if ($previousPackage.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to replace a linked package directory: $packageDir"
        }
        Remove-Item -LiteralPath $packageDir -Recurse -Force
    }
    Move-Item -LiteralPath $stagedPackage -Destination $packageDir
    Move-Item -LiteralPath $stagedZip -Destination (Join-Path $dist $zipName) -Force
    Write-Output "Package directory: $packageDir"
    Write-Output "Package archive: $(Join-Path $dist $zipName)"
} finally {
    if (Test-Path -LiteralPath $stagingDir -PathType Container) { Remove-Item -LiteralPath $stagingDir -Recurse -Force }
}
