[CmdletBinding()]
param(
    [ValidateSet('Static', 'Smoke', 'Functional', 'Full')]
    [string]$Profile = 'Static',
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [switch]$PlanOnly,
    [switch]$Overwrite,
    [switch]$StopOnFailure,
    [switch]$SkipBuild,
    [switch]$ExecuteControlledValidationP3,
    [switch]$AcknowledgeNonProduction
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Import-Module (Join-Path $PSScriptRoot 'modules\Acceptance.Common.psm1') -Force -ErrorAction Stop

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot 'config\acceptance\final-acceptance.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot $ConfigPath
}
$ConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Acceptance configuration not found: $ConfigPath"
}

$Config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$profileProperty = $Config.profiles.PSObject.Properties[$Profile]
if ($null -eq $profileProperty) {
    throw "Profile '$Profile' is not present in $ConfigPath."
}
$ProfileDefinition = $profileProperty.Value
$RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
$RunRoot = Resolve-NpAcceptanceOutputRoot -RepoRoot $RepoRoot -OutputRoot $OutputRoot -RunId $RunId -Overwrite:$Overwrite
$ComponentsRoot = Join-Path $RunRoot 'components'
New-Item -ItemType Directory -Force -Path $ComponentsRoot | Out-Null

$StartedAt = (Get-Date).ToUniversalTime()
$StageRows = [System.Collections.Generic.List[object]]::new()
$CommandRows = [System.Collections.Generic.List[object]]::new()
$BlockerRows = [System.Collections.Generic.List[object]]::new()

function Expand-NpAcceptanceTemplate {
    param(
        [AllowEmptyString()][string]$Value,
        [Parameter(Mandatory = $true)][string]$StageOutput,
        [Parameter(Mandatory = $true)][string]$StageEvidence
    )

    return $Value.Replace('{repo}', $RepoRoot).Replace('{runRoot}', $RunRoot).Replace('{componentOutput}', $StageOutput).Replace('{stageEvidence}', $StageEvidence)
}

function Get-NpAcceptanceVersion {
    param([Parameter(Mandatory = $true)][string]$Command)

    $resolved = Get-Command $Command -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return $null }
    try {
        $argument = if ($Command -eq 'pwsh') { '-Version' } else { '--version' }
        $result = & $resolved.Source $argument 2>&1
        return (($result | Out-String).Trim())
    }
    catch {
        return "available; version query failed: $($_.Exception.Message)"
    }
}

function Add-NpAcceptanceStageResult {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode,
        [double]$DurationSeconds,
        [Parameter(Mandatory = $true)][string]$Evidence,
        [Parameter(Mandatory = $true)][string]$Detail,
        [Parameter(Mandatory = $true)][string]$Command
    )

    $StageRows.Add([pscustomobject]@{
        id = $Id
        category = $Category
        status = $Status
        exitCode = $ExitCode
        durationSeconds = $DurationSeconds
        evidence = $Evidence
        detail = $Detail
    }) | Out-Null
    $CommandRows.Add([pscustomobject]@{
        id = $Id
        command = $Command
        exitCode = $ExitCode
        durationSeconds = $DurationSeconds
        evidence = $Evidence
    }) | Out-Null
    if ($Status -in @('FAIL', 'BLOCKED_PREREQUISITE', 'HARNESS_ERROR')) {
        $BlockerRows.Add([pscustomobject]@{
            id = $Id
            status = $Status
            detail = $Detail
            evidence = $Evidence
        }) | Out-Null
    }
}

