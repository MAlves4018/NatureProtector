[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Execute', 'FinalizeExisting')]
    [string]$Mode = 'Plan',
    [string]$AcceptanceRunRoot = '',
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [string]$Version = '',
    [switch]$Overwrite,
    [switch]$SkipAcceptanceBuild,
    [switch]$SkipPackageRestore,
    [switch]$SkipFrontendInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Import-Module (Join-Path $RepoRoot 'scripts\acceptance\modules\Acceptance.Common.psm1') -Force -ErrorAction Stop

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot 'config\acceptance\final-delivery.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot $ConfigPath
}
$ConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Final delivery configuration not found: $ConfigPath"
}
$Config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

$RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
$DeliveryBase = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\final-delivery'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $DeliveryBase $RunId
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot $OutputRoot
}
$DeliveryRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$DeliveryPrefix = $DeliveryBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($DeliveryRoot.Equals($DeliveryBase, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not ($DeliveryRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($DeliveryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Final delivery output must be a run-scoped child of: $DeliveryBase"
}
if (Test-Path -LiteralPath $DeliveryRoot) {
    $existing = @(Get-ChildItem -LiteralPath $DeliveryRoot -Force -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0 -and -not $Overwrite) {
        throw "Final delivery output is not empty: $DeliveryRoot. Use -Overwrite for this exact run directory."
    }
    if ($existing.Count -gt 0 -and $Overwrite) {
        $existing | Remove-Item -Recurse -Force
    }
}
New-Item -ItemType Directory -Force -Path $DeliveryRoot | Out-Null

$StartedAt = (Get-Date).ToUniversalTime()
$GateRows = [System.Collections.Generic.List[object]]::new()
$Status = 'HARNESS_ERROR'
$AcceptanceSummary = $null
$ReleaseArchive = $null
$ReleaseChecksum = $null
$SourceIdentity = [ordered]@{
    repositoryRoot = $RepoRoot
    isGitRepository = $false
    clean = $false
    commit = $null
    branch = $null
    detachedHead = $false
    workingTree = @()
    sourceFingerprint = $null
}

function Add-DeliveryGate {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode,
        [double]$DurationSeconds,
        [Parameter(Mandatory = $true)][string]$Evidence,
        [Parameter(Mandatory = $true)][string]$Detail,
        [Parameter(Mandatory = $true)][string]$Command
    )
    $GateRows.Add([pscustomobject]@{
        id = $Id
        status = $Status
        exitCode = $ExitCode
        durationSeconds = $DurationSeconds
        evidence = $Evidence
        detail = $Detail
        command = $Command
    }) | Out-Null
}

function Get-GateStatus {
    param([int]$ExitCode, [switch]$TimedOut, [string]$StartError)
    return Get-NpAcceptanceStageStatus -ExitCode $ExitCode -TimedOut:$TimedOut -StartError $StartError
}

function Invoke-DeliveryProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Executable,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 7200
    )
    $gateRoot = Join-Path $DeliveryRoot (Join-Path 'gates' $Id)
    $result = Invoke-NpAcceptanceProcess -Id $Id -Executable $Executable -Arguments $Arguments -WorkingDirectory $RepoRoot -OutputDirectory $gateRoot -TimeoutSeconds $TimeoutSeconds
    $gateStatus = Get-GateStatus -ExitCode $result.ExitCode -TimedOut:$result.TimedOut -StartError $result.StartError
    $detail = if ($gateStatus -eq 'PASS') { 'Gate completed successfully.' } elseif ($gateStatus -eq 'BLOCKED_PREREQUISITE') { 'Gate reported an unmet prerequisite.' } elseif ($gateStatus -eq 'HARNESS_ERROR') { 'Gate could not be executed safely.' } else { 'Gate violated its contract.' }
    Add-DeliveryGate -Id $Id -Status $gateStatus -ExitCode $result.ExitCode -DurationSeconds $result.DurationSeconds -Evidence $result.LogPath -Detail $detail -Command $result.Command
    return $result
}

