# Scenario Run Orchestrator (O1/O1.2)

## Objetivo

Permitir execuções reprodutíveis de cenários V1 através de um `run-spec.json`,
sem editar manualmente scripts, CSV, manifestos ou bootstrap em cada run.

O1.1 criou a camada local de orquestração por PowerShell:

- `scripts/scenarios/run-scenario.ps1`
- `scripts/scenarios/run-spec.schema.json`
- `scripts/scenarios/examples/scenario-b-default.json`

O1.2 adicionou suporte real no `Simulator.Host` para `Simulator:RunOverrides:*`,
incluindo persistência de valores pedidos e resolvidos em `SimulationRun.MetadataJson`.

## Âmbito O1/O1.2

- validação base do run spec;
- validação de pré-condições, incluindo Docker, PostgreSQL, área e cenário;
- bloqueio conservador de run paralela por defeito;
- execução de `Simulator.Host` com env vars `Simulator:RunOverrides:*`;
- artefactos por run em `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/`;
- geração de `run-spec.resolved.json` e `summary.md`;
- recolha opcional de evidência runtime;
- seleção determinística de sensores quando `sensorCount` e `seed` são fornecidos;
- persistência de overrides pedidos e resolvidos em `MetadataJson`.

## RunOverrides suportados

O `Simulator.Host` suporta os seguintes overrides:

| Run override | Config key | Papel |
|---|---|---|
| `SensorCount` | `Simulator:RunOverrides:SensorCount` | Número de sensores a selecionar para a run. |
| `NumberOfCycles` | `Simulator:RunOverrides:NumberOfCycles` | Número de ciclos gerados. |
| `IntervalSeconds` | `Simulator:RunOverrides:IntervalSeconds` | Intervalo lógico entre ciclos. |
| `Seed` | `Simulator:RunOverrides:Seed` | Seed para seleção determinística e reprodutibilidade. |
| `DegradationProfile` | `Simulator:RunOverrides:DegradationProfile` | Perfil operacional pedido para a run. |
| `OrchestratorCorrelationId` | `Simulator:RunOverrides:OrchestratorCorrelationId` | Correlação entre orquestrador, run e evidência. |

## Precedência

A precedência efetiva é:

```text
run-spec/env overrides
  > scenario parameters/control plane
  > appsettings
```

O `run-spec.json` é resolvido pelo script para variáveis de ambiente usadas
pelo `Simulator.Host`. O host guarda em `MetadataJson` tanto o bloco `requested`
como o bloco `resolved`, permitindo distinguir o que foi pedido do que foi
efetivamente aplicado.

Quando `SensorCount` é fornecido, a seleção de sensores é determinística com
base na seed resolvida. Isto permite comparar runs e repetir evidência sem
editar manualmente manifestos, CSV ou bootstrap.

## Política de segurança operacional

Por defeito (`allowParallelRun=false`), a execução é bloqueada se existir qualquer
entrada em `control.simulation_runs` sem `EndedAt`.

## Compatibilidade futura (Backoffice/API)

O `run-spec.json` deve ser tratado como contrato operacional provisório da O1,
com intenção de compatibilidade futura com um request body de API de orquestração.

A lógica de negócio pode migrar do script para serviços C# reutilizáveis numa
fase posterior:

- validação de spec;
- resolução de precedência;
- seleção de sensores;
- persistência de requested/resolved overrides;
- mapeamento de estado de run.

## Exemplo de execução

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\scenarios\run-scenario.ps1 `
  -SpecPath .\scripts\scenarios\examples\scenario-b-default.json
```

## Artefactos esperados

- `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/simulator-host.log`
- `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/run-spec.resolved.json`
- `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/summary.md`
- `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/v1-runtime-evidence-*.md` quando `collectEvidence=true`

## Run validada de referência

A evidência recente inclui uma run de referência com:

| Campo | Valor observado |
|---|---|
| `areaCode` | `proenca-a-nova` |
| `scenarioCode` | `scenario_b` |
| `sensorCount` | `6` |
| `numberOfCycles` | `5` |
| `intervalSeconds` | `5` |
| `seed` | `12345` |
| estado | `Completed` |
| overrides | `observed_match` |

Esta run confirma a transição de O1.1 para O1.2: os overrides deixam de ser
apenas pedido do script e passam a ser observáveis no comportamento do
`Simulator.Host` e na metadata da run.
