# Matriz PesquisaII vs Repo

Auditoria read-only ao repositorio NatureProtector em 2026-05-19. A comparacao usa a cadeia V1 indicada na PesquisaII:

`ScenarioDefinition -> DailyCellState -> TruthSnapshot -> LocalObservation -> OperationalEvent -> NormalizedReading -> RiskInput -> RiskAssessment -> AlertState -> OperationalProjection`

Nota de metodo: o PDF `PesquisaII.pdf` existe em `docs/planning/diary/MiguelAlves/DocsIntermedios/PesquisaII.pdf`, mas este ambiente nao tinha extrator PDF disponivel (`pypdf`, `PyPDF2`, `pdfplumber`, `fitz/pymupdf` ausentes). Por isso, usei como fonte metodologica a lista de pontos fornecida na prompt e docs derivadas do repo apenas como contexto. Documentacao nao foi usada como prova de implementacao, salvo quando o estado e `Documentado apenas`.

## 1. Sumario executivo

- Implementado: 44 pontos.
- Parcial: 12 pontos.
- Ausente: 0 pontos.
- Documentado apenas: 2 pontos.
- Nao confirmado: 0 pontos.
- Drift: 2 pontos.
- Implementado alem do esperado: 0 pontos.
- Bloqueado por regressao: 0 pontos.

Leitura critica: o repo esta operacionalmente forte para a demo V1: simulador, cenarios B/C, RabbitMQ, pipeline, inbox duravel, retry/quarantine, recovery, `SimulationRunId`, projections, alertas V1 e evidence runtime por run estao cobertos por codigo/testes/evidencia. As lacunas principais nao sao bloqueios de demo, mas ainda sao relevantes para fechar a V1 metodologica: `DailyCellState` ainda conserva semantica de sensor em partes do modelo/in-memory, `RiskInput` ainda e parcialmente canonico, quality flags continuam armazenadas como strings com catalogo tipado derivado, score usa escala normalizada [0,1] em vez de `round(100 * ...)`, e alguns pontos como vizinhanca 8, recomendacoes operacionais e relatorio final estao incompletos ou documentais.

## 2. Estado global

| Estado | Quantidade |
|---|---:|
| Implementado | 44 |
| Parcial | 12 |
| Ausente | 0 |
| Documentado apenas | 2 |
| Nao confirmado | 0 |
| Drift | 2 |
| Implementado alem do esperado | 0 |
| Bloqueado por regressao | 0 |

## 3. Matriz principal

