<#
.SYNOPSIS
Desliga a baseline local arrancada por Docker Compose.

.DESCRIPTION
O script muda para a raiz do repositório e executa `docker compose down`.

.NOTES
- Útil para fechar a infraestrutura no fim de uma sessão de desenvolvimento ou
  demonstração.
#>

# Move para a raiz do projeto.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Desliga os serviços containerizados da baseline local.
docker compose down
