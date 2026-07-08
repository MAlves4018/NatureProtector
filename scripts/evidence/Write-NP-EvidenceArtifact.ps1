<#
.SYNOPSIS
Shared helper functions for NatureProtector evidence harness scripts.

.DESCRIPTION
Provides path-safe artifact writing, command capture, manifest/hash generation,
secret redaction, and dry-run primitives. It writes only under EvidenceRoot/RunId.

.PARAMETER EvidenceRoot
Root directory that contains evidence runs.

.PARAMETER RunId
Run identifier directory under EvidenceRoot.

.PARAMETER Mode
DryRun validates structure and planned commands. Formal executes permitted commands.

.PARAMETER ContinueOnFailure
Preserve failures and continue when caller policy allows it.

.EXAMPLE
. .\Write-NP-EvidenceArtifact.ps1 -EvidenceRoot C:\evidence -RunId run-1 -Mode DryRun

.EXAMPLE
powershell -File .\Write-NP-EvidenceArtifact.ps1 -EvidenceRoot C:\evidence -RunId run-1 -Mode DryRun

.OUTPUTS
Helper functions only unless invoked directly, where it writes HELPER-SELFTEST.md.

.LIMITATIONS
Does not execute cloud, deploy, purge, destroy, or secret-dependent commands.

.SECURITY
Redacts common secret-bearing tokens before writing logs.
#>
[CmdletBinding()]
param(
    [string]$EvidenceRoot,
    [string]$RunId,
    [ValidateSet('DryRun', 'Formal')]
    [string]$Mode = 'DryRun',
    [switch]$ContinueOnFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NPRedactedText {
    param([AllowNull()][string]$Text)
    if ($null -eq $Text) { return '' }
    $redacted = $Text
    $patterns = @(
        '(?i)(password|passwd|pwd|secret|token|apikey|api_key|authorization|connectionstring)\s*[:=]\s*[^\s,;]+',
        '(?i)Bearer\s+[A-Za-z0-9._~+/=-]+',
        '(?i)(postgres|mongodb|mysql|sqlserver)://[^\s]+'
    )
    foreach ($pattern in $patterns) {
        $redacted = [regex]::Replace($redacted, $pattern, '$1=<redacted>')
    }
    return $redacted
}

function Get-NPEvidenceRunRoot {
    param(
        [Parameter(Mandatory = $true)][string]$EvidenceRoot,
        [Parameter(Mandatory = $true)][string]$RunId
    )
    if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) { throw 'EvidenceRoot is required.' }
    if ([string]::IsNullOrWhiteSpace($RunId)) { throw 'RunId is required.' }
    if ($RunId.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $RunId.Contains('..')) {
        throw "RunId '$RunId' is not a safe directory name."
    }

    $rootFull = [System.IO.Path]::GetFullPath($EvidenceRoot)
    $runFull = [System.IO.Path]::GetFullPath((Join-Path $rootFull $RunId))
    $rootWithSeparator = $rootFull.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $runFull.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved run path '$runFull' is outside EvidenceRoot '$rootFull'."
    }
    return $runFull
}

function Initialize-NPEvidenceRun {
    param(
        [Parameter(Mandatory = $true)][string]$EvidenceRoot,
        [Parameter(Mandatory = $true)][string]$RunId
    )
    $runRoot = Get-NPEvidenceRunRoot -EvidenceRoot $EvidenceRoot -RunId $RunId
    $dirs = @(
        '00-baseline', '01-build-test', '02-coverage', '03-data-schema',
        '04-runtime', '05-api', '06-scenarios', '07-ui', '08-observability',
        '09-manual', '10-summaries', 'logs', 'artifacts'
    )
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    foreach ($dir in $dirs) {
        New-Item -ItemType Directory -Path (Join-Path $runRoot $dir) -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath (Join-Path $runRoot 'COMMANDS-RUN.md'))) {
        "# Commands Run`n" | Set-Content -LiteralPath (Join-Path $runRoot 'COMMANDS-RUN.md') -Encoding UTF8
    }
    return $runRoot
}