| ID | Ponto esperado na V1 | Estado no repo | Evidencia encontrada | Gap / problema | Risco | Prioridade | Acao recomendada |
| -- | -------------------- | -------------- | -------------------- | -------------- | ----- | ---------- | ---------------- |
| V1-001 | `ScenarioDefinition` versionado | Implementado | `src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`; `data/manifests/scenarios/proenca-a-nova-scenarios.generated.json`; testes `PostgresSimulationContextSourceTests` | Sem gap material. | Baixo. | P2 | Manter manifests e control plane sincronizados. |
| V1-002 | `DailyCellState` por celula/dia/run | Parcial | `src/NatureProtector.Prevention/Risk/DailyCellState.cs`; `src/NatureProtector.Prevention.Host/Persistence/PostgresDailyCellStateRepository.cs`; `NatureProtectorControlDbContext.cs` indice unico Area/GridCell/Date/Run; testes `DailyCellStateTests` | PostgreSQL esta por celula/dia/run, e `ApplyRiskInput` ja nao exige sensor igual. Mas o modelo ainda tem `SensorId` obrigatorio e o repositorio in-memory ainda usa chave Area/Sensor/Day/Run. | Pode mascarar divergencia em testes/standalone e confundir o conceito metodologico. | P1 | Transformar `SensorId` em last-source/provenance e alinhar in-memory com GridCell. |
| V1-003 | `TruthSnapshot` formal | Implementado | `src/NatureProtector.Simulator.Host/Readings/TruthSnapshot.cs`; `ReadingGenerationService.cs`; testes do simulador | Sem gap material. | Baixo. | P2 | Manter interno ao simulador. |
| V1-004 | `LocalObservation` formal | Implementado | `src/NatureProtector.Simulator.Host/Readings/LocalObservation.cs`; testes `ReadingGenerationServiceTests` | Sem gap material. | Baixo. | P2 | Expandir apenas quando houver novos profiles. |
| V1-005 | `OperationalEvent` interno | Implementado | `src/NatureProtector.Prevention/Readings/OperationalEvent.cs`; testes `OperationalEventTests` | Sem gap material. | Baixo. | P2 | Manter como adapter interno. |
| V1-006 | `NormalizedReading` com qualidade | Implementado | `src/NatureProtector.Prevention/Readings/NormalizedReading.cs`; testes `NormalizedReadingTests` | Qualidade e classifiers sao preservados. | Baixo. | P2 | Persistir resumo se precisar de auditoria mais rica. |
| V1-007 | `RiskInput` canonico | Parcial | `src/NatureProtector.Prevention/Risk/RiskInput.cs`; `RiskInputMetricSet.cs`; testes `RiskInputTests` | Tem run, celula, janela, leituras, metricas, flags, classifiers, eligibility, integridade e contexto diario. Ainda e construido a partir de leitura individual e nao fecha totalmente input multi-leitura canonico. | Metodologico: pode limitar explicabilidade cientifica da V1. | P1 | Consolidar construcao multi-metrica por janela/celula. |
| V1-008 | `CompleteEligible`, `PartialButUsable`, `Blocked` | Implementado | `RiskInputStatus.cs`; `RiskEligibilityResult.cs`; `RiskEligibilityService.cs`; testes `RiskEligibilityServiceTests` | Sem gap material. | Baixo. | P2 | Manter testes regressivos. |
| V1-009 | `Blocked != RiskScore 0` | Implementado | `SimpleRiskScoringService.Score` lanca para `Blocked`; `ReadingRiskPipeline` nao chama scoring quando bloqueado; testes `ReadingRiskPipelineTests` | Sem gap material. | Baixo. | P2 | Manter regra. |
| V1-010 | quality flags tipadas | Parcial | `QualityFlag.cs`; `QualityFlagCatalog`; `RiskInput.TypedQualityFlags`; testes `QualityFlagCatalogTests` | Existe catalogo tipado, mas `OperationalEvent`, `NormalizedReading`, `RiskInput` ainda armazenam flags como `IReadOnlyList<string>`. | Manutencao/auditoria: typo/string drift ainda possivel. | P1 | Migrar fronteiras internas para `QualityFlag` mantendo wire names. |
| V1-011 | classifiers com status/severity | Implementado | `ClassifierResult.cs`; `ClassifierStatus.cs`; `ClassifierSeverity.cs`; testes `ClassifierResultTests` | Sem gap material. | Baixo. | P2 | Manter. |
| V1-012 | stale/lateness/reorder | Implementado | `ReadingTemporalClassifier.cs`; testes `ReadingTemporalClassifierTests`; `RiskEligibilityService.cs` | Cobre delayed/stale/out-of-order. | Baixo. | P2 | Expor melhor nos summaries agregados. |
| V1-013 | duplicate/idempotencia | Implementado | `PostgresReadingEventInbox.cs`; `InMemoryReadingEventInbox.cs`; unique handling e `duplicate_payload_mismatch`; testes `PostgresReadingEventInboxTests`, `InMemoryReadingEventInboxTests` | Sem gap material. | Baixo. | P2 | Manter evidence de duplicados. |
| V1-014 | retry/quarantine | Implementado | `PostgresReadingEventInbox.cs`; `InboxRetryWorkerTests`; migrations pipeline/quarantine | Sem gap material. | Baixo. | P2 | Manter monitorizacao. |
| V1-015 | recovery de `Processing` | Implementado | `PostgresReadingEventInbox.GetDueForRetryAsync`; logs "Recovered stale processing"; testes de inbox/retry | Sem gap material. | Baixo. | P2 | Manter lease configuravel. |
| V1-016 | FWI como referencia/provenance | Parcial | `FireWeatherIndexContext.cs`; `DailyCellState.FireWeatherIndex`; `SimpleRiskScoringService`; scripts `build_fire_weather_indexes_reference.py`; testes `SimpleRiskScoringServiceTests` | Existe contexto/provenance e fallback absent; nao e indice oficial validado em runtime completo. | Metodologico se for apresentado como oficial. | P1 | Manter linguagem "referencia aproximada/importada". |
| V1-017 | KBDI como secura/provenance | Parcial | `DailyCellState.KeetchByramDroughtIndex`; `SimpleRiskScoringService.ResolveDrynessComponent`; tests com `imported_reference` e `absent` | Mesma limitacao do FWI. | Metodologico. | P1 | Formalizar origem e limites. |
| V1-018 | `BaseRisk = 0.50M + 0.20D + 0.30T` | Implementado | `SimpleRiskScoringService.cs` linha formula; testes `SimpleRiskScoringServiceTests` | Sem gap material. | Baixo. | P2 | Manter como candidate set. |
| V1-019 | `T = 0.50H + 0.30F + 0.20G` | Drift | `SimpleRiskScoringService.ResolveTerritoryComponent` usa `StructuralHazardScore` direto; `AreaContext`/GridCell existem | A formula territorial H/F/G nao aparece implementada como tal. | Metodologico: componente T menos explicavel. | P1 | Implementar decomposicao territorial ou documentar desvio. |
| V1-020 | `AdjustedScore = round(100 * BaseRisk * C * I)` | Drift | `SimpleRiskScoringService.cs` calcula `adjustedScore = baseRisk * C * I`; `RiskAssessment` espera [0,1]; thresholds sao 0.60/0.80 | Repo usa escala normalizada [0,1], nao score 0-100 arredondado. | Apresentacao: pode haver mismatch com PesquisaII. | P1 | Decidir escala unica; se mantiver [0,1], atualizar PesquisaII/relatorio. |
| V1-021 | componentes M/D/T/C/I auditaveis | Implementado | `SimpleRiskScoringService` explanation inclui M/D/T/BaseRisk/AdjustedScore/C/I; testes verificam explanation | Sem gap material. | Baixo. | P2 | Se necessario, persistir campos separados. |
| V1-022 | `RiskAssessment` explicavel | Implementado | `src/NatureProtector.Core/Risk/RiskAssessment.cs`; `ExplanationSummary`; persisted `risk_assessment_log` | Sem gap material. | Baixo. | P2 | Expor explanation na API/UI quando util. |
| V1-023 | risk level | Implementado | `RiskLevelExtensions.FromScore`; `RiskAssessment.RiskLevel`; tests `RiskLevelTests` | Sem gap material. | Baixo. | P2 | Manter thresholds explicitos. |
| V1-024 | dominant driver | Parcial | `RuleSet.BuildExplanationSummary_ReturnsDominantDrivers` em testes; `RiskAssessment` nao tem `DominantDriver` dedicado; `SimpleRiskScoringService` nao persiste driver dominante | Ha explicacao textual, mas nao driver formal/auditavel no assessment V1 runtime. | Apresentacao/auditoria. | P1 | Adicionar campo/summary estruturado de driver dominante. |
| V1-025 | Warning/Alarm/None | Implementado | `V1AlertPolicy.cs`; `V1AlertState`; stores de projection; testes `V1AlertPolicyTests` | Sem gap material. | Baixo. | P2 | Manter. |
| V1-026 | Warning open/close 60/50 | Implementado | `V1AlertPolicy.WarningOpenThreshold=0.60`, `WarningCloseThreshold=0.50`; testes | Sem gap material. | Baixo. | P2 | Manter. |
| V1-027 | Alarm open/close 80/70 | Implementado | `V1AlertPolicy.AlarmOpenThreshold=0.80`, `AlarmCloseThreshold=0.70`; testes | Sem gap material. | Baixo. | P2 | Manter. |
| V1-028 | histerese | Implementado | `V1AlertPolicy.EvaluateTransition`; testes de histerese in-memory/postgres | Sem gap material. | Baixo. | P2 | Manter. |
| V1-029 | persistencia minima 2 ciclos | Implementado | `V1AlertPolicy.PersistenceCycles=2`; testes `EvaluateTransition_WithPersistence_*` | Sem gap material. | Baixo. | P2 | Manter. |
| V1-030 | cooldown `max(3*IntervalSeconds,180)` | Implementado | `V1AlertPolicy.ResolveCooldown`; migration `20260518142731_AddV1AlertPolicyState`; testes cooldown | Sem gap material. | Baixo. | P2 | Manter persistencia. |
| V1-031 | projection por celula | Parcial | `DailyCellStateRecord` e `risk_assessment_log.GridCellId`; `PostgresDailyCellStateRepository` | Ha estado diario/provenance por celula e assessments com GridCell, mas nao uma projection operacional de celula equivalente a `AreaOperationalState`. | UI/API pode nao mostrar celula de forma completa. | P1 | Criar/fechar cell operational state se a V1 exigir view por celula. |
| V1-032 | projection por area | Implementado | `AreaRiskSnapshot`, `area_risk_snapshot_log`, `area_operational_state`, `PostgresAreaOperationalProjectionStore` | Sem gap material. | Baixo. | P2 | Manter isolamento por run. |
| V1-033 | agregacao area `0.70*p80 + 0.30*max` | Implementado | `AreaRiskSnapshot.CreateFromAssessments`; testes `AreaRiskSnapshotServiceTests` | Sem gap material. | Baixo. | P2 | Manter. |
| V1-034 | p80 nearest-rank | Implementado | `AreaRiskSnapshot.CalculateNearestRankPercentile` usa `Ceiling(percentile*n)`; testes | Sem gap material. | Baixo. | P2 | Manter. |
| V1-035 | zero elegiveis nao vira score 0 | Implementado | `AreaRiskSnapshot.CreateFromAssessments` lanca se lista vazia; pipeline bloqueia inelegiveis | Sem gap material. | Baixo. | P2 | Manter. |
| V1-036 | baixa cobertura marcada | Parcial | `PostgresControlPlaneService.BuildFreshnessSummary`; runtime summary expõe missing/quality; `RuntimeSummaryServiceTests` | Ha summaries, mas politica formal de "baixa cobertura" nao parece fechar scoring/projection de modo estruturado. | Apresentacao/operacao. | P1 | Adicionar campo CoverageStatus nos snapshots/summaries. |
| V1-037 | clusters/vizinhanca 8 como principio | Documentado apenas | Encontrado apenas em docs/roadmap/conceitos, sem runtime especifico de vizinhanca 8 no core V1 | Nao implementado como algoritmo operacional. | Metodologico futuro. | P2 | Manter como futuro ou implementar explicitamente. |
| V1-038 | sugestoes operacionais simples | Parcial | `src/NatureProtector.Core/Communication/Recommendation.cs`; `RecommendationTests`; docs dizem futuro | Modelo existe, mas nao ha geracao runtime ligada a alertas/projections. | Apresentacao/funcionalidade. | P1 | Gerar recomendacoes simples por alert state ou marcar fora da V1. |
| V1-039 | API expoe projeções/runs | Implementado | `PostgresControlPlaneService`; contracts `ControlPlaneResponses.cs`; testes Backoffice API | Sem gap material. | Baixo. | P2 | Manter endpoints documentados. |
| V1-040 | UI mostra risco/alertas/qualidade | Parcial | `webUI` e Runtime Monitor referenciados; evidence `requirements-status.md` marca UI/polish parcial; API tem quality summary | Nao validei UI em browser nesta auditoria; evidence marca polish pendente. | Apresentacao. | P1 | Smoke visual antes da demo. |
| V1-041 | metricas de observabilidade | Implementado | `TelemetryTags`; `RabbitMqReadingPublisher`; `InfluxWriteService`; testes Influx | Sem gap material. | Baixo. | P2 | Manter tags por run/cenario. |
| V1-042 | evidencia runtime por run | Implementado | `docs/evidence/progress-2026-05-22/04-scenario-b-summary.sql.txt`; `05-scenario-c-summary.sql.txt`; `06-compare-b-vs-c.json` | Evidencia recente existe; nao foi rerun nesta auditoria. | Baixo se codigo nao mudou depois. | P2 | Recolher smoke final antes da apresentacao. |
| V1-043 | `SimulationRunId` em assessments/projeções | Implementado | `RiskAssessment.SimulationRunId`; `risk_assessment_log.SimulationRunId`; `area_risk_snapshot_log`; `area_operational_state`; migration `20260517120000_AddSimulationRunIdToRiskAssessmentLog` | Sem gap material. | Baixo. | P2 | Manter filtros por run. |
| V1-044 | versionamento de parametros | Parcial | `RiskInput.ParameterSetVersion`; `DailyCellState.CandidateParameterSetVersion`; `ConfigurationVersionId` | Existe string/version id, mas nao ha entidade completa de parameter set versionado com pesos/thresholds. | Manutencao/metodologia. | P1 | Persistir parameter set V1 com pesos e thresholds. |
| V1-045 | PostgreSQL para estado duravel | Implementado | `NatureProtectorControlDbContext`; migrations pipeline/projection/daily state; repositories Postgres | Sem gap material. | Baixo. | P2 | Manter migrations limpas. |
| V1-046 | InfluxDB para series/observabilidade | Implementado | `src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`; testes Influx | Sem gap material. | Baixo. | P2 | Validar dashboards antes da demo. |
| V1-047 | cenario A | Implementado | `data/manifests/scenarios/proenca-a-nova/scenario_a.base.json`; generated manifest | Sem gap material. | Baixo. | P2 | Manter como baseline. |
| V1-048 | cenario B | Implementado | `scenario_b.high-risk.json`; tests `ScenarioB_WithDegradationProfileNone_PublishesExpectedEventCount`; runtime B 30/30 | Sem gap material. | Baixo. | P2 | Usar como demo limpa. |
| V1-049 | cenario C como degradacao | Implementado | `scenario_c.degraded-pipeline.json` base B; `DegradationProfile=missing-readings`; tests e runtime C 24/30 | Sem gap material. | Baixo. | P2 | Manter como degradacao, nao terceiro clima. |
| V1-050 | degradation profile explicito | Implementado | `RunOverrides.DegradationProfile`; `ScenarioContextFactory`; `PostgresSimulationContextSource`; tests override precedence | Sem gap material. | Baixo. | P2 | Expandir profiles futuros com cautela. |
| V1-051 | B vs C comparavel | Implementado | `docs/evidence/progress-2026-05-22/06-compare-b-vs-c.json`; scripts `run-scenario.ps1` | Sem gap material. | Baixo. | P2 | Regerar apos mudancas. |
| V1-052 | testes unitarios | Implementado | `docs/evidence/progress-2026-05-22/03-tests.txt`: 959 passed; suites Core/Prevention/Simulator/etc. | Sem gap material. | Baixo. | P2 | Manter. |
| V1-053 | testes integracao | Implementado | `tests/NatureProtector.IntegrationTests`; Postgres repository tests; `03-tests.txt` inclui 2 IntegrationTests | Cobertura pequena, mas existe. | Baixo/medio. | P2 | Aumentar E2E reais quando estabilizar. |
| V1-054 | testes end-to-end | Parcial | Scripts `scripts/scenarios/run-scenario.ps1`; evidence runtime B/C; poucos testes automatizados E2E | Ha runtime evidence e scripts, mas E2E automatizado completo e limitado. | Demo/regressao. | P1 | Criar smoke E2E automatizado B/C. |
| V1-055 | nao equivalencia a indices oficiais | Implementado | `SimpleRiskScoringService` explanation; messages "Candidate Parameter Set V1.0 (non-official/non-calibrated)" | Sem gap material. | Baixo. | P2 | Repetir no relatorio. |
| V1-056 | score como candidate parameter set | Implementado | `RiskInput.ParameterSetVersion`; scoring explanation; tests procuram "Candidate Parameter Set V1.0" | Sem gap material. | Baixo. | P2 | Manter sem overclaim. |
| V1-057 | relatorio separa ciencia, engenharia e hipotese operacional | Documentado apenas | `docs/evidence/progress-2026-05-22/12-report-structure.md`; docs de plano | Estrutura existe, mas relatorio final completo nao foi validado como entregue. | Apresentacao/avaliacao. | P1 | Fechar relatorio final. |
| V1-058 | nao implementar ML no core | Implementado | `rg` nao encontrou motor ML no core/runtime; docs de escopo mantem fora | Sem gap material. | Baixo. | P2 | Manter fora do core V1. |
| V1-059 | nao implementar Haines/NFDRS como motor | Implementado | Ausencia de motor Haines/NFDRS no runtime; FWI/KBDI tratados como referencia | Sem gap material. | Baixo. | P2 | Manter como futuro/documental. |
| V1-060 | nao afirmar validacao cientifica final | Implementado | Strings `non-official`, `non-calibrated`, `Candidate Parameter Set`; evidence/report structure | Sem gap material. | Baixo. | P2 | Manter linguagem critica. |

