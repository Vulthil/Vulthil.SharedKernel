# Single source of truth for the coverage commands run by ci.yml's "Test with
# coverage" step -- keep this script and that step in sync; ci.yml invokes
# this script directly instead of duplicating the commands below.

param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not $NoBuild) {
    dotnet build -c Release
}

dotnet test -c Release --no-build -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml

dotnet tool restore

dotnet reportgenerator -reports:"**/TestResults/coverage.cobertura.xml" -targetdir:"./CoverageReport" -reporttypes:"Html;Badges"

$reportPath = Join-Path $PWD "CoverageReport" "index.html"
Write-Host "Coverage report: $reportPath"