function Assert-NPEvidencePath {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Path
    )
    $runFull = [System.IO.Path]::GetFullPath($RunRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (-not ($pathFull + [System.IO.Path]::DirectorySeparatorChar).StartsWith($runFull, [StringComparison]::OrdinalIgnoreCase) -and
        -not $pathFull.StartsWith($runFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside run root. Path='$pathFull' RunRoot='$RunRoot'."
    }
}

function Write-NPEvidenceFile {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [AllowNull()][string]$Content = '',
        [switch]$NoRedact
    )
    $path = Join-Path $RunRoot $RelativePath
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $path
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    $value = if ($NoRedact) { $Content } else { Get-NPRedactedText -Text $Content }
    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
    return $path
}

function Add-NPEvidenceCommandRecord {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Command,
        [string]$Status = 'PLANNED',
        [string]$Log = ''
    )
    $path = Join-Path $RunRoot 'COMMANDS-RUN.md'
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $path
    $line = "- status=$Status command=`$Command` log=$Log"
    Add-Content -LiteralPath $path -Value (Get-NPRedactedText -Text $line) -Encoding UTF8
}

function Resolve-NPEvidenceExecutablePath {
    param(
        [Parameter(Mandatory=$true)]
        [string]$FilePath
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        return $FilePath
    }

    if (Test-Path -LiteralPath $FilePath) {
        return (Resolve-Path -LiteralPath $FilePath).Path
    }

    $hasPathSeparator = $FilePath.Contains('\') -or $FilePath.Contains('/')

    if (-not $hasPathSeparator) {
        $command = Get-Command $FilePath -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) {
            if ($command.Source) {
                return $command.Source
            }

            if ($command.Path) {
                return $command.Path
            }
        }
    }

    return $FilePath
}
function Join-NPProcessArguments {
    param(
        [string[]]$Arguments = @()
    )

    $quoted = foreach ($arg in @($Arguments)) {
        if ($null -eq $arg) {
            continue
        }

        $text = [string]$arg

        if ($text.Length -eq 0) {
            '""'
            continue
        }

        if ($text -notmatch '[\s"]') {
            $text
            continue
        }

        '"' + ($text -replace '"', '\"') + '"'
    }

    return ($quoted -join ' ')
}
function Invoke-NPEvidenceCommand {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = (Get-Location).Path,
        [ValidateSet('DryRun', 'Formal')]
        [string]$Mode = 'DryRun',
        [switch]$ContinueOnFailure
    )
    $safeName = $Name -replace '[^A-Za-z0-9_.-]', '-'
    $stdoutPath = Join-Path $RunRoot "logs/$safeName.stdout.log"
    $stderrPath = Join-Path $RunRoot "logs/$safeName.stderr.log"
    $exitPath = Join-Path $RunRoot "logs/$safeName.exit-code.txt"
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $stdoutPath
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $stderrPath
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $exitPath
    $display = ($FilePath + ' ' + ($Arguments -join ' ')).Trim()

    if ($Mode -eq 'DryRun') {
        Set-Content -LiteralPath $stdoutPath -Value "DRY_RUN_PLANNED: $display" -Encoding UTF8
        Set-Content -LiteralPath $stderrPath -Value '' -Encoding UTF8
        Set-Content -LiteralPath $exitPath -Value '0' -Encoding UTF8
        Add-NPEvidenceCommandRecord -RunRoot $RunRoot -Command $display -Status 'DRY_RUN_PLANNED' -Log "logs/$safeName.stdout.log"
        return [pscustomobject]@{ Name = $Name; ExitCode = 0; Status = 'DRY_RUN_PLANNED'; Stdout = $stdoutPath; Stderr = $stderrPath }
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = Resolve-NPEvidenceExecutablePath -FilePath $FilePath
    $argumentListProperty = $startInfo.GetType().GetProperty('ArgumentList')
    if ($null -ne $argumentListProperty) {
        $argumentList = $argumentListProperty.GetValue($startInfo, $null)
        foreach ($arg in $Arguments) {
            [void]$argumentList.Add([string]$arg)
        }
    }
    else {
        $startInfo.Arguments = Join-NPProcessArguments -Arguments $Arguments
    }
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    Set-Content -LiteralPath $stdoutPath -Value (Get-NPRedactedText -Text $stdout) -Encoding UTF8
    Set-Content -LiteralPath $stderrPath -Value (Get-NPRedactedText -Text $stderr) -Encoding UTF8
    Set-Content -LiteralPath $exitPath -Value ([string]$process.ExitCode) -Encoding UTF8
    $status = if ($process.ExitCode -eq 0) { 'PASS' } else { 'FAIL_WITH_EVIDENCE' }
    Add-NPEvidenceCommandRecord -RunRoot $RunRoot -Command $display -Status $status -Log "logs/$safeName.stdout.log"
    if ($process.ExitCode -ne 0 -and -not $ContinueOnFailure) {
        throw "Command '$display' failed with exit code $($process.ExitCode)."
    }
    return [pscustomobject]@{ Name = $Name; ExitCode = $process.ExitCode; Status = $status; Stdout = $stdoutPath; Stderr = $stderrPath }
}