## 4. P0 obrigatorios

| ID | Problema | Evidencia | Correcao recomendada |
| -- | -------- | --------- | -------------------- |
| - | Nao encontrei P0 atual. | Runtime evidence B/C recente tem rejected=0, quarantined=0; build/test evidence passou. | Fazer smoke final antes da demo para evitar regressao nova. |

## 5. P1 importantes

| ID | Problema | Evidencia | Correcao recomendada |
| -- | -------- | --------- | -------------------- |
| V1-002 | `DailyCellState` ainda conserva sensor como identidade em partes do modelo/in-memory. | `DailyCellState.SensorId`; `InMemoryDailyCellStateRepository.StateKey(AreaId, SensorId, Day, Run)`. | Alinhar in-memory e dominio para Area/GridCell/Day/Run; manter sensor como provenance. |
| V1-007 | `RiskInput` ainda nao e plenamente canonico multi-leitura. | `RiskInput.FromNormalizedReading`; pipeline por leitura. | Consolidar janela por celula quando for fechar V1 metodologica. |
| V1-010 | Flags tipadas existem, mas armazenamento interno segue strings. | `RiskInput.QualityFlags` e `NormalizedReading.QualityFlags` sao strings. | Usar `QualityFlag` internamente e converter no limite. |
| V1-016/V1-017 | FWI/KBDI sao referencia/provenance, nao validacao oficial. | `FireWeatherIndexContext`, scripts e tests com `imported_reference`/`absent`. | Manter estatuto honesto e documentado. |
| V1-019 | Formula T da PesquisaII nao esta implementada. | `SimpleRiskScoringService.ResolveTerritoryComponent`. | Implementar H/F/G ou atualizar documento. |
| V1-020 | Score usa [0,1], nao `round(100 * ...)`. | `SimpleRiskScoringService`, `RiskAssessment` e thresholds. | Decidir escala final e documentar. |
| V1-024 | Dominant driver nao e campo runtime formal. | `RiskAssessment` sem propriedade; RuleSet tem teste isolado. | Persistir driver dominante estruturado. |
| V1-031 | Projection por celula ainda parcial. | `daily_cell_state` e GridCellId existem; sem `CellOperationalState` completo. | Criar projection de celula se for requisito V1. |
| V1-036 | Baixa cobertura nao e estado operacional forte. | API summaries/freshness existem, mas sem CoverageStatus formal. | Adicionar CoverageStatus. |
| V1-038 | Recomendacoes operacionais nao sao runtime. | `Recommendation` so no core/testes. | Gerar recomendacoes simples a partir de alertas. |
| V1-040 | UI nao foi validada visualmente nesta auditoria. | Evidence marca polish pendente. | Smoke visual Runtime Monitor/Grafana. |
| V1-044 | Parametros nao estao versionados como entidade completa. | `ParameterSetVersion` string. | Persistir parameter set V1. |
| V1-054 | E2E automatizado e parcial. | scripts/evidence existem; testes E2E limitados. | Criar smoke E2E B/C automatizado. |
| V1-057 | Relatorio final ainda documental/estrutura. | `12-report-structure.md`. | Fechar documento final. |

