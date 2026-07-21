[CmdletBinding()]
param(
    [ValidateSet("Plan", "Verify", "Execute")]
    [string]$Mode = "Plan",
    [string]$OutputRoot = "artifacts/final-freeze",
    [switch]$SkipPackageBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$OutputRoot = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    [IO.Path]::GetFullPath($OutputRoot)
}
else {
    [IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputRoot))
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Invoke-FreezeCommand {
    param(
        [string]$Name,
        [string]$Executable,
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 900
    )
    $safeName = $Name -replace '[^a-zA-Z0-9_.-]', '-'
    $logPath = Join-Path $OutputRoot "$safeName.log"
    $stdoutPath = Join-Path $OutputRoot "$safeName.stdout.tmp"
    $stderrPath = Join-Path $OutputRoot "$safeName.stderr.tmp"
    $started = Get-Date
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $Executable `
        -ArgumentList $Arguments `
        -WorkingDirectory $RepoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru `
        -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch {}
        try { $process.WaitForExit(5000) | Out-Null } catch {}
        $exitCode = 124
        $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
        $stderr = (@(
            "Timed out after $TimeoutSeconds seconds."
            if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine
    }
    else {
        $exitCode = [int]$process.ExitCode
        $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
        $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
    }
    @(
        "> $Executable $($Arguments -join ' ')"
        "exitCode=$exitCode"
        "durationSeconds=$([Math]::Round(((Get-Date) - $started).TotalSeconds, 3))"
        ""
        $stdout
        $stderr
    ) | Set-Content -LiteralPath $logPath -Encoding utf8
    return [pscustomobject]@{ name=$Name; exitCode=$exitCode; log=$logPath }
}

$results = [System.Collections.Generic.List[object]]::new()
$head = (git -C $RepoRoot rev-parse HEAD).Trim()
$branch = (git -C $RepoRoot branch --show-current).Trim()
$status = @(git -C $RepoRoot status --short)
$protectedZip = "docs/report/LaTeXReport_template.zip"
$zipStatus = (git -C $RepoRoot status --short -- $protectedZip)
$zipHash = if (Test-Path -LiteralPath (Join-Path $RepoRoot $protectedZip)) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $RepoRoot $protectedZip)).Hash
}
else {
    $null
}

$preconditions = [ordered]@{
    mode = $Mode
    branch = $branch
    head = $head
    workingTreeStatus = $status
    protectedLatexZipStatus = $zipStatus
    protectedLatexZipSha256 = $zipHash
    executeAllowed = $false
    executeReason = "Execute is intentionally blocked in repository hardening. Run only after merge with explicit owner confirmation."
}
$preconditions | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot "freeze-preconditions.json") -Encoding utf8

if ($Mode -eq "Execute") {
    throw "Freeze Execute is blocked by policy in this mission. Use Plan or Verify only."
}

$results.Add((Invoke-FreezeCommand "git-diff-check" "git" @("diff", "--check") 120)) | Out-Null
$results.Add((Invoke-FreezeCommand "np-hardening-verifyonly" "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts/hardening/Invoke-NP-FinalHardening.ps1", "-Mode", "VerifyOnly", "-OutputRoot", (Join-Path $OutputRoot "hardening-verifyonly")) 900)) | Out-Null

if ($Mode -eq "Verify" -and -not $SkipPackageBuild) {
    $results.Add((Invoke-FreezeCommand "release-candidate-dry-run" "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts/release/build-release-candidate.ps1", "-DryRun", "-OutputRoot", "artifacts/final-freeze/release-candidate") 900)) | Out-Null
}

$results | Export-Csv -LiteralPath (Join-Path $OutputRoot "freeze-command-results.csv") -NoTypeInformation -Encoding utf8
$failed = @($results | Where-Object { $_.exitCode -ne 0 })
$summary = [ordered]@{
    mode = $Mode
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    failedCommands = $failed.Count
    executePrepared = $false
    executeBlockedUntil = "PR merged and explicit owner confirmation."
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot "freeze-summary.json") -Encoding utf8

Get-ChildItem -Path $OutputRoot -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash,$_.Name } |
    Set-Content -LiteralPath (Join-Path $OutputRoot "SHA256SUMS.txt") -Encoding ascii

if ($failed.Count -eq 0) {
    Write-Host "FREEZE_${Mode}_STATUS=PASS"
    exit 0
}

Write-Host "FREEZE_${Mode}_STATUS=FAIL"
exit 1
