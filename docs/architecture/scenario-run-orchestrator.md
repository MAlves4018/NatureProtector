# Scenario Run Orchestrator (O1.1)

## Objetivo

Permitir execuções reprodutíveis de cenários V1 através de um `run-spec.json`,
sem editar manualmente scripts/CSV/control plane em cada run.

Nesta O1.1 a implementação é local, com PowerShell:

- `scripts/scenarios/run-scenario.ps1`
- `scripts/scenarios/run-spec.schema.json`
- `scripts/scenarios/examples/scenario-b-default.json`

## Âmbito O1.1

- validação base do run spec;
- validação de pré-condições (Docker/Postgres/área/cenário);
- bloqueio conservador de run paralela por defeito;
- execução de `Simulator.Host` com env vars;
- artefactos por run em `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/`;
- geração de `run-spec.resolved.json` e `summary.md`;
- recolha opcional de evidência runtime.

## Política de segurança operacional

Por defeito (`allowParallelRun=false`), a execução é bloqueada se existir qualquer
entrada em `control.simulation_runs` sem `EndedAt`.

## Limitações conhecidas em O1.1

- `Simulator:RunOverrides:*` pode não estar aplicado no Host até O1.2.
- `sensorCount` é tratado como pedido e pode ficar em
  `requested_not_confirmed_pending_host_support`.
- `orchestratorCorrelationId` é enviado como env var, mas a correlação direta por
  `MetadataJson` depende do suporte de O1.2.

## Compatibilidade futura (Backoffice/API)

O `run-spec.json` deve ser tratado como contrato operacional provisório da O1,
com intenção de compatibilidade futura com um request body de API de orquestração.

A lógica de negócio deve migrar do script para serviços C# reutilizáveis na O1.2+:

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
- `docs/evidence/runs/<timestamp>-<scenarioCode>-<runLabel>/v1-runtime-evidence-*.md` (se `collectEvidence=true`)
