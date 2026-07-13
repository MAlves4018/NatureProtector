# NatureProtector.Shared

Este projeto guarda os contratos e utilitários partilhados que hoje permitem ao simulador e ao host de prevenção falar a mesma língua ao nível do transporte de eventos.

## Papel do módulo

- Definir o envelope comum dos eventos.
- Definir o payload atual de `SensorReadingProduced`.
- Concentrar enums de métrica, unidade e estado operacional.
- Centralizar a topologia RabbitMQ usada pela solução atual.

## Conteúdo atual

- `Configuration/RabbitMqOptions.cs`
  - opções de ligação, vhost, nomes de exchange/filas e timeout de publisher confirms
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

A fila principal é sempre ligada à routing key de produção. A fila raw só é declarada e ligada quando `RabbitMq__ObservabilityRawEnabled=true`; o default é `false`. Recursos duráveis criados por revisões antigas continuam a exigir migração explícita no broker. O contrato e o rollout estão em [`../../docs/contracts/rabbitmq-runtime-topology-and-delivery-contract.md`](../../docs/contracts/rabbitmq-runtime-topology-and-delivery-contract.md).

## Porque este módulo é importante

Sem este projeto, o `Simulator.Host` e o `Prevention.Host` tenderiam a duplicar contratos, enums e serialização. O ganho principal aqui é consistência de mensagem e de topologia.

Este projeto nao deve carregar exporters, OpenTelemetry instrumentation ou wiring de hosts runtime. Essa responsabilidade pertence a `NatureProtector.Shared.Observability`.

## Limitações e direção futura

- O projeto mistura duas responsabilidades que a documentação de planeamento já distingue melhor: contratos partilhados e infraestrutura RabbitMQ.
- O roadmap em [../../docs/planning/project-completion-roadmap.md](../../docs/planning/project-completion-roadmap.md) já aponta para uma futura separação entre um módulo de contratos e outro de infraestrutura de mensagens.
- O wiring OpenTelemetry foi separado para `NatureProtector.Shared.Observability`; `NatureProtector.Shared` permanece livre de referencias `OpenTelemetry*`.
- O projeto de testes correspondente em [../../tests/NatureProtector.Shared.Tests](../../tests/NatureProtector.Shared.Tests) cobre serialização camelCase, enums textuais, fixtures JSON V1, compatibilidade com a forma V1 sem `ingestTime`, campos opcionais desconhecidos e topologia RabbitMQ.

## Relação com os hosts

- O `Simulator.Host` publica envelopes `SensorReadingProduced` com estes contratos.
- O `Prevention.Host` desserializa exatamente esses envelopes.
- O `Infrastructure.Influx` reutiliza o mesmo payload para escrever leituras aceites.
