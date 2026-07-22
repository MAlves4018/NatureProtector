<#
.SYNOPSIS
Runs Stryker.NET mutation testing with bounded local artifacts.

.DESCRIPTION
Wraps dotnet-stryker so mutation runs have a stable output directory, timeout,
captured stdout/stderr, a manifest, and explicit exit-code handling. This script
does not execute Git. It intentionally keeps the Stryker break threshold at 0
until a reliable baseline is produced and classified.
#>

[CmdletBinding()]
param(
    [ValidateSet("Smoke", "Configured")]
    [string]$Profile = "Smoke",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("Basic", "Standard", "Advanced", "Complete")]
    [string]$MutationLevel = "Basic",
    [int]$Concurrency = 2,
    [int]$TimeoutSeconds = 900,
    [string]$Reporters = "Progress,ClearText,Json,Html",
    [string]$MutateGlobs = "",
    [string]$OutputRoot = ".\artifacts\mutation",
    [switch]$NoRun
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"

function Resolve-UnderRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root)
    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $rootFullPath $Path
    }

    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    if (-not ($fullPath.StartsWith($rootFullPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($fullPath, $rootFullPath, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to write outside repository root: $fullPath"
    }

    return $fullPath
}

function Quote-Argument {
    param([string]$Argument)

    if ($Argument -match '\s|"') {
        return '"' + ($Argument -replace '"', '\"') + '"'
    }

    return $Argument
}

function Resolve-Reporters {
    param([string]$ReporterText)

    $allowed = [ordered]@{
        Progress = "Progress"
        Dots = "Dots"
        ClearText = "ClearText"
        ClearTextTree = "ClearTextTree"
        Json = "Json"
        Html = "Html"
        Markdown = "Markdown"
    }
    $allowedByLower = @{}
    foreach ($key in $allowed.Keys) {
        $allowedByLower[$key.ToLowerInvariant()] = $allowed[$key]
    }

    $values = @($ReporterText -split '[,; ]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($values.Count -eq 0) {
        throw "At least one Stryker reporter must be specified."
    }

    $resolved = [System.Collections.Generic.List[string]]::new()
    foreach ($value in $values) {
        $key = $value.Trim().ToLowerInvariant()
        if (-not $allowedByLower.ContainsKey($key)) {
            throw "Unsupported Stryker reporter '$value'. Allowed reporters: $($allowed.Keys -join ', ')."
        }

        $canonical = $allowedByLower[$key]
        if (-not $resolved.Contains($canonical)) {
            $resolved.Add($canonical) | Out-Null
        }
    }

    return $resolved.ToArray()
}

function Invoke-CheckedDotnetCommand {
    param(
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$LogPath
    )

    Add-Content -LiteralPath $LogPath -Encoding UTF8 -Value ("> dotnet " + (($Arguments | ForEach-Object { Quote-Argument $_ }) -join " "))
    Push-Location $WorkingDirectory
    try {
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($output) {
        Add-Content -LiteralPath $LogPath -Encoding UTF8 -Value ($output | Out-String)
    }
    Add-Content -LiteralPath $LogPath -Encoding UTF8 -Value "ExitCode: $exitCode"
    Add-Content -LiteralPath $LogPath -Encoding UTF8 -Value ""

    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code $exitCode. See $LogPath."
    }
}

function Invoke-ProcessWithTimeout {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$StandardOutputPath,
        [string]$StandardErrorPath,
        [int]$TimeoutSeconds
    )

    $command = Get-Command $FileName -ErrorAction Stop
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $command.Source
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($Arguments | ForEach-Object { Quote-Argument $_ }) -join " ")

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $startedAt = Get-Date
    [void]$process.Start()

    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $completed) {
        try {
            $process.Kill()
        }
        catch {
            Write-Warning "Failed to kill timed-out mutation process: $($_.Exception.Message)"
        }
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $finishedAt = Get-Date

    $stdout | Set-Content -LiteralPath $StandardOutputPath -Encoding UTF8
    $stderr | Set-Content -LiteralPath $StandardErrorPath -Encoding UTF8

    return [pscustomobject]@{
        ExitCode = if ($completed) { $process.ExitCode } else { 124 }
        TimedOut = -not $completed
        StartedAt = $startedAt.ToString("o")
        FinishedAt = $finishedAt.ToString("o")
        DurationSeconds = [Math]::Round(($finishedAt - $startedAt).TotalSeconds, 3)
    }
}

