#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$script:ProjectFile = Join-Path $script:ProjectRoot 'src/NotEnoughItems/NotEnoughItems.csproj'
$script:TestProjectFile = Join-Path $script:ProjectRoot 'tests/NotEnoughItems.Tests/NotEnoughItems.Tests.csproj'
$script:DistDirectory = Join-Path $script:ProjectRoot 'dist'
$script:ArtifactDll = Join-Path $script:DistDirectory 'NotEnoughItems.dll'
# The source directory's spelling may change, but the runtime path is lowercase.
$script:AssetRelativePath = 'assets/notenoughitems'
$script:LegacyBackgrounds = @{
    'textures/gui/backgrounds/bookmarks.png' = 'Textures/background-bookmarks.png'
    'textures/gui/backgrounds/catalog.png' = 'Textures/background-catalog.png'
    'textures/gui/backgrounds/options.png' = 'Textures/background-options.png'
    'textures/gui/backgrounds/recipe.png' = 'Textures/background-recipe.png'
    'textures/gui/backgrounds/default.png' = 'Textures/background.png'
}

function Get-RequiredItem {
    param([string]$Path, [string]$Label, [switch]$Directory)

    $item = Get-PathItem $Path
    if ($null -eq $item -or $item.PSProvider.Name -ne 'FileSystem' -or
        $item.PSIsContainer -ne $Directory.IsPresent) {
        $type = if ($Directory) { 'directory' } else { 'file' }
        throw "$Label $type not found or inaccessible: '$Path'."
    }
    return $item
}

function Get-PathItem {
    param([string]$Path)

    # Only absence is acceptable here; access errors are not evidence of absence.
    try { Get-Item -LiteralPath $Path -Force -ErrorAction Stop }
    catch [System.Management.Automation.ItemNotFoundException] { $null }
}

function Get-PhysicalPath {
    param([string]$Path)

    # Resolve directory junctions and symlinked files anywhere in an existing path.
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

function Assert-NoReparsePoints {
    param([string]$Root, [string[]]$RelativePaths)

    $rootItem = Get-RequiredItem $Root 'Trusted path root' -Directory
    $rootPath = Get-PhysicalPath $rootItem.FullName
    foreach ($relative in $RelativePaths) {
        if ([IO.Path]::IsPathFullyQualified($relative) -or
            ($relative -split '[\\/]') -contains '..') {
            throw "Expected a relative path below '$rootPath', got '$relative'."
        }
        $path = $rootPath
        foreach ($segment in ($relative -split '[\\/]')) {
            if (-not $segment -or $segment -eq '.') { continue }
            $path = Join-Path $path $segment
            $item = Get-PathItem $path
            if ($null -eq $item) { break }
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing to use a linked or reparse path component: '$path'."
            }
        }
    }
}

function Assert-SafeDistPath {
    $paths = @(
        'dist',
        'dist/NotEnoughItems.dll',
        'dist/assets',
        'dist/assets/notenoughitems',
        'src/NotEnoughItems/bin',
        'src/NotEnoughItems/obj',
        'src/NotEnoughItems/obj/Debug',
        'src/NotEnoughItems/obj/Release',
        'src/NotEnoughItems/obj/Debug/netstandard2.1',
        'src/NotEnoughItems/obj/Release/netstandard2.1',
        'tests/NotEnoughItems.Tests/bin',
        'tests/NotEnoughItems.Tests/obj',
        'tests/NotEnoughItems.Tests/bin/Debug/net8.0',
        'tests/NotEnoughItems.Tests/bin/Release/net8.0',
        'tests/NotEnoughItems.Tests/bin/Debug/core-only/net8.0',
        'tests/NotEnoughItems.Tests/bin/Release/core-only/net8.0',
        'tests/NotEnoughItems.Tests/obj/Debug/net8.0',
        'tests/NotEnoughItems.Tests/obj/Release/net8.0',
        'tests/NotEnoughItems.Tests/obj/Debug/core-only/net8.0',
        'tests/NotEnoughItems.Tests/obj/Release/core-only/net8.0'
    )
    foreach ($asset in Get-ProjectAssets) {
        $paths += 'dist/assets/notenoughitems/' + $asset.RelativePath
    }
    Assert-NoReparsePoints $script:ProjectRoot $paths
}

