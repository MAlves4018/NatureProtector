# Configuracao da baseline local

Este documento descreve o caminho clone-to-run para correr a baseline local do
NatureProtector em Development. O objetivo e permitir que uma pessoa clone o
repositorio, suba a infraestrutura, arranque API/Prevention/webUI, faca login e
execute uma run no Run Orchestrator.

## 1. Fluxo suportado

```text
Simulator.Host -> RabbitMQ -> Prevention.Host -> PostgreSQL/InfluxDB -> Backoffice.Api/Grafana -> webUI
```

`infra/scripts/up.ps1` sobe a infraestrutura Docker. O launcher
`scripts/dev/start-local-runtime.ps1` arranca Backoffice.Api, Prevention.Host e
webUI em background. O `Simulator.Host` e lancado pelo Run Orchestrator e deve
fechar no fim da run.

## 2. Pre-requisitos

- PowerShell.
- Git.
- Docker CLI/Engine e Docker Compose v2.
- .NET SDK usado pela solucao.
- Node.js e npm.
- `dotnet-ef` disponivel para migrations.

Validacao read-only:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
```

## 3. Preparar `.env` e token local

Depois de clonar:

```powershell
Copy-Item .\.env.example .\.env
```

Editar `.env` e definir um `INFLUXDB_TOKEN` local que comece por `apiv3_`.
Tokens reais nao devem ser versionados. `.env.example` deve manter apenas
placeholders.

Variaveis principais:

```text
POSTGRES_PORT=5432
RABBITMQ_AMQP_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672
INFLUXDB_PORT=8181
INFLUXDB_DATABASE=np_telemetry
INFLUXDB_BUCKET=np_telemetry
GRAFANA_PORT=3000
BACKOFFICE_API_PORT=5254
WEBUI_PORT=5173
```

## 4. Subir infraestrutura

```powershell
.\infra\scripts\up.ps1
```

O script:

- usa a raiz do repositorio;
- cria `.env` a partir de `.env.example` se faltar;
- gera o token file local de InfluxDB;
- executa Docker Compose;
- garante a database InfluxDB `np_telemetry`;
- nao arranca API/webUI.

Verificar containers e portas:

```powershell
docker ps
.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
```

## 5. Aplicar migrations

```powershell
dotnet-ef database update `
  --project .\src\NatureProtector.Infrastructure.Postgres\NatureProtector.Infrastructure.Postgres.csproj `
  --startup-project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj `
  --context NatureProtectorControlDbContext
```

## 6. Arrancar runtime local

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

O launcher:

- usa `docker compose --project-directory <repo> -f <repo>\docker-compose.yml up -d`;
- arranca Backoffice.Api, Prevention.Host e webUI em background;
- espera API e webUI ficarem acessiveis antes de abrir o browser;
- nao fica a seguir logs em foreground;
- devolve o prompt;
- escreve logs em `docs/evidence/dev-runtime/<timestamp>/`.

Mensagem final esperada:

```text
Launcher completed. Services continue in background.
Logs: <runRoot>
```

## 7. Login local em Development

```text
Development login:
Username: admin
Password: admin123
```

Estas credenciais sao apenas para baseline local/Development. Nao usar fora de
desenvolvimento.

Depois do login, abrir `Scenario Lab` -> `Run Orchestrator`.

## 8. Correr `scenario_b` no Run Orchestrator

Usar parametros de smoke local:

```text
Scenario: scenario_b
Degradation profile: none
Sensors: 6
Cycles: 5
Interval seconds: 5
Seed: 12345
Wait for completion: enabled quando disponivel
```

Com 6 sensores x 5 ciclos, o esperado e 30 eventos processados e 30 risk
assessments, sem erro.

## 9. Validar `scenario_b`

Runs recentes:

```powershell
@'
select "Id", "ScenarioCode", "StartedAt", "EndedAt", "Status"
from control.simulation_runs
order by "StartedAt" desc
limit 5;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Tentativas de processamento recentes:

```powershell
@'
select "Outcome", "ErrorCode", count(*) as count
from pipeline.processing_attempts
where "StartedAt" > now() - interval '30 minutes'
group by "Outcome", "ErrorCode"
order by count desc;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Risk assessments recentes:

```powershell
@'
select count(*) as risk_assessments,
       min("RiskScore") as min_score,
       max("RiskScore") as max_score
from projection.risk_assessment_log
where "CreatedAt" > now() - interval '30 minutes';
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Confirmar que `Simulator.Host` fechou:

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*NatureProtector.Simulator.Host*" } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

Esperado para `scenario_b` com 6 sensores x 5 ciclos:

- run com `EndedAt` preenchido;
- `processing_attempts = 30`;
- `risk_assessments = 30`;
- `ErrorCode` vazio;
- sem processo `NatureProtector.Simulator.Host` apos terminar.

## 10. Validacao completa da baseline

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

## 11. Troubleshooting

### PowerShell bloqueia scripts

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalPrerequisites.ps1
```

### Docker indisponivel

- abrir Docker Desktop;
- esperar o engine ficar pronto;
- repetir `.\infra\scripts\up.ps1`.

### `INFLUXDB_TOKEN` ainda e placeholder

- editar `.env`;
- definir token local `apiv3_...`;
- repetir `.\infra\scripts\up.ps1`.

### API/webUI nao ficam ready no launcher

Ver logs indicados no fim do erro:

```text
docs/evidence/dev-runtime/<timestamp>/
```

Ficheiros principais:

- `backoffice-api.log`;
- `backoffice-api.err.log`;
- `prevention-host.log`;
- `prevention-host.err.log`;
- `webui.log`;
- `webui.err.log`.

### Porta ocupada

Usar `-ForceRestart` apenas quando o processo for local do NatureProtector:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Se a porta pertencer a outro processo, parar manualmente ou alterar a porta em
`.env`.

### Control plane vazio

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

### Reset destrutivo

Nao usar no fluxo normal. Apenas com confirmacao textual:

```powershell
.\infra\scripts\reset-local-infra.ps1 -Confirm RESET_LOCAL_INFRA
```

Este comando apaga volumes locais da baseline.
