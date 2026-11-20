# Single source of truth for the test commands run by ci.yml's "Test" step --
# keep this script and that step in sync; ci.yml invokes this script directly
# instead of duplicating the commands below.
#
# The test projects run as three dotnet test invocations at the same time, so
# the slow suites start right away instead of waiting in one shared queue
# (dotnet test runs only as many test modules at once as the machine has CPU
# threads):
#
#   - Container suites: every test project that registers a Vulthil.xUnit
#     ContainerHost subclass as an assembly fixture, the documented way to
#     share test containers. They mostly wait on Docker, so xUnit runs their
#     test classes on twice as many threads as the machine has CPU threads.
#   - The Aspire DCP-hosted suite under tests/MessagingIntegrationTests. It
#     intermittently times out starting its host in CI, so its invocation adds
#     Microsoft.Testing.Extensions.Retry's --retry-failed-tests. A
#     Microsoft.Testing.Platform module fails outright on any CLI option it
#     doesn't recognize, so a single solution-wide invocation could not pass
#     --retry-failed-tests to just one project anyway -- and referencing the
#     Retry package from every test project, just to allow that, would let it
#     silently retry (mask) real flake in the unit suites too.
#   - Every other project.
#
# All project sets below are computed fresh from `dotnet sln list` and the
# project sources on every run -- never hardcode project names/paths here.
# Other test projects land in this solution independently of this script, and
# must fall into the right invocation automatically.

param(
    [switch]$NoBuild,
    # Runs the tests without collecting coverage and without the report.
    [switch]$NoCoverage
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path -Parent $PSScriptRoot

# A crashed prior run can leave generated solutions behind; with two .slnx
# files at the root, the no-args `dotnet build` below refuses to pick one.
# -Force is required: on Linux, pwsh treats dot-prefixed files as hidden and
# refuses to remove them otherwise.
Remove-Item -Path (Join-Path $repoRoot '.coverage-*.generated.slnx') -Force -ErrorAction SilentlyContinue

# Coverage files are GUID-named per test module (see the dotnet test calls
# below), so reruns add files instead of overwriting them -- without this
# sweep, reportgenerator would merge stale reports from prior runs into this
# run's report. Deletes what the -reports glob at the bottom matches.
Get-ChildItem -Path $repoRoot -Recurse -Directory -Filter 'TestResults' -ErrorAction SilentlyContinue |
    Get-ChildItem -Filter '*.cobertura.xml' -File |
    Remove-Item -Force

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

function Test-UsesContainerHost {
    param([string]$ProjectPath)

    $projectDirectory = Split-Path -Parent (Join-Path $repoRoot $ProjectPath)
    $sources = @(Get-ChildItem -Path $projectDirectory -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })

    # Both checks: Vulthil.xUnit's own tests define ContainerHost subclasses
    # without starting any container.
    $definesHost = [bool]($sources | Select-String -Pattern ':\s*ContainerHost\b' -List)
    $registersAssemblyFixture = [bool]($sources | Select-String -Pattern '\[assembly:\s*AssemblyFixture<' -List)

    return $definesHost -and $registersAssemblyFixture
}

