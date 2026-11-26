#Requires -Version 7.0
param([string]$GameRoot)

$ErrorActionPreference = 'Stop'
# Let Build.ps1 resolve the default game root when -GameRoot is omitted.
& (Join-Path $PSScriptRoot 'Build.ps1') @PSBoundParameters
