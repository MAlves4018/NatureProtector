# V1 Vocabulary Map

## Objetivo

Mapear termos metodológicos da V1 para os nomes atuais do repositório,
distinguindo contratos externos, camadas internas, estado implementado e trabalho
futuro.

## Regras operacionais

- Não fazer renames destrutivos sem plano de migração.
- Preservar o contrato externo RabbitMQ atual.
- Tratar `RiskScore` como compatibilidade ativa de `AdjustedScore`.
- Quando houver conflito entre docs e código/testes/evidência recente, prevalece o estado observado.

## Mapa Proposal -> Repo -> Decisão

| Termo V1 | Nome atual no repo | Estado | Decisão/compatibilidade | Nota |
|---|---|---|---|---|
| `TruthSnapshot` | `NatureProtector.Simulator.Host/Readings/TruthSnapshot` | Implementado | Manter interno ao simulador | Verdade fisica simulada antes de erro operacional; nao altera contrato RabbitMQ. |
| `LocalObservation` | `NatureProtector.Simulator.Host/Readings/LocalObservation` | Implementado | Manter interno ao simulador | Observacao local derivada da verdade fisica; suporta degradacao antes do payload externo. |
| `EventEnvelope<TPayload>` | `EventEnvelope<TPayload>` | Implementado | Manter | Envelope de transporte comum. |
| `SensorReadingProduced` | `EventTypes.SensorReadingProduced` | Implementado | Manter | Evento externo vivo da ingestão RabbitMQ. |
| `SensorReadingProducedPayload` | `SensorReadingProducedPayload` | Implementado | Manter | Payload RabbitMQ atual. |
| `OperationalEvent` | `OperationalEvent` | Implementado como camada interna | Manter interno | Adaptador `EventEnvelope<SensorReadingProducedPayload> -> OperationalEvent`; não é contrato externo RabbitMQ. |
| `NormalizedReading` | `NormalizedReading` | Implementado | Manter | Leitura interna enriquecida com `QualityFlags` e `ClassifierResults`. |
| `ClassifierResult` | `ClassifierResult` | Implementado | Manter | Resultado auditável de classificação, com flags/razões. |
| `ClassifierStatus` | `ClassifierStatus` | Implementado | Manter | Estado do classificador. |
| `ClassifierSeverity` | `ClassifierSeverity` | Implementado | Manter | Severidade do classificador. |
| `QualityFlag` / `QualityFlags` | `QualityFlag` + wire names | Implementado/parcial | Usar tipos fortes internamente e converter para string em storage/API/logs | O contrato externo continua compatível com strings onde já existiam. |
| `RiskInput` | `RiskInput` | Implementado | Manter pré-scoring | Não contém `BaseRisk`, `AdjustedScore`, `RiskScore`, `RiskLevel`, `AlertState` ou projeção. |
| `RiskInputStatus` | `RiskInputStatus` | Implementado | Manter | Enum com `CompleteEligible`, `PartialButUsable`, `Blocked`. |
| `CompleteEligible` | `RiskInputStatus.CompleteEligible` | Implementado | Manter | Pode gerar `RiskAssessment`. |
| `PartialButUsable` | `RiskInputStatus.PartialButUsable` | Implementado | Manter | Pode gerar `RiskAssessment` com fatores candidatos. |
| `Blocked` | `RiskInputStatus.Blocked` | Implementado | Manter decisão fechada | Não é risco zero; significa ausência de condições para novo assessment numérico. |
| `RiskAssessment` | `RiskAssessment` | Implementado | Manter | Resultado de scoring. |
| `BaseRisk` | `RiskAssessment.BaseRisk` | Implementado | Manter | Score base antes de fatores candidatos. |
| `AdjustedScore` | `RiskAssessment.AdjustedScore` | Implementado | Manter | Score ajustado usado para nível/projeções. |
| `RiskScore` | `RiskAssessment.RiskScore` | Implementado como compatibilidade | Manter enquanto persistência/projeções dependerem dele | Espelha `AdjustedScore`. |
| `RiskLevel` | `RiskAssessment.RiskLevel` | Implementado | Manter | Derivado do score ajustado. |
| `DominantDriver` | `RiskAssessment.DominantDriver` | Implementado | Manter | Driver principal candidato: meteorologia, seca, território, qualidade ou misto. |
| `ParameterSetVersion` | `CandidateParameterSetV1.Version` / `RiskAssessment.ParameterSetVersion` | Implementado | Manter | Marca pesos e thresholds como Candidate Parameter Set V1.0, não calibração científica. |
| `DailyCellState` | `DailyCellState` | Implementado | Manter como contexto diário | Artefacto de contexto/memória, não score final; materializa `daily_reference` do cenário quando existe. |
| `FireWeatherIndexResult` | `FireWeatherIndexContext` / `DailyCellState` | Implementado/parcial | Manter como comparação/proveniência | Calculável quando os inputs existem; `scenario_daily_reference` pode fornecer precipitação 24h e contexto; sem equivalência oficial. |
| `KbdiResult` | `Kbdi` / `DailyCellState` | Implementado/parcial | Manter como secura candidata/proveniência | Normalização candidata KBDI/800 com status e limitations; `precipitation_total_mm=0.0` é valor válido. |
| `CoverageStatus` | `CoverageStatus` | Implementado | Manter em projeções/API/UI | Estado operacional de cobertura: Complete, Partial, LowCoverage, Blocked, NoRecentData. |
| `FreshnessStatus` | `FreshnessStatus` | Implementado | Manter em projeções/API/UI | Atualidade temporal: Fresh, Stale, Expired. |
| `CarryForwardStatus` | `CarryForwardStatus` | Implementado | Manter em projeções/API/UI | Indica valor corrente, carry-forward, carry-forward expirado ou indisponível. |
| `AlertState` | `V1AlertPolicy`, `projection.alert_state`, DTOs da API | Implementado/parcial | Expor via projeção/API | API lê `alertState`; não recalcula risco. |
| `V1AlertPolicy` | `V1AlertPolicy` | Implementado | Manter como política interna | Estados `None`, `Warning`, `Alarm` com histerese. |
| `OperationalProjection` | `projection.*` e stores de projeção | Implementado/parcial | Manter | Estado operacional persistido por área/célula/alerta. |
| `AggregateRiskScore` | `AreaRiskSnapshot.AggregateRiskScore` e projeções | Implementado | Manter | Score agregado por área. |
| `area_aggregated_score` | Alias conceptual | Histórico/futuro | Não introduzir como novo nome sem necessidade | Usar `AggregateRiskScore` no código atual. |
| `DegradationProfiles` | `RunOverrides.DegradationProfiles` / metadata requested-resolved | Implementado | Preferir plural e manter `DegradationProfile` legacy | Lista de perfis de erro/degradação por run; `scenario_c` usa `missing-readings` por defeito. |
| `FWI/KBDI` | Contexto candidato em `DailyCellState` e diagnostics NP vs FWI | Implementado/parcial | Não declarar cálculo final validado | Implementado para comparação/proveniência técnica; calibração e validação científica ficam fora da V1. |

