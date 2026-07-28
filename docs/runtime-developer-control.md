---
id: NP-RUNTIME-DEVELOPER-CONTROL
status: CURRENT
owner: Miguel Alves
audience: developer, QA, presenter
source_of_truth: scripts/np.ps1, Backoffice controllers and webUI route registry
last_verified_against: NatureProtector repository snapshot 2026-07-22
last_verified_at: 2026-07-22
review_triggers: runtime command, controller, route, reset, evidence or diagnostic changes
---

# Controlo de runtime para desenvolvimento

A consola atual não está montada em `/dev/runtime` nem em `/ui-v2`. O runtime é operado através da webUI atual e da API:

```text
http://127.0.0.1:5173/simulation
http://127.0.0.1:5173/runs
http://127.0.0.1:5173/scenario-compare
http://127.0.0.1:5173/queries
http://127.0.0.1:5173/pipeline
http://127.0.0.1:5173/evidence
http://127.0.0.1:5173/admin
http://127.0.0.1:5173/p3
```

A visibilidade de cada rota é derivada das capabilities devolvidas pelo backend. A autorização da API continua a ser a fronteira de segurança.

## Launcher local

Usar o entrypoint canónico:

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start -OpenBrowser
.\scripts\np.ps1 health
```

O comando `start` arranca os serviços persistentes `Backoffice.Api`, `Prevention.Host` e `webUI`. O `Simulator.Host` é lançado por run através da API/UI e deve terminar no fim da execução.

Portas por omissão:

```text
Backoffice API:       http://127.0.0.1:5254
Prevention Host:      http://127.0.0.1:5260
webUI:                http://127.0.0.1:5173
RabbitMQ Management: http://127.0.0.1:15672
InfluxDB:             http://127.0.0.1:8181
Grafana:              http://127.0.0.1:3000
```

Os logs do launcher são escritos nos diretórios de evidence/runtime definidos pelos scripts atuais. Não depender de uma pasta histórica específica para localizar a execução: guardar o path devolvido pelo comando ou pela resposta de run.

## Login Development

```text
Username: admin
Password: admin123
```

Estas credenciais pertencem exclusivamente à baseline local `Development`.

## Criar uma run pela UI

1. Abrir `/login` e autenticar.
2. Abrir `/simulation`.
3. Selecionar a área `proenca-a-nova` e o cenário.
4. Definir sensores, ciclos, intervalo, seed e perfis de degradação.
5. Rever os valores requested/resolved.
6. Iniciar a run.
7. Acompanhar o lifecycle até o sistema ficar settled.
8. Abrir `/runs` para audit/timings e `/scenario-compare` para comparação B/C.

Baseline nominal curta:

```text
scenarioCode: scenario_b
sensorCount: 6
numberOfCycles: 5
intervalSeconds: 1
seed: 12345
degradationProfiles: none
collectEvidence: true
```

O valor nominal esperado é calculado como `sensorCount × numberOfCycles`; neste exemplo, `30`. Não reutilizar contagens históricas se o pedido resolvido for diferente.

## Criar uma run pela API

```powershell
$login = Invoke-RestMethod `
  -Method Post `
  -Uri 'http://127.0.0.1:5254/api/users-roles/login' `
  -ContentType 'application/json' `
  -Body (@{ username = 'admin'; password = 'admin123' } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.token)" }

$request = @{
  areaCode = 'proenca-a-nova'
  scenarioCode = 'scenario_b'
  sensorCount = 6
  numberOfCycles = 5
  intervalSeconds = 1
  seed = 12345
  degradationProfiles = @('none')
  collectEvidence = $true
  waitForCompletion = $false
  timeoutSeconds = 300
  allowParallelRun = $false
  runLabel = 'manual-nominal'
} | ConvertTo-Json

$started = Invoke-RestMethod `
  -Method Post `
  -Uri 'http://127.0.0.1:5254/api/control/runtime/runs' `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body $request

$started
```

