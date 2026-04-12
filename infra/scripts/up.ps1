<#
.SYNOPSIS
Levanta a baseline local em Docker Compose.

.DESCRIPTION
O script muda para a raiz do repositório, garante que existe um `.env` local e
executa `docker compose up -d` para arrancar a infraestrutura de apoio.

.NOTES
- Deve ser usado antes de correr a API, o simulador ou a pipeline de prevenção
  quando estes dependem dos serviços containerizados.
#>

# Move para a raiz do projeto, independentemente de onde o script é chamado.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Cria `.env` a partir do exemplo apenas na primeira execução local.
if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
}

# Levanta a infraestrutura em background para que os restantes processos possam
# ser arrancados em separado.
docker compose up -d
