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
- A `schemaVersion` runtime suportada para esse envelope é `1.0`; versões desconhecidas são rejeitadas pelo consumidor antes da materialização no inbox.
- A publicação RabbitMQ do simulador usa mensagens persistentes, metadata JSON explícita, `mandatory` routing e publisher confirms; ausência total de destinos routable deve falhar localmente. Isto não prova entrega à fila principal quando outro destino continua ligado.
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
| CTR-009 | Topologia e papéis RabbitMQ | RabbitMQ/runtime | `rabbitmq-runtime-topology-and-delivery-contract.md` | Contrato-alvo; implementação pendente |
| CTR-010 | Liveness/readiness | API/Prevention | `runtime-health-readiness-contract.md` | Contrato-alvo; implementação pendente |

## Checklist de compatibilidade

| Check ID | Verificação | Método | Resultado esperado |
|---|---|---|---|
| CHK-001 | RabbitMQ mantém envelope atual | Fixtures de `Shared`, testes do simulador e validação pré-inbox da prevenção | `SensorReadingProduced` serializa/deserializa sem alteração e versões desconhecidas são rejeitadas |
| CHK-002 | Camadas internas não substituem contrato externo | Testes de prevenção | `OperationalEvent` nasce de `EventEnvelope<SensorReadingProducedPayload>` |
| CHK-003 | `RiskInput` continua pré-scoring | Testes de risco | Não contém `BaseRisk`, `AdjustedScore`, `RiskScore`, `RiskLevel`, `AlertState` ou projeção |
| CHK-004 | `Blocked` não vira risco zero | Testes de elegibilidade/scoring | Não é criado novo `RiskAssessment` numérico |
| CHK-005 | API não recalcula risco | Testes de API/projeção | `alertState` é lido da projeção |
| CHK-006 | Publisher RabbitMQ falha sem qualquer destino routable | DockerIntegration RabbitMQ | `mandatory` + publisher confirms estão ativos e canal/conexão fechados são recriados |
| CHK-007 | Fila raw não bloqueia o pipeline principal | Testes de caracterização e integração RabbitMQ | raw desativada por omissão; quando ativa usa retenção limitada e `drop-head` |
| CHK-008 | Readiness representa dependências funcionais | Testes de processos publicados | DB down mantém live 200 e produz ready 503 quando a dependência está ativa |

## Relação com o Plano V1

Referências cruzadas atuais:

- [`../NatureProtector-V1-overview.md`](../NatureProtector-V1-overview.md)
- [`../planning/v1-implementation-map.md`](../planning/v1-implementation-map.md)
- [`../architecture/scenario-run-orchestrator.md`](../architecture/scenario-run-orchestrator.md)

## Contratos de remediação RabbitMQ e health

- [`rabbitmq-runtime-topology-and-delivery-contract.md`](rabbitmq-runtime-topology-and-delivery-contract.md)
- [`runtime-health-readiness-contract.md`](runtime-health-readiness-contract.md)

Estes dois documentos estão marcados como `TARGET_CONTRACT` e não devem ser usados para alegar que a baseline já implementa o comportamento.
