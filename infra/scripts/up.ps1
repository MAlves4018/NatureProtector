#Para executar correr na raiz do projeto o comando comando:
#.\infra\scripts\up.ps1

# Move para a raiz do projeto, independentemente de onde o script é chamado
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Cria .env a partir do exemplo, se ainda não existir
if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
}

# Sobe a baseline local em background
docker compose up -d