function Get-MutationLogSummary {
    param(
        [string]$StdoutPath,
        [string[]]$RequestedMutateGlobs
    )

    $stdoutLines = @()
    if (Test-Path -LiteralPath $StdoutPath) {
        $stdoutLines = @(Get-Content -LiteralPath $StdoutPath | ForEach-Object { [string]$_ })
    }

    $scopeLines = @($stdoutLines | Where-Object { $_ -match "Stryker will mutate " })
    $analysisLines = @($stdoutLines | Where-Object { $_ -match "Analyzing .*test project" })
    $buildLines = @($stdoutLines | Where-Object { $_ -match "Building (solution|project) " })
    $testCountLines = @($stdoutLines | Where-Object { $_ -match "Number of tests found:" })
    $compileErrorLines = @($stdoutLines | Where-Object { $_ -match "resulted in a compile error" })
    $safeModeLines = @($stdoutLines | Where-Object { $_ -match "Safe Mode!" })
    $allMutantsTested = @($stdoutLines | Where-Object { $_ -match "All mutants have been tested" }).Count -gt 0
    $mutantsCreated = $null
    $totalMutantsSkipped = $null
    $totalMutantsTested = $null
    $killed = $null
    $survived = $null
    $timeout = $null
    $statusCounts = [ordered]@{}

    foreach ($line in $stdoutLines) {
        $match = [regex]::Match($line, '^\[\d{2}:\d{2}:\d{2} INF\]\s+(?<count>\d+)\s+mutants created')
        if ($match.Success) {
            $mutantsCreated = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^\[\d{2}:\d{2}:\d{2} INF\]\s+(?<count>\d+)\s+mutants got status (?<status>[A-Za-z]+)\.')
        if ($match.Success) {
            $statusCounts[$match.Groups["status"].Value] = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^\[\d{2}:\d{2}:\d{2} INF\]\s+(?<count>\d+)\s+total mutants are skipped')
        if ($match.Success) {
            $totalMutantsSkipped = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^\[\d{2}:\d{2}:\d{2} INF\]\s+(?<count>\d+)\s+total mutants will be tested')
        if ($match.Success) {
            $totalMutantsTested = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^Killed:\s+(?<count>\d+)')
        if ($match.Success) {
            $killed = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^Survived:\s+(?<count>\d+)')
        if ($match.Success) {
            $survived = [int]$match.Groups["count"].Value
            continue
        }

        $match = [regex]::Match($line, '^Timeout:\s+(?<count>\d+)')
        if ($match.Success) {
            $timeout = [int]$match.Groups["count"].Value
            continue
        }
    }

    $normalizedGlobs = @($RequestedMutateGlobs | ForEach-Object { $_.Replace("\", "/").TrimStart("./") })
    $outOfScopeCompileErrors = @()
    foreach ($line in $compileErrorLines) {
        $normalizedLine = $line.Replace("\", "/")
        $matchesRequestedScope = $false
        foreach ($glob in $normalizedGlobs) {
            if ($glob -and $normalizedLine.Contains($glob)) {
                $matchesRequestedScope = $true
                break
            }
        }

        if (-not $matchesRequestedScope) {
            $outOfScopeCompileErrors += $line
        }
    }

    return [pscustomobject]@{
        AnalysisLines = $analysisLines
        ScopeLines = $scopeLines
        BuildLines = $buildLines
        TestCountLines = $testCountLines
        CompileErrorLines = $compileErrorLines
        SafeModeLines = $safeModeLines
        OutOfScopeCompileErrorLines = $outOfScopeCompileErrors
        MutantsCreated = $mutantsCreated
        StatusCounts = $statusCounts
        TotalMutantsSkipped = $totalMutantsSkipped
        TotalMutantsTested = $totalMutantsTested
        Killed = $killed
        Survived = $survived
        Timeout = $timeout
        AllMutantsTested = $allMutantsTested
    }
}

function Write-MutationDiagnostics {
    param(
        [string]$Path,
        [string]$RunId,
        [string]$Profile,
        [string]$Configuration,
        [string]$EffectiveConfigRelativePath,
        [string]$IsolationMode,
        [string]$IsolatedSolutionPath,
        [string[]]$Reporters,
        [string[]]$RequestedMutateGlobs,
        [object]$Result,
        [string[]]$JsonReports,
        [string[]]$HtmlReports,
        [object]$LogSummary
    )

    $markdown = [System.Collections.Generic.List[string]]::new()
    $markdown.Add("# Mutation diagnostics") | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add('- Run: `' + $RunId + '`') | Out-Null
    $markdown.Add('- Profile: `' + $Profile + '`') | Out-Null
    $markdown.Add('- Configuration: `' + $Configuration + '`') | Out-Null
    $markdown.Add('- Effective config: `' + $EffectiveConfigRelativePath + '`') | Out-Null
    $markdown.Add('- Isolation mode: `' + $IsolationMode + '`') | Out-Null
    if ($IsolatedSolutionPath) {
        $markdown.Add('- Isolated solution: `' + $IsolatedSolutionPath + '`') | Out-Null
    }
    $markdown.Add('- Reporters: `' + ($Reporters -join ", ") + '`') | Out-Null
    $markdown.Add('- Exit code: `' + $Result.ExitCode + '`') | Out-Null
    $markdown.Add('- Timed out: `' + $Result.TimedOut + '`') | Out-Null
    $markdown.Add('- Duration: `' + $Result.DurationSeconds + 's`') | Out-Null
    $markdown.Add('- JSON reports: `' + $JsonReports.Count + '`') | Out-Null
    $markdown.Add('- HTML reports: `' + $HtmlReports.Count + '`') | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add("## Requested mutate globs") | Out-Null
    $markdown.Add("") | Out-Null
    foreach ($glob in $RequestedMutateGlobs) {
        $markdown.Add('- `' + $glob + '`') | Out-Null
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Key log signals") | Out-Null
    $markdown.Add("") | Out-Null
    foreach ($line in @($LogSummary.AnalysisLines + $LogSummary.ScopeLines + $LogSummary.BuildLines + $LogSummary.TestCountLines)) {
        $markdown.Add('- `' + $line + '`') | Out-Null
    }
    if (($LogSummary.AnalysisLines.Count + $LogSummary.ScopeLines.Count + $LogSummary.BuildLines.Count + $LogSummary.TestCountLines.Count) -eq 0) {
        $markdown.Add("- No Stryker scope, build or test-count lines were parsed.") | Out-Null
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Mutation summary parsed from stdout") | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add('- Mutants created: `' + $LogSummary.MutantsCreated + '`') | Out-Null
    $markdown.Add('- Mutants skipped: `' + $LogSummary.TotalMutantsSkipped + '`') | Out-Null
    $markdown.Add('- Mutants selected for testing: `' + $LogSummary.TotalMutantsTested + '`') | Out-Null
    $markdown.Add('- Killed: `' + $LogSummary.Killed + '`') | Out-Null
    $markdown.Add('- Survived: `' + $LogSummary.Survived + '`') | Out-Null
    $markdown.Add('- Timeout: `' + $LogSummary.Timeout + '`') | Out-Null
    $markdown.Add('- All mutants tested marker: `' + $LogSummary.AllMutantsTested + '`') | Out-Null
    if ($LogSummary.StatusCounts.Count -gt 0) {
        foreach ($status in $LogSummary.StatusCounts.Keys) {
            $markdown.Add('- Status ' + $status + ': `' + $LogSummary.StatusCounts[$status] + '`') | Out-Null
        }
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Compile-error mutants") | Out-Null
    $markdown.Add("") | Out-Null
    if ($LogSummary.CompileErrorLines.Count -eq 0) {
        $markdown.Add("No compile-error mutant lines were parsed.") | Out-Null
    }
    else {
        foreach ($line in $LogSummary.CompileErrorLines) {
            $scope = if ($LogSummary.OutOfScopeCompileErrorLines -contains $line) { "out-of-requested-scope" } else { "requested-scope" }
            $markdown.Add('- `' + $scope + '`: `' + $line + '`') | Out-Null
        }
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Interpretation") | Out-Null
    $markdown.Add("") | Out-Null
    $requiresJsonReport = @($Reporters | Where-Object { $_ -eq "Json" }).Count -gt 0
    $requiresHtmlReport = @($Reporters | Where-Object { $_ -eq "Html" }).Count -gt 0
    $requiredReportsPresent = ((-not $requiresJsonReport) -or $JsonReports.Count -gt 0) -and ((-not $requiresHtmlReport) -or $HtmlReports.Count -gt 0)
    if ($Result.TimedOut) {
        if ($LogSummary.AllMutantsTested -and (-not $requiredReportsPresent)) {
            $markdown.Add('- Classification remains `BLOCKED_AFTER_REMEDIATION_ATTEMPT`: Stryker tested all selected mutants but did not exit with required reports before the bounded process timeout.') | Out-Null
        }
        else {
            $markdown.Add('- Classification remains `DEFECT`: the bounded mutation run timed out before producing a reliable baseline.') | Out-Null
        }
    }
    elseif (-not $requiredReportsPresent) {
        $markdown.Add('- Classification remains `DEFECT`: the mutation process did not produce both JSON and HTML reports.') | Out-Null
    }
    else {
        $markdown.Add('- Classification is `IMPLEMENTED_NOT_PROVED`: reports were produced, but survivors/no-coverage/compile-error details still need review before enforcing a break threshold.') | Out-Null
    }

    if ($LogSummary.OutOfScopeCompileErrorLines.Count -gt 0) {
        $markdown.Add("- At least one compile-error mutant was outside the requested mutate glob; do not treat the run as a scoped smoke baseline.") | Out-Null
    }

    $markdown | Set-Content -LiteralPath $Path -Encoding UTF8
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'stryker-config.json')
$outputRootPath = Resolve-UnderRoot $repoRoot $OutputRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $outputRootPath $runId
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1") -Quiet | Out-Null

$configPath = Join-Path $repoRoot "stryker-config.json"
$configSnapshotPath = Join-Path $runDirectory "stryker-config.snapshot.json"
Copy-Item -LiteralPath $configPath -Destination $configSnapshotPath -Force
$effectiveConfigRelativePath = "stryker-config.json"
$requestedMutateGlobs = @("configured")
$reporterList = Resolve-Reporters $Reporters
$configuredReporters = @($reporterList | ForEach-Object { $_.ToLowerInvariant() })
$requestedMutateOverride = @($MutateGlobs -split '[,;]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$isolationMode = "RepositorySolution"
$isolatedSolutionPath = $null
$isolationLogPath = $null

if ($Profile -eq "Smoke") {
    $isolationMode = "TemporarySolution"
    $isolatedSolutionDirectory = Join-Path $runDirectory "isolated-solution"
    $isolatedSolutionPath = Join-Path $isolatedSolutionDirectory "NatureProtector.MutationSmoke.sln"
    $isolationLogPath = Join-Path $runDirectory "isolation.log"
    New-Item -ItemType Directory -Force -Path $isolatedSolutionDirectory | Out-Null
    Invoke-CheckedDotnetCommand `
        -Arguments @("new", "sln", "--name", "NatureProtector.MutationSmoke", "--output", $isolatedSolutionDirectory) `
        -WorkingDirectory $repoRoot `
        -LogPath $isolationLogPath
    Invoke-CheckedDotnetCommand `
        -Arguments @(
            "sln",
            $isolatedSolutionPath,
            "add",
            (Join-Path $repoRoot "src\NatureProtector.Prevention\NatureProtector.Prevention.csproj"),
            (Join-Path $repoRoot "tests\NatureProtector.Prevention.Tests\NatureProtector.Prevention.Tests.csproj")
        ) `
        -WorkingDirectory $repoRoot `
        -LogPath $isolationLogPath

    $smokeConfigPath = Join-Path $runDirectory "stryker-smoke-config.json"
    $requestedMutateGlobs = if ($requestedMutateOverride.Count -gt 0) {
        $requestedMutateOverride
    }
    else {
        @("Risk/ReadingTemporalClassifier.cs")
    }
    $smokeConfig = [ordered]@{
        '$schema' = "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.CLI/Stryker.CLI/Resources/stryker.schema.json"
        'stryker-config' = [ordered]@{
            solution = $isolatedSolutionPath
            project = "src/NatureProtector.Prevention/NatureProtector.Prevention.csproj"
            'test-projects' = @("tests/NatureProtector.Prevention.Tests/NatureProtector.Prevention.Tests.csproj")
            mutate = @($requestedMutateGlobs)
            'mutation-level' = $MutationLevel
            reporters = $configuredReporters
            thresholds = [ordered]@{
                high = 80
                low = 60
                break = 0
            }
        }
    }

    $smokeConfig | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $smokeConfigPath -Encoding UTF8
    $effectiveConfigRelativePath = Get-NpPathUnderRoot $repoRoot $smokeConfigPath
}

$stdoutPath = Join-Path $runDirectory "stdout.log"
$stderrPath = Join-Path $runDirectory "stderr.log"
$manifestPath = Join-Path $runDirectory "manifest.json"
$diagnosticsPath = Join-Path $runDirectory "diagnostics.md"

Push-Location $repoRoot
try {
    dotnet tool restore | Out-Null

    $strykerArguments = @(
        "tool", "run", "dotnet-stryker", "--",
        "--config-file", $effectiveConfigRelativePath,
        "--configuration", $Configuration,
        "--output", $runDirectory,
        "--concurrency", [string]$Concurrency,
        "--break-at", "0"
    )
    foreach ($reporter in $reporterList) {
        $strykerArguments += @("--reporter", $reporter)
    }
    $strykerArguments += "--skip-version-check"

    if ($NoRun) {
        $manifest = [pscustomobject]@{
            RunId = $runId
            Profile = $Profile
            Configuration = $Configuration
            MutationLevel = $MutationLevel
            Concurrency = $Concurrency
            TimeoutSeconds = $TimeoutSeconds
            Reporters = $reporterList
            Command = "dotnet " + (($strykerArguments | ForEach-Object { Quote-Argument $_ }) -join " ")
            OutputDirectory = $runDirectory
            IsolationMode = $isolationMode
            IsolatedSolution = $isolatedSolutionPath
            IsolationLog = $isolationLogPath
            RequestedMutateGlobs = $requestedMutateGlobs
            NoRun = $true
            Classification = "IMPLEMENTED_NOT_PROVED"
            Notes = "NoRun only validates wrapper argument construction."
        }

        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        Write-Host "Mutation wrapper dry run written to $runDirectory"
        return
    }

    $result = Invoke-ProcessWithTimeout `
        -FileName "dotnet" `
        -Arguments $strykerArguments `
        -WorkingDirectory $repoRoot `
        -StandardOutputPath $stdoutPath `
        -StandardErrorPath $stderrPath `
        -TimeoutSeconds $TimeoutSeconds

    $jsonReports = @(Get-ChildItem -Path $runDirectory -Recurse -Filter "*.json" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -ne $manifestPath -and
            $_.FullName -ne $configSnapshotPath -and
            $_.Name -notmatch '^stryker-.*config\.json$'
        } |
        Select-Object -ExpandProperty FullName)
    $htmlReports = @(Get-ChildItem -Path $runDirectory -Recurse -Filter "*.html" -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty FullName)

    $logSummary = Get-MutationLogSummary -StdoutPath $stdoutPath -RequestedMutateGlobs $requestedMutateGlobs
    $requiresJsonReport = @($reporterList | Where-Object { $_ -eq "Json" }).Count -gt 0
    $requiresHtmlReport = @($reporterList | Where-Object { $_ -eq "Html" }).Count -gt 0
    $requiredReportsPresent = ((-not $requiresJsonReport) -or $jsonReports.Count -gt 0) -and ((-not $requiresHtmlReport) -or $htmlReports.Count -gt 0)

    $classification = if ($result.TimedOut -and $logSummary.AllMutantsTested -and (-not $requiredReportsPresent)) {
        "BLOCKED_AFTER_REMEDIATION_ATTEMPT"
    }
    elseif ($result.TimedOut) {
        "DEFECT"
    }
    elseif ($result.ExitCode -eq 0 -and $requiredReportsPresent) {
        "IMPLEMENTED_NOT_PROVED"
    }
    else {
        "DEFECT"
    }

    $manifest = [pscustomobject]@{
        RunId = $runId
        Profile = $Profile
        Configuration = $Configuration
        MutationLevel = $MutationLevel
        Concurrency = $Concurrency
        TimeoutSeconds = $TimeoutSeconds
        Reporters = $reporterList
        Command = "dotnet " + (($strykerArguments | ForEach-Object { Quote-Argument $_ }) -join " ")
        OutputDirectory = $runDirectory
        ExitCode = $result.ExitCode
        TimedOut = $result.TimedOut
        StartedAt = $result.StartedAt
        FinishedAt = $result.FinishedAt
        DurationSeconds = $result.DurationSeconds
        JsonReports = $jsonReports
        HtmlReports = $htmlReports
        Classification = $classification
        MutationSummary = $logSummary
        IsolationMode = $isolationMode
        IsolatedSolution = $isolatedSolutionPath
        IsolationLog = $isolationLogPath
        RequestedMutateGlobs = $requestedMutateGlobs
        Diagnostics = $diagnosticsPath
        Notes = "break-at remains 0 until a reliable mutation baseline is classified."
    }

    Write-MutationDiagnostics `
        -Path $diagnosticsPath `
        -RunId $runId `
        -Profile $Profile `
        -Configuration $Configuration `
        -EffectiveConfigRelativePath $effectiveConfigRelativePath `
        -IsolationMode $isolationMode `
        -IsolatedSolutionPath $isolatedSolutionPath `
        -Reporters $reporterList `
        -RequestedMutateGlobs $requestedMutateGlobs `
        -Result $result `
        -JsonReports $jsonReports `
        -HtmlReports $htmlReports `
        -LogSummary $logSummary

    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    Write-Host "Mutation run artifacts: $runDirectory"
    Write-Host "Manifest: $manifestPath"
    Write-Host "Diagnostics: $diagnosticsPath"
    Write-Host "stdout: $stdoutPath"
    Write-Host "stderr: $stderrPath"

    if ($result.ExitCode -ne 0) {
        exit $result.ExitCode
    }
}
finally {
    Pop-Location
}
