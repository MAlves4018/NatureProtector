[CmdletBinding()]
param(
    [ValidateSet(
        "Audit",
        "Hygiene",
        "Setup",
        "Documentation",
        "Coverage",
        "Functional",
        "Integration",
        "Routes",
        "Mutation",
        "Reliability",
        "Security",
        "Creative",
        "FreezePlan",
        "All",
        "Resume",
        "VerifyOnly"
    )]
    [string]$Mode = "Audit",
    [string]$OutputRoot = "",
    [switch]$Enforce
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = if ($env:NP_HARDENING_ROOT) {
        $env:NP_HARDENING_ROOT
    }
    else {
        Join-Path $RepoRoot "artifacts\final-hardening"
    }
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$StatePath = Join-Path $OutputRoot "PHASE_STATE.json"
$CommandLedger = Join-Path $OutputRoot "COMMAND_LEDGER.csv"
$GateResults = Join-Path $OutputRoot "GATE_RESULTS.csv"
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$Commands = [System.Collections.Generic.List[object]]::new()
$Gates = [System.Collections.Generic.List[object]]::new()

function ConvertTo-RelativePath {
    param([Parameter(Mandatory)][string]$Path)
    return [IO.Path]::GetRelativePath($RepoRoot, [IO.Path]::GetFullPath($Path)).Replace("\", "/")
}

function Get-FileSetHash {
    param([string[]]$Paths)
    $hashInput = [System.Text.StringBuilder]::new()
    foreach ($path in @($Paths | Where-Object { $_ } | Sort-Object -Unique)) {
        if (Test-Path -LiteralPath (Join-Path $RepoRoot $path) -PathType Leaf) {
            $fileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $RepoRoot $path)).Hash
            [void]$hashInput.AppendLine("$path=$fileHash")
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($hashInput.ToString())
    $sha = [System.Security.Cryptography.SHA256]::Create()
    return [Convert]::ToHexString($sha.ComputeHash($bytes)).ToLowerInvariant()
}

function New-ReproducibilityFingerprint {
    $trackedFiles = @(git -C $RepoRoot ls-files)
    $lockFiles = @($trackedFiles | Where-Object { $_ -match '(^|/)(package-lock\.json|packages\.lock\.json|global\.json|Directory\.Packages\.props|pyproject\.toml|stryker-config\.json|coverage\.runsettings)$' })
    $workflowFiles = @($trackedFiles | Where-Object { $_ -like ".github/workflows/*" })
    $migrationFiles = @($trackedFiles | Where-Object { $_ -like "src/NatureProtector.Infrastructure.Postgres/Migrations/*" })
    $scriptFiles = @($trackedFiles | Where-Object { $_ -like "scripts/*" -or $_ -like "scripts/*/*" })
    $docFiles = @($trackedFiles | Where-Object { $_ -like "docs/*" -or $_ -like "README.md" })
    $testFiles = @($trackedFiles | Where-Object { $_ -like "tests/*" -or $_ -like "webUI/src/**/*.test.*" })
    $routeFiles = @($trackedFiles | Where-Object { $_ -like "src/NatureProtector.Backoffice.Api/Controllers/*" -or $_ -like "webUI/src/*" })

    return [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        branch = (git -C $RepoRoot branch --show-current)
        commit = (git -C $RepoRoot rev-parse HEAD)
        tree = (git -C $RepoRoot rev-parse "HEAD^{tree}")
        os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        dotnet = (& dotnet --version)
        node = (& node --version)
        npm = (& npm --version)
        python = (& python --version)
        powershell = $PSVersionTable.PSVersion.ToString()
        docker = (& docker --version)
        lockfilesHash = Get-FileSetHash $lockFiles
        migrationInventoryHash = Get-FileSetHash $migrationFiles
        workflowHash = Get-FileSetHash $workflowFiles
        routeInventoryHash = Get-FileSetHash $routeFiles
        documentationInventoryHash = Get-FileSetHash $docFiles
        testInventoryHash = Get-FileSetHash $testFiles
        scriptsHash = Get-FileSetHash $scriptFiles
        coveragePolicyHash = Get-FileSetHash @("coverage.runsettings", "stryker-config.json")
    }
}

function Add-Gate {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Evidence,
        [string]$Limitation = ""
    )
    $Gates.Add([pscustomobject]@{
        gate = $Name
        status = $Status
        evidence = $Evidence
        limitation = $Limitation
    }) | Out-Null
}

function Invoke-HardeningCommand {
    param(
        [string]$Name,
        [string]$Executable,
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 900
    )
    $safeName = $Name -replace '[^a-zA-Z0-9_.-]', '-'
    $logPath = Join-Path $OutputRoot "$safeName.log"
    $started = Get-Date
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Executable
    foreach ($argument in $Arguments) { [void]$psi.ArgumentList.Add($argument) }
    $psi.WorkingDirectory = $RepoRoot
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($psi)
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch {}
        $exitCode = 124
        $stdout = ""
        $stderr = "Timed out after $TimeoutSeconds seconds."
    }
    else {
        $exitCode = [int]$process.ExitCode
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
    }
    $duration = [Math]::Round(((Get-Date) - $started).TotalSeconds, 3)
    @(
        "> $Executable $($Arguments -join ' ')"
        "exitCode=$exitCode"
        "durationSeconds=$duration"
        ""
        $stdout
        $stderr
    ) | Set-Content -LiteralPath $logPath -Encoding utf8
    $Commands.Add([pscustomobject]@{
        name = $Name
        command = "$Executable $($Arguments -join ' ')"
        exitCode = $exitCode
        durationSeconds = $duration
        log = $logPath
    }) | Out-Null
    return [pscustomobject]@{ ExitCode = $exitCode; Log = $logPath }
}

