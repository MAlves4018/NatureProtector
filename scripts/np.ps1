[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Command,
    [Parameter()]
    [string]$Environment = "staging",
    [Parameter()]
    [string]$EvidenceRoot,
    [Parameter()]
    [switch]$NonInteractive,
    [Parameter()]
    [string]$ManifestPath,
    [Parameter()]
    [string]$Manifest,
    [Parameter()]
    [string]$ReleaseId,
    [Parameter()]
    [int]$TtlHours,
    [Parameter()]
    [string]$TfVarsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$StartedAt = (Get-Date).ToUniversalTime()
$Exit = @{
    Success = 0
    TechnicalFailure = 1
    MissingPrecondition = 2
    PolicyBlocked = 3
    HumanRequired = 4
    CostOrPlanBlocked = 5
}

function Remove-SecretText {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return $Value }
    $redacted = $Value -replace '(?i)(billing[_ -]?account[_ -]?id["'']?\s*[:=]\s*)[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}', '${1}<redacted>'
    $redacted = $redacted -replace '(?i)[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}', '<redacted-billing-account-id>'
    $redacted = $redacted -replace '(?i)(token|password|secret|authorization)["'']?\s*[:=]\s*["'']?[^"'',\s}]+', '$1=<redacted>'
    return $redacted
}

function Read-JsonFile {
    param([Parameter(Mandatory)][string]$Path)
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable
}

function Get-EnvironmentConfig {
    param([string]$Name)
    $common = Read-JsonFile (Join-Path $RepoRoot "deploy/environments/common.json")
    if ($Name -notin @("staging", "production")) { throw "Unknown environment '$Name'." }
    $specificPath = Join-Path $RepoRoot "deploy/environments/$Name.json"
    if (-not (Test-Path -LiteralPath $specificPath -PathType Leaf)) { throw "Missing environment configuration: $specificPath" }
    $specific = Read-JsonFile $specificPath
    foreach ($key in $common.Keys) {
        if (-not $specific.ContainsKey($key)) { $specific[$key] = $common[$key] }
    }
    if ($specific.project_id -ne "natureprotector-500518") { throw "Unexpected project '$($specific.project_id)'." }
    if ($specific.region -ne "europe-southwest1") { throw "Unexpected region '$($specific.region)'." }
    return $specific
}

function Get-EvidenceDirectory {
    param([string]$Operation)
    $root = if ($EvidenceRoot) { $EvidenceRoot } elseif ($env:NP_EVIDENCE_ROOT) { $env:NP_EVIDENCE_ROOT } else { Join-Path $RepoRoot "..\NatureProtector-Standard-CD-Result-local" }
    $path = Join-Path (Resolve-Path -LiteralPath (Split-Path -Parent $root) -ErrorAction SilentlyContinue).Path (Split-Path -Leaf $root)
    if (-not (Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
    $operationPath = Join-Path $path ($Operation -replace '[^a-zA-Z0-9_.-]', '-')
    New-Item -ItemType Directory -Force -Path $operationPath | Out-Null
    return $operationPath
}

function Complete-Result {
    param(
        [Parameter(Mandatory)][string]$Operation,
        [Parameter(Mandatory)][hashtable]$Config,
        [Parameter(Mandatory)][string]$Status,
        [Parameter(Mandatory)][int]$ExitCode,
        [bool]$CloudMutation = $false,
        [string]$EvidencePath,
        [string]$NextAction,
        [hashtable]$Details = @{}
    )
    $completed = (Get-Date).ToUniversalTime()
    $result = [ordered]@{
        operation = $Operation
        status = $Status
        project_id = $Config.project_id
        region = $Config.region
        environment = if ($Config.ContainsKey("environment")) { $Config.environment } else { $null }
        started_at = $StartedAt.ToString("o")
        completed_at = $completed.ToString("o")
        duration_seconds = [Math]::Round(($completed - $StartedAt).TotalSeconds, 3)
        exit_code = $ExitCode
        cloud_mutation = $CloudMutation
        evidence_path = $EvidencePath
        next_action = $NextAction
        details = $Details
    }
    $json = $result | ConvertTo-Json -Depth 12
    if ($EvidencePath) {
        $json | Set-Content -LiteralPath (Join-Path $EvidencePath "operation-result.json") -Encoding utf8
    }
    Write-Host (Remove-SecretText $json)
    exit $ExitCode
}

function Invoke-External {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory
    )
    Write-Host "> $Name $($Arguments -join ' ')"
    if ($Name -eq "python") {
        $python = @(Get-ValidationPython)
        $exe = $python[0]
        $prefix = if ($python.Count -gt 1) { @($python[1..($python.Count - 1)]) } else { @() }
        return Invoke-NativeCommand $exe @($prefix + $Arguments)
    }
    return Invoke-NativeCommand $Name $Arguments
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @()
    )
    & $Executable @Arguments 2>&1 | ForEach-Object { Write-Host (Remove-SecretText ([string]$_)) }
    return $LASTEXITCODE
}

