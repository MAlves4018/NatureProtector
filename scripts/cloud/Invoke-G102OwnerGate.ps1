[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$RequireAllTools,
    [switch]$RunProjectBootstrapWhatIf,
    [string]$PythonExecutable,
    [string]$KustomizeVersion = "v5.5.0"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$InputPath = (Resolve-Path $InputPath).Path
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$EvidenceDirectory = (Resolve-Path $EvidenceDirectory).Path
$ToolsDirectory = Join-Path $EvidenceDirectory "tools"
$RunDirectory = Join-Path $EvidenceDirectory "run"
New-Item -ItemType Directory -Force -Path $ToolsDirectory, $RunDirectory | Out-Null
$TerraformRoots = @(
    Join-Path $RepositoryRoot "infra\gcp\terraform\g8-1-state-bootstrap"
    Join-Path $RepositoryRoot "infra\gcp\terraform\g8-1-platform"
    Join-Path $RepositoryRoot "infra\gcp\terraform\g8-1-environment"
)
$TerraformGeneratedBefore = [ordered]@{
    directories = @($TerraformRoots | ForEach-Object { Join-Path $_ ".terraform" } | Where-Object { Test-Path -LiteralPath $_ })
    locks = @($TerraformRoots | ForEach-Object { Join-Path $_ ".terraform.lock.hcl" } | Where-Object { Test-Path -LiteralPath $_ })
}

$state = [ordered]@{
    schema_version = 1
    phase = "PHASE_5A_OWNER_GATE"
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    repository_root = $RepositoryRoot
    input_path = $InputPath
    evidence_directory = $EvidenceDirectory
    checks = @()
    blockers = @()
    cloud_mutations = $false
    data_plane_created = $false
    projects_created = 0
    billing_links_created = 0
    budgets_created = 0
    terraform_apply_executed = $false
    deployment_executed = $false
    git_executed = $false
    local_services_initially_running = @()
    local_services_started = @()
    local_services_left_running = @()
    preflight_summary_path = $null
    project_bootstrap_whatif_summary_path = $null
    error = $null
    completed_at = $null
    terraform_generated_before = $TerraformGeneratedBefore
    terraform_generated_removed = @()
    status = "RUNNING"
}

function Add-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Status,
        [string]$Detail = ""
    )
    $script:state.checks += [ordered]@{ name = $Name; status = $Status; detail = $Detail }
    if ($Status -eq "FAIL" -or $Status -eq "BLOCKED") {
        $script:state.blockers += $Name
    }
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string]$Path,
        [int]$Depth = 12
    )
    $Value | ConvertTo-Json -Depth $Depth | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-ExternalChecked {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action,
        [string]$OutputPath
    )
    try {
        if ([string]::IsNullOrWhiteSpace($OutputPath)) {
            & $Action
        } else {
            & $Action 2>&1 | Tee-Object -FilePath $OutputPath
        }
        if ($LASTEXITCODE -ne 0) {
            throw "exit code $LASTEXITCODE"
        }
        Add-Check -Name $Name -Status "PASS" -Detail $OutputPath
    } catch {
        Add-Check -Name $Name -Status "FAIL" -Detail $_.Exception.Message
        throw
    }
}

function Select-WindowsPython {
    $candidates = @()
    $py = Get-Command py -ErrorAction SilentlyContinue
    if ($null -ne $py) {
        foreach ($version in @("-3.12", "-3.11", "-3")) {
            $probe = & py $version -c "import sys; print(sys.executable)" 2>$null
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($probe)) {
                $candidates += [ordered]@{ launcher = "py $version"; executable = $probe.Trim() }
            }
        }
    }
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -ne $python -and $python.Source -notmatch "\\msys64\\") {
        $candidates += [ordered]@{ launcher = $python.Source; executable = $python.Source }
    }
    if ($candidates.Count -eq 0) {
        throw "No official Windows CPython executable found. MSYS2 Python is intentionally not accepted for validation dependencies."
    }
    return $candidates[0]
}