function Write-NPEvidenceManifest {
    param([Parameter(Mandatory = $true)][string]$RunRoot)
    $manifest = Join-Path $RunRoot 'MANIFEST.csv'
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $manifest
    Get-ChildItem -LiteralPath $RunRoot -Recurse -File |
        Where-Object { $_.FullName -ne $manifest } |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                relative_path = $_.FullName.Substring($RunRoot.Length).TrimStart('\', '/')
                bytes = $_.Length
                last_write_utc = $_.LastWriteTimeUtc.ToString('o')
            }
        } | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $manifest -Encoding UTF8
    return $manifest
}

function Write-NPEvidenceHashes {
    param([Parameter(Mandatory = $true)][string]$RunRoot)
    $hashPath = Join-Path $RunRoot 'SHA256SUMS.txt'
    Assert-NPEvidencePath -RunRoot $RunRoot -Path $hashPath
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        Get-ChildItem -LiteralPath $RunRoot -Recurse -File |
            Where-Object { $_.FullName -ne $hashPath } |
            Sort-Object FullName |
            ForEach-Object {
                $stream = [System.IO.File]::Open($_.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                try {
                    $hashBytes = $sha256.ComputeHash($stream)
                } finally {
                    $stream.Dispose()
                }

                $hash = -join ($hashBytes | ForEach-Object { $_.ToString('x2') })
                "$hash  $($_.FullName.Substring($RunRoot.Length).TrimStart('\', '/').Replace('\','/'))"
            } | Set-Content -LiteralPath $hashPath -Encoding UTF8
    } finally {
        $sha256.Dispose()
    }
    return $hashPath
}

function Write-NPEvidenceLedgerDefaults {
    param([Parameter(Mandatory = $true)][string]$RunRoot)
    Write-NPEvidenceFile -RunRoot $RunRoot -RelativePath 'EVIDENCE-LEDGER.csv' -Content "artifact,status,claim_effect,caveat`nharness,DryRun,NO_PROMOTION,EVC-00 does not collect formal evidence." | Out-Null
    Write-NPEvidenceFile -RunRoot $RunRoot -RelativePath 'CLAIM-UPGRADE-MATRIX.csv' -Content "claim_id,current_status,evidence_artifact,proposed_decision,human_decision_required`nALL,UNCHANGED,EVC-00 harness,NO_AUTOMATIC_PROMOTION,true" | Out-Null
    Write-NPEvidenceFile -RunRoot $RunRoot -RelativePath 'REPORT-IMPACT.md' -Content "# Report Impact`n`nEVC-00 creates automation harness only. No report claim is promoted." | Out-Null
    Write-NPEvidenceFile -RunRoot $RunRoot -RelativePath 'ACCEPTED-GAPS.csv' -Content "gap,status,reason`nformal_evidence_not_collected,accepted,EVC-00 is dry-run harness preparation." | Out-Null
}

if ($MyInvocation.InvocationName -ne '.') {
    $runRoot = Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
    Write-NPEvidenceFile -RunRoot $runRoot -RelativePath 'artifacts/HELPER-SELFTEST.md' -Content "# Helper self-test`n`nMode: $Mode`nContinueOnFailure: $($ContinueOnFailure.IsPresent)" | Out-Null
    Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
    Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
}


