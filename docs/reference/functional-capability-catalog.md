---
id: NP-REF-FUNCTIONAL-CATALOG
status: CURRENT
owner: Miguel Alves
audience: engineering, QA, report, presentation
source_of_truth: repository code, configuration and generated reference catalogs
last_verified_against: NatureProtector Phase 3 P0 runtime coverage
last_verified_at: 2026-07-22
review_triggers: controller, route, capability, schema, scenario, workflow or test changes
---

# Catálogo funcional e de utilização

Este documento é o ponto de entrada para perceber **o que o NatureProtector faz**, **como usar cada superfície** e **como cada capacidade deverá ser aceite automaticamente**. Não substitui os contratos de código: quando existir divergência, prevalecem os controllers, políticas, page registry, entidades PostgreSQL e scripts canónicos atuais.

Os inventários derivados diretamente do código são regenerados por:

```powershell
python .\scripts\docs\generate_reference_catalogs.py
```

Resultados gerados:

- [API endpoint catalog](generated/api-endpoint-catalog.csv) — 75 endpoints e respetiva fronteira de acesso;
- [UI route/capability matrix](generated/ui-route-capability-matrix.csv) — rotas montadas, capabilities e inconsistências estruturais;
- [runtime diagnostic catalog](generated/runtime-diagnostic-catalog.csv) — 28 diagnósticos fechados;
- [role/capability matrix](generated/role-capability-matrix.csv);
- [engineering operation catalog](generated/operation-catalog.csv).

A matriz manual que liga capacidades, armazenamento e testes está em [functional-traceability-matrix.csv](functional-traceability-matrix.csv). Os critérios de cenário estão em [scenario-acceptance-invariants.md](scenario-acceptance-invariants.md) e na versão machine-readable [`config/acceptance/scenario-invariants.json`](../../config/acceptance/scenario-invariants.json). A implementação executável P0 está descrita em [P0 runtime functional coverage](../testing/p0-runtime-functional-coverage.md) e configurada em [`config/acceptance/p0-runtime-coverage.json`](../../config/acceptance/p0-runtime-coverage.json).

## Caminho suportado de utilização local

A partir da raiz do repositório, em Windows/PowerShell:

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
```

Serviços persistentes esperados:

| Serviço | URL/porta local | Função |
| --- | --- | --- |
| webUI | `http://127.0.0.1:5173` | interface pública e autenticada |
| Backoffice API | `http://127.0.0.1:5254` | control plane, runtime, operações e administração |
| Prevention Host | `http://127.0.0.1:5260` | consumo, validação, scoring e projeções |
| RabbitMQ Management | `http://127.0.0.1:15672` | observação do broker local |
| PostgreSQL | `localhost:5433` | fonte de verdade de controlo, pipeline e projeções |
| InfluxDB | `http://127.0.0.1:8181` | séries temporais/telemetria |
| Grafana | `http://127.0.0.1:3000` | dashboards locais |

Credenciais de desenvolvimento por omissão: `admin / admin123`. São apenas para `Development` local.

Para terminar:

