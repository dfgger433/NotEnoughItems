#Requires -Version 7.0

<#
.SYNOPSIS
Build the plugin without installing it. Run regression tests by default.
.DESCRIPTION
Build artifacts go to the repository's dist directory. Use -Package to create a versioned ZIP
there and -SkipTests to omit tests. The installed SDK and targeting packs are used offline;
no dependencies are downloaded.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [string]$GameRoot,
    [string]$CUCoreLibPath,
    [switch]$SkipTests,
    [switch]$Package
)

. (Join-Path $PSScriptRoot 'Common.ps1')

function New-PluginPackage {
    param([hashtable]$Artifact, [string]$Version)

    $packageDir = $script:DistDirectory
    $packagePath = Join-Path $packageDir "NotEnoughItems-$Version.zip"
    $entries = [ordered]@{ ($Artifact.Dll.Name) = $Artifact.Dll.FullName }
    foreach ($file in $Artifact.Assets) {
        $entries["$script:AssetRelativePath/$($file.RelativePath)"] = $file.Source
    }
    foreach ($name in @('README.md', 'CHANGELOG.md')) {
        $path = Join-Path $script:ProjectRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { $entries[$name] = $path }
    }

    # Create the replacement ZIP next to its destination and publish it only
    # after all entries were added successfully. Only clean up our own temp file.
    $tempPath = Join-Path $packageDir ('.NotEnoughItems-' + [guid]::NewGuid() + '.tmp')
    $stream = [IO.File]::Open($tempPath, [IO.FileMode]::CreateNew)
    try {
        try {
            $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($entry in $entries.GetEnumerator()) {
                    $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Value, $entry.Key)
                }
            } finally { $archive.Dispose() }
        } finally { $stream.Dispose() }
        [IO.File]::Move($tempPath, $packagePath, $true)
    } finally {
        if ([IO.File]::Exists($tempPath)) { [IO.File]::Delete($tempPath) }
    }
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
} else {
    Write-Host 'Regression tests skipped (-SkipTests).'
}

if ($Package) {
    New-PluginPackage $artifact $build.Version
}