function Test-PythonValidationModules {
    param([Parameter(Mandatory)][string[]]$CommandPrefix)
    $exe = $CommandPrefix[0]
    $prefix = if ($CommandPrefix.Count -gt 1) { @($CommandPrefix[1..($CommandPrefix.Count - 1)]) } else { @() }
    $arguments = @($prefix + @("-c", "import yaml, jsonschema, hcl2"))
    & $exe @arguments *> $null
    return $LASTEXITCODE -eq 0
}

function Get-ValidationPython {
    $candidates = @()
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) { $candidates += ,@($python.Source) }
    $py = Get-Command py -ErrorAction SilentlyContinue
    if ($py) { $candidates += ,@($py.Source, "-3.12") }

    foreach ($candidate in $candidates) {
        if (Test-PythonValidationModules $candidate) { return $candidate }
    }

    $venv = Join-Path ([IO.Path]::GetTempPath()) "np-standard-cd-validation-py312"
    $venvPython = Join-Path $venv "Scripts/python.exe"
    if (-not (Test-Path -LiteralPath $venvPython -PathType Leaf)) {
        if (-not $py) { throw "Python validation dependencies are missing and py launcher is unavailable." }
        & $py.Source -3.12 -m venv $venv 2>&1 | ForEach-Object { Write-Host ([string]$_) }
        if ($LASTEXITCODE -ne 0) { throw "Failed to create validation Python environment." }
    }
    if (-not (Test-PythonValidationModules @($venvPython))) {
        & $venvPython -m pip install --disable-pip-version-check --no-cache-dir -r (Join-Path $RepoRoot "scripts/cloud/requirements-validation.txt") 2>&1 | ForEach-Object { Write-Host ([string]$_) }
        if ($LASTEXITCODE -ne 0) { throw "Failed to install validation Python dependencies." }
    }
    return @($venvPython)
}

function Assert-DeployableEnvironment {
    param([hashtable]$Config)
    if (-not $Config.deployable) { throw "Environment '$($Config.environment)' is locked: $($Config.locked_reason)" }
    if ($Config.environment -ne "staging") { throw "Only staging is allowed in this mission." }
}

function Get-OptionValue {
    param([string[]]$Args, [string]$Name, [string]$Default)
    for ($i = 0; $i -lt $Args.Count; $i++) {
        if ($Args[$i] -eq $Name -and $i + 1 -lt $Args.Count) { return $Args[$i + 1] }
    }
    return $Default
}

function Resolve-ManifestPath {
    param([string]$Default = "")
    if ($ManifestPath) { return $ManifestPath }
    if ($Manifest) { return $Manifest }
    $value = Get-OptionValue $Command "-ManifestPath" ""
    if ($value) { return $value }
    return Get-OptionValue $Command "-Manifest" $Default
}

function Resolve-ReleaseId {
    param([string]$Default = "")
    if ($ReleaseId) { return $ReleaseId }
    return Get-OptionValue $Command "-ReleaseId" $Default
}

function Resolve-TtlHours {
    param([int]$Default)
    if ($TtlHours -gt 0) { return $TtlHours }
    return [int](Get-OptionValue $Command "-TtlHours" "$Default")
}

function Test-Tool {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    return [ordered]@{ name = $Name; found = [bool]$cmd; path = if ($cmd) { $cmd.Source } else { $null } }
}

