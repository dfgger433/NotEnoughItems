#Requires -Version 7.0
param(
    [string]$GameRoot,
    # New version (0.1.35) or major/minor/patch. Bumps the release metadata before building.
    [string]$Bump
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$propsPath = Join-Path $projectRoot 'src\Directory.Build.props'
$changelogPath = Join-Path $projectRoot 'docs\CHANGELOG.md'
$nexusChangelogPath = Join-Path $projectRoot 'docs\release\nexus\CHANGELOG_NEXUS.md'
$nexusMetadataPath = Join-Path $projectRoot 'docs\release\nexus\NEXUS_METADATA.md'
$unreleasedHeading = '## 未发布'

function Get-ReleaseVersion {
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $version = [string]$props.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid <Version> '$version' in $propsPath. Expected MAJOR.MINOR.PATCH."
    }
    return $version
}

function Resolve-NewVersion([string]$requested, [string]$current) {
    if ($requested -match '^\d+\.\d+\.\d+$') {
        return $requested
    }
    if ($requested -notin @('major', 'minor', 'patch')) {
        throw "Invalid -Bump '$requested'. Use MAJOR.MINOR.PATCH (for example 0.1.35) or major/minor/patch."
    }

    $parts = @($current.Split('.') | ForEach-Object { [int]$_ })
    switch ($requested) {
        'major' { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
        'minor' { $parts[1]++; $parts[2] = 0 }
        'patch' { $parts[2]++ }
    }
    return ($parts -join '.')
}

function Test-VersionIncreased([string]$current, [string]$next) {
    return ([version]$next -gt [version]$current)
}

# Read-only checks run before any file is rewritten, so a failed bump leaves the tree untouched.
function Assert-BumpInputs([string]$current, [string]$next) {
    $changelog = Get-Content -LiteralPath $changelogPath -Raw
    $nexusChangelog = Get-Content -LiteralPath $nexusChangelogPath -Raw
    $metadata = Get-Content -LiteralPath $nexusMetadataPath -Raw
    $props = Get-Content -LiteralPath $propsPath -Raw

    if ($props -notmatch ('<Version>{0}</Version>' -f [regex]::Escape($current))) {
        throw "Could not find <Version>$current</Version> in $propsPath."
    }
    if ($metadata -notmatch [regex]::Escape("dist/NotEnoughItems-$current.zip")) {
        throw "The Nexus metadata does not reference dist/NotEnoughItems-$current.zip; fix the version fields before bumping."
    }
    if ($metadata -notmatch ('Not Enough Items {0} \(Mono\)' -f [regex]::Escape($current))) {
        throw "The Nexus metadata does not reference 'Not Enough Items $current (Mono)'; fix the version fields before bumping."
    }
    $versionRows = [regex]::Matches($metadata, '\|\s*\*\*Version\*\*\s*\|\s*`(?<value>[^`]+)`')
    if ($versionRows.Count -ne 2) {
        throw "The Nexus metadata must have two '| **Version** |' rows; found $($versionRows.Count). Fix the version fields before bumping."
    }
    foreach ($row in $versionRows) {
        if ($row.Groups['value'].Value -ne $current) {
            throw "A Nexus metadata Version row says '$($row.Groups['value'].Value)' instead of '$current'; fix the version fields before bumping."
        }
    }

    foreach ($entry in @(
            @{ Path = $changelogPath; Text = $changelog; Label = 'docs/CHANGELOG.md' },
            @{ Path = $nexusChangelogPath; Text = $nexusChangelog; Label = 'docs/release/nexus/CHANGELOG_NEXUS.md' })) {
        $alreadyRenamed = $entry.Text -match ('(?m)^##[ \t]+{0}[ \t]*\r?$' -f [regex]::Escape($next))
        $hasUnreleased = $entry.Text -match '(?m)^##[ \t]+未发布[ \t]*\r?$'
        if (-not $hasUnreleased -and -not $alreadyRenamed) {
            throw "$($entry.Label) must start with '$unreleasedHeading' containing this release's entries before -Bump."
        }
    }
    if ($current -eq $next) {
        return
    }
    if (-not (Test-VersionIncreased $current $next)) {
        throw "New version $next is not greater than the current version $current."
    }
}

function Set-ChangelogVersion([string]$path, [string]$next) {
    $text = Get-Content -LiteralPath $path -Raw
    if ($text -match ('(?m)^##[ \t]+{0}[ \t]*\r?$' -f [regex]::Escape($next))) {
        return
    }
    # Swap only the heading text so the file's existing line endings are preserved.
    $updated = [regex]::Replace($text, '(?m)(^##[ \t]+)未发布(?=[ \t]*\r?$)', ('## {0}' -f $next))
    Set-Content -LiteralPath $path -Value $updated -Encoding utf8 -NoNewline
}

function Set-NexusMetadataVersion([string]$current, [string]$next) {
    $text = Get-Content -LiteralPath $nexusMetadataPath -Raw
    $updated = $text.Replace($current, $next)
    Set-Content -LiteralPath $nexusMetadataPath -Value $updated -Encoding utf8 -NoNewline
}

function Set-SourceVersion([string]$current, [string]$next) {
    $text = Get-Content -LiteralPath $propsPath -Raw
    $updated = $text.Replace("<Version>$current</Version>", "<Version>$next</Version>")
    Set-Content -LiteralPath $propsPath -Value $updated -Encoding utf8 -NoNewline
}

function Get-FirstChangelogHeading([string]$text) {
    $match = [regex]::Match($text, '(?m)^##[ \t]+(?<title>[^\r\n]+?)[ \t]*\r?$')
    if (-not $match.Success) { return $null }
    return $match.Groups['title'].Value
}

# The release rule: every version reference must agree with src/Directory.Build.props.
function Assert-ReleaseMetadata {
    $version = Get-ReleaseVersion
    $problems = [System.Collections.Generic.List[string]]::new()

    foreach ($entry in @(
            @{ Path = $changelogPath; Label = 'docs/CHANGELOG.md' },
            @{ Path = $nexusChangelogPath; Label = 'docs/release/nexus/CHANGELOG_NEXUS.md' })) {
        $text = Get-Content -LiteralPath $entry.Path -Raw
        $heading = Get-FirstChangelogHeading $text
        if ($heading -eq '未发布') {
            $problems.Add("$($entry.Label) still starts with '$unreleasedHeading'; run Release.ps1 -Bump <version> to name the release.")
        } elseif ($null -eq $heading) {
            $problems.Add("$($entry.Label) has no '## <version>' release heading; expected '## $version'.")
        } elseif ($heading -ne $version) {
            $problems.Add("$($entry.Label) starts with '## $heading' but the source version is $version.")
        }
    }

    $metadata = Get-Content -LiteralPath $nexusMetadataPath -Raw
    if ($metadata -notmatch ('dist/NotEnoughItems-{0}\.zip' -f [regex]::Escape($version))) {
        $problems.Add("docs/release/nexus/NEXUS_METADATA.md: the Archive field must be dist/NotEnoughItems-$version.zip.")
    }
    if ($metadata -notmatch ('Not Enough Items {0} \(Mono\)' -f [regex]::Escape($version))) {
        $problems.Add("docs/release/nexus/NEXUS_METADATA.md: the Display Name must be 'Not Enough Items $version (Mono)'.")
    }
    $versionFields = [regex]::Matches($metadata, '\|\s*\*\*Version\*\*\s*\|\s*`(?<value>[^`]+)`')
    if ($versionFields.Count -ne 2) {
        $problems.Add("docs/release/nexus/NEXUS_METADATA.md: expected two '| **Version** |' rows, found $($versionFields.Count).")
    }
    foreach ($field in $versionFields) {
        if ($field.Groups['value'].Value -ne $version) {
            $problems.Add("docs/release/nexus/NEXUS_METADATA.md: a Version row says '$($field.Groups['value'].Value)' but the source version is $version.")
        }
    }

    if ($problems.Count -gt 0) {
        throw ("Release metadata is out of sync:`n - " + ($problems -join "`n - "))
    }
    Write-Output "Release metadata: $version (changelogs, version config and Nexus fields agree)"
}

$current = Get-ReleaseVersion
if ($Bump) {
    $next = Resolve-NewVersion $Bump $current
    if ($next -eq $current) {
        Write-Output "Version is already $current; verifying the release metadata without bumping."
    } else {
        Assert-BumpInputs $current $next
        Set-ChangelogVersion $changelogPath $next
        Set-ChangelogVersion $nexusChangelogPath $next
        Set-NexusMetadataVersion $current $next
        Set-SourceVersion $current $next
        Write-Output "Bumped $current -> $next in docs/CHANGELOG.md, CHANGELOG_NEXUS.md, NEXUS_METADATA.md and Directory.Build.props"
    }
}

Assert-ReleaseMetadata

$buildArgs = @{}
if ($PSBoundParameters.ContainsKey('GameRoot')) { $buildArgs.GameRoot = $GameRoot }
& (Join-Path $PSScriptRoot 'Build.ps1') @buildArgs
