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
    $zipPath = Join-Path $dist $zipName
    $checksumPath = "$zipPath.sha256"
    $stagedZip = Join-Path $stagingDir $zipName
    $stagedChecksum = "$stagedZip.sha256"
    # Archive the directory itself, not its contents: drag it into BepInEx/plugins.
    Compress-Archive -LiteralPath $stagedPackage -DestinationPath $stagedZip
    $sha256 = (Get-FileHash -LiteralPath $stagedZip -Algorithm SHA256).Hash.ToLowerInvariant()
    # Use the sha256sum format with an archive filename relative to the checksum file.
    Set-Content -LiteralPath $stagedChecksum -Value "$sha256  $zipName`n" -Encoding ascii -NoNewline

    foreach ($file in @($zipPath, $checksumPath)) {
        if (Test-Path -LiteralPath $file -PathType Container) { throw "Expected a file, not a directory: $file" }
    }
    if (Test-Path -LiteralPath $packageDir -PathType Container) {
        $previousPackage = Get-Item -LiteralPath $packageDir -Force
        if ($previousPackage.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to replace a linked package directory: $packageDir"
        }
        Remove-Item -LiteralPath $packageDir -Recurse -Force
    }
    Move-Item -LiteralPath $stagedPackage -Destination $packageDir
    [IO.File]::Move($stagedZip, $zipPath, $true)
    [IO.File]::Move($stagedChecksum, $checksumPath, $true)
    Write-Output "Package directory: $packageDir"
    Write-Output "Package archive: $zipPath"
    Write-Output "SHA-256: $sha256"
    Write-Output "Checksum file: $checksumPath"
} finally {
    if (Test-Path -LiteralPath $stagingDir -PathType Container) { Remove-Item -LiteralPath $stagingDir -Recurse -Force }
}