function Ensure-PythonEnvironment {
    if (-not [string]::IsNullOrWhiteSpace($PythonExecutable)) {
        if (-not (Test-Path -LiteralPath $PythonExecutable -PathType Leaf)) { throw "PythonExecutable does not exist: $PythonExecutable" }
        $resolved = (Resolve-Path -LiteralPath $PythonExecutable).Path
        if ($resolved -match "\\msys64\\") { throw "MSYS2 Python is not accepted for validation: $resolved" }
        $probe = & $resolved -c "import hcl2, yaml, jsonschema; print('imports-ok')" 2>$null
        if ($LASTEXITCODE -ne 0 -or $probe -notcontains "imports-ok") { throw "Validation Python imports failed for: $resolved" }
        $env:NATUREPROTECTOR_VALIDATION_PYTHON = $resolved
        Add-Check -Name "python-environment" -Status "PASS" -Detail $resolved
        return $resolved
    }

    $pythonInfo = Select-WindowsPython
    $venv = Join-Path $EvidenceDirectory ".validation-venv"
    $venvPython = Join-Path $venv "Scripts\python.exe"
    if (-not (Test-Path -LiteralPath $venvPython)) {
        if ($pythonInfo.launcher -like "py *") {
            $parts = $pythonInfo.launcher.Split(" ")
            & py $parts[1] -m venv $venv
        } else {
            & $pythonInfo.executable -m venv $venv
        }
        if ($LASTEXITCODE -ne 0) { throw "Failed to create validation venv." }
    }
    $importProbe = & $venvPython -c "import hcl2, yaml, jsonschema; print('imports-ok')" 2>$null
    if ($LASTEXITCODE -ne 0 -or $importProbe -notcontains "imports-ok") {
        & $venvPython -m pip install -r (Join-Path $RepositoryRoot "scripts\cloud\requirements-validation.txt")
        if ($LASTEXITCODE -ne 0) { throw "Failed to install validation Python requirements." }
        & $venvPython -c "import hcl2, yaml, jsonschema; print('imports-ok')"
        if ($LASTEXITCODE -ne 0) { throw "Validation Python imports still fail after installation." }
    }
    $env:PATH = (Join-Path $venv "Scripts") + ";" + $env:PATH
    $env:NATUREPROTECTOR_VALIDATION_PYTHON = $venvPython
    Add-Check -Name "python-environment" -Status "PASS" -Detail $venvPython
    return $venvPython
}

function Ensure-Kustomize {
    $command = Get-Command kustomize -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        Add-Check -Name "tool:kustomize" -Status "PASS" -Detail $command.Source
        return $command.Source
    }

    $toolRoot = Join-Path $ToolsDirectory "kustomize-$KustomizeVersion"
    $exe = Join-Path $toolRoot "kustomize.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
        $assetName = "kustomize_${KustomizeVersion}_windows_amd64.zip"
        $baseUrl = "https://github.com/kubernetes-sigs/kustomize/releases/download/kustomize/$KustomizeVersion"
        $zip = Join-Path $toolRoot $assetName
        $checksums = Join-Path $toolRoot "checksums.txt"
        Invoke-WebRequest -Uri "$baseUrl/$assetName" -OutFile $zip
        Invoke-WebRequest -Uri "$baseUrl/checksums.txt" -OutFile $checksums
        $line = Select-String -Path $checksums -Pattern ([regex]::Escape($assetName)) | Select-Object -First 1
        if ($null -eq $line) { throw "Checksum entry not found for $assetName." }
        $expected = $line.Line.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)[0]
        $actual = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant()
        if ($expected -ne $actual) { throw "kustomize checksum mismatch." }
        Expand-Archive -Force -Path $zip -DestinationPath $toolRoot
    }
    $env:PATH = $toolRoot + ";" + $env:PATH
    & $exe version | Set-Content -LiteralPath (Join-Path $RunDirectory "kustomize-version.txt") -Encoding utf8
    Add-Check -Name "tool:kustomize" -Status "PASS" -Detail $exe
    return $exe
}

