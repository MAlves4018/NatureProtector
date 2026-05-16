# Contracts README

## Objetivo

Documentar os contratos de integração e o vocabulário V1 que não devem ser
alterados sem versionamento explícito. Esta pasta distingue contratos externos,
camadas internas e conceitos metodológicos para evitar que documentação antiga
seja lida como estado runtime atual.

## Política

- Contratos externos são fronteiras de compatibilidade.
- Alterações de contrato exigem versionamento explícito e plano de migração.
- O contrato RabbitMQ vivo da V1 continua a ser `EventEnvelope<SensorReadingProducedPayload>`.
- `SensorReadingProduced` é o evento externo atual da ingestão.
- `OperationalEvent` é camada interna da prevenção, não contrato RabbitMQ externo.
- Quando houver conflito entre documentação antiga e código/testes/evidência recente, prevalece o estado observado.

## Inventário mínimo V1

| ID | Tipo | Sistema/canal | Localização principal | Estado |
|---|---|---|---|---|
| CTR-001 | Evento externo | RabbitMQ | `src/NatureProtector.Shared/Messaging/EventTypes.cs` | Ativo: `SensorReadingProduced` |
| CTR-002 | Envelope externo | RabbitMQ | `src/NatureProtector.Shared/Messaging/EventEnvelope.cs` | Ativo: `EventEnvelope<TPayload>` |
| CTR-003 | Payload externo | RabbitMQ | `src/NatureProtector.Shared/Contracts/Readings/SensorReadingProducedPayload.cs` | Ativo: `SensorReadingProducedPayload` |
| CTR-004 | Adaptador interno | Prevention pipeline | `src/NatureProtector.Prevention/Readings/OperationalEvent.cs` | Interno, não publicado no broker |
| CTR-005 | Leitura normalizada interna | Prevention pipeline | `src/NatureProtector.Prevention/Readings/NormalizedReading.cs` | Interno, enriquecido com qualidade/classificadores |
| CTR-006 | Input de risco interno | Prevention risk | `src/NatureProtector.Prevention/Risk/RiskInput.cs` | Pré-scoring |
| CTR-007 | Resultado de risco | Core risk | `src/NatureProtector.Core/Risk/RiskAssessment.cs` | `BaseRisk`, `AdjustedScore`, `RiskScore` compatível |
| CTR-008 | Estado de alerta exposto | Projection/API | `projection.alert_state`, `Backoffice.Api` | Exposto como `alertState` a partir da projeção |

## Checklist de compatibilidade

| Check ID | Verificação | Método | Resultado esperado |
|---|---|---|---|
| CHK-001 | RabbitMQ mantém envelope atual | Testes de `Shared` e simulador | `SensorReadingProduced` serializa/deserializa sem alteração |
| CHK-002 | Camadas internas não substituem contrato externo | Testes de prevenção | `OperationalEvent` nasce de `EventEnvelope<SensorReadingProducedPayload>` |
| CHK-003 | `RiskInput` continua pré-scoring | Testes de risco | Não contém `BaseRisk`, `AdjustedScore`, `RiskScore`, `RiskLevel`, `AlertState` ou projeção |
| CHK-004 | `Blocked` não vira risco zero | Testes de elegibilidade/scoring | Não é criado novo `RiskAssessment` numérico |
| CHK-005 | API não recalcula risco | Testes de API/projeção | `alertState` é lido da projeção |

## Relação com o Plano V1

Referências cruzadas atuais:

- [`../NatureProtector-V1-overview.md`](../NatureProtector-V1-overview.md)
- [`../planning/v1-implementation-map.md`](../planning/v1-implementation-map.md)
- [`../architecture/scenario-run-orchestrator.md`](../architecture/scenario-run-orchestrator.md)