try {
    $gitAvailable = $null -ne (Get-Command git -ErrorAction SilentlyContinue)
    if ($gitAvailable -and (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
        $SourceIdentity.isGitRepository = $true
        $SourceIdentity.commit = (& git -C $RepoRoot rev-parse HEAD).Trim()
        $SourceIdentity.branch = (& git -C $RepoRoot branch --show-current).Trim()
        $SourceIdentity.detachedHead = [string]::IsNullOrWhiteSpace([string]$SourceIdentity.branch)
        $SourceIdentity.workingTree = @(& git -C $RepoRoot status --porcelain=v1 --untracked-files=all)
        $SourceIdentity.clean = @($SourceIdentity.workingTree).Count -eq 0
    }
    $SourceIdentity.sourceFingerprint = Get-NpAcceptanceSourceFingerprint -Root $RepoRoot
    Write-NpAcceptanceJson -Path (Join-Path $DeliveryRoot 'source-identity.json') -Value $SourceIdentity

    $requiredCommands = @($Config.requiredCommands | ForEach-Object { [string]$_ })
    $missingCommands = @(Get-NpAcceptanceMissingCommands -Commands $requiredCommands)
    $preflight = [ordered]@{
        mode = $Mode
        requiredCommands = $requiredCommands
        missingCommands = $missingCommands
        reliabilityAuthenticationConfigured = -not [string]::IsNullOrWhiteSpace($env:NP_RELIABILITY_AUTH_TOKEN)
        sourcePolicy = $Config.sourcePolicy
        sourceIdentity = $SourceIdentity
    }
    Write-NpAcceptanceJson -Path (Join-Path $DeliveryRoot 'preflight.json') -Value $preflight

    if ($Mode -eq 'Plan') {
        Add-DeliveryGate -Id 'preflight' -Status 'PLAN_ONLY' -ExitCode 0 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'preflight.json') -Detail 'Plan generated; no acceptance or release command executed.' -Command ''
        $Status = 'PLAN_ONLY'
    }
    else {
        if ($missingCommands.Count -gt 0) {
            Add-DeliveryGate -Id 'preflight' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'preflight.json') -Detail "Missing required commands: $($missingCommands -join ', ')." -Command ''
            $Status = 'BLOCKED_PREREQUISITE'
            throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
        }
        if ([bool]$Config.sourcePolicy.requireGitRepository -and -not [bool]$SourceIdentity.isGitRepository) {
            Add-DeliveryGate -Id 'preflight' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'source-identity.json') -Detail 'Final delivery requires a Git repository so the exact commit can be recorded.' -Command ''
            $Status = 'BLOCKED_PREREQUISITE'
            throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
        }
        if ([bool]$Config.sourcePolicy.requireCleanWorkingTree -and -not [bool]$SourceIdentity.clean) {
            Add-DeliveryGate -Id 'preflight' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'source-identity.json') -Detail 'Final delivery requires a clean working tree.' -Command 'git status --porcelain=v1 --untracked-files=all'
            $Status = 'BLOCKED_PREREQUISITE'
            throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
        }
        if ([string]::IsNullOrWhiteSpace($env:NP_RELIABILITY_AUTH_TOKEN)) {
            Add-DeliveryGate -Id 'preflight' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'preflight.json') -Detail 'NP_RELIABILITY_AUTH_TOKEN is required for the Full acceptance profile.' -Command ''
            $Status = 'BLOCKED_PREREQUISITE'
            throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
        }
        Add-DeliveryGate -Id 'preflight' -Status 'PASS' -ExitCode 0 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'preflight.json') -Detail 'Tools, source identity, clean tree and P3 authentication are present.' -Command ''

        if ($Mode -eq 'Execute') {
            $acceptanceRelative = "artifacts/final-acceptance/final-delivery-$RunId"
            $AcceptanceRunRoot = Join-Path $RepoRoot $acceptanceRelative
            $acceptanceArguments = [System.Collections.Generic.List[string]]::new()
            foreach ($argument in @(
                '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', [string]$Config.acceptance.runner,
                '-Profile', [string]$Config.requiredAcceptanceProfile,
                '-OutputRoot', $acceptanceRelative,
                '-StopOnFailure', '-ExecuteControlledValidationP3', '-AcknowledgeNonProduction'
            )) { $acceptanceArguments.Add($argument) | Out-Null }
            if ($SkipAcceptanceBuild) { $acceptanceArguments.Add('-SkipBuild') | Out-Null }
            $acceptanceResult = Invoke-DeliveryProcess -Id 'acceptance-full' -Executable 'pwsh' -Arguments @($acceptanceArguments) -TimeoutSeconds 28800
            if ($acceptanceResult.ExitCode -ne 0) {
                $Status = Get-GateStatus -ExitCode $acceptanceResult.ExitCode -TimedOut:$acceptanceResult.TimedOut -StartError $acceptanceResult.StartError
                throw [System.InvalidOperationException]::new('DELIVERY_GATE_FAILED')
            }
        }
        else {
            if ([string]::IsNullOrWhiteSpace($AcceptanceRunRoot)) {
                Add-DeliveryGate -Id 'acceptance-full' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence $DeliveryRoot -Detail 'FinalizeExisting requires -AcceptanceRunRoot.' -Command ''
                $Status = 'BLOCKED_PREREQUISITE'
                throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
            }
            if (-not [System.IO.Path]::IsPathRooted($AcceptanceRunRoot)) { $AcceptanceRunRoot = Join-Path $RepoRoot $AcceptanceRunRoot }
            $AcceptanceRunRoot = [System.IO.Path]::GetFullPath($AcceptanceRunRoot)
            $acceptanceBase = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\final-acceptance'))
            $acceptancePrefix = $acceptanceBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
            if ($AcceptanceRunRoot.Equals($acceptanceBase, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not ($AcceptanceRunRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($acceptancePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-DeliveryGate -Id 'acceptance-full' -Status 'BLOCKED_PREREQUISITE' -ExitCode 2 -DurationSeconds 0 -Evidence $AcceptanceRunRoot -Detail 'Existing acceptance evidence must be a run-scoped child of artifacts/final-acceptance.' -Command ''
                $Status = 'BLOCKED_PREREQUISITE'
                throw [System.InvalidOperationException]::new('DELIVERY_BLOCKED')
            }
            Add-DeliveryGate -Id 'acceptance-full' -Status 'PASS' -ExitCode 0 -DurationSeconds 0 -Evidence $AcceptanceRunRoot -Detail 'Existing acceptance campaign selected for strict verification.' -Command ''
        }

        $acceptanceVerificationPath = Join-Path $DeliveryRoot 'acceptance-verification.json'
        $verifyAcceptance = Invoke-DeliveryProcess -Id 'acceptance-evidence-verification' -Executable 'python' -Arguments @(
            [string]$Config.acceptance.verifier,
            $AcceptanceRunRoot,
            '--config', [string]$Config.acceptance.configPath,
            '--result', $acceptanceVerificationPath,
            '--expected-commit', [string]$SourceIdentity.commit,
            '--expected-source-fingerprint', [string]$SourceIdentity.sourceFingerprint
        ) -TimeoutSeconds 900
        if ($verifyAcceptance.ExitCode -ne 0) {
            $Status = 'FAIL'
            throw [System.InvalidOperationException]::new('DELIVERY_GATE_FAILED')
        }
        $AcceptanceSummary = Get-Content -LiteralPath (Join-Path $AcceptanceRunRoot 'summary.json') -Raw | ConvertFrom-Json
        $acceptanceProofRoot = Join-Path $DeliveryRoot 'acceptance-proof'
        New-Item -ItemType Directory -Force -Path $acceptanceProofRoot | Out-Null
        foreach ($proofName in @('environment.json', 'run-spec.json', 'summary.json', 'SUMMARY.md', 'tests.csv', 'commands.csv', 'blockers.csv', 'evidence-manifest.csv', 'hashes.sha256')) {
            $proofSource = Join-Path $AcceptanceRunRoot $proofName
            if (Test-Path -LiteralPath $proofSource -PathType Leaf) {
                Copy-Item -LiteralPath $proofSource -Destination (Join-Path $acceptanceProofRoot $proofName) -Force
            }
        }
        Copy-Item -LiteralPath $acceptanceVerificationPath -Destination (Join-Path $acceptanceProofRoot 'acceptance-verification.json') -Force

        if ([string]::IsNullOrWhiteSpace($Version)) {
            $shortCommit = ([string]$SourceIdentity.commit).Substring(0, [Math]::Min(12, ([string]$SourceIdentity.commit).Length))
            $Version = "final-$($StartedAt.ToString('yyyyMMdd-HHmmss'))-$shortCommit"
        }
        $releaseRelativeRoot = "artifacts/final-delivery/$RunId/release"
        $buildArguments = [System.Collections.Generic.List[string]]::new()
        foreach ($argument in @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', [string]$Config.release.builder, '-Version', $Version, '-OutputRoot', $releaseRelativeRoot)) { $buildArguments.Add($argument) | Out-Null }
        if ($SkipPackageRestore) { $buildArguments.Add('-SkipRestore') | Out-Null }
        if ($SkipFrontendInstall) { $buildArguments.Add('-SkipFrontendInstall') | Out-Null }
        $build = Invoke-DeliveryProcess -Id 'release-candidate-build' -Executable 'pwsh' -Arguments @($buildArguments) -TimeoutSeconds 7200
        if ($build.ExitCode -ne 0) {
            $Status = Get-GateStatus -ExitCode $build.ExitCode -TimedOut:$build.TimedOut -StartError $build.StartError
            throw [System.InvalidOperationException]::new('DELIVERY_GATE_FAILED')
        }
        $ReleaseArchive = Join-Path $DeliveryRoot (Join-Path 'release' "natureprotector-$Version.zip")
        $ReleaseChecksum = "$ReleaseArchive.sha256"
        if (-not (Test-Path -LiteralPath $ReleaseArchive -PathType Leaf) -or -not (Test-Path -LiteralPath $ReleaseChecksum -PathType Leaf)) {
            Add-DeliveryGate -Id 'release-artifact-contract' -Status 'HARNESS_ERROR' -ExitCode 3 -DurationSeconds 0 -Evidence (Join-Path $DeliveryRoot 'release') -Detail 'Release builder did not produce the expected archive and external checksum.' -Command ''
            $Status = 'HARNESS_ERROR'
            throw [System.InvalidOperationException]::new('DELIVERY_GATE_FAILED')
        }

        foreach ($releaseGate in @(
            @{ Id='clean-install'; Script=[string]$Config.release.cleanInstall; Args=@('-ArchivePath', $ReleaseArchive, '-OutputRoot', "artifacts/final-delivery/$RunId/clean-install"); Timeout=1800 },
            @{ Id='tamper-detection'; Script=[string]$Config.release.tamperDetection; Args=@('-ArchivePath', $ReleaseArchive, '-OutputRoot', "artifacts/final-delivery/$RunId/tamper-detection"); Timeout=1800 },
            @{ Id='functional-package-smoke'; Script=[string]$Config.release.functionalSmoke; Args=@('-ArchivePath', $ReleaseArchive, '-OutputRoot', "artifacts/final-delivery/$RunId/functional-package-smoke"); Timeout=3600 }
        )) {
            $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $releaseGate.Script) + @($releaseGate.Args)
            $gate = Invoke-DeliveryProcess -Id $releaseGate.Id -Executable 'pwsh' -Arguments $arguments -TimeoutSeconds ([int]$releaseGate.Timeout)
            if ($gate.ExitCode -ne 0) {
                $Status = Get-GateStatus -ExitCode $gate.ExitCode -TimedOut:$gate.TimedOut -StartError $gate.StartError
                throw [System.InvalidOperationException]::new('DELIVERY_GATE_FAILED')
            }
        }
        $Status = 'PASS'
    }
}
catch {
    if ($_.Exception.Message -notin @('DELIVERY_BLOCKED', 'DELIVERY_GATE_FAILED')) {
        $Status = 'HARNESS_ERROR'
        Add-DeliveryGate -Id 'finalizer' -Status 'HARNESS_ERROR' -ExitCode 3 -DurationSeconds 0 -Evidence $DeliveryRoot -Detail $_.Exception.Message -Command ''
    }
}
finally {
    $CompletedAt = (Get-Date).ToUniversalTime()
    $gatesPath = Join-Path $DeliveryRoot 'delivery-gates.csv'
    $GateRows | Export-Csv -LiteralPath $gatesPath -NoTypeInformation -Encoding utf8
    $releaseSha = if ($ReleaseArchive -and (Test-Path -LiteralPath $ReleaseArchive -PathType Leaf)) { (Get-FileHash -LiteralPath $ReleaseArchive -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    $summary = [ordered]@{
        schemaVersion = 1
        runId = $RunId
        mode = $Mode
        status = $Status
        startedAtUtc = $StartedAt.ToString('o')
        completedAtUtc = $CompletedAt.ToString('o')
        durationSeconds = [Math]::Round(($CompletedAt - $StartedAt).TotalSeconds, 3)
        gitCommit = $SourceIdentity.commit
        gitBranch = $SourceIdentity.branch
        sourceClean = $SourceIdentity.clean
        sourceFingerprint = $SourceIdentity.sourceFingerprint
        acceptanceRoot = if ([string]::IsNullOrWhiteSpace($AcceptanceRunRoot)) { $null } else { $AcceptanceRunRoot }
        acceptanceRunId = if ($null -eq $AcceptanceSummary) { $null } else { $AcceptanceSummary.runId }
        acceptanceProfile = if ($null -eq $AcceptanceSummary) { $null } else { $AcceptanceSummary.profile }
        acceptanceStatus = if ($null -eq $AcceptanceSummary) { $null } else { $AcceptanceSummary.status }
        releaseVersion = if ([string]::IsNullOrWhiteSpace($Version)) { $null } else { $Version }
        acceptanceProof = if (Test-Path -LiteralPath (Join-Path $DeliveryRoot 'acceptance-proof')) { 'acceptance-proof' } else { $null }
        releaseArchive = if ($ReleaseArchive) { [System.IO.Path]::GetRelativePath($DeliveryRoot, $ReleaseArchive).Replace('\', '/') } else { $null }
        releaseArchiveChecksum = if ($ReleaseChecksum) { [System.IO.Path]::GetRelativePath($DeliveryRoot, $ReleaseChecksum).Replace('\', '/') } else { $null }
        releaseArchiveSha256 = $releaseSha
        selectedGateCount = $GateRows.Count
        passedGateCount = @($GateRows | Where-Object { $_.status -eq 'PASS' }).Count
        failedGateCount = @($GateRows | Where-Object { $_.status -in @('FAIL', 'BLOCKED_PREREQUISITE', 'HARNESS_ERROR') }).Count
        gates = @($GateRows)
    }
    Write-NpAcceptanceJson -Path (Join-Path $DeliveryRoot 'final-delivery-summary.json') -Value $summary

    @(
        '# NatureProtector Final Delivery'
        ''
        "- Run: $RunId"
        "- Mode: $Mode"
        "- Status: $Status"
        "- Git commit: $($SourceIdentity.commit)"
        "- Source fingerprint: $($SourceIdentity.sourceFingerprint)"
        "- Acceptance: $($summary.acceptanceProfile) / $($summary.acceptanceStatus)"
        "- Release archive: $ReleaseArchive"
        ''
        'A deliverable PASS is emitted only after a current Full acceptance campaign, strict evidence verification, release build, clean-install verification, tamper detection and functional package smoke all pass from a clean Git source.'
    ) | Set-Content -LiteralPath (Join-Path $DeliveryRoot 'FINAL-DELIVERY.md') -Encoding utf8

    $manifestPath = Join-Path $DeliveryRoot 'delivery-manifest.csv'
    $hashManifestPath = Join-Path $DeliveryRoot 'hashes.sha256'
    $manifestFull = [System.IO.Path]::GetFullPath($manifestPath)
    $hashManifestFull = [System.IO.Path]::GetFullPath($hashManifestPath)
    Get-ChildItem -LiteralPath $DeliveryRoot -Recurse -File |
        Where-Object {
            $fullName = [System.IO.Path]::GetFullPath($_.FullName)
            $fullName -ne $manifestFull -and $fullName -ne $hashManifestFull
        } |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                path = [System.IO.Path]::GetRelativePath($DeliveryRoot, $_.FullName).Replace('\', '/')
                sizeBytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        } | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding utf8
    Write-NpAcceptanceHashManifest -Root $DeliveryRoot -OutputPath $hashManifestPath
}

Write-Host "NATUREPROTECTOR_FINAL_DELIVERY=$Status"
Write-Host "DELIVERY_ROOT=$DeliveryRoot"
$exitProperty = $Config.exitCodes.PSObject.Properties[$Status]
if ($null -eq $exitProperty) { exit 3 }
exit ([int]$exitProperty.Value)
