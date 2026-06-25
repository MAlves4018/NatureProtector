[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")),
    [switch]$SkipDotNet,
    [switch]$SkipDocker,
    [switch]$SkipTerraform
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Gate([string]$Name, [scriptblock]$Action) {
    Write-Host "`n=== $Name ==="
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

Push-Location $RepositoryRoot
try {
    Invoke-Gate "G8.1 static policy" { python scripts/cloud/Test-G81Static.py }
    Invoke-Gate "G8 regression" { python scripts/cloud/Test-G8Static.py }
    Invoke-Gate "G7 regression" { python scripts/cloud/Test-G7Static.py }

    if (-not $SkipDotNet) {
        Invoke-Gate ".NET restore" { dotnet restore NatureProtector.sln }
        Invoke-Gate ".NET build" { dotnet build NatureProtector.sln -c Release --no-restore }
        Invoke-Gate ".NET tests" { dotnet test NatureProtector.sln -c Release --no-build --logger "trx;LogFileName=g81-owner-gate.trx" }
    }

    Push-Location webUI
    try {
        Invoke-Gate "Frontend clean install" { npm ci }
        Invoke-Gate "Frontend toolchain" { npm run check:toolchain }
        Invoke-Gate "Frontend typecheck" { npm run typecheck }
        Invoke-Gate "Frontend lint" { npm run lint }
        Invoke-Gate "Frontend format" { npm run format:check }
        Invoke-Gate "Frontend tests" { npm test }
        Invoke-Gate "Frontend build" { npm run build }
        Invoke-Gate "Frontend audit policy" { npm run test:audit-script; npm run audit:ci }
    } finally { Pop-Location }

    if (-not $SkipTerraform) {
        foreach ($root in @("infra/gcp/terraform/g8-1-state-bootstrap", "infra/gcp/terraform/g8-1-platform", "infra/gcp/terraform/g8-1-environment")) {
            Invoke-Gate "Terraform fmt $root" { terraform -chdir=$root fmt -check -recursive }
            Invoke-Gate "Terraform init $root" { terraform -chdir=$root init -backend=false -input=false }
            Invoke-Gate "Terraform validate $root" { terraform -chdir=$root validate }
        }
    }

    if (-not $SkipDocker) {
        Invoke-Gate "DockerIntegration" { pwsh ./scripts/ci/Invoke-DockerIntegration.ps1 }
    }

    Write-Host "`nG8_1_OWNER_GATE_PASSED_LOCALLY"
    Write-Host "This does not prove Google Cloud runtime, production authorization, or deployment."
} finally { Pop-Location }