$environment = [ordered]@{
    runId = $RunId
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    repositoryRoot = $RepoRoot
    operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    powershell = Get-NpAcceptanceVersion -Command 'pwsh'
    python = Get-NpAcceptanceVersion -Command 'python'
    dotnet = Get-NpAcceptanceVersion -Command 'dotnet'
    node = Get-NpAcceptanceVersion -Command 'node'
    npm = Get-NpAcceptanceVersion -Command 'npm'
    docker = Get-NpAcceptanceVersion -Command 'docker'
    gitCommit = $null
    gitBranch = $null
    gitWorkingTreeStatus = @()
    gitSourceClean = $false
    sourceFingerprint = Get-NpAcceptanceSourceFingerprint -Root $RepoRoot
}
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    try { $environment.gitCommit = (& git -C $RepoRoot rev-parse HEAD).Trim() } catch { }
    try { $environment.gitBranch = (& git -C $RepoRoot branch --show-current).Trim() } catch { }
    try { $environment.gitWorkingTreeStatus = @(& git -C $RepoRoot status --porcelain=v1 --untracked-files=all) } catch { }
    $environment.gitSourceClean = @($environment.gitWorkingTreeStatus).Count -eq 0
}
Write-NpAcceptanceJson -Path (Join-Path $RunRoot 'environment.json') -Value $environment

$runSpec = [ordered]@{
    schemaVersion = 1
    runId = $RunId
    profile = $Profile
    profileDescription = [string]$ProfileDefinition.description
    planOnly = [bool]$PlanOnly
    stopOnFailure = [bool]$StopOnFailure
    skipBuild = [bool]$SkipBuild
    executeControlledValidationP3 = [bool]$ExecuteControlledValidationP3
    acknowledgeNonProduction = [bool]$AcknowledgeNonProduction
    p3AuthenticationConfigured = -not [string]::IsNullOrWhiteSpace($env:NP_RELIABILITY_AUTH_TOKEN)
    configPath = $ConfigPath
    outputRoot = $RunRoot
    selectedStages = @($ProfileDefinition.stages)
    startedAtUtc = $StartedAt.ToString('o')
}
Write-NpAcceptanceJson -Path (Join-Path $RunRoot 'run-spec.json') -Value $runSpec

