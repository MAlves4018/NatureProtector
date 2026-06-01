# NatureProtector

NatureProtector é uma baseline local em .NET 9 para simulação, processamento e consulta operacional de risco de incêndio. O estado atual corresponde a uma versão beta técnica/demonstrável centrada na área piloto de Proença-a-Nova.

O projeto liga:

- simulação de leituras de sensores;
- transporte por RabbitMQ;
- ingestão durável e processamento no `Prevention.Host`;
- persistência em PostgreSQL;
- telemetria em InfluxDB/Grafana;
- API de backoffice;
- webUI local com monitorização, evidência e Run Orchestrator.

A baseline atual é uma implementação técnica e metodológica candidata. Não deve ser apresentada como sistema de produção, alerta oficial, modelo cientificamente calibrado ou equivalente ao RCM/IPMA/ICNF.

---

## Estado atual

### Funcionalidades principais

- Plano de controlo persistido em PostgreSQL.
- Cenários operacionais persistidos em `control.simulation_runs`.
- Publicação de leituras simuladas através de RabbitMQ.
- Inbox durável em `pipeline.event_inbox`.
- Registo de tentativas em `pipeline.processing_attempts`.
- Rejeição e quarentena persistidas para falhas de pipeline.
- Normalização, elegibilidade e scoring operacional.
- Projeções em `projection.*`.
- WebUI local com:
  - Monitoring;
  - Scenario Lab;
  - Run Orchestrator;
  - Evidence & Comparison;
  - Flow Explorer;
  - Model & Provenance.
- Comparação técnica entre:
  - NatureProtector Score;
  - Fire Weather Index candidate calculation;
  - KBDI candidate dryness indicator;
  - Portuguese Context Proxy candidato.

### Estado metodológico

O score NatureProtector, o FWI, o KBDI e o Portuguese Context Proxy são usados como instrumentos técnicos de comparação, explicabilidade e proveniência. A formulação atual deve ser lida como **Candidate Parameter Set**, não como validação científica final.

---

## Arquitetura local

Componentes principais:

- `Simulator.Host`
  - gera leituras simuladas a partir de cenários;
  - é lançado pelo Run Orchestrator;
  - deve terminar no fim da run.

- `RabbitMQ`
  - transporta eventos `EventEnvelope<SensorReadingProducedPayload>`.

- `Prevention.Host`
  - consome eventos;
  - materializa inbox;
  - processa validação, normalização, elegibilidade, scoring, projeções e alertas.

- `PostgreSQL`
  - fonte principal de verdade para controlo, pipeline e projeções.

- `InfluxDB`
  - séries temporais e telemetria local.

- `Grafana`
  - dashboards locais de apoio.

- `Backoffice.Api`
  - API HTTP para plano de controlo, runtime, diagnósticos e UI.

- `webUI`
  - interface local para observar estado, executar cenários e recolher evidência.

---

## Como executar localmente

O caminho suportado de execução local está documentado em:

[docs/setup/local-baseline-setup.md](docs/setup/local-baseline-setup.md)

Esse guia é a fonte principal para correr a baseline. Deve ser preferido a notas antigas ou arranques manuais.

Fluxo resumido:

```powershell
copy .env.example .env
```

Configurar um `INFLUXDB_TOKEN` local no `.env`. O ficheiro `.env` é local e não deve ser versionado.

Depois:

```powershell
.\infra\scripts\up.ps1
```

Aplicar migrations:

```powershell
dotnet-ef database update `
  --project .\src\NatureProtector.Infrastructure.Postgres\NatureProtector.Infrastructure.Postgres.csproj `
  --startup-project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj `
  --context NatureProtectorControlDbContext
