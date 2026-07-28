[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [switch]$SkipBuild,
    [switch]$KeepRuntime,
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Import-Module (Join-Path $PSScriptRoot 'modules\Acceptance.Common.psm1') -Force -ErrorAction Stop

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot 'config\acceptance\ui-performance-coverage.json'
}
elseif (-not [IO.Path]::IsPathRooted($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot $ConfigPath
}
$ConfigPath = [IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "UI/performance acceptance configuration not found: $ConfigPath"
}
$Config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

$ArtifactsRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
$RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $ArtifactsRoot "ui-performance-coverage\$RunId"
}
elseif (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot $OutputRoot
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$artifactsPrefix = $ArtifactsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($OutputRoot.Equals($ArtifactsRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not ($OutputRoot + [IO.Path]::DirectorySeparatorChar).StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be a run-scoped child of $ArtifactsRoot"
}
if (Test-Path -LiteralPath $OutputRoot) {
    $existing = @(Get-ChildItem -LiteralPath $OutputRoot -Force -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0 -and -not $Overwrite) {
        throw "OutputRoot exists and is not empty: $OutputRoot. Use -Overwrite for this exact run directory."
    }
    if ($existing.Count -gt 0 -and $Overwrite) { $existing | Remove-Item -Recurse -Force }
}

$Directories = [ordered]@{
    Logs = Join-Path $OutputRoot 'logs'
    UiFixture = Join-Path $OutputRoot 'ui\fixture'
    UiLive = Join-Path $OutputRoot 'ui\live'
    RateLimit = Join-Path $OutputRoot 'rate-limit'
    HttpPerformance = Join-Path $OutputRoot 'performance\http'
    SystemPerformance = Join-Path $OutputRoot 'performance\system'
    Verification = Join-Path $OutputRoot 'verification'
    Shutdown = Join-Path $OutputRoot 'shutdown'
}
(@($OutputRoot) + @($Directories.Values)) | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

$StartedAt = (Get-Date).ToUniversalTime()
$Tests = [System.Collections.Generic.List[object]]::new()
$Commands = [System.Collections.Generic.List[object]]::new()
$Blockers = [System.Collections.Generic.List[object]]::new()
$RuntimeStarted = $false
$HarnessException = $null
$AdminToken = ''

function Get-UiPerfDotEnvValues {
    $values = @{}
    $path = Join-Path $RepoRoot '.env'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $values }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#') -or $line -notmatch '=') { continue }
        $parts = $line -split '=', 2
        $values[$parts[0].Trim()] = $parts[1].Trim().Trim('"').Trim("'")
    }
    return $values
}

$DotEnv = Get-UiPerfDotEnvValues
function Get-UiPerfConfiguredValue {
    param([string]$EnvironmentVariable, [string]$DefaultValue)
    $value = [Environment]::GetEnvironmentVariable($EnvironmentVariable)
    if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
    if ($DotEnv.ContainsKey($EnvironmentVariable) -and -not [string]::IsNullOrWhiteSpace([string]$DotEnv[$EnvironmentVariable])) {
        return [string]$DotEnv[$EnvironmentVariable]
    }
    return $DefaultValue
}

$AdminUsername = Get-UiPerfConfiguredValue -EnvironmentVariable ([string]$Config.runtime.adminUsernameEnvironmentVariable) -DefaultValue ([string]$Config.runtime.defaultAdminUsername)
$AdminPassword = Get-UiPerfConfiguredValue -EnvironmentVariable ([string]$Config.runtime.adminPasswordEnvironmentVariable) -DefaultValue ([string]$Config.runtime.defaultAdminPassword)

function ConvertTo-UiPerfRedactedText {
    param([AllowNull()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $value = $Text
    $value = $value -replace '(?i)(Authorization\s*[:=]\s*Bearer\s+)[A-Za-z0-9._-]+', '${1}<redacted>'
    $value = $value -replace '(?i)("token"\s*:\s*")[^"]+("\s*)', '${1}<redacted>${2}'
    $value = $value -replace '(?i)(password\s*[:=]\s*)[^,\s}]+', '${1}<redacted>'
    return $value
}

function Add-UiPerfTest {
    param(
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'WARN')][string]$Status,
        [Parameter(Mandatory)][string]$Detail,
        [string]$Evidence = ''
    )
    $row = [pscustomobject]@{ area = $Area; name = $Name; status = $Status; detail = (ConvertTo-UiPerfRedactedText $Detail); evidence = $Evidence }
    $Tests.Add($row) | Out-Null
    if ($Status -eq 'FAIL') { $Blockers.Add($row) | Out-Null }
}