function Test-SamePath {
    param([string]$Left, [string]$Right)

    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    return [string]::Equals($Left, $Right, $comparison)
}

function ConvertTo-MSBuildPropertyValue {
    param([string]$Value)

    # MSBuild parses comma and semicolon as property-list delimiters.
    return $Value.Replace('%', '%25').Replace(',', '%2C').Replace(';', '%3B')
}

function Get-GameSettings {
    param([string]$Root, [string]$CoreLib)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        $Root = Join-Path $script:ProjectRoot '../..'
    }
    $rootItem = Get-RequiredItem $Root 'Game root (set -GameRoot to the game installation)' -Directory
    $rootPath = $rootItem.FullName
    $managed = Join-Path $rootPath 'CasualtiesUnknown_Data/Managed'
    $bepInEx = Join-Path $rootPath 'BepInEx'
    $plugins = (Get-RequiredItem (Join-Path $bepInEx 'plugins') 'BepInEx plugins' -Directory).FullName
    foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll')) {
        $null = Get-RequiredItem (Join-Path $managed $name) 'Game assembly'
    }
    foreach ($name in @('BepInEx.dll', '0Harmony.dll')) {
        $null = Get-RequiredItem (Join-Path $bepInEx "core/$name") 'BepInEx assembly'
    }
    if ([string]::IsNullOrWhiteSpace($CoreLib)) {
        $CoreLib = Join-Path $plugins 'CUCoreLib.dll'
    }
    $coreLibPath = (Get-RequiredItem $CoreLib 'CUCoreLib assembly (set -CUCoreLibPath if installed elsewhere)').FullName

    return @{
        Root = $rootPath
        CoreLib = $coreLibPath
        PluginDir = Join-Path $plugins 'NotEnoughItems'
        Properties = @(
            '-p:GameRoot=' + (ConvertTo-MSBuildPropertyValue $rootPath)
            '-p:CUCoreLibPath=' + (ConvertTo-MSBuildPropertyValue $coreLibPath)
        )
    }
}