```powershell
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

## Superfícies funcionais

### 1. Consulta pública territorial

**Objetivo:** consultar área, mapa, grelha, sensores e alertas ativos sem iniciar operações.

**UI:** `/demo`, `/dashboard`, `/context`, `/about`.

**API principal:**

```text
GET /api/control/areas
GET /api/control/areas/{areaCode}
GET /api/control/areas/{areaCode}/GeoJSON
GET /api/control/areas/{areaCode}/grid-cells
GET /api/control/areas/{areaCode}/sensor-nodes
GET /api/control/areas/{areaCode}/alerts/active
```

**Fonte de dados:** `control.areas`, `control.area_contexts`, `control.grid_cells`, `control.sensor_nodes`, `projection.alert_state`.

**Aceitação mínima:** HTTP 200, referências territoriais coerentes, paginação bounded e ausência de dados internos protegidos na jornada anónima.

### 2. Autenticação, identidade e capabilities

**Objetivo:** iniciar/terminar sessão e obter a autoridade efetiva calculada pelo backend.

**UI:** `/login`; identidade apresentada no shell autenticado.

**API:**

```text
POST /api/users-roles/login
POST /api/users-roles/logout
GET  /api/users-roles/me
GET  /api/users-roles/me/capabilities
```

**Fonte de dados:** schema `user_base` (`users`, `roles`, `user_roles`) e token JWT emitido pela API.

**Invariantes:** credenciais inválidas não emitem token; capabilities vêm do backend; logout não concede acesso posterior; uma role desconhecida não recebe capabilities protegidas.

### 3. Administração de utilizadores e roles

**Objetivo:** criar, consultar, alterar e remover utilizadores/roles e respetivas associações.

**UI:** `/users` para identidades com `users.manage` e `roles.manage`.

**API:** família `/api/users-roles/users*` e `/api/users-roles/roles*`.

**Fonte de dados:** `user_base.users`, `user_base.roles`, `user_base.user_roles`.

**Invariantes:** nomes únicos; referências inexistentes devolvem `404`; duplicados devolvem conflito quando aplicável; apenas Admin executa mutações; recursos temporários usados por testes são removidos no cleanup.

### 4. Configuração e catálogo de cenários

**Objetivo:** consultar/ativar versões de configuração e resolver cenários por área.

**UI:** contexto distribuído por `/simulation`, `/runs`, `/admin`.

**API:**

```text
GET  /api/control/configurations
GET  /api/control/configurations/active
POST /api/control/configurations/{versionNumber}/activate
GET  /api/control/areas/{areaCode}/scenarios
```

**Fonte de dados:** `control.configuration_versions`, `control.scenario_definitions`, `control.dataset_artifacts`, `control.scenario_dataset_bindings`.

**Invariantes:** existe no máximo uma configuração ativa; cenário refere área/configuração válidas; ativação inexistente não altera estado.

### 5. Run Orchestrator e simulação

**Objetivo:** iniciar uma run bounded e acompanhar pedido, operação, processo produtor e convergência do pipeline.

**UI:** `/simulation`, `/runs`, `/scenario-compare`.

**API:**

```text
POST /api/control/runtime/runs
GET  /api/control/runtime/operations/{operationId}
GET  /api/control/runtime/operations/by-request/{requestId}
GET  /api/control/runtime/runs/{runId}
GET  /api/control/runtime/runs/{runId}/operation
GET  /api/control/runtime/runs/{runId}/audit
GET  /api/control/runtime/runs/{runId}/timings
```

Pedido nominal recomendado:

```json
{
  "areaCode": "proenca-a-nova",
  "scenarioCode": "scenario_b",
  "sensorCount": 6,
  "numberOfCycles": 5,
  "intervalSeconds": 1,
  "seed": 12345,
  "degradationProfiles": ["none"],
  "collectEvidence": true,
  "waitForCompletion": false,
  "timeoutSeconds": 300,
  "allowParallelRun": false,
  "runLabel": "acceptance-nominal"
}
```

**Fonte de dados:** `control.simulation_runs`, `control.runtime_orchestrator_executions` e artefactos runtime allowlisted.

**Invariantes:** identidade por `requestId`, `operationId` e `simulationRunId`; estado terminal explícito; `Simulator.Host` termina após a run; o sucesso só é declarado depois de `Accounting.Settled=true`.

### 6. Pipeline durável e scoring

**Objetivo:** transportar, validar, processar e projetar cada leitura elegível.

**Fluxo:** `Simulator.Host → RabbitMQ → Prevention.Host → PostgreSQL/InfluxDB`.

**Persistência principal:**

```text
pipeline.event_inbox
pipeline.processing_attempts
pipeline.rejected_events
pipeline.quarantined_events
projection.accepted_reading_log
projection.risk_assessment_log
projection.daily_cell_state
projection.cell_operational_state
projection.area_operational_state
projection.alert_state
projection.cycle_settlement
projection.cycle_observation
projection.cell_cycle_snapshot
projection.area_cycle_snapshot
```

**Invariantes transversais:** idempotência por evento; nenhuma avaliação de risco para rejeitados/quarentenados; uma avaliação por leitura aceite/elegível; tentativas e erros auditáveis; nenhum item fica indefinidamente em estado ativo depois da convergência.

### 7. Perfis de degradação

Perfis atuais:

```text
none
missing-readings
noise
bias
drift
stuck-value
outlier
clipping/range
lag/delay
duplicate
out-of-order
retry-transient
```

Podem ser combinados através de `degradationProfiles`; quando existem vários, `none` é removido. Cada perfil exige uma asserção semântica própria — não basta verificar que a run terminou. Consultar [scenario-acceptance-invariants.md](scenario-acceptance-invariants.md).

### 8. Risco, projeções e alertas

**UI:** `/risk`, `/dashboard`, `/runs`.

**API:** estado operacional de área/células, alertas ativos, runtime summary e audit de run.

**Invariantes:** score e componentes vêm de persistência; freshness/coverage/eligibility não são inventados no browser; alertas têm transições consistentes e não duplicam abertura para a mesma chave operacional.

### 9. Diagnósticos preparados

**UI:** `/queries`.

**API:**

```text
GET  /api/control/runtime/diagnostics
POST /api/control/runtime/diagnostics/{diagnosticId}
```

A lista atual contém 28 diagnósticos e é obtida do catálogo do backend. A automação não deverá manter uma lista independente; deve listar o catálogo e executar cada ID devolvido. Consultar [runtime-diagnostic-catalog.csv](generated/runtime-diagnostic-catalog.csv).

### 10. Observabilidade e evidence runtime

**UI:** `/pipeline`, `/evidence`, `/deployment-health`.

**API:**

```text
GET /health/live
GET /health/ready
GET /api/control/runtime/observability/health
GET /api/control/runtime/observability/rabbitmq
GET /api/control/runtime/observability/evidence
GET /api/control/runtime/observability/evidence/{evidenceId}
```

**Invariantes:** indisponível não equivale a saudável; zero só é apresentado quando medido; downloads ficam dentro da allowlist; segredos não são incluídos em artefactos; evidence histórica não é promovida automaticamente a prova da execução atual.

### 11. Qualidade, evidence, deployment, cloud e aprovações

**UI:** `/qa`, `/evidence`, `/deployments`, `/cloud`, `/approvals`, `/admin`.

Estas superfícies usam um catálogo fechado. O browser não fornece comandos arbitrários. Um endpoint de arranque pode exigir apenas uma capability de leitura à entrada do controller, mas o serviço volta a validar a `requiredCapability` da operação selecionada.

O estado atual de cada operação — incluindo as deliberadamente bloqueadas — está em [operation-catalog.csv](generated/operation-catalog.csv).

### 12. Reset e recuperação

**API:** `POST /api/control/runtime/reset`.

Confirmação exata:

```text
RESET_RUNTIME_STATE
```

O reset exige quiescência, suporta dry-run e deve preservar catálogos, identidades e configuração. A aceitação futura deve provar PostgreSQL, RabbitMQ e InfluxDB quando `requireExternalStores=true`, e uma run nominal após recuperação.

### 13. Validação negativa P3

**UI:** `/p3` para leitura/execução controlada em Development/Evidence.

**API:**

```text
GET  /api/dev/controlled-validation/p3
POST /api/dev/controlled-validation/p3/run
```

A execução real exige ambiente não produtivo, autenticação, prefixo de run permitido e confirmação explícita no wrapper `scripts/reliability/run-controlled-validation-p3.py`. O resultado necessita sempre de auditoria persistida; um HTTP 2xx isolado não prova os casos negativos.

## Estado das rotas atuais

A rota suportada é diretamente `/<page>`, por exemplo `/simulation` ou `/pipeline`. `/dev/runtime` e `/ui-v2` pertencem a iterações antigas e não são superfícies atuais montadas.

O registo de rotas foi reconciliado na Fase 2:

- `/quality` está montada e registada no `UI_PAGE_REGISTRY`, protegida por `quality.read` e incluída na navegação técnica de QA;
- `/qa` permanece a superfície técnica para QA/evidence;
- `/qa-tests` está explicitamente retirada;
- `/db-queries` redireciona para `/queries`.

O teste `tests/acceptance/test_final_acceptance_contract.py` e o modo `--check` do gerador de catálogos impedem que uma rota montada volte a ficar sem registo canónico.

## Fronteira de aceitação

A existência de código ou de testes unitários não equivale a aceitação final. Para cada linha da matriz de rastreabilidade, a campanha futura deverá produzir um dos seguintes estados:

```text
PASS
FAIL
BLOCKED_PREREQUISITE
NOT_SELECTED
HARNESS_ERROR
```

Uma capacidade selecionada que não correu não pode ser marcada como `PASS` nem ser coberta por evidence histórica.
## Aceitação funcional P0 atual

A campanha `Functional`/`Full` executa `scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1` para fechar, numa única execução atual, as lacunas live de perfis, RBAC, diagnósticos, alertas, observabilidade, evidence e shutdown. O harness produz evidência por run e falha quando um perfil selecionado não apresenta o efeito semântico esperado.

A existência do harness não equivale a prova runtime. O snapshot desta fase foi validado estaticamente; a entrega final exige uma campanha atual em Windows/.NET/Docker.