function Invoke-UiPerfProcess {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $RepoRoot,
        [hashtable]$Environment = @{},
        [int]$TimeoutSeconds = 900
    )
    $safe = $Id -replace '[^A-Za-z0-9_.-]', '-'
    $stdout = Join-Path $Directories.Logs "$safe.stdout.log"
    $stderr = Join-Path $Directories.Logs "$safe.stderr.log"
    $combined = Join-Path $Directories.Logs "$safe.log"
    $started = (Get-Date).ToUniversalTime()
    $exitCode = 125
    $timedOut = $false
    $saved = @{}
    $commandText = ConvertTo-NpAcceptanceCommandText -Executable $Executable -Arguments $Arguments
    try {
        foreach ($key in $Environment.Keys) {
            $saved[$key] = [Environment]::GetEnvironmentVariable([string]$key, 'Process')
            [Environment]::SetEnvironmentVariable([string]$key, [string]$Environment[$key], 'Process')
        }
        $invocation = New-NpAcceptanceProcessInvocation -Executable $Executable -Arguments $Arguments
        $quoted = @($invocation.Arguments | ForEach-Object { ConvertTo-NpAcceptanceQuotedArgument -Value $_ })
        $process = Start-Process -FilePath $invocation.FilePath -ArgumentList $quoted -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        foreach ($key in $Environment.Keys) { [Environment]::SetEnvironmentVariable([string]$key, $saved[$key], 'Process') }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            try { $process.Kill($true) } catch { }
            try { $process.WaitForExit(5000) | Out-Null } catch { }
            $exitCode = 124
        }
        else { $exitCode = [int]$process.ExitCode }
    }
    catch {
        foreach ($key in $Environment.Keys) { [Environment]::SetEnvironmentVariable([string]$key, $saved[$key], 'Process') }
        $_.Exception.Message | Set-Content -LiteralPath $stderr -Encoding utf8
        $exitCode = 125
    }
    $completed = (Get-Date).ToUniversalTime()
    $stdoutText = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
    $stderrText = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    @(
        "> $(ConvertTo-UiPerfRedactedText $commandText)"
        "exitCode=$exitCode"
        "timedOut=$timedOut"
        ''
        (ConvertTo-UiPerfRedactedText $stdoutText)
        (ConvertTo-UiPerfRedactedText $stderrText)
    ) | Set-Content -LiteralPath $combined -Encoding utf8
    $row = [pscustomobject]@{
        id = $Id
        command = ConvertTo-UiPerfRedactedText $commandText
        exitCode = $exitCode
        timedOut = $timedOut
        durationSeconds = [Math]::Round(($completed - $started).TotalSeconds, 3)
        log = $combined
    }
    $Commands.Add($row) | Out-Null
    return $row
}

