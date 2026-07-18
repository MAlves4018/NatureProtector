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
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
```

O caminho real usado para o freeze candidate local é:

```text
C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector
```

Para parar:

```powershell
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

`scripts\np.ps1` é o entrypoint recomendado para clone-to-run. `prepare-local` executa o restore .NET e `npm ci` a partir dos lockfiles antes do arranque. `scripts\workspace.ps1` continua disponível como compatibilidade para fluxos antigos.

O `.env.example` não contém um token InfluxDB local válido. `.\scripts\np.ps1 init-local -Force` gera `.env`, cria um `INFLUXDB_TOKEN` com prefixo `apiv3_` e prepara o token admin local usado pelo Docker Compose. O ficheiro `.env` é local e não deve ser versionado. `dotnet ef` fica reservado para validação avançada/desenvolvimento; não é parte do caminho normal clone-to-run.

Se `init-local -Force` regenerar o token e já existir um volume InfluxDB local inicializado com token antigo, `up` pode falhar com HTTP 401. Para ambiente local/dev, use `.\scripts\np.ps1 clean-local`; este comando executa limpeza scoped ao compose NatureProtector (`docker compose down -v --remove-orphans`) e não executa `docker system prune`.

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

A baseline local do freeze candidate foi validada com `scenario_b` e `scenario_c` pelo harness:

```powershell
.\scripts\validation\Invoke-LocalFunctionalValidation.ps1 -Full -Evidence -Ui
```

Resultado aceito da validação funcional local:

- `scenario_b`: `30` eventos aceites, `30` risk assessments, `0` missing, `0` rejected, `0` quarantined;
- `scenario_c` com `missing-readings`: `24` eventos aceites, `24` risk assessments, `6` missing, `0` rejected, `0` quarantined;
- comparação B/C concluída;
- validações DB, RabbitMQ, Prevention Host e UI smoke concluídas;
- ausência de processo `Simulator.Host` persistente após as runs.

O harness aguarda convergência assíncrona do audit antes de declarar PASS. Isto evita tratar uma leitura parcial de auditoria como regressão funcional.

---

## Documentação relacionada

- [docs/setup/local-baseline-setup.md](docs/setup/local-baseline-setup.md)
- [docs/freeze/FREEZE-CANDIDATE.md](docs/freeze/FREEZE-CANDIDATE.md)
- [docs/runtime/local-runtime.md](docs/runtime/local-runtime.md)
- [docs/runtime/simulation-runs.md](docs/runtime/simulation-runs.md)
- [docs/testing/validation-gates.md](docs/testing/validation-gates.md)
- [docs/scripts/script-inventory.md](docs/scripts/script-inventory.md)
- [docs/README.md](docs/README.md)
- [docs/architecture/README.md](docs/architecture/README.md)
- [docs/architecture/implementation.md](docs/architecture/implementation.md)
- [docs/contracts/v1-vocabulary-map.md](docs/contracts/v1-vocabulary-map.md)
- [docs/architecture/scenario-run-orchestrator.md](docs/architecture/scenario-run-orchestrator.md)
- [docs/current-state/data-risk-and-scientific-boundaries.md](docs/current-state/data-risk-and-scientific-boundaries.md)
- [tests/README.md](tests/README.md)

---

## Limitações conhecidas

- O score NatureProtector é candidato e não está cientificamente calibrado.
- FWI e KBDI são usados como comparação/proveniência, não como validação científica final.
- O Portuguese Context Proxy é candidato e não corresponde ao RCM/IPMA/ICNF oficial.
- A validação B/C é local e funcional; não prova readiness cloud, produção, carga, stress ou calibração científica.
- InfluxDB/Grafana fazem parte da observabilidade local; problemas nestes serviços devem ser diagnosticados pelo guia de setup.
- Autenticação local usa credenciais de Development (`admin` / `admin123`) apenas para baseline local.

## Unified Operations Control Plane

The Backoffice UI now includes a closed, auditable engineering operations surface for quality, evidence, deployment and cloud workflows. The backend is the authorization authority; the browser never receives provider credentials and cannot submit arbitrary commands.

Documentation:

- `docs/implementation/operations/unified-operations-control-plane.md`
- `docs/implementation/operations/security-model.md`
- `docs/implementation/operations/workflow-callback.md`
- `docs/implementation/operations/demo-narrative.md`
- `docs/implementation/operations/open-gates.md`

Success criteria are machine-readable in `config/operations/success-criteria.json`.
