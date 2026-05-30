# Pesquisa II vs implementation state

Este documento acompanha o paralelismo entre a metodologia V1 descrita na Pesquisa II e o estado atual do repositório. A leitura correta é técnica/metodológica: o score NatureProtector, FWI e KBDI são Candidate Parameter Set V1.0 e comparação/proveniência, não validação científica final.

## Matriz principal

| Tema Pesquisa II | Estado no repo | Ficheiros principais | Implementado? | Limitações | Evidência/testes |
| --- | --- | --- | --- | --- | --- |
| Fórmula NatureProtector M/D/T | `BaseRisk = 0.50M + 0.20D + 0.30T`; `AdjustedScore` aplica C/I; `RiskScore` preserva compatibilidade com adjusted. | `src/NatureProtector.Prevention/Risk/CandidateParameterSetV1.cs`; `src/NatureProtector.Prevention/Risk/SimpleRiskScoringService.cs`; `src/NatureProtector.Core/Risk/RiskAssessment.cs` | Sim | Pesos são candidatos, não calibrados cientificamente. | `tests/NatureProtector.Prevention.Tests/Risk/SimpleRiskScoringServiceTests.cs`; `tests/NatureProtector.Prevention.Tests/Risk/CandidateParameterSetV1Tests.cs` |
| Território H/F/G | `T = 0.50H + 0.30F + 0.20G`; suporta defaults candidatos/proveniência quando dado territorial falta. | `src/NatureProtector.Prevention/Risk/TerritorialRiskContext.cs`; `src/NatureProtector.Prevention/Risk/RiskInput.cs` | Sim/parcial | Depende da qualidade dos dados territoriais disponíveis por célula. | `tests/NatureProtector.Prevention.Tests/Risk/TerritorialRiskContextTests.cs` |
| C/I | Confiança observacional e integridade operacional alimentam o score ajustado e penalizam inputs parciais/low coverage. | `CandidateParameterSetV1.cs`; `RiskInput.cs`; `SimpleRiskScoringService.cs` | Sim | Fatores são parâmetros candidatos. | `RiskInputTests.cs`; `SimpleRiskScoringServiceTests.cs` |
| FWI | Calculador C# produz FFMC, DMC, DC, ISI, BUI, FWI, normalizedFWI, status e limitations. | `src/NatureProtector.Prevention/Risk/CanadianFireWeatherIndexCalculator.cs`; `FireWeatherIndexResult.cs`; `FireWeatherIndexContext.cs` | Sim/parcial | `CompleteWithCandidateDefaults` indica defaults antecedentes; sem equivalência oficial IPMA/EFFIS. | `tests/NatureProtector.Prevention.Tests/Risk/CanadianFireWeatherIndexCalculatorTests.cs`; `FireWeatherIndexContextTests.cs` |
| KBDI | Calculador candidato produz previousKbdi, kbdi, normalizedKbdi = KBDI/800, status e limitations. | `src/NatureProtector.Prevention/Risk/CandidateKbdiCalculator.cs`; `KbdiResult.cs`; `DailyCellState.cs` | Sim/parcial | Precipitação média anual/proxy e antecedente podem ser candidate defaults. | `tests/NatureProtector.Prevention.Tests/Risk/CandidateKbdiCalculatorTests.cs` |
| RiskInput | Fronteira pré-scoring, com run/área/célula/janela, métricas normalizadas, DailyCellState, contexto territorial, flags e elegibilidade. | `src/NatureProtector.Prevention/Risk/RiskInput.cs`; `RiskInputMetricSet.cs`; `ReadingRiskPipeline.cs` | Sim/parcial | Ainda existe adapter de compatibilidade a partir de leitura individual. | `tests/NatureProtector.Prevention.Tests/Risk/RiskInputTests.cs`; `tests/NatureProtector.Prevention.Host.Tests/...ReadingRiskPipeline...` |
| QualityFlags/Classifiers | Flags tipadas e classifiers com status/severity/action; strings ficam em fronteiras. | `QualityFlag.cs`; `ClassifierResult.cs`; `ClassifierAction.cs`; `ReadingTemporalClassifier.cs`; `ReadingRangeClassifier.cs` | Sim/parcial | Nem todos os perfis futuros têm classificador completo end-to-end. | `ClassifierResultTests.cs`; `ReadingRangeClassifierTests.cs` |
| Eligibility | Estados `CompleteEligible`, `PartialButUsable`, `Blocked`; 3/3 métricas mínimas é completo, 2/3 é parcial, 1/3 fica `PartialButUsable` com `low_coverage` e C/I penalizados por compatibilidade, 0/3 fica `Blocked`. | `RiskEligibilityService.cs`; `RiskInput.cs`; `RiskInputMetricSet.cs` | Sim/parcial | Caminhos legados ainda existem para compatibilidade; low coverage não deve ser confundido com risco físico. | `RiskEligibilityServiceTests.cs`; `RiskInputTests.cs` |
| DailyCellState | Estado por área/célula/dia/run, com FWI/KBDI e memória diária. | `DailyCellState.cs`; `IDailyCellStateRepository.cs`; `PostgresDailyCellStateRepository.cs`; migrations FWI/KBDI | Sim | Aplicação real das migrations não foi validada neste ambiente sem PostgreSQL. | `PostgresDailyCellStateRepositoryTests.cs`; migrations `AddDailyCellFireWeatherSubcomponents`, `AddDailyCellKbdiStatus` |
| Scenario B/C | B preserva perfil `none`; C usa degradação operacional `missing-readings` por defeito. | `SimulationDegradationProfiles.cs`; `ScenarioContextFactory.cs`; `SimulationRunner.cs`; `ReadingGenerationService.cs` | Sim | Runtime real depende de Docker/PostgreSQL/RabbitMQ. | `ReadingGenerationServiceTests.cs`; `SimulationRunnerTests.cs`; smoke script |
| degradationProfiles | Plural suportado com compatibilidade legacy `degradationProfile`; UI usa checkboxes. | `RuntimeOperationsContracts.cs`; `SimulatorOptions.cs`; `Workspace.tsx`; `DeveloperRuntimeControl.tsx` | Sim | Perfis avançados como delay/out-of-order ainda são frente futura. | `RuntimeOperationsServiceTests.cs`; `ScenarioContextFactoryTests.cs`; `npm run build` |
| Freshness | Projeções/API/UI expõem `Fresh`, `Stale`, `Expired`. | `OperationalProjectionStatus.cs`; `ProjectionRecords.cs`; `ControlPlaneResponses.cs`; `Workspace.tsx` | Sim | Validação runtime real ainda pendente se infra indisponível. | `OperationalProjectionStatusTests.cs`; `PostgresAreaOperationalProjectionStoreTests.cs` |
| Coverage | Projeções/API/UI expõem `Complete`, `Partial`, `LowCoverage`, `Blocked`, `NoRecentData`. | `OperationalProjectionStatus.cs`; `RiskInput.cs`; `PostgresAreaOperationalProjectionStore.cs` | Sim | Low coverage é degradação operacional, não risco físico. | `RiskInputTests.cs`; `OperationalProjectionStatusTests.cs` |
| Carry-forward | Estado operacional distingue valor atual, carry-forward e expired carry-forward. | `OperationalProjectionStatus.cs`; projection stores; API DTOs; UI | Sim | Política candidata; precisa validação em runs longas. | `OperationalProjectionStatusTests.cs`; API tests |
| AlertState | `V1AlertPolicy` mantém `None`, `Warning`, `Alarm`; blocked não deve abrir novo alerta. | `V1AlertPolicy.cs`; `projection.alert_state`; API DTOs | Sim/parcial | Persistência mínima/cooldown completos dependem da política candidata existente. | `PostgresAreaOperationalProjectionStoreTests.cs`; alert policy tests |
| Runtime Monitor | Mostra score NP, FWI, KBDI, componentes, freshness/coverage/carry-forward e perfis ativos. | `webUI/src/app/components/views/Workspace.tsx`; `webUI/src/app/types/index.tsx` | Sim | Confirmação visual depende de runtime local. | `npm run build` |
| Evidence B/C | Script smoke B/C exporta summary, runs, audits, diagnostics, `compare-b-vs-c.json`, `np-vs-fwi-kbdi.json`, components e limitations. | `scripts/evidence/run-v1-bc-smoke.ps1`; `docs/evidence/runs/...` | Sim/parcial | Execução real fica bloqueada se API/Docker/PostgreSQL/RabbitMQ indisponíveis. | `run-v1-bc-smoke.ps1 -DryRun`; runtime real quando disponível |
| NP vs FWI/KBDI | API diagnostics, summary, audit e UI expõem comparação técnica lado a lado. | `PostgresControlPlaneService.cs`; `ControlPlaneResponses.cs`; `Workspace.tsx`; `run-v1-bc-smoke.ps1` | Sim/parcial | Valores nulos/Missing/Partial devem ser apresentados sem claim científica. | `RuntimeSummaryServiceTests.cs`; `RuntimeOperationsServiceTests.cs`; UI build |
| Validação técnica vs científica | Docs e UI declaram que FWI/KBDI são comparação/proveniência e score NP é candidato. | `docs/NatureProtector-V1-overview.md`; `docs/contracts/v1-vocabulary-map.md`; `Workspace.tsx` | Sim | Validação científica final continua fora da V1. | Este documento; docs atualizados; tests README |

