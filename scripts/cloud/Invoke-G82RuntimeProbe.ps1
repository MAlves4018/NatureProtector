[CmdletBinding()]
param(
  [Parameter(Mandatory)][ValidateSet('pilot-1','pilot-2','pilot-3','soak-start','soak-observe','soak-finish','capacity','security-rotation','incident-drill','collect-audit','cost-observation','second-operator','rollback-drill','teardown-rehearsal')][string]$Action,
  [Parameter(Mandatory)][string]$QualificationId,
  [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceCommit,
  [Parameter(Mandatory)][string]$EvidenceDirectory,
  [Parameter(Mandatory)][string]$Confirmation,
  [Parameter(Mandatory)][string]$ProjectId,
  [string]$Region='europe-southwest1',
  [string]$ClusterName='np-g81',
  [string]$CloudSqlInstance='np-g81-postgres',
  [string]$SecurityCenterParent=''
)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$expected="RUN_G82_$($Action.ToUpperInvariant().Replace('-','_'))"
if($Confirmation -ne $expected){throw "Exact confirmation '$expected' is required."}
if($ProjectId -match '(?i)cn2526'){throw 'CN projects are forbidden.'}
if($Region -ne 'europe-southwest1'){throw "Unexpected region '$Region'."}
New-Item -ItemType Directory -Force -Path $EvidenceDirectory|Out-Null
$raw=Join-Path $EvidenceDirectory 'raw-probe-source.json'
$started=(Get-Date).ToUniversalTime().ToString('o')

# G8.2 never accepts a measurement JSON or executable command as a workflow
# input. The action maps to a reviewed repository adapter and fails closed when
# that adapter cannot derive the raw facts from runtime APIs/cloud evidence.
$adapter=Join-Path $PSScriptRoot (Join-Path 'probes' ("{0}.ps1" -f $Action))
if(-not(Test-Path -LiteralPath $adapter -PathType Leaf)){throw "Missing reviewed probe adapter: $adapter"}
$env:G82_ACTION=$Action; $env:G82_QUALIFICATION_ID=$QualificationId; $env:G82_SOURCE_COMMIT=$SourceCommit
$env:G82_PROJECT_ID=$ProjectId; $env:G82_REGION=$Region; $env:G82_CLUSTER_NAME=$ClusterName; $env:G82_CLOUD_SQL_INSTANCE=$CloudSqlInstance
$env:G82_SECURITY_CENTER_PARENT=$SecurityCenterParent; $env:G82_EVIDENCE_DIRECTORY=$EvidenceDirectory; $env:G82_RAW_SOURCE_PATH=$raw
$adapterEvidence=[ordered]@{action=$Action;adapter=(Resolve-Path $adapter).Path;adapter_sha256=(Get-FileHash -Algorithm SHA256 $adapter).Hash.ToLowerInvariant();started_at=$started;actor=$env:GITHUB_ACTOR;workflow_run_id=$env:GITHUB_RUN_ID;production_authorized=$false;production_deployed=$false}
$adapterEvidence|ConvertTo-Json -Depth 8|Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory 'adapter-command.json')
& $adapter
if($LASTEXITCODE -ne 0){throw "Reviewed probe adapter failed with exit code $LASTEXITCODE."}
if(-not(Test-Path -LiteralPath $raw -PathType Leaf)){throw 'Trusted probe adapter did not produce raw-probe-source.json.'}
python scripts/cloud/New-G82ProbeMeasurement.py --source $raw --action $Action --qualification-id $QualificationId --source-commit $SourceCommit --evidence-root $EvidenceDirectory --output (Join-Path $EvidenceDirectory 'measurement.json')
if($LASTEXITCODE -ne 0){throw 'Probe measurement derivation failed.'}
[ordered]@{schema_version=2;phase='G8.2';action=$Action;qualification_id=$QualificationId;source_commit=$SourceCommit;status='probe-derived';finished_at=(Get-Date).ToUniversalTime().ToString('o');production_authorized=$false;production_deployed=$false}|ConvertTo-Json -Depth 6|Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory 'probe-result.json')