function Start-DotnetTest {
    param([string]$Solution, [string[]]$TestArguments)

    $arguments = @('test', $Solution, '-c', 'Release', '--no-build')
    # A bare trailing `--` reaches every test module as an argument, which
    # Microsoft.Testing.Platform rejects (exit code 5).
    if ($TestArguments.Count -gt 0) {
        $arguments += @('--') + $TestArguments
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    foreach ($argument in $arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    # The invocations share this console, so their progress lines interleave,
    # each naming its own test module.
    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = $repoRoot

    return [System.Diagnostics.Process]::Start($startInfo)
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
$dcpSuitePattern = '^tests[\\/]MessagingIntegrationTests[\\/]'
$dcpTestProjects = @($allProjects | Where-Object { $_ -match $dcpSuitePattern -and $_ -match '\.Tests\.csproj$' })
$remainingProjects = @($allProjects | Where-Object { $_ -notmatch $dcpSuitePattern })
$containerTestProjects = @($remainingProjects | Where-Object { Test-UsesContainerHost -ProjectPath $_ })
$otherProjects = @($remainingProjects | Where-Object { $_ -notin $containerTestProjects })

$coverageArguments = if ($NoCoverage) { @() } else { @('--coverage', '--coverage-output-format', 'cobertura') }

$otherSolution = Join-Path $repoRoot '.coverage-other.generated.slnx'
$containerSolution = Join-Path $repoRoot '.coverage-containers.generated.slnx'
$dcpSolution = Join-Path $repoRoot '.coverage-integration.generated.slnx'

$invocations = [System.Collections.Generic.List[object]]::new()
try {
    # No --coverage-output on any invocation: dotnet test hands every test
    # module the same value, and since CodeCoverage 18.9 (Testing.Platform
    # 2.3) a relative filename resolves against the shared
    # <repo root>/TestResults instead of the module's own results directory,
    # so parallel modules race on a single file -- IOException on Windows,
    # silent last-writer-wins under-reporting on Linux. That resolution change
    # is intentional (microsoft/testfx#7265, confirmed in
    # microsoft/codecoverage#226), so don't pin the package back; the option
    # also has no per-module placeholder support. The default GUID filenames
    # are unique per module, keeping the parallel writes disjoint.
    if ($containerTestProjects.Count -gt 0) {
        New-GeneratedSolution -ProjectPaths $containerTestProjects -OutFile $containerSolution
        $invocations.Add([pscustomobject]@{
            Name    = 'container suites'
            Process = Start-DotnetTest -Solution $containerSolution -TestArguments ($coverageArguments + @('--max-threads', '2.0x'))
        })
    }

    if ($dcpTestProjects.Count -gt 0) {
        New-GeneratedSolution -ProjectPaths $dcpTestProjects -OutFile $dcpSolution
        $invocations.Add([pscustomobject]@{
            Name    = 'Aspire DCP suite'
            Process = Start-DotnetTest -Solution $dcpSolution -TestArguments ($coverageArguments + @('--retry-failed-tests', '1'))
        })
    }
    else {
        Write-Warning "No project matched tests/MessagingIntegrationTests/**/*.Tests.csproj -- skipping the isolated retried run."
    }

    New-GeneratedSolution -ProjectPaths $otherProjects -OutFile $otherSolution
    $invocations.Add([pscustomobject]@{
        Name    = 'other projects'
        Process = Start-DotnetTest -Solution $otherSolution -TestArguments $coverageArguments
    })

    foreach ($invocation in $invocations) {
        $invocation.Process.WaitForExit()
    }
}
finally {
    # A failure while starting the invocations must not leave a started one
    # running against the generated solutions deleted below.
    foreach ($invocation in $invocations) {
        if (-not $invocation.Process.HasExited) {
            $invocation.Process.Kill($true)
            $invocation.Process.WaitForExit()
        }
    }

    # Raw unlink instead of Remove-Item: on Linux, pwsh maps the leading dot to
    # the Hidden attribute and Remove-Item then refuses the file without -Force
    # (the swallowed refusal left the files behind and broke the no-args
    # `dotnet pack` that runs after this script in CI). File.Delete has no
    # hidden-file gate and is a documented no-op when the file is absent.
    [System.IO.File]::Delete($otherSolution)
    [System.IO.File]::Delete($containerSolution)
    [System.IO.File]::Delete($dcpSolution)
}

$failedInvocations = @($invocations | Where-Object { $_.Process.ExitCode -ne 0 })
foreach ($invocation in $failedInvocations) {
    Write-Host "dotnet test for the $($invocation.Name) failed with exit code $($invocation.Process.ExitCode)."
}
if ($failedInvocations.Count -gt 0) {
    exit 1
}

if ($NoCoverage) {
    return
}

dotnet tool restore

dotnet reportgenerator -reports:"**/TestResults/*.cobertura.xml" -targetdir:"./CoverageReport" -reporttypes:"Html;Badges"

$reportPath = Join-Path $PWD "CoverageReport" "index.html"
Write-Host "Coverage report: $reportPath"
