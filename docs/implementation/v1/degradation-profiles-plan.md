# Plano V1 de degradation profiles

Esta nota congela a taxonomia curta para a proxima demo/design review. Segue a cadeia V1:

`TruthSnapshot -> LocalObservation -> OperationalEvent/EventEnvelope -> NormalizedReading -> RiskInput`

Falhas operacionais nao devem alterar `TruthSnapshot`. Devem atuar na observacao local, na publicacao/evento, ou ser classificadas a jusante pela pipeline.

| Profile | Camada | O que simula | Prioridade | Entra ate dia 1? | Testes necessarios |
|---|---|---|---|---|---|
| `none` | nenhuma | Baseline limpa para scenario B: sem missing, skip, drop, duplicacao, atraso, unidade invalida ou estado invalido forcado. | P0 | sim | 6 sensores * 5 ciclos publica 30 payloads nominais; 0 rejected; 0 quarantined. |
| `missing-readings` | `LocalObservation` | Perda controlada de observacoes no scenario C depois da verdade fisica. | P0 | sim | C publica menos de 30; sequencia fisica comparavel com B; sem payload invalido por este profile. |
| `noisy-readings` | `LocalObservation` | Ruido de medicao sem falha de transporte. | P1 | sim, se simples | Ruido dentro de limites; `TruthSnapshot` inalterado; contrato externo estavel. |
| `stuck-value` | `LocalObservation` | Sensor repete ultimo valor observado durante N ciclos. | P1 | sim, se simples | Valores repetidos detetados; timestamps/proveniencia distintos. |
| `duplicate-events` | publicacao/evento | Entrega duplicada para testar idempotencia. | P1 | sim, se simples | Duplicate flag; sem `RiskAssessment` duplicado. |
| `delayed-events` | publicacao/evento | Entrega atrasada com `EventTime` logico original. | P1 | talvez | Evento classificado como delayed/lateness; sem falso dado fresco se ultrapassar janela. |
| `out-of-order-events` | publicacao/evento | Ordem de chegada diferente da ordem logica. | P1 | talvez | Janela de reorder aplicada; idempotencia preservada. |
| `stale-readings` | publicacao/evento | Leituras com timestamp logico antigo. | P1 | talvez | Threshold `max(5*IntervalSeconds, 300)` aplicado; elegibilidade parcial/bloqueada conforme regra. |
| `invalid-unit` | `LocalObservation`/evento | Unidade invalida explicita para validacao semantica. | P2 | depois | Rejeitada antes do scoring com `invalid_unit`. |
| `mounting-error` | `LocalObservation` | Bias estavel por montagem/localizacao do sensor. | P2 | depois | Bias deterministico por seed; `TruthSnapshot` inalterado. |
| `mixed-degradation` | multiplas | Composicao limitada de falhas operacionais. | P2 | depois | Contagens por componente batem com configuracao; sem falhas opacas. |

## Notas de implementacao

- `TruthSnapshot` nao deve ser alterado por degradacao operacional.
- `LocalObservation` recebe missing/noise/stuck/bias.
- Publicacao/event envelope recebe duplicate/delayed/out-of-order/stale.
- Pipeline classifica/reage; nao inventa degradacao.
- `none` e `missing-readings` sao os unicos profiles necessarios para a apresentacao de progresso de 2026-05-22.
- O contrato externo RabbitMQ `EventEnvelope<SensorReadingProducedPayload>` permanece estavel.