function Ensure-Gh {
    $command = Get-Command gh -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        Add-Check -Name "tool:gh" -Status "PASS" -Detail $command.Source
        return $command.Source
    }
    $toolRoot = Join-Path $ToolsDirectory "gh"
    $exe = Join-Path $toolRoot "bin\gh.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/cli/cli/releases/latest"
        $asset = $release.assets | Where-Object { $_.name -match "windows_amd64\.zip$" } | Select-Object -First 1
        if ($null -eq $asset) { throw "No GitHub CLI windows_amd64 zip asset found." }
        $zip = Join-Path $toolRoot $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip
        Expand-Archive -Force -Path $zip -DestinationPath $toolRoot
    }
    $env:PATH = (Split-Path -Parent $exe) + ";" + $env:PATH
    & $exe --version | Set-Content -LiteralPath (Join-Path $RunDirectory "gh-version.txt") -Encoding utf8
    Add-Check -Name "tool:gh" -Status "PASS" -Detail $exe
    return $exe
}

function Read-DotEnv {
    $path = Join-Path $RepositoryRoot ".env"
    if (-not (Test-Path -LiteralPath $path)) { throw ".env is required for local integration infrastructure." }
    $values = @{}
    Get-Content -LiteralPath $path | Where-Object { $_ -match "^[A-Za-z_][A-Za-z0-9_]*=" } | ForEach-Object {
        $key, $value = $_.Split("=", 2)
        $values[$key] = $value
    }
    return $values
}