## 6. P2 melhorias

| ID | Problema | Evidencia | Correcao recomendada |
| -- | -------- | --------- | -------------------- |
| V1-037 | Vizinhanca 8/clusters so aparece como principio, nao runtime. | Busca encontrou docs/futuro, nao codigo V1. | Manter fora da demo ou implementar explicitamente. |
| V1-001,V1-003,V1-004,V1-005,V1-006,V1-008,V1-009,V1-011,V1-012,V1-013,V1-014,V1-015,V1-018,V1-021,V1-022,V1-023,V1-025,V1-026,V1-027,V1-028,V1-029,V1-030,V1-032,V1-033,V1-034,V1-035,V1-039,V1-041,V1-042,V1-043,V1-045,V1-046,V1-047,V1-048,V1-049,V1-050,V1-051,V1-052,V1-053,V1-055,V1-056,V1-058,V1-059,V1-060 | Pontos implementados; manter rastreabilidade e testes. | Ver matriz principal. | Nao mexer antes da demo salvo regressao. |

## 7. Regressoes ou bloqueios runtime

- `DailyCellState` mismatch antigo: nao encontrei a validacao antiga `RiskInput SensorId does not match DailyCellState SensorId` no dominio atual. `DailyCellState.ApplyRiskInput` valida area, grid cell, run, config e dia; nao valida sensor igual. Testes cobrem sensores diferentes na mesma celula/run/dia e acumulacao humidade + temperatura + vento.
- Persistencia PostgreSQL: `PostgresDailyCellStateRepository` procura/upsert por AreaId + GridCellId + LogicalDate + SimulationRunId; `NatureProtectorControlDbContext` tem indice unico nessa combinacao.
- Divergencia remanescente: `InMemoryDailyCellStateRepository` ainda usa chave AreaId + SensorId + Day + SimulationRunId, logo nao esta alinhado com a semantica por celula.
- Runtime evidence recente: B `d8203d4b-1839-4908-87ef-05633c1f1ae5` tem 30/30, rejected 0, quarantined 0; C `36caca67-352c-41f1-80e3-8fe951a1582c` tem 24/30, rejected 0, quarantined 0.
- Build/testes: evidence existente em `docs/evidence/progress-2026-05-22/02-build.txt` indica build success; `03-tests.txt` indica 959 testes passados. Nao rerun nesta auditoria para manter postura read-only.

