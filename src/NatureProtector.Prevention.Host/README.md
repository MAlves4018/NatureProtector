# NatureProtector.Prevention.Host

Este projeto é o host de execução do módulo de prevenção. É ele que liga a mensageria, o cálculo de risco, a persistência em memória e a escrita em InfluxDB.

## Fluxo ativo de execução

O caminho que está realmente ligado pelo `Program.cs` é este:

1. `PreventionWorker` cria ligação a RabbitMQ e declara a topologia base.
2. O worker consome da fila `np.ingestion.readings`.
3. Cada mensagem é desserializada como `EventEnvelope<SensorReadingProducedPayload>`.
4. A mensagem segue para `ReadingRiskPipeline`.
5. A pipeline:
   - guarda o envelope no repositório em memória de leituras aceites;
   - escreve a leitura aceite em InfluxDB;
   - cria uma `RiskAssessment`;
   - guarda a avaliação em memória;
   - agrega um `AreaRiskSnapshot`;
   - escreve o snapshot em InfluxDB.

## Ficheiros principais

- `Program.cs`
  - composição do host e registo de dependências
- `PreventionWorker.cs`
  - consumo de RabbitMQ e ack/nack
- `Processing/ReadingRiskPipeline.cs`
  - pipeline ativa de cálculo e escrita
- `Persistence/IAcceptedReadingRepository.cs`
- `Persistence/InMemoryAcceptedReadingRepository.cs`

## Configuração usada hoje

- Secção `RabbitMq`
  - host, porta, credenciais e exchange
- Secção `InfluxDb`
  - URL, token, organização e bucket

O `Program.cs` atual não liga uma configuração própria de `PreventionOptions`; o fluxo ativo vive apenas com `RabbitMq` e `InfluxDb`.

## O que este host já fecha

- Consumo real de eventos gerados pelo simulador.
- Declaração de topologia RabbitMQ suficiente para o fluxo atual.
- Escrita de telemetria operacional em InfluxDB.
- Cálculo simples de risco e agregação por área.

## Limitações que a documentação deve tornar explícitas

- Hoje, uma leitura é tratada como “aceite” assim que o envelope é desserializado e entra na pipeline ativa. Ainda não existe, neste host, um estágio explícito e separado de validação semântica antes do cálculo.
- O host ainda não publica `ReadingAccepted`, `ReadingRejected` ou `ReadingNormalized`.
- Não existe inbox durável, nem idempotência forte por `event_id`, nem retry com backoff, nem DLQ.
- Os repositórios de leituras, avaliações e snapshots continuam em memória.
- PostgreSQL ainda não faz parte do caminho de runtime deste host.

## Relação com outros módulos

- Consome contratos de `NatureProtector.Shared`.
- Usa o scoring de `NatureProtector.Prevention`.
- Usa `NatureProtector.Infrastructure.Influx` para persistência time-series.
- Espera eventos produzidos por `NatureProtector.Simulator.Host`.

## Nota importante de contexto

Existe código com namespace `NatureProtector.Prevention.Host` ainda alojado dentro de [../NatureProtector.Simulator.Host](../NatureProtector.Simulator.Host). Esse código não corresponde ao caminho ativo deste projeto e deve ser lido como resíduo de transição, não como a implementação principal do host de prevenção.
