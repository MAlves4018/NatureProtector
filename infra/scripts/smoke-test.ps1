<#
.SYNOPSIS
Mostra o estado atual dos contentores da baseline local.

.DESCRIPTION
O script muda para a raiz do repositório e executa `docker compose ps`.

.NOTES
- Serve como verificação rápida antes de arrancar os hosts .NET.
#>

# Move para a raiz do projeto.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Mostra o estado corrente dos contentores.
docker compose ps
