<#
.SYNOPSIS
Compresses an evidence run into a zip archive.
.DESCRIPTION
Creates a zip under the run artifacts directory. DryRun writes the compression plan only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Compress-NP-Evidence.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Compress-NP-Evidence.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
artifacts/<RunId>.zip in Formal mode.
.LIMITATIONS
No external compressor dependency.
.SECURITY
Compresses only files under EvidenceRoot/RunId.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
$zipPath=Join-Path $runRoot "artifacts/$RunId.zip"
if ($Mode -eq 'DryRun') {
    Write-NPEvidenceFile -RunRoot $runRoot -RelativePath 'artifacts/COMPRESSION-PLAN.md' -Content "# Compression Plan`n`nWould create: artifacts/$RunId.zip" | Out-Null
} else {
    Assert-NPEvidencePath -RunRoot $runRoot -Path $zipPath
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $tempParent = Join-Path ([System.IO.Path]::GetTempPath()) ("np-evidence-compress-" + [Guid]::NewGuid().ToString("N"))
    $stageRoot = Join-Path $tempParent "stage"
    $tempZip = Join-Path $tempParent "$RunId.zip"

    try {
        New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

        Get-ChildItem -LiteralPath $runRoot -Recurse -File |
            Where-Object { $_.Extension -ne '.zip' } |
            ForEach-Object {
                $relativePath = $_.FullName.Substring($runRoot.Length).TrimStart('\', '/')
                $targetPath = Join-Path $stageRoot $relativePath
                New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
            }

        [System.IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $tempZip)

        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        Move-Item -LiteralPath $tempZip -Destination $zipPath -Force
        Write-NPEvidenceFile -RunRoot $runRoot -RelativePath 'artifacts/COMPRESSION-SUMMARY.md' -Content "# Compression Summary`n`nCreated: artifacts/$RunId.zip`nTemporary workspace removed after compression." | Out-Null
    }
    finally {
        if (Test-Path -LiteralPath $tempParent) {
            Remove-Item -LiteralPath $tempParent -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
