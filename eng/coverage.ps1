# Single source of truth for the coverage commands run by ci.yml's "Test with
# coverage" step -- keep this script and that step in sync; ci.yml invokes
# this script directly instead of duplicating the commands below.
#
# The Aspire DCP-hosted suite under tests/MessagingIntegrationTests
# intermittently times out starting its host in CI, so it runs as its own
# dotnet test invocation with Microsoft.Testing.Extensions.Retry's
# --retry-failed-tests, instead of joining the shared invocation used for
# every other project. A Microsoft.Testing.Platform module fails outright on
# any CLI option it doesn't recognize, so a single solution-wide invocation
# could not pass --retry-failed-tests to just one project anyway -- and
# referencing the Retry package from every test project, just to allow that,
# would let it silently retry (mask) real flake in the unit suites too.
#
# Both project sets below are computed fresh from `dotnet sln list` on every
# run -- never hardcode project names/paths here. Other test projects land in
# this solution independently of this script, and must fall into the shared
# invocation automatically.

param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path -Parent $PSScriptRoot

# A crashed prior run can leave generated solutions behind; with two .slnx
# files at the root, the no-args `dotnet build` below refuses to pick one.
# -Force is required: on Linux, pwsh treats dot-prefixed files as hidden and
# refuses to remove them otherwise.
Remove-Item -Path (Join-Path $repoRoot '.coverage-*.generated.slnx') -Force -ErrorAction SilentlyContinue

if (-not $NoBuild) {
    dotnet build -c Release
}

function Get-FilteredSolutionProjects {
    param([string]$SolutionPath)

    dotnet sln $SolutionPath list |
        Select-Object -Skip 2 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ }
}

function New-GeneratedSolution {
    param([string[]]$ProjectPaths, [string]$OutFile)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('<Solution>')
    foreach ($path in $ProjectPaths) {
        $lines.Add("  <Project Path=`"$($path.Replace('\', '/'))`" />")
    }
    $lines.Add('</Solution>')

    # Written next to the real solution (and global.json) rather than under
    # the system temp directory, so MSBuild's upward SDK-resolution walk
    # (e.g. Aspire.Hosting's AppHost SDK, version-pinned in global.json)
    # still finds it -- a filtered solution file living outside the repo
    # tree fails to resolve that SDK.
    Set-Content -Path $OutFile -Value $lines -Encoding utf8
}

$solutionFile = Get-ChildItem -Path $repoRoot -Filter '*.slnx' -File |
    Where-Object { $_.Name -notlike '.coverage-*' } |
    Select-Object -First 1
if (-not $solutionFile) {
    throw "No .slnx solution file found at repository root ($repoRoot)."
}

$allProjects = @(Get-FilteredSolutionProjects -SolutionPath $solutionFile.FullName)

# Path-pattern exclusion, not a project-name list: matches the flaky suite's
# directory on both Windows (local) and Linux (CI) path separators.
$integrationSuitePattern = '^tests[\\/]MessagingIntegrationTests[\\/]'
$integrationTestProjects = @($allProjects | Where-Object { $_ -match $integrationSuitePattern -and $_ -match '\.Tests\.csproj$' })
$otherProjects = @($allProjects | Where-Object { $_ -notmatch $integrationSuitePattern })

$otherSolution = Join-Path $repoRoot '.coverage-other.generated.slnx'
$integrationSolution = Join-Path $repoRoot '.coverage-integration.generated.slnx'

try {
    New-GeneratedSolution -ProjectPaths $otherProjects -OutFile $otherSolution

    dotnet test $otherSolution -c Release --no-build -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml

    if ($integrationTestProjects.Count -gt 0) {
        New-GeneratedSolution -ProjectPaths $integrationTestProjects -OutFile $integrationSolution

        dotnet test $integrationSolution -c Release --no-build -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --retry-failed-tests 1
    }
    else {
        Write-Warning "No project matched tests/MessagingIntegrationTests/**/*.Tests.csproj -- skipping the isolated retried run."
    }
}
finally {
    # Raw unlink instead of Remove-Item: on Linux, pwsh maps the leading dot to
    # the Hidden attribute and Remove-Item then refuses the file without -Force
    # (the swallowed refusal left both files behind and broke the no-args
    # `dotnet pack` that runs after this script in CI). File.Delete has no
    # hidden-file gate and is a documented no-op when the file is absent.
    [System.IO.File]::Delete($otherSolution)
    [System.IO.File]::Delete($integrationSolution)
}

dotnet tool restore

dotnet reportgenerator -reports:"**/TestResults/coverage.cobertura.xml" -targetdir:"./CoverageReport" -reporttypes:"Html;Badges"

$reportPath = Join-Path $PWD "CoverageReport" "index.html"
Write-Host "Coverage report: $reportPath"