Guardar `requestId` e `operationId`. Quando `simulationRunId` estiver resolvido, validar também:

```text
GET /api/control/runtime/operations/{operationId}
GET /api/control/runtime/operations/by-request/{requestId}
GET /api/control/runtime/runs/{runId}
GET /api/control/runtime/runs/{runId}/operation
GET /api/control/runtime/runs/{runId}/audit
GET /api/control/runtime/runs/{runId}/timings
```

Uma run só deve ser tratada como concluída quando o estado terminal for de sucesso e `Accounting.Settled=true`.

## Diagnósticos preparados

A UI `/queries` utiliza o catálogo fechado:

```text
GET  /api/control/runtime/diagnostics
POST /api/control/runtime/diagnostics/{diagnosticId}
```

Existem atualmente 28 IDs. Consultar o inventário gerado em [reference/generated/runtime-diagnostic-catalog.csv](reference/generated/runtime-diagnostic-catalog.csv). Scripts futuros devem pedir o catálogo à API e executar os IDs devolvidos, evitando listas duplicadas.

## Evidence por run

Quando `collectEvidence=true`, a resposta e a operação podem incluir `evidenceId`, `evidenceLocation` ou diretórios de logs/evidence. A consulta segura é feita por:

```text
GET /api/control/runtime/observability/evidence
GET /api/control/runtime/observability/evidence/{evidenceId}
```

O download é autenticado e limitado à allowlist do catálogo. Não abrir paths arbitrários recebidos do browser.

## Reset runtime

Endpoint:

```text
POST /api/control/runtime/reset
```

Confirmação exata:

```text
RESET_RUNTIME_STATE
```

Exemplo dry-run:

```json
{
  "scope": "system",
  "confirm": "RESET_RUNTIME_STATE",
  "dryRun": true,
  "requireExternalStores": true,
  "reconcileTerminalOrphans": true
}
```

Regras:

- exige capability `simulation.execute`;
- bloqueia quando existem operações ou inbox ativos;
- o dry-run não altera dados;
- o reset normal limpa apenas estado runtime, preservando áreas, sensores, cenários, configurações e identidades;
- com `requireExternalStores=true`, o resultado deve incluir PostgreSQL, RabbitMQ e InfluxDB;
- eliminação de volumes Docker não é o caminho normal de rebaseline.

## Validação negativa P3

Disponível apenas nos ambientes permitidos pelo backend:

```text
GET  /api/dev/controlled-validation/p3
POST /api/dev/controlled-validation/p3/run
```

Wrapper seguro:

```powershell
$env:NP_RELIABILITY_AUTH_TOKEN = '<token>'
python .\scripts\reliability\run-controlled-validation-p3.py `
  --output .\artifacts\reliability\p3-dry-run
```

A execução real exige também `--execute --acknowledge-non-production`. O wrapper prepara evidence, mas a aceitação exige auditoria das tabelas `pipeline.*` e `projection.*`.

## Workloads bounded

Readiness HTTP local:

```powershell
.\scripts\performance\run-local-readiness-workload.ps1
```

Capacidade sistémica local:

```powershell
.\scripts\performance\run-system-capacity-workload.ps1 -Profile Calibration -UseDevelopmentAdminDefault
.\scripts\performance\run-system-capacity-workload.ps1 -Profile B0 -UseDevelopmentAdminDefault -CalibrationRunDirectory <path>
```

Estes workloads não são testes de stress, SLOs ou prova de produção.

## Encerramento

```powershell
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

Confirmar que não existe processo persistente do simulador:

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like '*NatureProtector.Simulator.Host*' } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

O resultado esperado depois de todas as runs terminarem é uma lista vazia.

## Referências

- [Catálogo funcional](reference/functional-capability-catalog.md)
- [Matriz de rastreabilidade](reference/functional-traceability-matrix.csv)
- [Invariantes por cenário](reference/scenario-acceptance-invariants.md)
- [Runtime local](runtime/local-runtime.md)
- [Simulation runs](runtime/simulation-runs.md)
