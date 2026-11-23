param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& dotnet build (Join-Path $projectRoot 'src\NotEnoughItems\NotEnoughItems.csproj') -c Release "-p:GameRoot=$GameRoot" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& dotnet run --project (Join-Path $projectRoot 'tests\NotEnoughItems.Tests\NotEnoughItems.Tests.csproj') -c Release "-p:GameRoot=$GameRoot"
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