## Decisões fechadas

| ID | Decisão | Estado |
|---|---|---|
| VOC-D01 | Manter `EventEnvelope<SensorReadingProducedPayload>` como contrato RabbitMQ atual. | Fechada |
| VOC-D02 | Tratar `OperationalEvent` como camada interna da prevenção. | Fechada |
| VOC-D03 | Manter `RiskInput` como fronteira pré-scoring. | Fechada |
| VOC-D04 | Tratar `Blocked` como ausência de novo assessment numérico, não risco zero. | Fechada |
| VOC-D05 | Usar `BaseRisk` e `AdjustedScore` em `RiskAssessment`, preservando `RiskScore` como compatibilidade. | Fechada |
| VOC-D06 | Expor `alertState` a partir da projeção/API, sem recalcular risco no Backoffice. | Fechada |
| VOC-D07 | Manter `degradationProfile` singular como compatibilidade e preferir `degradationProfiles` plural em novos fluxos. | Fechada |
| VOC-D08 | Tratar FWI/KBDI como comparação/proveniência candidata, não validação científica final. | Fechada |

## Detalhe de status FWI/KBDI

`FireWeatherIndexResult` e `KbdiResult` sao artefactos internos de comparacao/proveniencia. FWI inclui FFMC, DMC, DC, ISI, BUI, FWI, `normalizedFWI`, status e limitations. KBDI inclui `previousKbdi`, `kbdi`, `normalizedKbdi`, status e limitations. O status `CompleteWithCandidateDefaults` deve ser preservado quando o calculo usa defaults antecedentes candidatos; isto nao equivale a dado observado nem a validacao cientifica oficial.

O contexto `daily_reference` dos cenarios e fonte explicita para memoria diaria do cenario. Ele pode preencher precipitacao 24h, temperatura maxima e contexto de indices em `DailyCellState`, com proveniencia `scenario_daily_reference`. Valor zero de precipitacao e dado observado/importado valido, nao ausencia.

## Classes interpretativas e proxy portugues candidato

| Termo | Significado V1 | Restricao |
| --- | --- | --- |
| `FireWeatherIndexClassification` | Classe interpretativa FWI com limiares IPMA e EFFIS auxiliar. | Nao valida o score NatureProtector nem reproduz produto oficial. |
| `KbdiDrynessClassification` | Classe candidata de secura KBDI em escala 0..800. | KBDI mede secura/deficit hidrico, nao risco final. |
| `NatureProtectorRiskClassification` | Classe candidata do score NP 0..1. | Thresholds sao `Candidate Parameter Set V1.0`. |
| `PortugueseContextRiskProxy` | Proxy candidato que combina classe FWI IPMA com perigo territorial T/H/F/G. | Nao e RCM/PIR/IPMA oficial e nao usa perigosidade rural oficial ICNF. |
| `LimitedAntecedentHistory` | Status KBDI quando falta serie antecedente diaria suficiente. | Valor pode ser usado como contexto candidato, nao como secura historica calibrada. |
| `LocalFwiPercentile` | Percentil local de FWI quando houver distribuicao historica materializada. | Estado atual: `NotAvailable` com `historical_local_fwi_distribution_not_materialized`. |

## Itens futuros

| ID | Tema | Próxima ação |
|---|---|---|
| VOC-F01 | `RiskInput` canónico por janela/célula | Completar a migração gradual dos adapters legados sem quebrar a pipeline atual. |
| VOC-F02 | Quality flags em contratos públicos | Avaliar se a API deve expor enum versionado em vez de strings compatíveis. |
| VOC-F03 | Eventos externos derivados | Definir publicação formal de accepted/rejected/normalized/warning/alarm, se necessário. |
| VOC-F04 | FWI/KBDI | Tratar calibração científica e equivalência oficial como frente separada, com validação própria. |
