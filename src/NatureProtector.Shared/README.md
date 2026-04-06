# NatureProtector.Shared

Este projeto guarda os contratos e utilitários partilhados que hoje permitem ao simulador e ao host de prevenção falar a mesma língua ao nível do transporte de eventos.

## Papel do módulo

- Definir o envelope comum dos eventos.
- Definir o payload atual de `SensorReadingProduced`.
- Concentrar enums de métrica, unidade e estado operacional.
- Centralizar a topologia RabbitMQ usada pela solução atual.

## Conteúdo atual

- `Configuration/RabbitMqOptions.cs`
  - opções de ligação e nome do exchange
- `Contracts/Readings/`
  - `SensorReadingProducedPayload`
  - `MeasurementUnit`
  - `SensorMetricType`
  - `SensorOperationalState`
- `Messaging/`
  - `EventEnvelope`
  - `EventTypes`
  - `JsonEventSerializer`
  - `NatureProtectorRabbitMqTopology`
  - `RoutingKeys`

## Topologia atual que o projeto fixa

- Exchange: `np.events`
- Filas:
  - `np.ingestion.readings`
  - `np.observability.raw`
- Routing key de produção:
  - `simulation.reading.produced`

Neste momento, ambas as filas são ligadas à mesma routing key de produção.

## Porque este módulo é importante

Sem este projeto, o `Simulator.Host` e o `Prevention.Host` tenderiam a duplicar contratos, enums e serialização. O ganho principal aqui é consistência de mensagem e de topologia.

## Limitações e direção futura

- O projeto mistura duas responsabilidades que a documentação de planeamento já distingue melhor: contratos partilhados e infraestrutura RabbitMQ.
- O roadmap em [../../docs/planning/project-completion-roadmap.md](../../docs/planning/project-completion-roadmap.md) já aponta para uma futura separação entre um módulo de contratos e outro de infraestrutura de mensageria.
- O projeto de testes correspondente em [../../tests/NatureProtector.Shared.Tests](../../tests/NatureProtector.Shared.Tests) existe, mas ainda não tem testes substantivos.

## Relação com os hosts

- O `Simulator.Host` publica envelopes `SensorReadingProduced` com estes contratos.
- O `Prevention.Host` desserializa exatamente esses envelopes.
- O `Infrastructure.Influx` reutiliza o mesmo payload para escrever leituras aceites.
