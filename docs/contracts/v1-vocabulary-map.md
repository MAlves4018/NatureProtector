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
| `TruthSnapshot` | Conceito metodológico | Futuro | Introduzir apenas quando o simulador em camadas for materializado | Não é contrato implementado. |
| `LocalObservation` | Conceito metodológico | Futuro | Introduzir como camada entre verdade física e evento quando fizer sentido | Não é contrato implementado. |
| `EventEnvelope<TPayload>` | `EventEnvelope<TPayload>` | Implementado | Manter | Envelope de transporte comum. |
| `SensorReadingProduced` | `EventTypes.SensorReadingProduced` | Implementado | Manter | Evento externo vivo da ingestão RabbitMQ. |
| `SensorReadingProducedPayload` | `SensorReadingProducedPayload` | Implementado | Manter | Payload RabbitMQ atual. |
| `OperationalEvent` | `OperationalEvent` | Implementado como camada interna | Manter interno | Adaptador `EventEnvelope<SensorReadingProducedPayload> -> OperationalEvent`; não é contrato externo RabbitMQ. |
| `NormalizedReading` | `NormalizedReading` | Implementado | Manter | Leitura interna enriquecida com `QualityFlags` e `ClassifierResults`. |
| `ClassifierResult` | `ClassifierResult` | Implementado | Manter | Resultado auditável de classificação, com flags/razões. |
| `ClassifierStatus` | `ClassifierStatus` | Implementado | Manter | Estado do classificador. |
| `ClassifierSeverity` | `ClassifierSeverity` | Implementado | Manter | Severidade do classificador. |
| `QualityFlags` | `IReadOnlyList<string>` / listas de strings | Parcial | Manter formato atual até existir enum/contrato canónico forte | Ainda não há enum canónico transversal. |
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
| `DailyCellState` | `DailyCellState` | Implementado | Manter como contexto diário | Artefacto de contexto/memória, não score final. |
| `AlertState` | `V1AlertPolicy`, `projection.alert_state`, DTOs da API | Implementado/parcial | Expor via projeção/API | API lê `alertState`; não recalcula risco. |
| `V1AlertPolicy` | `V1AlertPolicy` | Implementado | Manter como política interna | Estados `None`, `Warning`, `Alarm` com histerese. |
| `OperationalProjection` | `projection.*` e stores de projeção | Implementado/parcial | Manter | Estado operacional persistido por área/célula/alerta. |
| `AggregateRiskScore` | `AreaRiskSnapshot.AggregateRiskScore` e projeções | Implementado | Manter | Score agregado por área. |
| `area_aggregated_score` | Alias conceptual | Histórico/futuro | Não introduzir como novo nome sem necessidade | Usar `AggregateRiskScore` no código atual. |
| `FWI/KBDI` | Referência metodológica/contexto preparatório | Futuro | Não declarar cálculo final validado | Requer frente própria de validação científica. |

## Decisões fechadas

| ID | Decisão | Estado |
|---|---|---|
| VOC-D01 | Manter `EventEnvelope<SensorReadingProducedPayload>` como contrato RabbitMQ atual. | Fechada |
| VOC-D02 | Tratar `OperationalEvent` como camada interna da prevenção. | Fechada |
| VOC-D03 | Manter `RiskInput` como fronteira pré-scoring. | Fechada |
| VOC-D04 | Tratar `Blocked` como ausência de novo assessment numérico, não risco zero. | Fechada |
| VOC-D05 | Usar `BaseRisk` e `AdjustedScore` em `RiskAssessment`, preservando `RiskScore` como compatibilidade. | Fechada |
| VOC-D06 | Expor `alertState` a partir da projeção/API, sem recalcular risco no Backoffice. | Fechada |

## Itens futuros

| ID | Tema | Próxima ação |
|---|---|---|
| VOC-F01 | `TruthSnapshot` e `LocalObservation` | Materializar quando a separação física/observacional do simulador avançar. |
| VOC-F02 | Enum/contrato canónico de quality flags | Avaliar se a lista de strings atual deve evoluir para enum versionado. |
| VOC-F03 | Eventos externos derivados | Definir publicação formal de accepted/rejected/normalized/warning/alarm, se necessário. |
| VOC-F04 | FWI/KBDI | Tratar como frente científica/metodológica separada, com validação própria. |