## 8. Lacunas metodologicas face a PesquisaII

### Ciencia/validacao

- FWI/KBDI existem como referencia/provenance e entram no score, mas continuam a nao ser validacao cientifica oficial.
- O repo evita overclaim com `Candidate Parameter Set V1.0`, `non-official` e `non-calibrated`.

### Score

- Formula M/D/T base existe.
- Formula territorial especifica `T = 0.50H + 0.30F + 0.20G` nao esta implementada literalmente.
- Escala do score diverge: repo usa [0,1], PesquisaII pede `round(100 * ...)`.

### Pipeline

- Cadeia operacional esta bem representada ate `RiskAssessment`.
- `RiskInput` ainda deve evoluir de fronteira por leitura para input canonico multi-leitura/janela/celula.
- Quality flags tipadas existem, mas ainda convivem com strings.

### Alertas

- Warning/Alarm/None, thresholds, histerese, persistencia minima e cooldown estao implementados.
- Falta politica mais visivel para low coverage/partial/blocked na projection final.

### UI/API

- API expõe runs/projections/quality summaries.
- UI/runtime monitor parece parcial em evidence; nao foi validada visualmente nesta auditoria.

### Evidencia

- Evidence runtime por run existe e e recente.
- Falta smoke E2E automatizado robusto para repetir B/C sem trabalho manual.

