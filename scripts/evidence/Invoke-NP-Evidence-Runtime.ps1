<#
.SYNOPSIS
Plans or runs local runtime evidence.
.DESCRIPTION
DryRun records runtime startup plan. Formal requires readiness and starts local runtime only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Runtime.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Runtime.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
04-runtime summaries and logs.
.LIMITATIONS
No cloud/deploy/production commands.
.SECURITY
Redacts logs and never prints secret values intentionally.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '04-runtime/RUNTIME-SUMMARY.md' -Content "# Runtime`n`nMode: $Mode`nFormal mode may invoke local runtime after readiness gate only." | Out-Null
$launcherScriptPath = Join-Path $RepoRoot 'scripts/dev/start-local-runtime.ps1'
$launcherArguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$launcherScriptPath,'-ForceRestart','-ScenarioCode','scenario_b','-SensorCount','2','-NumberOfCycles','1','-IntervalSeconds','1','-Seed','20260706','-DegradationProfile','none','-SimulatorTimeoutSeconds','240')

if ($Mode -eq 'DryRun') {
    Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'local-runtime-plan' -FilePath 'pwsh' -Arguments $launcherArguments -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
} else {
    $stdoutPath = Join-Path $runRoot 'logs/local-runtime-plan.stdout.log'
    $stderrPath = Join-Path $runRoot 'logs/local-runtime-plan.stderr.log'
    $exitPath = Join-Path $runRoot 'logs/local-runtime-plan.exit-code.txt'
    $beforeLaunchUtc = [DateTime]::UtcNow
    $process = Start-Process -FilePath 'pwsh' -ArgumentList $launcherArguments -WorkingDirectory $RepoRoot -WindowStyle Hidden -PassThru
    $devRuntimeRoot = Join-Path $RepoRoot 'docs/evidence/dev-runtime'
    $latestDevRuntime = $null
    $apiReady = $false
    $preventionReady = $false
    $webReady = $false
    $deadline = (Get-Date).AddSeconds(300)

    while ((Get-Date) -lt $deadline) {
        $latestDevRuntime = Get-ChildItem -LiteralPath $devRuntimeRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $beforeLaunchUtc.AddMinutes(-1) -and (Test-Path -LiteralPath (Join-Path $_.FullName 'launcher-summary.md')) } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        try {
            $apiReady = ((Invoke-WebRequest -UseBasicParsing 'http://localhost:5254/health/ready' -TimeoutSec 5).StatusCode -eq 200)
        } catch {
            $apiReady = $false
        }

        try {
            $preventionReady = ((Invoke-WebRequest -UseBasicParsing 'http://localhost:5260/health/live' -TimeoutSec 5).StatusCode -eq 200)
        } catch {
            $preventionReady = $false
        }

        try {
            $webReady = ((Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:5173' -TimeoutSec 5).StatusCode -eq 200)
        } catch {
            $webReady = $false
        }

        if ($latestDevRuntime -and $apiReady -and $preventionReady -and $webReady) {
            break
        }

        Start-Sleep -Seconds 2
    }

    $exitCode = if ($latestDevRuntime -and $apiReady -and $preventionReady -and $webReady) { 0 } else { 1 }
    Set-Content -LiteralPath $exitPath -Value ([string]$exitCode) -Encoding UTF8
    $stdoutLines = @(
        "Detached launcher process id: $($process.Id)"
        "Launcher command: pwsh $($launcherArguments -join ' ')"
        "Readiness result: API=$apiReady Prevention=$preventionReady Web=$webReady"
    )
    if ($latestDevRuntime) {
        $stdoutLines += "Latest dev runtime root: $($latestDevRuntime.FullName)"
        $summaryPath = Join-Path $latestDevRuntime.FullName 'launcher-summary.md'
        if (Test-Path -LiteralPath $summaryPath) {
            $stdoutLines += ''
            $stdoutLines += Get-Content -LiteralPath $summaryPath
        }
    } else {
        $stdoutLines += 'Latest dev runtime root: NOT_FOUND_AFTER_SEARCH'
    }
    Set-Content -LiteralPath $stdoutPath -Value $stdoutLines -Encoding UTF8
    if (-not (Test-Path -LiteralPath $stderrPath)) {
        Set-Content -LiteralPath $stderrPath -Value '' -Encoding UTF8
    }
    Add-NPEvidenceCommandRecord -RunRoot $runRoot -Command ('pwsh ' + ($launcherArguments -join ' ')) -Status "EXIT_CODE_$exitCode" -Log 'logs/local-runtime-plan.stdout.log'
    if ($exitCode -ne 0 -and -not $ContinueOnFailure) {
        throw "local-runtime-plan failed with exit code $exitCode."
    }
}
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