function Wait-UiPerfHttp {
    param([Parameter(Mandatory)][string]$Uri, [int]$TimeoutSeconds = 5)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 5
            if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 400) { return $true }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Invoke-UiPerfStartRuntime {
    param([int]$TimeoutSeconds = 420)
    $id = 'np-start'
    $stdout = Join-Path $Directories.Logs "$id.stdout.log"
    $stderr = Join-Path $Directories.Logs "$id.stderr.log"
    $combined = Join-Path $Directories.Logs "$id.log"
    Remove-Item -LiteralPath $stdout, $stderr, $combined -Force -ErrorAction SilentlyContinue
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'start', '-NoBrowser', '-ForceRestart')
    $commandText = ConvertTo-NpAcceptanceCommandText -Executable 'pwsh' -Arguments $arguments
    $started = (Get-Date).ToUniversalTime()
    $process = $null
    $exitCode = 1
    $timedOut = $false
    $note = ''
    try {
        $resolved = Get-Command 'pwsh' -ErrorAction Stop
        $quoted = @($arguments | ForEach-Object { ConvertTo-NpAcceptanceQuotedArgument -Value $_ })
        $process = Start-Process -FilePath $resolved.Source -ArgumentList $quoted -WorkingDirectory $RepoRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            $apiReady = Wait-UiPerfHttp -Uri (([string]$Config.runtime.apiBaseUrl).TrimEnd('/') -replace '/api$', '') + '/health' -TimeoutSeconds 1
            $preventionReady = Wait-UiPerfHttp -Uri 'http://127.0.0.1:5260/health/live' -TimeoutSeconds 1
            $webReady = Wait-UiPerfHttp -Uri ([string]$Config.runtime.webBaseUrl) -TimeoutSeconds 1
            if ($apiReady -and $preventionReady -and $webReady) {
                $exitCode = 0
                if (-not $process.HasExited) {
                    $note = "Runtime became healthy while the np start wrapper was still active; wrapper PID $($process.Id) was stopped after health proof."
                    try { $process.Kill($true) } catch { try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { } }
                }
                break
            }
            if ($process.HasExited -and [int]$process.ExitCode -ne 0) {
                $exitCode = [int]$process.ExitCode
                $note = 'np start exited before all runtime endpoints became healthy.'
                break
            }
            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $deadline)
        if ($exitCode -ne 0 -and $null -ne $process -and -not $process.HasExited) {
            $timedOut = $true
            $exitCode = 124
            $note = "np start did not produce all healthy endpoints within $TimeoutSeconds seconds."
            try { $process.Kill($true) } catch { try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { } }
        }
    }
    catch {
        $note = $_.Exception.Message
        $exitCode = 125
        if ($null -ne $process -and -not $process.HasExited) {
            try { $process.Kill($true) } catch { try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { } }
        }
    }
    finally {
        if ($null -ne $process) { try { $process.Dispose() } catch { } }
    }
    $completed = (Get-Date).ToUniversalTime()
    $stdoutText = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
    $stderrText = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    @(
        "> $(ConvertTo-UiPerfRedactedText $commandText)"
        "exitCode=$exitCode"
        "timedOut=$timedOut"
        "durationSeconds=$([Math]::Round(($completed - $started).TotalSeconds, 3))"
        $note
        ''
        (ConvertTo-UiPerfRedactedText $stdoutText)
        (ConvertTo-UiPerfRedactedText $stderrText)
    ) | Set-Content -LiteralPath $combined -Encoding utf8
    $row = [pscustomobject]@{
        id = $id
        command = ConvertTo-UiPerfRedactedText $commandText
        exitCode = $exitCode
        timedOut = $timedOut
        durationSeconds = [Math]::Round(($completed - $started).TotalSeconds, 3)
        log = $combined
    }
    $Commands.Add($row) | Out-Null
    return $row
}

function Assert-UiPerfProcess {
    param([object]$Result, [string]$Area, [string]$Name, [string]$Evidence)
    $passed = [int]$Result.exitCode -eq 0 -and -not [bool]$Result.timedOut
    Add-UiPerfTest -Area $Area -Name $Name -Status $(if ($passed) { 'PASS' } else { 'FAIL' }) -Detail "exitCode=$($Result.exitCode); timedOut=$($Result.timedOut)" -Evidence $Evidence
    if (-not $passed) { throw "$Name failed. See $($Result.log)" }
}