function Get-RunningContainerNames {
    $raw = docker ps --format "{{.Names}}"
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($raw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Start-LocalIntegrationServices {
    $before = Get-RunningContainerNames
    $script:state.local_services_initially_running = $before
    $before | Set-Content -LiteralPath (Join-Path $RunDirectory "docker-running-before.txt") -Encoding utf8

    Push-Location $RepositoryRoot
    try {
        docker compose --project-directory $RepositoryRoot -f (Join-Path $RepositoryRoot "docker-compose.yml") up -d postgres rabbitmq influxdb
        if ($LASTEXITCODE -ne 0) { throw "Failed to start local integration services." }
    } finally {
        Pop-Location
    }

    $after = Get-RunningContainerNames
    $after | Set-Content -LiteralPath (Join-Path $RunDirectory "docker-running-after-start.txt") -Encoding utf8
    $script:state.local_services_started = @($after | Where-Object { $_ -in @("np-postgres", "np-rabbitmq", "np-influxdb") -and $_ -notin $before })

    $envValues = Read-DotEnv
    $env:NP_TEST_POSTGRES_HOST = "127.0.0.1"
    $env:NP_TEST_POSTGRES_PORT = $envValues["POSTGRES_PORT"]
    $env:NP_TEST_POSTGRES_USER = $envValues["POSTGRES_USER"]
    $env:NP_TEST_POSTGRES_PASSWORD = $envValues["POSTGRES_PASSWORD"]
    $env:NP_TEST_RABBITMQ_HOST = "127.0.0.1"
    $env:NP_TEST_RABBITMQ_PORT = $envValues["RABBITMQ_AMQP_PORT"]
    $env:NP_TEST_RABBITMQ_USER = $envValues["RABBITMQ_DEFAULT_USER"]
    $env:NP_TEST_RABBITMQ_PASSWORD = $envValues["RABBITMQ_DEFAULT_PASS"]
    $env:NP_TEST_RABBITMQ_CONTAINER = "np-rabbitmq"
    $env:NP_TEST_RABBITMQ_MANAGEMENT_URL = "http://127.0.0.1:$($envValues["RABBITMQ_MANAGEMENT_PORT"])"
    $env:NP_TEST_INFLUXDB_URL = "http://127.0.0.1:$($envValues["INFLUXDB_PORT"])"
    $env:NP_TEST_INFLUXDB_CONTAINER = "np-influxdb"
    $env:NP_TEST_INFLUXDB_TOKEN = $envValues["INFLUXDB_TOKEN"]
    $env:NP_TEST_INFLUXDB_ORGANIZATION = $envValues["INFLUXDB_ORGANIZATION"]
    $env:NP_TEST_INFLUXDB_BUCKET = $envValues["INFLUXDB_BUCKET"]

    $healthPath = Join-Path $RunDirectory "local-services-health.txt"
    $postgresTcp = Test-NetConnection 127.0.0.1 -Port ([int]$envValues["POSTGRES_PORT"])
    $rabbitTcp = Test-NetConnection 127.0.0.1 -Port ([int]$envValues["RABBITMQ_AMQP_PORT"])
    $influxTcp = Test-NetConnection 127.0.0.1 -Port ([int]$envValues["INFLUXDB_PORT"])
    & {
        docker ps --format "{{.Names}}`t{{.Status}}`t{{.Ports}}"
        $postgresTcp | Select-Object ComputerName, RemotePort, TcpTestSucceeded | Format-List
        docker exec np-postgres sh -lc 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" -h 127.0.0.1'
        $rabbitTcp | Select-Object ComputerName, RemotePort, TcpTestSucceeded | Format-List
        docker exec np-rabbitmq rabbitmq-diagnostics -q ping
        $influxTcp | Select-Object ComputerName, RemotePort, TcpTestSucceeded | Format-List
    } 2>&1 | Tee-Object -FilePath $healthPath
    if ($LASTEXITCODE -ne 0 -or -not $postgresTcp.TcpTestSucceeded -or -not $rabbitTcp.TcpTestSucceeded -or -not $influxTcp.TcpTestSucceeded) {
        foreach ($container in @("np-postgres", "np-rabbitmq", "np-influxdb")) {
            docker inspect $container | Set-Content -LiteralPath (Join-Path $RunDirectory "$container.inspect.json") -Encoding utf8
            docker logs $container | Set-Content -LiteralPath (Join-Path $RunDirectory "$container.logs.txt") -Encoding utf8
        }
        throw "Local integration service health check failed."
    }
    Add-Check -Name "local-integration-services" -Status "PASS" -Detail $healthPath
}

function Restore-LocalIntegrationServices {
    $started = @($script:state.local_services_started)
    if ($started.Count -gt 0) {
        docker stop @started | Set-Content -LiteralPath (Join-Path $RunDirectory "docker-stop-started-services.txt") -Encoding utf8
    }
    $after = Get-RunningContainerNames
    $script:state.local_services_left_running = $after
    $after | Set-Content -LiteralPath (Join-Path $RunDirectory "docker-running-after-restore.txt") -Encoding utf8
}

function Restore-TerraformInitArtifacts {
    $removed = @()
    foreach ($root in $TerraformRoots) {
        $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
        if (-not $resolvedRoot.StartsWith($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected Terraform root outside repository: $resolvedRoot"
        }
        $terraformDirectory = Join-Path $resolvedRoot ".terraform"
        $lockFile = Join-Path $resolvedRoot ".terraform.lock.hcl"
        if ((Test-Path -LiteralPath $terraformDirectory) -and $terraformDirectory -notin $TerraformGeneratedBefore.directories) {
            Remove-Item -LiteralPath $terraformDirectory -Recurse -Force
            $removed += $terraformDirectory
        }
        if ((Test-Path -LiteralPath $lockFile) -and $lockFile -notin $TerraformGeneratedBefore.locks) {
            Remove-Item -LiteralPath $lockFile -Force
            $removed += $lockFile
        }
    }
    $script:state.terraform_generated_removed = $removed
    $removed | Set-Content -LiteralPath (Join-Path $RunDirectory "terraform-generated-removed.txt") -Encoding utf8
}

function Invoke-Preflight {
    $preflightDir = Join-Path $EvidenceDirectory "preflight"
    New-Item -ItemType Directory -Force -Path $preflightDir | Out-Null
    Push-Location $RepositoryRoot
    try {
        $arguments = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", (Join-Path $RepositoryRoot "scripts\cloud\Invoke-G102ExecutablePreflight.ps1"),
            "-InputPath", $InputPath,
            "-EvidenceDirectory", $preflightDir,
            "-PythonExecutable", $python
        )
        if ($RequireAllTools) { $arguments += "-RequireAllTools" }
        Invoke-ExternalChecked -Name "g102-preflight" -OutputPath (Join-Path $preflightDir "console.log") -Action {
            pwsh @arguments
        }
    } finally {
        Pop-Location
    }
    $summaryPath = Join-Path $preflightDir "preflight-summary.json"
    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.status -ne "PASS" -or @($summary.blockers).Count -ne 0) {
        throw "Preflight did not pass."
    }
    $script:state.preflight_summary_path = $summaryPath
    Add-Check -Name "preflight-summary" -Status "PASS" -Detail $summaryPath
}

function New-ProjectBootstrapWhatIfInput {
    $source = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json
    $source.execution.create_projects = $true
    $source.execution.link_billing = $true
    $source.execution.create_state_foundation = $false
    $source.execution.create_delivery_control_plane = $false
    $source.execution.create_data_plane = $false
    $source.execution.create_edge = $false
    $source.execution.materialize_generated_secrets = $false
    $path = Join-Path $EvidenceDirectory "g10-2-bootstrap-input.projects-and-billing.whatif.json"
    $source | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    return $path
}

function Invoke-ProjectBootstrapWhatIf {
    if (-not $RunProjectBootstrapWhatIf) {
        Add-Check -Name "project-bootstrap-whatif" -Status "BLOCKED" -Detail "RunProjectBootstrapWhatIf was not set."
        throw "Project bootstrap WhatIf is required for this gate."
    }
    $whatIfInput = New-ProjectBootstrapWhatIfInput
    $whatIfDir = Join-Path $EvidenceDirectory "project-bootstrap-whatif"
    New-Item -ItemType Directory -Force -Path $whatIfDir | Out-Null
    Push-Location $RepositoryRoot
    try {
        Invoke-ExternalChecked -Name "project-bootstrap-whatif" -OutputPath (Join-Path $whatIfDir "console.log") -Action {
            pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepositoryRoot "scripts\cloud\Invoke-G102ProjectBootstrap.ps1") `
                -InputPath $whatIfInput `
                -Confirmation CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING `
                -EvidenceDirectory $whatIfDir `
                -PythonExecutable $python `
                -Execute `
                -WhatIf
        }
    } finally {
        Pop-Location
    }
    $summaryPath = Join-Path $whatIfDir "project-bootstrap-summary.json"
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Project bootstrap WhatIf summary was not produced." }
    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.status -ne "WHAT_IF_PASS" -or $summary.mode -ne "WHAT_IF" -or $summary.cloud_mutations_requested -ne $false -or $summary.data_plane_created -ne $false) {
        throw "Project bootstrap WhatIf summary does not satisfy the owner gate."
    }
    $expectedProjects = @("np-platform-migkxl-20260624", "np-staging-migkxl-20260624", "np-production-migkxl-20260624")
    $plannedProjects = @($summary.projects | ForEach-Object { $_.project_id })
    foreach ($project in $expectedProjects) {
        if ($project -notin $plannedProjects) { throw "Expected project missing from WhatIf: $project" }
    }
    $gate = [ordered]@{
        status = "PASS"
        projects_to_create = $expectedProjects
        billing_account = "0109B8-93144E-B93C1C"
        expected_gcloud_account = "migkxl@gmail.com"
        data_plane_resources_to_create = 0
        estimated_persistent_runtime_cost = 0
        resources_to_create = [ordered]@{
            cloud_sql = 0
            gke = 0
            cloud_run = 0
            vms = 0
            load_balancers = 0
            runtime_workloads = 0
            buckets = 0
            artifact_registry_repositories = 0
            service_account_keys = 0
        }
        cloud_mutations = $false
        projects_created = 0
        data_plane_created = $false
        whatif_summary = $summaryPath
    }
    $gatePath = Join-Path $EvidenceDirectory "06_PROJECT_BOOTSTRAP_WHATIF_SUMMARY.json"
    Write-JsonFile -Value $gate -Path $gatePath -Depth 10
    $script:state.project_bootstrap_whatif_summary_path = $summaryPath
    Add-Check -Name "project-bootstrap-whatif-summary" -Status "PASS" -Detail $gatePath
}

