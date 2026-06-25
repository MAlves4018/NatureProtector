[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Bucket,
  [Parameter(Mandatory)][string]$QualificationId,
  [Parameter(Mandatory)][string]$BundlePath,
  [Parameter(Mandatory)][string]$EvidenceIndexPath,
  [Parameter(Mandatory)][string]$QualificationSummaryPath,
  [Parameter(Mandatory)][string]$PreArchiveVerdictPath,
  [Parameter(Mandatory)][string]$CandidateManifestPath,
  [Parameter(Mandatory)][string]$OutputPath,
  [Parameter(Mandatory)][ValidateSet('ARCHIVE_G82_PREQUALIFICATION_EVIDENCE')][string]$Confirmation
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Bucket -notmatch '^gs://[a-z0-9][a-z0-9._-]{1,221}[a-z0-9]$') { throw 'Bucket must be an explicit gs:// URI.' }
if ($QualificationId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{2,79}$') { throw 'Invalid qualification id.' }
$inputs = @($BundlePath, $EvidenceIndexPath, $QualificationSummaryPath, $PreArchiveVerdictPath, $CandidateManifestPath)
foreach ($path in $inputs) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing archive input: $path" }
}
$pre = Get-Content -LiteralPath $PreArchiveVerdictPath -Raw | ConvertFrom-Json
if ($pre.phase -ne 'G8.2' -or $pre.status -ne 'G82_PRE_ARCHIVE_QUALIFICATION_PASSED') {
  throw 'Only a passed G8.2 pre-archive verdict may be archived.'
}
$manifestHash = (Get-FileHash -LiteralPath $CandidateManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$indexHash = (Get-FileHash -LiteralPath $EvidenceIndexPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($pre.candidate_manifest_sha256 -ne $manifestHash -or $pre.evidence_index_sha256 -ne $indexHash) {
  throw 'Pre-archive verdict does not bind to the supplied manifest/index.'
}

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$prefix = "$Bucket/g8-2/$QualificationId/$timestamp"
$objects = @()
foreach ($path in $inputs) {
  $name = [IO.Path]::GetFileName($path)
  $destination = "$prefix/$name"
  & gcloud storage cp $path $destination --quiet
  if ($LASTEXITCODE -ne 0) { throw "Archive upload failed: $path" }
  $descriptionRaw = & gcloud storage objects describe $destination --format=json
  if ($LASTEXITCODE -ne 0) { throw "Unable to describe archived object: $destination" }
  $description = $descriptionRaw | ConvertFrom-Json
  $objects += @{
    name = [string]$description.name
    generation = [string]$description.generation
    sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    size_bytes = [int64](Get-Item -LiteralPath $path).Length
  }
}

$bucketRaw = & gcloud storage buckets describe $Bucket --format=json
if ($LASTEXITCODE -ne 0) { throw 'Unable to describe evidence bucket.' }
$bucketMetadata = $bucketRaw | ConvertFrom-Json
$retentionSeconds = [int64]($bucketMetadata.retentionPolicy.retentionPeriod ?? 0)
$retentionDays = [int][Math]::Floor($retentionSeconds / 86400)
$publicAccessPrevention = ([string]$bucketMetadata.iamConfiguration.publicAccessPrevention -eq 'enforced')
$versioning = ($bucketMetadata.versioning.enabled -eq $true)

$result = [ordered]@{
  schema_version = 2
  phase = 'G8.2'
  qualification_id = $QualificationId
  status = if ($versioning -and $publicAccessPrevention -and $retentionDays -ge 365 -and $objects.Count -ge 4) { 'passed' } else { 'blocked' }
  bucket = $Bucket
  archive_prefix = $prefix
  archived_at = [DateTime]::UtcNow.ToString('o')
  retention_days = $retentionDays
  versioning_enabled = $versioning
  public_access_prevention = $publicAccessPrevention
  objects = $objects
  pre_archive_verdict_sha256 = (Get-FileHash -LiteralPath $PreArchiveVerdictPath -Algorithm SHA256).Hash.ToLowerInvariant()
  evidence_index_sha256 = $indexHash
  candidate_manifest_sha256 = $manifestHash
  production_authorized = $false
  production_deployed = $false
}
$result | ConvertTo-Json -Depth 10 | Out-File -LiteralPath $OutputPath -Encoding utf8
if ($result.status -ne 'passed') { throw 'Evidence archive does not satisfy G8.2 controls.' }
