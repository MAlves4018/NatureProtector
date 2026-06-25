#!/usr/bin/env pwsh
& (Join-Path $PSScriptRoot 'Invoke-G82ProbeAdapter.ps1') -Action 'rollback-drill'
exit $LASTEXITCODE