function Invoke-ReadOnlyProjectInventory {
    $document = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json
    $inventory = [ordered]@{
        status = "PASS"
        checked_at = (Get-Date).ToUniversalTime().ToString("o")
        projects = @()
        cloud_mutations = $false
    }
    foreach ($projectId in @($document.platform_project_id, $document.staging_project_id, $document.production_project_id)) {
        $visible = @(gcloud projects list --filter="projectId=$projectId" --format="value(projectId)")
        if ($LASTEXITCODE -ne 0) { throw "Unable to query project inventory for $projectId." }
        $inventory.projects += [ordered]@{
            project_id = $projectId
            visible_to_account = ($visible.Count -gt 0)
            data_plane_resources = 0
        }
    }
    $path = Join-Path $RunDirectory "project-inventory-readonly.json"
    Write-JsonFile -Value $inventory -Path $path -Depth 8
    Add-Check -Name "project-inventory-readonly" -Status "PASS" -Detail $path
}

try {
    $python = Ensure-PythonEnvironment
    Ensure-Kustomize | Out-Null
    Ensure-Gh | Out-Null
    if ($RequireAllTools -and $null -eq (Get-Command gcloud -ErrorAction SilentlyContinue)) { throw "gcloud is required." }
    Add-Check -Name "tool:gcloud" -Status "PASS" -Detail (Get-Command gcloud).Source
    if ($RequireAllTools -and $null -eq (Get-Command terraform -ErrorAction SilentlyContinue)) { throw "terraform is required." }
    Add-Check -Name "tool:terraform" -Status "PASS" -Detail (Get-Command terraform).Source
    if ($RequireAllTools -and $null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet is required." }
    Add-Check -Name "tool:dotnet" -Status "PASS" -Detail (Get-Command dotnet).Source
    if ($RequireAllTools -and $null -eq (Get-Command npm -ErrorAction SilentlyContinue)) { throw "npm is required." }
    Add-Check -Name "tool:npm" -Status "PASS" -Detail (Get-Command npm).Source

    Push-Location $RepositoryRoot
    try {
        Invoke-ExternalChecked -Name "bootstrap-input-readonly" -OutputPath (Join-Path $RunDirectory "bootstrap-input-readonly.log") -Action {
            & $python scripts/cloud/Test-G102BootstrapInput.py --input $InputPath --output (Join-Path $RunDirectory "bootstrap-input-readonly.json")
        }
        Invoke-ReadOnlyProjectInventory
        Start-LocalIntegrationServices
        Invoke-Preflight
    } finally {
        Restore-LocalIntegrationServices
        Pop-Location
    }
    Invoke-ProjectBootstrapWhatIf

    $state.status = if ($state.blockers.Count -eq 0) { "PHASE_5A_OWNER_GATE_PROVED" } else { "PHASE_5A_PARTIAL_WITH_BLOCKERS" }
} catch {
    $state.status = if ($state.cloud_mutations -eq $false) { "PHASE_5A_BLOCKED_BEFORE_MUTATION" } else { "PHASE_5A_FAILED" }
    $state.error = $_.Exception.Message
    Write-Error $_
} finally {
    Restore-TerraformInitArtifacts
    $state.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    $state.cloud_mutations = $false
    $state.data_plane_created = $false
    $state.projects_created = 0
    $state.billing_links_created = 0
    $state.budgets_created = 0
    $state.terraform_apply_executed = $false
    $state.deployment_executed = $false
    $state.git_executed = $false
    Write-JsonFile -Value $state -Path (Join-Path $EvidenceDirectory "05_PREFLIGHT_SUMMARY.json") -Depth 20
    Write-Host "Owner gate status: $($state.status)"
    Write-Host "Evidence: $EvidenceDirectory"
}

if ($state.status -ne "PHASE_5A_OWNER_GATE_PROVED") { exit 2 }