## 9. Evidencia usada

### Comandos

- `rg -n "ScenarioDefinition|ScenarioCode|ScenarioId|ControlPlaneScenarioCode|DegradationProfile|scenario_a|scenario_b|scenario_c" src tests data scripts`
- `rg -n "DailyCellState|TruthSnapshot|LocalObservation|OperationalEvent|NormalizedReading|RiskInput|RiskAssessment|AlertState|OperationalProjection" src tests`
- `rg -n "CompleteEligible|PartialButUsable|Blocked|RiskInputStatus|Eligibility" src tests`
- `rg -n "QualityFlag|ClassifierResult|ClassifierStatus|ClassifierSeverity|stale|delayed|lateness|out-of-order|duplicate|Dropped" src tests`
- `rg -n "BaseRisk|AdjustedScore|RiskScore|Meteorology|Drought|Territory|Confidence|Integrity|DominantDriver" src tests`
- `rg -n "0.50|0.20|0.30|0.70|p80|percentile|nearest|Average|AggregateRisk" src tests`
- `rg -n "Warning|Alarm|Hysteresis|Cooldown|Persistence|60|50|80|70|V1AlertPolicy" src tests`
- `rg -n "SimulationRunId|risk_assessment_log|area_risk_snapshot_log|area_operational_state|daily_cell_state|alert_state" src tests docs/evidence/progress-2026-05-22`
- `rg -n "FWI|FireWeatherIndex|KBDI|Keetch|FireIndexProvenance|imported_reference|absent" src tests data scripts docs/evidence/progress-2026-05-22`
- `git -c safe.directory=C:/Users/Miguel/UNI/6sem/PS/IMP/D/NatureProtector status --short`