## Limitações atuais

- PostgreSQL/Docker/RabbitMQ podem não estar disponíveis no ambiente local; nesses casos não há validação runtime real e o smoke deve ser tratado como dry-run/documentado.
- As migrations novas são reconhecidas pelo EF quando o projeto compila, mas a aplicação à base de dados exige PostgreSQL local disponível.
- Coverage recente está em cerca de `87.6%` line e `76.8%` branch devido a API/diagnostics/glue runtime novos; a cobertura precisa de reforço focado, não de testes artificiais.
- FWI/KBDI não são validação científica do score NatureProtector; são índices de comparação/proveniência com status e limitations.

## Comandos de validação

```powershell
dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config
dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1
npm run build --prefix .\webUI
dotnet-ef migrations list --project .\src\NatureProtector.Infrastructure.Postgres\NatureProtector.Infrastructure.Postgres.csproj --startup-project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj --context NatureProtectorControlDbContext
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\run-v1-bc-smoke.ps1 -DryRun
```

## Atualizacao 2026-05-30: daily_reference, FWI/KBDI e smoke real

A auditoria runtime mostrou que os cenarios `scenario_b` e `scenario_c` tinham `daily_reference` em `control.scenario_definitions.ParametersJson`, mas esse contexto nao era materializado em `DailyCellState`. A consequencia era:

