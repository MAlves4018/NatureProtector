#!/usr/bin/env pwsh
& (Join-Path $PSScriptRoot 'Invoke-G82ProbeAdapter.ps1') -Action 'pilot-1'
exit $LASTEXITCODE