function Invoke-UiPerfNp {
    param([string]$Id, [string[]]$Arguments, [int]$TimeoutSeconds = 900)
    return Invoke-UiPerfProcess -Id $Id -Executable 'pwsh' -Arguments (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1')) + $Arguments) -TimeoutSeconds $TimeoutSeconds
}

function Write-UiPerfResult {
    param([string]$Status, [string]$NativeStatus, [int]$ExitCode)
    $completed = (Get-Date).ToUniversalTime()
    $summary = [ordered]@{
        schemaVersion = 1
        runId = $RunId
        startedAtUtc = $StartedAt.ToString('o')
        completedAtUtc = $completed.ToString('o')
        durationSeconds = [Math]::Round(($completed - $StartedAt).TotalSeconds, 3)
        status = $Status
        nativeStatus = $NativeStatus
        profile = 'UI_AND_BOUNDED_PERFORMANCE'
        tests = $Tests.Count
        passed = @($Tests | Where-Object status -eq 'PASS').Count
        failed = @($Tests | Where-Object status -eq 'FAIL').Count
        warnings = @($Tests | Where-Object status -eq 'WARN').Count
        claimBoundary = [string]$Config.performance.claimBoundary
    }
    Write-NpAcceptanceJson -Path (Join-Path $OutputRoot 'summary.json') -Value $summary
    Write-NpAcceptanceJson -Path (Join-Path $OutputRoot 'acceptance-result.json') -Value ([ordered]@{ status = $Status; nativeStatus = $NativeStatus; summary = 'summary.json' })
    Write-NpAcceptanceJson -Path (Join-Path $OutputRoot 'run-spec.json') -Value ([ordered]@{ runId = $RunId; configPath = $ConfigPath; skipBuild = [bool]$SkipBuild; keepRuntime = [bool]$KeepRuntime })
    $Tests | Export-Csv -LiteralPath (Join-Path $OutputRoot 'tests.csv') -NoTypeInformation -Encoding utf8
    $Commands | Export-Csv -LiteralPath (Join-Path $OutputRoot 'commands.csv') -NoTypeInformation -Encoding utf8
    $Blockers | Export-Csv -LiteralPath (Join-Path $OutputRoot 'blockers.csv') -NoTypeInformation -Encoding utf8
    @(
        '# UI and bounded performance acceptance'
        ''
        "- Status: **$Status**"
        "- Native status: ``$NativeStatus``"
        "- Tests: $($summary.passed) passed, $($summary.failed) failed, $($summary.warnings) warnings"
        "- Claim boundary: $($summary.claimBoundary)"
        ''
        '## Results'
        ''
        '| Area | Test | Status | Detail |'
        '| --- | --- | --- | --- |'
        @($Tests | ForEach-Object { "| $($_.area) | $($_.name) | $($_.status) | $($_.detail -replace '\|', '\\|') |" })
    ) | Set-Content -LiteralPath (Join-Path $OutputRoot 'SUMMARY.md') -Encoding utf8
    Write-NpAcceptanceHashManifest -Root $OutputRoot -OutputPath (Join-Path $OutputRoot 'hashes.sha256')
    exit $ExitCode
}

$requiredCommands = @('pwsh', 'python', 'dotnet', 'node', 'npm', 'docker')
$missing = @(Get-NpAcceptanceMissingCommands -Commands $requiredCommands)
if ($missing.Count -gt 0) {
    Add-UiPerfTest -Area 'prerequisite' -Name 'required commands' -Status 'FAIL' -Detail "Missing: $($missing -join ', ')"
    Write-UiPerfResult -Status 'BLOCKED_PREREQUISITE' -NativeStatus 'UI_AND_BOUNDED_PERFORMANCE_BLOCKED' -ExitCode 2
}

try {
    if (-not $SkipBuild) {
        $prepare = Invoke-UiPerfNp -Id 'np-prepare-local' -Arguments @('prepare-local') -TimeoutSeconds 1800
        Assert-UiPerfProcess -Result $prepare -Area 'runtime' -Name 'prepare local workspace' -Evidence $prepare.log
    }
    else {
        Add-UiPerfTest -Area 'runtime' -Name 'prepare local workspace' -Status 'WARN' -Detail '-SkipBuild selected; unchanged prepared workspace is assumed.'
    }

    $fixtureEnvironment = @{
        LIVE_RUNTIME = '0'
        UI_REVISION_RUNS = $Directories.UiFixture
        UI_REVISION_SCREENSHOTS = (Join-Path $Directories.UiFixture 'screenshots')
        PLAYWRIGHT_JSON_OUTPUT_NAME = (Join-Path $Directories.UiFixture 'playwright-results.json')
        NP_UI_ACCEPTANCE_CONFIG = $ConfigPath
        NP_UI_SENSITIVE_ACCEPTANCE = '0'
    }
    $fixtureArguments = @('exec', '--', 'playwright', 'test', [string]$Config.playwright.fixtureSpec)
    foreach ($project in @($Config.playwright.fixtureProjects)) { $fixtureArguments += "--project=$project" }
    $fixtureArguments += @('--workers', [string]$Config.playwright.workers, '--reporter=line,json')
    $fixture = Invoke-UiPerfProcess -Id 'playwright-fixture' -Executable 'npm' -Arguments $fixtureArguments -WorkingDirectory (Join-Path $RepoRoot 'webUI') -Environment $fixtureEnvironment -TimeoutSeconds 1200
    Assert-UiPerfProcess -Result $fixture -Area 'ui' -Name 'fixture browser journeys' -Evidence (Join-Path $Directories.UiFixture 'playwright-results.json')

    foreach ($step in @(
        @{ id = 'np-clean-local'; args = @('clean-local'); timeout = 900 },
        @{ id = 'np-up'; args = @('up'); timeout = 900 }
    )) {
        $result = Invoke-UiPerfNp -Id $step.id -Arguments $step.args -TimeoutSeconds $step.timeout
        if ($step.id -eq 'np-up') { $RuntimeStarted = $true }
        Assert-UiPerfProcess -Result $result -Area 'runtime' -Name $step.id -Evidence $result.log
    }
    $startResult = Invoke-UiPerfStartRuntime -TimeoutSeconds ([int]$Config.runtime.serviceStartTimeoutSeconds)
    Assert-UiPerfProcess -Result $startResult -Area 'runtime' -Name 'np-start' -Evidence $startResult.log
    $healthResult = Invoke-UiPerfNp -Id 'np-health' -Arguments @('health') -TimeoutSeconds ([int]$Config.runtime.serviceStartTimeoutSeconds)
    Assert-UiPerfProcess -Result $healthResult -Area 'runtime' -Name 'np-health' -Evidence $healthResult.log

    $liveEnvironment = @{
        LIVE_RUNTIME = '1'
        UI_REVISION_RUNS = $Directories.UiLive
        UI_REVISION_SCREENSHOTS = (Join-Path $Directories.UiLive 'screenshots')
        PLAYWRIGHT_JSON_OUTPUT_NAME = (Join-Path $Directories.UiLive 'playwright-results.json')
        NP_UI_ACCEPTANCE_CONFIG = $ConfigPath
        NP_UI_API_BASE_URL = [string]$Config.runtime.apiBaseUrl
        NP_BOOTSTRAP_ADMIN_USERNAME = $AdminUsername
        NP_BOOTSTRAP_ADMIN_PASSWORD = $AdminPassword
        NP_EVIDENCE_RUN_ID = $RunId
        NP_UI_SENSITIVE_ACCEPTANCE = '1'
    }
    $liveArguments = @('exec', '--', 'playwright', 'test', [string]$Config.playwright.liveSpec, "--project=$($Config.playwright.liveProject)", '--workers', [string]$Config.playwright.workers, '--reporter=line,json')
    $live = Invoke-UiPerfProcess -Id 'playwright-live-roles' -Executable 'npm' -Arguments $liveArguments -WorkingDirectory (Join-Path $RepoRoot 'webUI') -Environment $liveEnvironment -TimeoutSeconds 1800
    Assert-UiPerfProcess -Result $live -Area 'ui' -Name 'live role journeys and accessibility' -Evidence (Join-Path $Directories.UiLive 'playwright-results.json')

    $loginBody = @{ usernameOrEmail = $AdminUsername; password = $AdminPassword } | ConvertTo-Json
    $loginUri = ([string]$Config.runtime.apiBaseUrl).TrimEnd('/') + '/api/users-roles/login'
    $login = Invoke-RestMethod -Method Post -Uri $loginUri -ContentType 'application/json' -Body $loginBody -TimeoutSec 30
    $AdminToken = [string]$login.token
    if ([string]::IsNullOrWhiteSpace($AdminToken)) { throw 'Administrator login returned no token for bounded performance.' }
    Add-UiPerfTest -Area 'performance' -Name 'authenticated performance authority' -Status 'PASS' -Detail 'Administrator token acquired and retained only in process memory.'

    $performanceEnvironment = @{ NP_PERFORMANCE_AUTH_TOKEN = $AdminToken; NP_EVIDENCE_RUN_ID = $RunId }
    foreach ($httpSpec in @($Config.performance.http.profiles)) {
        $profile = [string]$httpSpec.profile
        $output = Join-Path $Directories.HttpPerformance $profile
        $arguments = @('scripts/performance/run-http-workload.py', '--profile', $profile, '--api-base-url', [string]$Config.runtime.apiBaseUrl, '--web-base-url', [string]$Config.runtime.webBaseUrl, '--area-code', [string]$Config.runtime.areaCode, '--output', $output)
        if ([bool]$Config.performance.http.includeWeb) { $arguments += '--include-web' }
        if ([bool]$Config.performance.http.requireAuthentication) { $arguments += '--auth-required' }
        $result = Invoke-UiPerfProcess -Id "http-$profile" -Executable 'python' -Arguments $arguments -Environment $performanceEnvironment -TimeoutSeconds 900
        Assert-UiPerfProcess -Result $result -Area 'performance' -Name "HTTP $profile workload" -Evidence (Join-Path $output 'summary.json')
    }

    $calibrationRoot = Join-Path $Directories.SystemPerformance 'Calibration'
    $calibration = Invoke-UiPerfProcess -Id 'system-Calibration' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'scripts/performance/run-system-capacity-workload.ps1', '-Profile', 'Calibration', '-OutputRoot', $calibrationRoot, '-CollectRuntimeProcessEvidence') -Environment $performanceEnvironment -TimeoutSeconds 1200
    Assert-UiPerfProcess -Result $calibration -Area 'performance' -Name 'system Calibration workload' -Evidence $calibrationRoot
    $calibrationSummary = Get-ChildItem -LiteralPath $calibrationRoot -Recurse -File -Filter 'summary.json' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($null -eq $calibrationSummary) { throw 'Calibration summary was not produced.' }
    $calibrationRunDirectory = Split-Path -Parent $calibrationSummary.FullName

    $b0Root = Join-Path $Directories.SystemPerformance 'B0'
    $b0 = Invoke-UiPerfProcess -Id 'system-B0' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'scripts/performance/run-system-capacity-workload.ps1', '-Profile', 'B0', '-OutputRoot', $b0Root, '-CalibrationRunDirectory', $calibrationRunDirectory, '-CollectRuntimeProcessEvidence') -Environment $performanceEnvironment -TimeoutSeconds 1800
    Assert-UiPerfProcess -Result $b0 -Area 'performance' -Name 'system B0 workload' -Evidence $b0Root

    $rate = Invoke-UiPerfProcess -Id 'rate-limit-contract' -Executable 'python' -Arguments @('scripts/acceptance/verify_rate_limit_contract.py', '--config', $ConfigPath, '--output', $Directories.RateLimit) -TimeoutSeconds 300
    Assert-UiPerfProcess -Result $rate -Area 'rate-limit' -Name 'live authentication limiter' -Evidence (Join-Path $Directories.RateLimit 'rate-limit-result.json')

    $verificationPath = Join-Path $Directories.Verification 'ui-performance-verification.json'
    $verification = Invoke-UiPerfProcess -Id 'verify-ui-performance' -Executable 'python' -Arguments @('scripts/acceptance/verify_ui_performance_coverage.py', '--config', $ConfigPath, '--evidence-root', $OutputRoot, '--output', $verificationPath) -TimeoutSeconds 300
    Assert-UiPerfProcess -Result $verification -Area 'verification' -Name 'closed UI/performance evidence contract' -Evidence $verificationPath
}
catch {
    $HarnessException = $_
    Add-UiPerfTest -Area 'harness' -Name 'campaign execution' -Status 'FAIL' -Detail $_.Exception.Message
}
finally {
    [Environment]::SetEnvironmentVariable('NP_PERFORMANCE_AUTH_TOKEN', $null, 'Process')
    if ($KeepRuntime) {
        Add-UiPerfTest -Area 'shutdown' -Name 'runtime cleanup' -Status 'FAIL' -Detail '-KeepRuntime selected; acceptance cannot pass without clean shutdown.'
    }
    elseif ($RuntimeStarted) {
        foreach ($step in @(
            @{ id = 'np-stop'; args = @('stop') },
            @{ id = 'np-down'; args = @('down') }
        )) {
            $result = Invoke-UiPerfNp -Id $step.id -Arguments $step.args -TimeoutSeconds 600
            Add-UiPerfTest -Area 'shutdown' -Name $step.id -Status $(if ($result.exitCode -eq 0) { 'PASS' } else { 'FAIL' }) -Detail "exitCode=$($result.exitCode)" -Evidence $result.log
        }
        $docker = Invoke-UiPerfProcess -Id 'docker-residual-containers' -Executable 'docker' -Arguments @('ps', '--filter', 'name=np-', '--format', '{{.Names}}') -TimeoutSeconds 60
        $residual = if (Test-Path -LiteralPath $docker.log) { @(Get-Content -LiteralPath $docker.log | Where-Object { $_ -match '^np-' }) } else { @() }
        Add-UiPerfTest -Area 'shutdown' -Name 'no running project containers' -Status $(if ($docker.exitCode -eq 0 -and $residual.Count -eq 0) { 'PASS' } else { 'FAIL' }) -Detail "exitCode=$($docker.exitCode); residual=$($residual -join ',')" -Evidence $docker.log
    }
}

$failed = @($Tests | Where-Object status -eq 'FAIL').Count
if ($failed -gt 0 -or $null -ne $HarnessException) {
    Write-UiPerfResult -Status 'FAIL' -NativeStatus 'UI_AND_BOUNDED_PERFORMANCE_FAIL' -ExitCode 1
}
Write-UiPerfResult -Status 'PASS' -NativeStatus 'UI_AND_BOUNDED_PERFORMANCE_PASS' -ExitCode 0