```

Arrancar runtime local:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Login local em ambiente `Development`:

```text
Username: admin
Password: admin123
```

Estas credenciais são apenas para baseline local/development. Não devem ser usadas fora desse contexto.

---

## Run Orchestrator

O fluxo suportado para executar simulações é através da webUI:

```text
Scenario Lab → Run Orchestrator
```

Para uma validação nominal rápida:

```text
scenarioCode: scenario_b
sensorCount: 6
numberOfCycles: 5
intervalSeconds: 5
seed: 12345
degradationProfile: none
```

Resultado esperado para esta configuração:

- run concluída com `EndedAt` preenchido;
- `processing_attempts = 30`;
- `risk_assessments = 30`;
- `ErrorCode` vazio;
- sem processo `NatureProtector.Simulator.Host` vivo após a run.

O `Simulator.Host` não deve ser arrancado manualmente como parte do fluxo principal. O Run Orchestrator é responsável por lançar a simulação e esta deve terminar após a run.

---

## Validação rápida

### Verificar infraestrutura

```powershell
docker ps -a --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Serviços esperados na baseline local:

- `np-postgres`;
- `np-rabbitmq`;
- `np-influxdb`;
- `np-grafana`.

### Verificar runs

```powershell
@'
select "Id", "ScenarioCode", "StartedAt", "EndedAt", "Status"
from control.simulation_runs
order by "StartedAt" desc
limit 5;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

### Verificar tentativas de processamento

```powershell
@'
select "Outcome", "ErrorCode", count(*) as count
from pipeline.processing_attempts
where "StartedAt" > now() - interval '30 minutes'
group by "Outcome", "ErrorCode"
order by count desc;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

### Verificar risk assessments

```powershell
@'
select count(*) as risk_assessments,
       min("RiskScore") as min_score,
       max("RiskScore") as max_score
from projection.risk_assessment_log
where "CreatedAt" > now() - interval '30 minutes';
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

### Verificar lifecycle do Simulator

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*NatureProtector.Simulator.Host*" } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

Depois de uma run terminada, o resultado esperado é não aparecer nenhum processo `NatureProtector.Simulator.Host`.

---

## Build, testes e frontend

Compilar a solução:

```powershell
dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config
```

Correr testes:

```powershell
dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1
```

Build da webUI:

```powershell
cd webUI
npm run build
```

Se o build .NET falhar por ficheiros bloqueados, confirmar que `Backoffice.Api`, `Prevention.Host` e `Simulator.Host` não estão vivos antes de repetir o comando.

---

## Evidência operacional recente

A baseline foi validada com uma run `scenario_b` usando 6 sensores e 5 ciclos, produzindo:

- `simulation_runs` com `StartedAt` e `EndedAt`;
- `processing_attempts = 30`;
- `risk_assessments = 30`;
- scores entre aproximadamente `0.4178` e `0.4482`;
- ausência de processo `Simulator.Host` após a run.

A nova evidência runtime de `scenario_c` deve ser recolhida quando se pretender fechar a comparação B/C final.

---

## Documentação relacionada

- [docs/setup/local-baseline-setup.md](docs/setup/local-baseline-setup.md)
- [docs/README.md](docs/README.md)
- [docs/architecture/README.md](docs/architecture/README.md)
- [docs/architecture/implementation.md](docs/architecture/implementation.md)
- [docs/contracts/v1-vocabulary-map.md](docs/contracts/v1-vocabulary-map.md)
- [docs/architecture/scenario-run-orchestrator.md](docs/architecture/scenario-run-orchestrator.md)
- [docs/planning/pesquisa-ii-vs-implementation-state.md](docs/planning/pesquisa-ii-vs-implementation-state.md)
- [tests/README.md](tests/README.md)

---

## Limitações conhecidas

- O score NatureProtector é candidato e não está cientificamente calibrado.
- FWI e KBDI são usados como comparação/proveniência, não como validação científica final.
- O Portuguese Context Proxy é candidato e não corresponde ao RCM/IPMA/ICNF oficial.
- `scenario_c` deve ser revalidado em runtime antes de ser usado como evidência final de comparação B/C.
- InfluxDB/Grafana fazem parte da observabilidade local; problemas nestes serviços devem ser diagnosticados pelo guia de setup.
- Autenticação local usa credenciais de Development (`admin` / `admin123`) apenas para baseline local.