function Get-ProjectAssets {
    $assetRoot = $script:ProjectRoot
    foreach ($segment in @('Assets', 'notenoughitems')) {
        $matches = @(Get-ChildItem -LiteralPath $assetRoot -Directory -Force | Where-Object Name -EQ $segment)
        if ($matches.Count -ne 1) {
            throw "Expected exactly one asset directory named '$segment' (ignoring case) in '$assetRoot'."
        }
        $assetRoot = $matches[0].FullName
    }
    $files = @(Get-ChildItem -LiteralPath $assetRoot -File -Recurse -Force | Sort-Object FullName)
    if ($files.Count -eq 0) {
        throw "The plugin asset namespace has no files: '$assetRoot'."
    }
    $assetPaths = @('Assets/NotEnoughItems')
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($assetRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
        $assetPaths += 'Assets/NotEnoughItems/' + $relative
    }
    Assert-NoReparsePoints $script:ProjectRoot $assetPaths
    foreach ($file in $files) {
        [pscustomobject]@{
            RelativePath = [IO.Path]::GetRelativePath($assetRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
            Bytes = $file.Length
        }
    }
}

function Get-BuildArtifact {
    $dll = Get-RequiredItem $script:ArtifactDll 'Build artifact (run Build.ps1 first)'
    if ($dll.Length -eq 0) { throw "Build artifact is empty: '$($dll.FullName)'." }
    $assetRoot = Join-Path $script:DistDirectory $script:AssetRelativePath
    $assets = foreach ($asset in Get-ProjectAssets) {
        $path = Join-Path $assetRoot $asset.RelativePath
        $file = Get-RequiredItem $path 'Build artifact (run Build.ps1 first)'
        [pscustomobject]@{
            RelativePath = $asset.RelativePath
            Source = $file.FullName
            Bytes = $file.Length
        }
    }
    return @{ Dll = $dll; Assets = @($assets) }
}

function Format-DotNetCommand {
    param([string[]]$Arguments)

    $display = foreach ($argument in $Arguments) {
        if ($argument -match '^[A-Za-z0-9_./:\\=-]+$') { $argument }
        else { "'" + $argument.Replace("'", "''") + "'" }
    }
    return 'dotnet ' + ($display -join ' ')
}

function Invoke-DotNetCommand {
    param([string[]]$Arguments, [switch]$CaptureOutput)

    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $dotnet) { throw 'dotnet SDK not found on PATH.' }
    $command = Format-DotNetCommand $Arguments
    Write-Host "> $command"
    # Match PowerShell's native pipe decoder to the child CLI. The host may start
    # with CP936 (or another code page), even when .NET writes UTF-8 to the pipe.
    $previousConsoleEncoding = [Console]::OutputEncoding
    $previousForceUtf8 = [Environment]::GetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', 'Process')
    Push-Location -LiteralPath $script:ProjectRoot
    try {
        [Environment]::SetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', '1', 'Process')
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        if ($CaptureOutput) {
            $output = & $dotnet.Source @Arguments
        } else {
            & $dotnet.Source @Arguments | Out-Host
        }
        if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $command" }
        if ($CaptureOutput) { return $output }
    } finally {
        [Console]::OutputEncoding = $previousConsoleEncoding
        [Environment]::SetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING', $previousForceUtf8, 'Process')
        Pop-Location
    }
}

function Get-ProjectBuildInfo {
    param([string]$Configuration, [string[]]$BuildArguments)

    $output = Invoke-DotNetCommand (@('msbuild', $script:ProjectFile, '-nologo',
        '-getProperty:TargetPath,Version', "-p:Configuration=$Configuration") + $BuildArguments) -CaptureOutput
    try {
        $properties = ($output -join "`n" | ConvertFrom-Json).Properties
        if (-not [IO.Path]::IsPathFullyQualified($properties.TargetPath) -or
            $properties.Version -notmatch '^\d[A-Za-z0-9.+-]*$') {
            throw 'MSBuild did not return an absolute DLL path and a filename-safe version.'
        }
    } catch {
        throw "Cannot read TargetPath and Version from MSBuild: $($_.Exception.Message)"
    }
    if (-not (Test-SamePath ([IO.Path]::GetFullPath($properties.TargetPath)) $script:ArtifactDll)) {
        throw "Unexpected MSBuild TargetPath '$($properties.TargetPath)'; expected '$script:ArtifactDll'."
    }
    return $properties
}

function Get-OfflineBuildArguments {
    param([hashtable]$Game)

    # This project has no NuGet package dependencies. Restore from the project
    # directory only; required SDK/targeting packs must already be installed.
    $source = ConvertTo-MSBuildPropertyValue $script:ProjectRoot
    return @('-warnaserror', "-p:RestoreSources=$source", '-p:NuGetAudit=false') + $Game.Properties
}

function Get-GameExecutable {
    param([string]$Root)

    $executable = Get-RequiredItem (Join-Path $Root 'CasualtiesUnknown.exe') 'Game executable'
    return Get-PhysicalPath $executable.FullName
}

function Assert-GameStopped {
    param([string]$Root)

    $expectedExe = Get-GameExecutable $Root
    foreach ($process in (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue)) {
        $runningExe = $null
        try {
            if ($process.Path) { $runningExe = Get-PhysicalPath $process.Path }
        } catch {
            $runningExe = $null
        }
        if (-not $runningExe) {
            if ($process.HasExited) { continue }
            throw "Cannot verify the executable path of CasualtiesUnknown (PID $($process.Id)). Close it before installing."
        }
        if (Test-SamePath $runningExe $expectedExe) {
            throw "The game is running from '$Root' (PID $($process.Id)). Close it before installing."
        }
    }
}