foreach ($stageValue in @($ProfileDefinition.stages)) {
    $stageId = [string]$stageValue
    $definitionProperty = $Config.components.PSObject.Properties[$stageId]
    if ($null -eq $definitionProperty) {
        Add-NpAcceptanceStageResult -Id $stageId -Category 'configuration' -Status 'HARNESS_ERROR' -ExitCode 3 -DurationSeconds 0 -Evidence $ConfigPath -Detail "Stage is selected but has no component definition." -Command ''
        if ($StopOnFailure) { break }
        continue
    }

    $definition = $definitionProperty.Value
    $stageOutput = Join-Path $ComponentsRoot $stageId
    $stageEvidence = Join-Path $stageOutput 'evidence'
    New-Item -ItemType Directory -Force -Path $stageOutput, $stageEvidence | Out-Null
    $executable = [string]$definition.executable
    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @($definition.arguments)) {
        $arguments.Add((Expand-NpAcceptanceTemplate -Value ([string]$argument) -StageOutput $stageOutput -StageEvidence $stageEvidence)) | Out-Null
    }
    if ($SkipBuild -and ($definition.PSObject.Properties.Name -contains 'supportsSkipBuild') -and [bool]$definition.supportsSkipBuild) {
        $arguments.Add('-SkipBuild') | Out-Null
    }
    if ($stageId -eq 'performance-smoke' -and $SkipBuild) {
        $arguments.Add('-NoBuild') | Out-Null
    }
    if (($definition.PSObject.Properties.Name -contains 'requiresControlledValidationExecution') -and [bool]$definition.requiresControlledValidationExecution) {
        if (-not $ExecuteControlledValidationP3 -or -not $AcknowledgeNonProduction) {
            $detail = 'Controlled validation P3 requires both -ExecuteControlledValidationP3 and -AcknowledgeNonProduction.'
            Add-NpAcceptanceStageResult -Id $stageId -Category ([string]$definition.category) -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence $stageOutput -Detail $detail -Command (ConvertTo-NpAcceptanceCommandText -Executable $executable -Arguments @($arguments))
            if ($StopOnFailure) { break }
            continue
        }
        if ([string]::IsNullOrWhiteSpace($env:NP_RELIABILITY_AUTH_TOKEN)) {
            $detail = 'NP_RELIABILITY_AUTH_TOKEN is not configured for controlled validation P3.'
            Add-NpAcceptanceStageResult -Id $stageId -Category ([string]$definition.category) -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence $stageOutput -Detail $detail -Command (ConvertTo-NpAcceptanceCommandText -Executable $executable -Arguments @($arguments))
            if ($StopOnFailure) { break }
            continue
        }
        foreach ($executionArgument in @($definition.controlledValidationExecutionArguments)) {
            $arguments.Add([string]$executionArgument) | Out-Null
        }
    }

    $commandText = ConvertTo-NpAcceptanceCommandText -Executable $executable -Arguments @($arguments)
    if ($PlanOnly) {
        Add-NpAcceptanceStageResult -Id $stageId -Category ([string]$definition.category) -Status 'NOT_SELECTED' -ExitCode 0 -DurationSeconds 0 -Evidence $stageOutput -Detail 'Plan only; command was not executed.' -Command $commandText
        continue
    }

    $requiredCommands = @($definition.requiredCommands | ForEach-Object { [string]$_ })
    $missing = @(Get-NpAcceptanceMissingCommands -Commands $requiredCommands)
    if ($missing.Count -gt 0) {
        $detail = "Missing required commands: $($missing -join ', ')."
        Add-NpAcceptanceStageResult -Id $stageId -Category ([string]$definition.category) -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence $stageOutput -Detail $detail -Command $commandText
        if ($StopOnFailure) { break }
        continue
    }

    $processResult = Invoke-NpAcceptanceProcess `
        -Id $stageId `
        -Executable $executable `
        -Arguments @($arguments) `
        -WorkingDirectory $RepoRoot `
        -OutputDirectory $stageOutput `
        -TimeoutSeconds ([int]$definition.timeoutSeconds)
    $declaredResult = Get-NpAcceptanceDeclaredResult -OutputDirectory $stageOutput
    if ($null -ne $declaredResult) {
        $status = [string]$declaredResult.Status
        $detail = [string]$declaredResult.Detail
        $evidencePath = [string]$declaredResult.Path
        $declaredExitMismatch = ($status -eq 'PASS' -and $processResult.ExitCode -ne 0) -or
            ($status -in @('FAIL', 'BLOCKED_PREREQUISITE', 'HARNESS_ERROR') -and $processResult.ExitCode -eq 0)
        if ($declaredExitMismatch) {
            $status = 'HARNESS_ERROR'
            $detail = "Delegated status and process exit code disagree: declared=$($declaredResult.Status), exit=$($processResult.ExitCode)."
        }
    }
    else {
        $status = Get-NpAcceptanceStageStatus -ExitCode $processResult.ExitCode -TimedOut:$processResult.TimedOut -StartError $processResult.StartError
        $evidencePath = $processResult.LogPath
        $detail = if ($status -eq 'PASS') {
            'Selected stage completed successfully.'
        }
        elseif ($status -eq 'BLOCKED_PREREQUISITE') {
            "Stage reported an unmet prerequisite with exit code $($processResult.ExitCode)."
        }
        elseif ($status -eq 'HARNESS_ERROR') {
            if ($processResult.TimedOut) { "Stage timed out after $([int]$definition.timeoutSeconds) seconds." } else { "Runner could not execute the stage: $($processResult.StartError)" }
        }
        else {
            "Stage failed with exit code $($processResult.ExitCode)."
        }
    }
    Add-NpAcceptanceStageResult -Id $stageId -Category ([string]$definition.category) -Status $status -ExitCode $processResult.ExitCode -DurationSeconds $processResult.DurationSeconds -Evidence $evidencePath -Detail $detail -Command $processResult.Command
    if ($StopOnFailure -and $status -ne 'PASS') { break }
}