function Invoke-AuditMode {
    $trackedForbidden = @(git -C $RepoRoot ls-files | Where-Object { $_ -match '(^|/)__pycache__/|\.py[co]$|docs/report/LaTeXReport_template\.zip$' })
    $ignoredPythonCache = @(Get-ChildItem -Path $RepoRoot -Recurse -Force -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\.git\\' -and ($_.FullName -match '\\__pycache__\\|\.py[co]$') } |
        Select-Object -First 200 |
        ForEach-Object { ConvertTo-RelativePath $_.FullName })
    $personalPaths = @(rg -n --fixed-strings "C:\Users\Miguel" scripts docs README.md .github 2>$null)

    $trackedForbidden | Set-Content -LiteralPath (Join-Path $OutputRoot "FORBIDDEN_TRACKED_ARTIFACTS.txt") -Encoding utf8
    $ignoredPythonCache | Set-Content -LiteralPath (Join-Path $OutputRoot "IGNORED_PYTHON_CACHE_SAMPLE.txt") -Encoding utf8
    $personalPaths | Set-Content -LiteralPath (Join-Path $OutputRoot "PERSONAL_PATH_REFERENCES.txt") -Encoding utf8

    Add-Gate "forbidden-tracked-artifacts" ($(if ($trackedForbidden.Count -eq 0) { "PASS" } else { "FAIL" })) "FORBIDDEN_TRACKED_ARTIFACTS.txt"
    Add-Gate "ignored-python-cache-detected" "WARN" "IGNORED_PYTHON_CACHE_SAMPLE.txt" "Ignored caches are local hygiene findings, not tracked repository content."
    Add-Gate "personal-path-references" ($(if ($personalPaths.Count -eq 0) { "PASS" } else { "WARN" })) "PERSONAL_PATH_REFERENCES.txt"
}

function Invoke-HygieneMode {
    $diffCheck = Invoke-HardeningCommand "git-diff-check" "git" @("diff", "--check") 120
    Add-Gate "git-diff-check" ($(if ($diffCheck.ExitCode -eq 0) { "PASS" } else { "FAIL" })) $diffCheck.Log

    $scriptParse = Invoke-HardeningCommand "hardening-script-parse" "pwsh" @("-NoProfile", "-Command", "[scriptblock]::Create((Get-Content -Raw 'scripts/hardening/Invoke-NP-FinalHardening.ps1')) | Out-Null") 120
    Add-Gate "hardening-script-parse" ($(if ($scriptParse.ExitCode -eq 0) { "PASS" } else { "FAIL" })) $scriptParse.Log
}

function Invoke-SetupMode {
    $runRoot = Join-Path $OutputRoot "clean-clone-functional"
    $result = Invoke-HardeningCommand "clean-clone-functional-smoke" "pwsh" @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/validation/Invoke-LocalFunctionalValidation.ps1",
        "-CleanRoom",
        "-Smoke",
        "-RunRoot",
        $runRoot
    ) 3600
    Add-Gate "clean-clone-functional-smoke" ($(if ($result.ExitCode -eq 0) { "PASS" } else { "FAIL" })) $result.Log
}

function Invoke-VerifyOnlyMode {
    Invoke-AuditMode
    Invoke-HygieneMode
    $doctor = Invoke-HardeningCommand "np-doctor" "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts/np.ps1", "doctor") 300
    Add-Gate "np-doctor" ($(if ($doctor.ExitCode -eq 0) { "PASS" } else { "FAIL" })) $doctor.Log
}

$fingerprint = New-ReproducibilityFingerprint
$fingerprint | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot "REPRODUCIBILITY_FINGERPRINT.json") -Encoding utf8

switch ($Mode) {
    "Audit" { Invoke-AuditMode }
    "Hygiene" { Invoke-HygieneMode }
    "Setup" { Invoke-SetupMode }
    "VerifyOnly" { Invoke-VerifyOnlyMode }
    "All" {
        Invoke-VerifyOnlyMode
        Invoke-SetupMode
    }
    "Resume" {
        if (-not (Test-Path -LiteralPath $StatePath)) { throw "No PHASE_STATE.json found at $StatePath" }
        Invoke-VerifyOnlyMode
    }
    default {
        Add-Gate $Mode "NOT_IMPLEMENTED" "" "This mode is declared for resumable orchestration and must be backed by the existing authority before it can pass."
        if ($Enforce) { throw "Mode $Mode is not implemented yet." }
    }
}

$Commands | Export-Csv -LiteralPath $CommandLedger -NoTypeInformation -Encoding utf8
$Gates | Export-Csv -LiteralPath $GateResults -NoTypeInformation -Encoding utf8

$state = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    mode = $Mode
    outputRoot = $OutputRoot
    fingerprint = "REPRODUCIBILITY_FINGERPRINT.json"
    gateResults = "GATE_RESULTS.csv"
    commandLedger = "COMMAND_LEDGER.csv"
    pass = -not [bool](@($Gates | Where-Object { $_.status -eq "FAIL" }).Count)
}
$state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $StatePath -Encoding utf8

Get-ChildItem -Path $OutputRoot -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash,$_.Name } |
    Set-Content -LiteralPath (Join-Path $OutputRoot "SHA256SUMS.txt") -Encoding ascii

if ($state.pass) {
    Write-Host "FINAL_HARDENING_ORCHESTRATOR_STATUS=PASS"
    exit 0
}

Write-Host "FINAL_HARDENING_ORCHESTRATOR_STATUS=FAIL"
exit 1
