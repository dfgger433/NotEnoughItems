#Requires -Version 7.0
param(
    [string]$GameRoot,
    # New version (MAJOR.MINOR.PATCH) or major/minor/patch. Bumps the release metadata before building.
    [string]$Bump
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$propsRelativePath = 'src/Directory.Build.props'
$propsPath = Join-Path $projectRoot $propsRelativePath
$changelogPath = Join-Path $projectRoot 'docs/CHANGELOG.md'
$nexusChangelogPath = Join-Path $projectRoot 'docs/release/nexus/CHANGELOG_NEXUS.md'
$nexusMetadataPath = Join-Path $projectRoot 'docs/release/nexus/NEXUS_METADATA.md'
$changelogs = @(
    @{ Path = $changelogPath; Label = 'docs/CHANGELOG.md'; Languages = @() },
    @{ Path = $nexusChangelogPath; Label = 'docs/release/nexus/CHANGELOG_NEXUS.md'; Languages = @('English', '中文') }
)
$unreleasedTitle = '未发布'
$releaseHeadingPattern = '(?m)^##[ \t]+(?<title>[^\r\n]+?)[ \t]*\r?$'
$changelogBulletPattern = '(?m)^[ \t]*[-*+][ \t]+\S'

function Get-ReleaseVersion([string]$text, [string]$source) {
    [xml]$props = $text
    $version = [string]$props.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid <Version> '$version' in $source. Expected MAJOR.MINOR.PATCH."
    }
    return $version
}

function Invoke-ReleaseGit([string[]]$arguments) {
    $result = & git -C $projectRoot @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check the release baseline (git $($arguments -join ' ')). Run Release.ps1 from a Git checkout with a committed version."
    }
    return $result
}

function Assert-WorkspaceVersion([string]$version) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Git is required to verify the release version in this checkout.'
    }
    if (Invoke-ReleaseGit @('rev-parse', '--show-prefix')) {
        throw 'The project must be at the root of its Git checkout to verify the release version.'
    }
    $committedProps = (Invoke-ReleaseGit @('show', "HEAD:$propsRelativePath")) -join "`n"
    $headVersion = Get-ReleaseVersion $committedProps "Git HEAD:$propsRelativePath"
    if ([version]$version -lt [version]$headVersion) {
        throw "Release version $version is lower than Git HEAD version $headVersion."
    }
    # Porcelain status includes staged, unstaged, and non-ignored untracked files.
    $changes = Invoke-ReleaseGit @('status', '--porcelain=v1', '--untracked-files=normal', '--ignore-submodules=none')
    if ($changes -and [version]$version -eq [version]$headVersion) {
        throw "The Git working tree has changes, but the release version is still $headVersion (Git HEAD). Update both changelogs and run Release.ps1 -Bump patch (or use a higher version); do not commit the changes at the old version."
    }
}

function Assert-ChangelogSection($entry, [string]$text, [string[]]$allowedTitles) {
    $headings = [regex]::Matches($text, $releaseHeadingPattern)
    if ($headings.Count -eq 0 -or $headings[0].Groups['title'].Value -notin $allowedTitles) {
        $expected = ($allowedTitles | ForEach-Object { "'## $_'" }) -join ' or '
        throw "$($entry.Label): the first release heading must be $expected. Write this change's entries before packaging."
    }
    $heading = $headings[0]
    $title = $heading.Groups['title'].Value
    $start = $heading.Index + $heading.Length
    $end = if ($headings.Count -gt 1) { $headings[1].Index } else { $text.Length }
    $section = $text.Substring($start, $end - $start)
    if ($section -notmatch $changelogBulletPattern) {
        throw "$($entry.Label): '## $title' must contain at least one changelog bullet."
    }
    foreach ($language in $entry.Languages) {
        $pattern = '(?ms)^###[ \t]+{0}[ \t]*\r?\n(?<body>.*?)(?=^###[ \t]+|\z)' -f [regex]::Escape($language)
        $languageSection = [regex]::Match($section, $pattern)
        if (-not $languageSection.Success -or $languageSection.Groups['body'].Value -notmatch $changelogBulletPattern) {
            throw "$($entry.Label): '## $title' must contain a changelog bullet under '### $language'."
        }
    }
    foreach ($other in ($headings | Select-Object -Skip 1)) {
        $otherTitle = $other.Groups['title'].Value
        if ($otherTitle -eq $unreleasedTitle -or $otherTitle -in $allowedTitles) {
            throw "$($entry.Label): unexpected '## $otherTitle' below the first release section."
        }
    }
}

function Resolve-NewVersion([string]$requested, [string]$current) {
    if ($requested -match '^\d+\.\d+\.\d+$') {
        return $requested
    }
    if ($requested -notin @('major', 'minor', 'patch')) {
        throw "Invalid -Bump '$requested'. Use MAJOR.MINOR.PATCH or major/minor/patch."
    }

    $parts = @($current.Split('.') | ForEach-Object { [int]$_ })
    switch ($requested) {
        'major' { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
        'minor' { $parts[1]++; $parts[2] = 0 }
        'patch' { $parts[2]++ }
    }
    return ($parts -join '.')
}

# Read-only checks run before any file is rewritten, so a failed preflight leaves the tree untouched.
function Assert-BumpInputs([string]$current, [string]$next) {
    if ([version]$next -le [version]$current) {
        throw "New version $next is not greater than the current version $current."
    }
    foreach ($entry in $changelogs) {
        Assert-ChangelogSection $entry (Get-Content -LiteralPath $entry.Path -Raw) @($unreleasedTitle, $next)
    }
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
}

function Set-ChangelogVersion([string]$path, [string]$next) {
    $text = Get-Content -LiteralPath $path -Raw
    $title = [regex]::Match($text, $releaseHeadingPattern).Groups['title']
    if ($title.Value -eq $unreleasedTitle) {
        # Swap only the heading text so the file's existing line endings are preserved.
        $updated = $text.Remove($title.Index, $title.Length).Insert($title.Index, $next)
        Set-Content -LiteralPath $path -Value $updated -Encoding utf8 -NoNewline
    }
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

# The release rule: every version reference must agree with src/Directory.Build.props.
function Assert-ReleaseMetadata([string]$version) {
    $problems = [System.Collections.Generic.List[string]]::new()

    foreach ($entry in $changelogs) {
        Assert-ChangelogSection $entry (Get-Content -LiteralPath $entry.Path -Raw) @($version)
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

$current = Get-ReleaseVersion (Get-Content -LiteralPath $propsPath -Raw) $propsRelativePath
$next = if ($Bump) { Resolve-NewVersion $Bump $current } else { $current }
Assert-WorkspaceVersion $next

if ($next -ne $current) {
    Assert-BumpInputs $current $next
    foreach ($entry in $changelogs) {
        Set-ChangelogVersion $entry.Path $next
    }
    Set-NexusMetadataVersion $current $next
    Set-SourceVersion $current $next
    Write-Output "Bumped $current -> $next in docs/CHANGELOG.md, CHANGELOG_NEXUS.md, NEXUS_METADATA.md and Directory.Build.props"
}

Assert-ReleaseMetadata $next

$buildArgs = @{}
if ($PSBoundParameters.ContainsKey('GameRoot')) { $buildArgs.GameRoot = $GameRoot }
& (Join-Path $PSScriptRoot 'Build.ps1') @buildArgs