function Get-InstallPathItem {
    param([string]$PluginParent, [string]$RelativePath, [switch]$Directory)

    $path = $PluginParent
    $segments = $RelativePath -split '/'
    for ($i = 0; $i -lt $segments.Count; $i++) {
        $path = Join-Path $path $segments[$i]
        $item = Get-PathItem $path
        if ($null -eq $item) { return $null }
        # Do not follow links below BepInEx/plugins or write outside the plugin directory.
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Unexpected link or reparse point in plugin path: '$path'."
        }
        $expectDirectory = ($i -lt ($segments.Count - 1)) -or $Directory.IsPresent
        if ($item.PSIsContainer -ne $expectDirectory) {
            $expectedType = if ($expectDirectory) { 'directory' } else { 'file' }
            throw "Expected a $expectedType at plugin path '$path'."
        }
    }
    return $item
}

function Assert-SafeInstallPath {
    param([string]$GameRoot)

    $paths = @('BepInEx', 'BepInEx/plugins', 'BepInEx/plugins/NotEnoughItems')
    foreach ($asset in Get-ProjectAssets) {
        $paths += 'BepInEx/plugins/NotEnoughItems/' + $script:AssetRelativePath + '/' + $asset.RelativePath
    }
    $paths += 'BepInEx/plugins/NotEnoughItems/NotEnoughItems.dll'
    Assert-NoReparsePoints $GameRoot $paths
}

function Get-InstallPlan {
    param([string]$GameRoot, [string]$Destination, [long]$DllBytes, [object[]]$Assets)

    Assert-SafeInstallPath $GameRoot
    $pluginParent = Split-Path -Parent $Destination
    $pluginName = Split-Path -Leaf $Destination
    $pluginDir = Get-InstallPathItem $pluginParent $pluginName -Directory
    $dll = Get-InstallPathItem $pluginParent "$pluginName/NotEnoughItems.dll"
    if ($null -ne $dll -and $dll.IsReadOnly) {
        throw "Plugin DLL destination is read-only: '$($dll.FullName)'."
    }

    $required = $DllBytes
    $assetCopies = foreach ($asset in $Assets) {
        $relative = "$pluginName/$script:AssetRelativePath/$($asset.RelativePath)"
        $existing = Get-InstallPathItem $pluginParent $relative
        if ($null -ne $existing) { continue }  # Preserve all existing overrides.
        $legacy = if ($script:LegacyBackgrounds.ContainsKey($asset.RelativePath)) {
            Get-InstallPathItem $pluginParent "$pluginName/$($script:LegacyBackgrounds[$asset.RelativePath])"
        }
        if ($null -ne $legacy) {
            $required += $legacy.Length
            $source = $legacy.FullName
        } else {
            $required += $asset.Bytes
            $source = if ($asset.PSObject.Properties['Source']) { $asset.Source }  # Only supplied by Install.
        }
        [pscustomobject]@{
            Source = $source
            Destination = Join-Path $pluginParent $relative
        }
    }
    # Check the actual volume of an existing ancestor, with filesystem overhead.
    $existingDirectory = if ($null -eq $pluginDir) { $pluginParent } else { $pluginDir.FullName }
    Assert-FreeSpace (Get-PhysicalPath $existingDirectory) ($required + 1MB)
    return $assetCopies
}

function Assert-FreeSpace {
    param([string]$Directory, [long]$MinimumBytes)

    $pathRoot = [System.IO.Path]::GetPathRoot($Directory)
    try {
        $free = [System.IO.DriveInfo]::new($pathRoot).AvailableFreeSpace
    } catch {
        throw "Cannot determine free space for '$Directory': $($_.Exception.Message)"
    }
    if ($free -lt $MinimumBytes) {
        throw "Insufficient free space for '$Directory': need at least $MinimumBytes bytes; have $free."
    }
}