- `projection.daily_cell_state."DailyPrecipitationMillimeters"` nulo em runs novas;
- FWI em `Partial` com `precipitation_24h_missing`;
- KBDI em `Missing`;
- UI com NP Score calculado, mas comparacao FWI/KBDI incompleta apesar de `precipitation_total_mm = 0.0` existir no cenario.

Correcao implementada:

- `PostgresDailyCellStateRepository` le `ParametersJson.daily_reference` da definicao de cenario associada ao `SimulationRunId`;
- `DailyCellState` materializa precipitacao diaria, temperatura maxima, humidade, vento, `fire_index_reference_kind` e provenance `scenario_daily_reference`;
- `precipitation_total_mm = 0.0` e tratado como valor valido, nao como missing;
- `ReadingRiskPipeline` recarrega o `DailyCellState` atualizado antes do scoring para que `RiskAssessment` use o contexto diario efetivo;
- diagnostics/API/UI expõem `dailyPrecipitationMillimeters`, NP vs FWI/KBDI, contexto diario e efeitos de degradacao.

Colunas que continuam nullable por desenho:

| Campo | Razao |
| --- | --- |
| FWI/KBDI em `projection.daily_cell_state` | Runs antigas ou cenarios sem inputs suficientes podem manter `Missing`/`Partial`. |
| `DailyPrecipitationMillimeters` | Deve ficar nulo quando nao existe `daily_reference` nem fonte diaria valida. |
| campos FWI/KBDI em `projection.risk_assessment_log` | O log de assessment guarda componentes NP; a comparacao FWI/KBDI vem de `DailyCellState`/diagnostics. |
| imported/reference FWI/KBDI separados | Ainda nao existem colunas separadas para valor calculado vs valor de referencia importado; provenance/limitations distinguem a semantica atual. |

Smoke real validada em `docs/evidence/runs/v1-bc-smoke-20260530-145056-123/`:

