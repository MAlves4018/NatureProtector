<#
.SYNOPSIS
Segue os logs da baseline local em Docker Compose.

.DESCRIPTION
O script muda para a raiz do repositório e executa `docker compose logs -f`.

.NOTES
- É útil para confirmar rapidamente se PostgreSQL, RabbitMQ, InfluxDB e Grafana
  arrancaram corretamente.
#>

# Move para a raiz do projeto.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Mostra logs em tempo real de todos os serviços da baseline.
docker compose logs -f
