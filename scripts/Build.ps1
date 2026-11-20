#Requires -Version 7.0

<#
.SYNOPSIS
Build the plugin without installing it. Run regression tests by default.
.DESCRIPTION
Use -Package to create a versioned ZIP in bin/<Configuration> and -SkipTests to omit tests.
The installed .NET SDK and targeting packs are used offline; no dependencies are downloaded.
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
    param([string]$DllPath, [string]$Version, [string]$Config)

    $packageDir = (Get-RequiredItem (Join-Path $script:ProjectRoot "bin/$Config") 'Package output' -Directory).FullName
    $packagePath = Join-Path $packageDir "NotEnoughItems-$Version.zip"
    $entries = [ordered]@{ ([IO.Path]::GetFileName($DllPath)) = $DllPath }
    foreach ($file in (Get-ProjectAssets | Sort-Object FullName)) {
        $relative = [IO.Path]::GetRelativePath($script:AssetRoot, $file.FullName).Replace('\', '/')
        $entries["assets/notenoughitems/$relative"] = $file.FullName
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
Write-Host "Game root: $($game.Root)"
Invoke-DotNetCommand (@('build', $script:ProjectFile, '-c', $Configuration) + $buildArguments)
$build = Get-ProjectBuildInfo $Configuration $buildArguments
Write-Host "Plugin DLL: $($build.TargetPath)"

if (-not $SkipTests) {
    $testArguments = $buildArguments + ('-p:NeiAssemblyPath=' + (ConvertTo-MSBuildPropertyValue $build.TargetPath))
    Invoke-DotNetCommand (@('build', $script:TestProjectFile, '-c', $Configuration) + $testArguments)
    Invoke-DotNetCommand @('run', '--project', $script:TestProjectFile, '-c', $Configuration, '--no-build')
} else {
    Write-Host 'Regression tests skipped (-SkipTests).'
}

if ($Package) {
    New-PluginPackage $build.TargetPath $build.Version $Configuration
}