| Campo | Scenario B | Scenario C |
| --- | ---: | ---: |
| Run id | `3cca562e-8742-4427-8f3f-3b27716a4fb6` | `b35e0fd4-fc8e-48c4-873e-017633c4ecee` |
| Status | `Completed` | `Completed` |
| Accepted / risk / missing | `30 / 30 / 0` | `24 / 24 / 6` |
| Rejected / quarantined | `0 / 0` | `0 / 0` |
| FWI status latest-run | calculado no diagnostic | `CompleteWithCandidateDefaults` |
| KBDI status latest-run | calculado no diagnostic | `Complete` |
| Daily precipitation latest-run | materializada via `scenario_daily_reference` | `0` |

Artefactos principais: `summary.md`, `np-vs-fwi-kbdi.json`, `daily-cell-state.json`, `degradation-effects.json`, `compare-b-vs-c.json`.

## Atualizacao 2026-05-30: classes, KBDI temporal e contexto portugues

| Tema Pesquisa II | Estado no repo | Ficheiros principais | Implementado? | Limitacoes | Evidencia/testes |
| --- | --- | --- | --- | --- | --- |
| FWI IPMA classification | FWI exposto com classe IPMA, classe EFFIS auxiliar e distancia ao proximo limiar. | `src/NatureProtector.Prevention/Risk/IndexClassifications.cs`; `src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs`; `webUI/src/app/components/views/Workspace.tsx` | Sim | Interpretativo; nao reproduz produto oficial IPMA. | `tests/NatureProtector.Prevention.Tests/Risk/IndexClassificationsTests.cs`; API/UI build |
| KBDI temporal semantics | KBDI tratado como estado diario; mesmo LogicalDate nao avanca por evento. Primeiro dia sem serie antecedente fica `LimitedAntecedentHistory`. | `CandidateKbdiCalculator.cs`; `PostgresDailyCellStateRepository.cs`; `InMemoryDailyCellStateRepository.cs` | Sim/parcial | Nao ha serie historica longa materializada; valor continua candidato quando historico e limitado. | `CandidateKbdiCalculatorTests.cs`; `PostgresDailyCellStateRepositoryTests.cs` |
| KBDI dryness class | KBDI exposto com classe de secura 0..800. | `IndexClassifications.cs`; API DTOs; `Workspace.tsx` | Sim | Secura nao e perigo final de incendio. | `IndexClassificationsTests.cs` |
| NatureProtector risk class | NP Score exposto com classe candidata 0..1. | `IndexClassifications.cs`; `PostgresControlPlaneService.cs`; `Workspace.tsx` | Sim | Thresholds candidatos, sem calibracao cientifica. | `IndexClassificationsTests.cs`; API tests |
| PortugueseContextRiskProxy | Proxy candidato combina classe FWI IPMA e territorio T/H/F/G para leitura portuguesa. | `IndexClassifications.cs`; `PostgresControlPlaneService.cs`; `Workspace.tsx` | Sim | Nao e RCM/PIR/IPMA oficial; nao usa matriz oficial nem perigosidade rural oficial ICNF. | `IndexClassificationsTests.cs`; diagnostic `latest-run-portuguese-context-proxy` |
| Local FWI percentile/anomaly | Preparado como status explicito. | `PostgresControlPlaneService.cs`; `ControlPlaneResponses.cs`; `Workspace.tsx` | Parcial | `NotAvailable` ate haver distribuicao historica local materializada. | API/UI build; smoke dry-run |
| Calculated vs reference FWI/KBDI | API distingue campos calculados e referencia quando a proveniencia permite. | `ControlPlaneResponses.cs`; `PostgresControlPlaneService.cs`; `Workspace.tsx` | Parcial | Ainda nao ha colunas separadas; separacao e por contrato/API/provenance. | API tests; UI build |
| Degradation effects audit | Diagnostic distingue perfil inativo, efeito observado, below threshold e not materialized; noise inativo nao e apresentado como efeito injetado. | `PostgresControlPlaneService.cs`; `run-v1-bc-smoke.ps1` | Sim/parcial | TruthSnapshot degradado nao e persistido, logo noise ativo ainda e estimado. | diagnostics; smoke dry-run |

Leitura correta: estas adicoes melhoram interpretabilidade para Portugal, mas nao implementam RCM oficial, PIR oficial, matriz IPMA oficial nem validacao cientifica final.