### Ficheiros consultados

- `src/NatureProtector.Prevention/Risk/DailyCellState.cs`
- `src/NatureProtector.Prevention/Persistence/InMemoryDailyCellStateRepository.cs`
- `src/NatureProtector.Prevention.Host/Persistence/PostgresDailyCellStateRepository.cs`
- `src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`
- `src/NatureProtector.Prevention/Risk/RiskInput.cs`
- `src/NatureProtector.Prevention/Risk/SimpleRiskScoringService.cs`
- `src/NatureProtector.Core/Risk/RiskAssessment.cs`
- `src/NatureProtector.Core/Risk/AreaRiskSnapshot.cs`
- `src/NatureProtector.Prevention/Readings/OperationalEvent.cs`
- `src/NatureProtector.Prevention/Readings/NormalizedReading.cs`
- `src/NatureProtector.Prevention/Risk/QualityFlag.cs`
- `src/NatureProtector.Prevention.Host/Projection/V1AlertPolicy.cs`
- `docs/evidence/progress-2026-05-22/02-build.txt`
- `docs/evidence/progress-2026-05-22/03-tests.txt`
- `docs/evidence/progress-2026-05-22/04-scenario-b-summary.sql.txt`
- `docs/evidence/progress-2026-05-22/05-scenario-c-summary.sql.txt`
- `docs/evidence/progress-2026-05-22/06-compare-b-vs-c.json`

## 10. Veredito final

`Parcialmente alinhado sem lacunas P0`

O repo esta substancialmente funcional para uma demo V1: a cadeia principal existe, o pipeline nao transforma `Blocked` em risco zero, B/C estao comparaveis, runtime evidence recente e limpa, e alertas/projeções/SimulationRunId estao implementados. Nao encontrei bloqueio P0 atual nem quarantine massiva ligada a `DailyCellState`. Contudo, ainda ha lacunas P1 reais: `DailyCellState` precisa fechar a semantica por celula tambem no in-memory/modelo, `RiskInput` ainda nao e plenamente canonico, quality flags continuam em strings, o score diverge na escala e na decomposicao territorial, e algumas pecas de UI/E2E/relatorio continuam parciais. A recomendacao e nao reabrir arquitetura antes da demo, mas fechar os P1 por ordem apos o smoke final.