$CompletedAt = (Get-Date).ToUniversalTime()
$overall = Get-NpAcceptanceOverallStatus -Rows @($StageRows)
$testsPath = Join-Path $RunRoot 'tests.csv'
$commandsPath = Join-Path $RunRoot 'commands.csv'
$blockersPath = Join-Path $RunRoot 'blockers.csv'
$StageRows | Export-Csv -LiteralPath $testsPath -NoTypeInformation -Encoding utf8
$CommandRows | Export-Csv -LiteralPath $commandsPath -NoTypeInformation -Encoding utf8
if ($BlockerRows.Count -eq 0) {
    @([pscustomobject]@{ id = 'none'; status = 'PASS'; detail = 'No blockers recorded.'; evidence = '' }) |
        Export-Csv -LiteralPath $blockersPath -NoTypeInformation -Encoding utf8
}
else {
    $BlockerRows | Export-Csv -LiteralPath $blockersPath -NoTypeInformation -Encoding utf8
}

$summary = [ordered]@{
    schemaVersion = 1
    runId = $RunId
    profile = $Profile
    status = $overall
    startedAtUtc = $StartedAt.ToString('o')
    completedAtUtc = $CompletedAt.ToString('o')
    durationSeconds = [Math]::Round(($CompletedAt - $StartedAt).TotalSeconds, 3)
    selectedStageCount = @($ProfileDefinition.stages).Count
    executedStageCount = @($StageRows | Where-Object { $_.status -ne 'NOT_SELECTED' }).Count
    passedStageCount = @($StageRows | Where-Object { $_.status -eq 'PASS' }).Count
    failedStageCount = @($StageRows | Where-Object { $_.status -eq 'FAIL' }).Count
    blockedStageCount = @($StageRows | Where-Object { $_.status -eq 'BLOCKED_PREREQUISITE' }).Count
    harnessErrorStageCount = @($StageRows | Where-Object { $_.status -eq 'HARNESS_ERROR' }).Count
    notSelectedStageCount = @($StageRows | Where-Object { $_.status -eq 'NOT_SELECTED' }).Count
    outputRoot = $RunRoot
    stages = @($StageRows)
}
Write-NpAcceptanceJson -Path (Join-Path $RunRoot 'summary.json') -Value $summary

$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryLines.Add('# NatureProtector Final Acceptance')
$summaryLines.Add('')
$summaryLines.Add("- Run: $RunId")
$summaryLines.Add("- Profile: $Profile")
$summaryLines.Add("- Status: $overall")
$summaryLines.Add("- Started: $($StartedAt.ToString('o'))")
$summaryLines.Add("- Completed: $($CompletedAt.ToString('o'))")
$summaryLines.Add("- Output: $RunRoot")
$summaryLines.Add('')
$summaryLines.Add('## Stages')
$summaryLines.Add('')
foreach ($row in $StageRows) {
    $summaryLines.Add("- $($row.id): $($row.status) (exit=$($row.exitCode), duration=$($row.durationSeconds)s)")
}
$summaryLines.Add('')
$summaryLines.Add('Historical evidence is not promoted to current execution evidence. Only stages recorded above contributed to this verdict.')
$summaryLines | Set-Content -LiteralPath (Join-Path $RunRoot 'SUMMARY.md') -Encoding utf8

$manifestPath = Join-Path $RunRoot 'evidence-manifest.csv'
Get-ChildItem -LiteralPath $RunRoot -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        [pscustomobject]@{
            path = [System.IO.Path]::GetRelativePath($RunRoot, $_.FullName).Replace('\', '/')
            sizeBytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    } | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding utf8
Write-NpAcceptanceHashManifest -Root $RunRoot -OutputPath (Join-Path $RunRoot 'hashes.sha256')

Write-Host "NATUREPROTECTOR_FINAL_ACCEPTANCE=$overall"
Write-Host "PROFILE=$Profile"
Write-Host "RUN_ROOT=$RunRoot"
$exitProperty = $Config.exitCodes.PSObject.Properties[$overall]
if ($null -eq $exitProperty) { exit 3 }
exit ([int]$exitProperty.Value)
