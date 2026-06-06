# Query Pack de Auditoria de Dados - Momento 2

## Objectivo

Esta pasta contém tooling read-only para auditar dados PostgreSQL e InfluxDB no contexto da V3/Momento 2.

O objectivo é produzir evidência reprodutível sobre:

- inventário de schema;
- contagens;
- colunas críticas;
- intervalos temporais;
- distribuições relevantes para M1/M3/M5;
- rastreabilidade positiva para M5;
- amostras pequenas e sem dumps brutos.

Este tooling não treina modelos, não exporta datasets ML finais e não altera bases de dados.

## Escopo

Incluído:

- queries PostgreSQL read-only;
- queries InfluxDB 3 read-only;
- runners PowerShell para produzir outputs datados;
- modelo de manifesto.

Excluído:

- dataset exporter;
- baseline model;
- notebooks;
- GNN;
- ML runtime;
- model registry;
- dumps completos.

## Pré-requisitos

PostgreSQL:

- `psql` disponível no `PATH`;
- connection string fornecida explicitamente pelo utilizador.

InfluxDB:

- `influxdb3` disponível no `PATH`;
- database/bucket fornecido explicitamente;
- token apenas por ambiente, se necessário para a ferramenta local.

## Como correr PostgreSQL

Exemplo:

```powershell
.\tools\data-audit\run-postgres-audit.ps1 `
  -ConnectionString "Host=localhost;Port=5433;Database=natureprotector;Username=np;Password=***"
```

O runner:

- cria uma pasta em `docs/evidence/ml/momento2/runs/<timestamp>/`;
- cria subpastas `postgres/` e `summaries/`;
- executa os scripts em `postgres/`;
- grava CSVs;
- grava `manifest.md`;
- redige password/token na connection string registada no manifesto.

## Como correr InfluxDB

Exemplo:

```powershell
.\tools\data-audit\run-influx-audit.ps1 -Database "np_telemetry"
```

O runner:

- assume `influxdb3 query`;
- usa token apenas via ambiente, se a instalação local o exigir;
- não grava token em ficheiro;
- cria outputs em `docs/evidence/ml/momento2/runs/<timestamp>/influx/`;
- grava `manifest.md`.

## Política de secrets

- Não colocar tokens nos scripts.
- Não colocar passwords nos SQL.
- Não imprimir tokens.
- Connection strings são redigidas no manifesto.
- Se um token aparecer em output de ferramenta, esse output não deve ser commitado.

## Política de dados brutos

Este query pack evita dumps completos.

As amostras são pequenas e têm `LIMIT 50`. Campos volumosos como payload/envelope/GeoJSON são evitados quando possível.

## Interpretação por modelo

| Modelo | Como usar os resultados |
|---|---|
| M1 | Avaliar disponibilidade de leituras, scores, níveis de risco, runs, sensores, áreas e séries temporais |
| M3 | Verificar se há rejeições, quarentenas, outcomes, stages e códigos de erro suficientes |
| M5 | Confirmar joins positivos entre inbox, attempts, leituras aceites, avaliações de risco e snapshots |

## Extensão P0 de validação controlada

O script PostgreSQL `08_controlled_validation_p0.sql` acrescenta outputs read-only para os casos P0 dos cenários controlados V3:

- `rejected_by_reason.csv`;
- `quarantined_by_reason.csv`;
- `processing_errors_by_code.csv`;
- `duplicate_mismatch_summary.csv`;
- `negative_traceability_m5.csv`;
- `expected_vs_observed_fault_cases.csv`;
- `scenario_profile_summary.csv`.

Estes outputs usam apenas schema existente (`pipeline.*` e `projection.*`) e procuram `fault_case_id` a partir da convenção `CorrelationId = cv:<run_label>:<fault_case_id>:<sequence>`. Para `invalid_json`, a rastreabilidade pode depender do marcador bruto no `RawBodyUtf8` e do sidecar manifest com hash do raw body, porque o envelope pode não ser parseável antes do inbox. O script não trata `missing-readings` como negativo M3 e não converte `Blocked` em score.

## Extensão P1 de validação controlada

O script PostgreSQL `09_controlled_validation_p1.sql` acrescenta outputs read-only para retry e falhas de processamento P1:

- `retry_summary.csv`;
- `retry_transitions.csv`;
- `retry_then_success.csv`;
- `retry_to_quarantine.csv`;
- `processing_faults_by_case.csv`;
- `p1_expected_vs_observed.csv`;
- `p1_negative_traceability_m5.csv`.

Estes outputs destinam-se a validar N5 `transient_failure -> retry -> success` e N6 `permanent_failure -> quarantine`. N7 `sensor_inactive` e N8 `area_mismatch` aparecem no expected-vs-observed P1, mas só devem ser tratados como fechados depois de existir fixture segura P1.5.

## Extensão P3 de validação controlada

O script PostgreSQL `11_controlled_validation_p3_negative_pipeline.sql` acrescenta outputs read-only para fecho P3 de outcomes negativos, retries e readiness M3/M5:

- `p3_expected_vs_observed.csv`;
- `p3_rejected_by_fault_case.csv`;
- `p3_quarantined_by_fault_case.csv`;
- `p3_retry_paths_by_fault_case.csv`;
- `p3_processing_attempts_by_fault_case.csv`;
- `p3_m3_label_support.csv`;
- `p3_negative_m5_traceability.csv`;
- `p3_unexpected_accepted_or_risk.csv`;
- `p3_blocked_or_skipped_cases.csv`.

O pack valida rejeições técnicas, quarentenas sem projeções positivas indevidas, `retry -> success` e `retry -> quarantine`. `sensor_inactive` e `sensor_area_mismatch` são mantidos como `blocked_needs_fixture` quando a base de controlo não tem sensor inativo nem segunda área segura; não se deve alterar sensores reais nominais para fabricar estes casos.

## Relação com Momento 2

Momento 2 só deve ser fechado depois de uma execução controlada produzir manifesto, CSVs e análise das lacunas.

## Relação com Momento 3

Momento 3 só deve avançar depois de existirem:

- dataset contract;
- leakage policy;
- split policy;
- query pack/manifest;
- decisão de viabilidade por M1/M3/M5.