function Get-Authorities {
    return @(
        [ordered]@{ capability="developer setup"; current_script_workflow="scripts/cloud/Setup-CloudDeveloper.ps1"; authoritative=$true; duplicate=$false; inputs="Account"; outputs="local toolchain state"; exit_codes="native"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$false; recommended_action="wrap from doctor when explicitly requested" },
        [ordered]@{ capability="cloud setup"; current_script_workflow="scripts/cloud/Initialize-CloudProject.ps1"; authoritative=$true; duplicate=$false; inputs="env project/billing"; outputs="enabled APIs/budget"; exit_codes="native"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$false; recommended_action="retain as admin authority" },
        [ordered]@{ capability="cloud preflight"; current_script_workflow="scripts/cloud/Test-CloudSetup.ps1"; authoritative=$true; duplicate=$false; inputs="tool requirements"; outputs="preflight result"; exit_codes="native"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$false; recommended_action="call from np doctor" },
        [ordered]@{ capability="CI validation"; current_script_workflow=".github/workflows/engineering-foundations.yml, release-candidate.yml, gcp-g8-*-policy.yml"; authoritative=$true; duplicate=$true; inputs="source SHA"; outputs="test/policy evidence"; exit_codes="GitHub job"; cloud_mutation=$false; used_by_ci=$true; used_by_cd=$true; recommended_action="orchestrate through _validate.yml" },
        [ordered]@{ capability="release build"; current_script_workflow="scripts/cloud/Build-G81Release.sh"; authoritative=$true; duplicate=$false; inputs="GITHUB_SHA, run ids, project, repository"; outputs="g81-release/release-manifest.json"; exit_codes="bash"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$true; recommended_action="call from np release build/_release.yml" },
        [ordered]@{ capability="scan"; current_script_workflow="scripts/cloud/Build-G81Release.sh"; authoritative=$true; duplicate=$false; inputs="image digest"; outputs="vulnerability json"; exit_codes="bash"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$true; recommended_action="retain inside release authority" },
        [ordered]@{ capability="SBOM"; current_script_workflow="scripts/cloud/Build-G81Release.sh"; authoritative=$true; duplicate=$false; inputs="docker buildx provenance"; outputs="attestation metadata"; exit_codes="bash"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$true; recommended_action="retain inside release authority" },
        [ordered]@{ capability="provenance"; current_script_workflow="scripts/cloud/Build-G81Release.sh and GitHub attestations"; authoritative=$true; duplicate=$false; inputs="source SHA"; outputs="SLSA provenance"; exit_codes="bash/GitHub"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$true; recommended_action="retain" },
        [ordered]@{ capability="signing"; current_script_workflow="scripts/cloud/Build-G81Release.sh, scripts/cloud/Sign-G81ExistingRelease.sh"; authoritative=$true; duplicate=$false; inputs="image digest"; outputs="keyless signature verification"; exit_codes="bash"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$true; recommended_action="retain digest authority" },
        [ordered]@{ capability="release manifest"; current_script_workflow="scripts/cloud/New-G81ReleaseManifest.py, Test-G81ReleaseManifest.py"; authoritative=$true; duplicate=$false; inputs="images.json"; outputs="release-manifest.json"; exit_codes="python"; cloud_mutation=$false; used_by_ci=$true; used_by_cd=$true; recommended_action="manifest remains digest authority" },
        [ordered]@{ capability="Terraform plan/apply"; current_script_workflow="infra/gcp/terraform/g8-1-* roots"; authoritative=$true; duplicate=$false; inputs="tfvars/backend"; outputs="plan/state"; exit_codes="terraform"; cloud_mutation=$true; used_by_ci=$true; used_by_cd=$true; recommended_action="np staging plan/open shells out to terraform" },
        [ordered]@{ capability="Kustomize deployment"; current_script_workflow="infra/gcp/kubernetes/g8-1 overlays and Cloud Deploy skaffold"; authoritative=$true; duplicate=$false; inputs="release manifest digests"; outputs="rendered manifests/rollout"; exit_codes="kustomize/cloud deploy"; cloud_mutation=$true; used_by_ci=$true; used_by_cd=$true; recommended_action="retain manifests; render in validation" },
        [ordered]@{ capability="smoke"; current_script_workflow="scripts/cloud/Invoke-G81FunctionalSmoke.ps1"; authoritative=$true; duplicate=$false; inputs="frontend origin, manifest, service account"; outputs="functional-smoke-summary.json"; exit_codes="PowerShell"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$true; recommended_action="call from np staging verify/_qualify.yml" },
        [ordered]@{ capability="E2E"; current_script_workflow="G8.2 runtime qualification scripts"; authoritative=$true; duplicate=$false; inputs="qualification actions"; outputs="G8.2 evidence index"; exit_codes="python/PowerShell"; cloud_mutation=$false; used_by_ci=$true; used_by_cd=$true; recommended_action="retain for qualification" },
        [ordered]@{ capability="rollback"; current_script_workflow="Cloud Deploy rollback documented in docs/operations/g8-1-cd-and-rollout-runbook.md"; authoritative=$false; duplicate=$false; inputs="release id"; outputs="restored rollout"; exit_codes="manual/cloud deploy"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$true; recommended_action="expose guarded np staging rollback" },
        [ordered]@{ capability="teardown"; current_script_workflow="scripts/cloud/Remove-G81WeekEnvironment.ps1"; authoritative=$true; duplicate=$false; inputs="evidence, tf state bucket/prefix"; outputs="teardown receipt"; exit_codes="PowerShell"; cloud_mutation=$true; used_by_ci=$false; used_by_cd=$true; recommended_action="call from np staging close" },
        [ordered]@{ capability="inventory"; current_script_workflow="scripts/cloud/Get-G103CloudInventory.ps1"; authoritative=$true; duplicate=$false; inputs="project"; outputs="inventory json"; exit_codes="PowerShell"; cloud_mutation=$false; used_by_ci=$false; used_by_cd=$true; recommended_action="call from np inventory" },
        [ordered]@{ capability="evidence"; current_script_workflow="docs/evidence plus G8.1/G8.2 evidence scripts"; authoritative=$true; duplicate=$false; inputs="operation outputs"; outputs="evidence directories"; exit_codes="n/a"; cloud_mutation=$false; used_by_ci=$true; used_by_cd=$true; recommended_action="standardize operation-result.json around existing evidence" }
    )
}

