[CmdletBinding()]
param([Parameter(Mandatory)][string]$Action)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
foreach($name in 'G82_QUALIFICATION_ID','G82_SOURCE_COMMIT','G82_PROJECT_ID','G82_REGION','G82_EVIDENCE_DIRECTORY','G82_RAW_SOURCE_PATH'){
  if([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))){throw "Missing required probe environment variable $name."}
}
if($env:G82_PROJECT_ID -match '(?i)cn2526'){throw 'CN projects are forbidden.'}
$sourceAdapter=Join-Path $PSScriptRoot ("sources/{0}.ps1" -f $Action)
if(-not(Test-Path -LiteralPath $sourceAdapter -PathType Leaf)){
  throw "Runtime source adapter '$sourceAdapter' is not implemented. G8.2 fails closed rather than accepting a manual measurement."
}
& $sourceAdapter
if($LASTEXITCODE -ne 0){throw "Runtime source adapter failed: $sourceAdapter"}