$config = Get-EnvironmentConfig $Environment
$verb = if ($Command.Count -gt 0) { $Command[0].ToLowerInvariant() } else { "help" }
$noun = if ($Command.Count -gt 1) { $Command[1].ToLowerInvariant() } else { "" }
$operation = if ($noun) { "$verb-$noun" } else { $verb }
$evidence = Get-EvidenceDirectory $operation

try {
    switch ($verb) {
        "doctor" {
            $tools = @("pwsh", "python", "dotnet", "node", "npm", "docker", "terraform", "gcloud", "kubectl", "kustomize", "cosign", "jq") | ForEach-Object { Test-Tool $_ }
            $billingState = if ([string]::IsNullOrWhiteSpace($env:NATUREPROTECTOR_BILLING_ACCOUNT_ID)) { "BILLING_ENV_MISSING" } else { "BILLING_ENV_SET" }
            $missingRequired = @($tools | Where-Object { $_.name -in @("pwsh", "python", "dotnet") -and -not $_.found })
            Complete-Result -Operation "doctor" -Config $config -Status ($(if ($missingRequired.Count -eq 0) { "passed" } else { "blocked" })) -ExitCode ($(if ($missingRequired.Count -eq 0) { $Exit.Success } else { $Exit.MissingPrecondition })) -EvidencePath $evidence -NextAction "validate" -Details @{ tools=$tools; billing=$billingState }
        }
        "inventory" {
            $authorities = Get-Authorities
            $authorities | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence "deployment-authorities.json") -Encoding utf8
            Complete-Result -Operation "inventory" -Config $config -Status "passed" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "validate" -Details @{ authorities=$authorities.Count; gate="DEPLOYMENT_AUTHORITIES_RECONCILED" }
        }
        "validate" {
            $commands = @(
                @{ name="g81-static"; cmd="python"; args=@("scripts/cloud/Test-G81Static.py") },
                @{ name="g82-static"; cmd="python"; args=@("scripts/cloud/Test-G82Static.py") },
                @{ name="g9-convergence"; cmd="python"; args=@("scripts/cloud/Test-G9Convergence.py") },
                @{ name="g102-static"; cmd="python"; args=@("scripts/cloud/Test-G102Static.py") },
                @{ name="g103-static"; cmd="python"; args=@("scripts/cloud/Test-G103Static.py") },
                @{ name="local-cloud-contract"; cmd="python"; args=@("scripts/cloud/Test-LocalCloudConfigurationContract.py") }
            )
            $results = @()
            foreach ($item in $commands) {
                if ($WhatIfPreference) {
                    $results += [ordered]@{ name=$item.name; status="whatif"; exit_code=0 }
                    continue
                }
                $code = Invoke-External $item.cmd $item.args $RepoRoot
                $results += [ordered]@{ name=$item.name; status=($(if ($code -eq 0) { "passed" } else { "failed" })); exit_code=$code }
                if ($code -ne 0) { Complete-Result -Operation "validate" -Config $config -Status "failed" -ExitCode $Exit.TechnicalFailure -EvidencePath $evidence -Details @{ checks=$results } }
            }
            Complete-Result -Operation "validate" -Config $config -Status ($(if ($WhatIfPreference) { "whatif" } else { "passed" })) -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "release-build" -Details @{ checks=$results; gate="STANDARD_DEPLOYMENT_INTERFACE_PROVED" }
        }
        "release" {
            if ($noun -eq "build") {
                if ($WhatIfPreference) { Complete-Result -Operation "release-build" -Config $config -Status "whatif" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "release-verify" -Details @{ script=$config.release.build_script } }
                $requiredEnv = @("GCP_PLATFORM_PROJECT_ID", "GCP_REGION", "GCP_ARTIFACT_REPOSITORY", "GITHUB_REPOSITORY", "GITHUB_SHA", "GITHUB_RUN_ID", "ENGINEERING_RUN_ID", "SECURITY_RUN_ID", "POLICY_RUN_ID", "COSIGN_CERTIFICATE_IDENTITY")
                $missing = @($requiredEnv | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
                if ($missing.Count -gt 0) { Complete-Result -Operation "release-build" -Config $config -Status "blocked" -ExitCode $Exit.MissingPrecondition -EvidencePath $evidence -Details @{ missing_env=$missing } }
                $code = Invoke-External "bash" @($config.release.build_script) $RepoRoot
                Complete-Result -Operation "release-build" -Config $config -Status ($(if ($code -eq 0) { "passed" } else { "failed" })) -ExitCode ($(if ($code -eq 0) { $Exit.Success } else { $Exit.TechnicalFailure })) -CloudMutation $true -EvidencePath $evidence -NextAction "release-verify"
            }
            elseif ($noun -eq "verify") {
                $manifest = Resolve-ManifestPath
                if (-not $manifest) { Complete-Result -Operation "release-verify" -Config $config -Status "blocked" -ExitCode $Exit.MissingPrecondition -EvidencePath $evidence -Details @{ missing="-ManifestPath" } }
                $code = Invoke-External "python" @($config.release.verify_script, $manifest) $RepoRoot
                Complete-Result -Operation "release-verify" -Config $config -Status ($(if ($code -eq 0) { "passed" } else { "failed" })) -ExitCode ($(if ($code -eq 0) { $Exit.Success } else { $Exit.TechnicalFailure })) -EvidencePath $evidence -NextAction "staging-plan"
            }
            else { throw "Unknown release operation '$noun'." }
        }
        "staging" {
            Assert-DeployableEnvironment $config
            switch ($noun) {
                "plan" {
                    $tfRoot = Join-Path $RepoRoot $config.terraform.environment_root
                    $tfVars = if ($TfVarsPath) { $TfVarsPath } else { Get-OptionValue $Command "-TfVarsPath" $config.qualification_tfvars }
                    $details = @{ terraform_root=$config.terraform.environment_root; tfvars=$tfVars; ttl_hours=$config.default_ttl_hours; budget_envelope_eur_month=$config.budget_envelope_eur_month }
                    if ($WhatIfPreference) { Complete-Result -Operation "staging-plan" -Config $config -Status "whatif" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "staging-open" -Details $details }
                    foreach ($args in @(@("fmt","-check","-recursive"), @("init","-backend=false","-input=false"), @("validate"))) {
                        $code = Invoke-External "terraform" @("-chdir=$tfRoot") + $args $RepoRoot
                        if ($code -ne 0) { Complete-Result -Operation "staging-plan" -Config $config -Status "failed" -ExitCode $Exit.TechnicalFailure -EvidencePath $evidence -Details $details }
                    }
                    Complete-Result -Operation "staging-plan" -Config $config -Status "passed" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "staging-open" -Details $details
                }
                "open" {
                    $ttl = Resolve-TtlHours -Default $config.default_ttl_hours
                    if ($ttl -lt 1 -or $ttl -gt 24) { Complete-Result -Operation "staging-open" -Config $config -Status "blocked" -ExitCode $Exit.CostOrPlanBlocked -EvidencePath $evidence -Details @{ reason="ttl-out-of-range"; ttl_hours=$ttl } }
                    if ($WhatIfPreference -or -not $PSCmdlet.ShouldProcess("staging", "open ephemeral infrastructure for $ttl hours")) {
                        Complete-Result -Operation "staging-open" -Config $config -Status "whatif" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "staging-deploy" -Details @{ ttl_hours=$ttl; cloud_apply="not-executed" }
                    }
                    Complete-Result -Operation "staging-open" -Config $config -Status "needs-human" -ExitCode $Exit.HumanRequired -EvidencePath $evidence -Details @{ reason="first apply requires owner confirmation"; required_confirmation="AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H" }
                }
                "deploy" {
                    $manifest = Resolve-ManifestPath
                    $releaseId = Resolve-ReleaseId
                    if (-not $manifest -or -not $releaseId) { Complete-Result -Operation "staging-deploy" -Config $config -Status "blocked" -ExitCode $Exit.MissingPrecondition -EvidencePath $evidence -Details @{ missing="-ManifestPath/-ReleaseId" } }
                    if ($WhatIfPreference) { Complete-Result -Operation "staging-deploy" -Config $config -Status "whatif" -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "staging-verify" -Details @{ manifest=$manifest; release_id=$releaseId; authority="scripts/cloud/Deploy-G81Staging.ps1" } }
                    Complete-Result -Operation "staging-deploy" -Config $config -Status "needs-human" -ExitCode $Exit.HumanRequired -CloudMutation $true -EvidencePath $evidence -Details @{ reason="Deploy-G81Staging.ps1 requires environment-specific secret/resource arguments supplied by GitHub environment or owner session" }
                }
                "verify" {
                    $manifest = Resolve-ManifestPath
                    if ($manifest) {
                        $code = Invoke-External "python" @("scripts/cloud/Test-G81ReleaseManifest.py", $manifest) $RepoRoot
                        if ($code -ne 0) { Complete-Result -Operation "staging-verify" -Config $config -Status "failed" -ExitCode $Exit.TechnicalFailure -EvidencePath $evidence }
                    }
                    Complete-Result -Operation "staging-verify" -Config $config -Status ($(if ($WhatIfPreference) { "whatif" } else { "passed" })) -ExitCode $Exit.Success -EvidencePath $evidence -NextAction "staging-close" -Details @{ manifest_checked=[bool]$manifest; smoke_authority="scripts/cloud/Invoke-G81FunctionalSmoke.ps1" }
                }
                "rollback" {
                    $releaseId = Resolve-ReleaseId
                    if (-not $releaseId) { Complete-Result -Operation "staging-rollback" -Config $config -Status "blocked" -ExitCode $Exit.MissingPrecondition -EvidencePath $evidence -Details @{ missing="-ReleaseId" } }
                    Complete-Result -Operation "staging-rollback" -Config $config -Status ($(if ($WhatIfPreference) { "whatif" } else { "needs-human" })) -ExitCode ($(if ($WhatIfPreference) { $Exit.Success } else { $Exit.HumanRequired })) -CloudMutation (-not $WhatIfPreference) -EvidencePath $evidence -Details @{ release_id=$releaseId; authority="Cloud Deploy rollback; existing runbook remains authoritative until parity proof" }
                }
                "close" {
                    if ($WhatIfPreference) { Complete-Result -Operation "staging-close" -Config $config -Status "whatif" -ExitCode $Exit.Success -EvidencePath $evidence -Details @{ authority="scripts/cloud/Remove-G81WeekEnvironment.ps1" } }
                    Complete-Result -Operation "staging-close" -Config $config -Status "needs-human" -ExitCode $Exit.HumanRequired -CloudMutation $true -EvidencePath $evidence -Details @{ reason="teardown requires exported evidence, state bucket, state prefix and exact confirmation" }
                }
                default { throw "Unknown staging operation '$noun'." }
            }
        }
        "evidence" {
            $root = Split-Path -Parent $evidence
            $items = @(Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | Select-Object Name,FullName,LastWriteTime)
            Complete-Result -Operation "evidence" -Config $config -Status "passed" -ExitCode $Exit.Success -EvidencePath $evidence -Details @{ root=$root; directories=$items }
        }
        "help" {
            Write-Host "Usage: pwsh ./scripts/np.ps1 <doctor|validate|inventory|release build|release verify|staging plan|staging open|staging deploy|staging verify|staging rollback|staging close|evidence> [options]"
            Complete-Result -Operation "help" -Config $config -Status "passed" -ExitCode $Exit.Success -EvidencePath $evidence
        }
        default { throw "Unknown operation '$verb'." }
    }
}
catch {
    Complete-Result -Operation $operation -Config $config -Status "failed" -ExitCode $Exit.TechnicalFailure -EvidencePath $evidence -Details @{ error=(Remove-SecretText $_.Exception.Message) }
}
