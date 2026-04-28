@page simulator_flow Fluxo nominal do simulador

# Fluxo nominal do simulador

## Objetivo da página

Explicar como o `Simulator.Host` constrói uma execução de simulação, gera leituras e publica eventos consumíveis pela prevenção.

## Âmbito

Esta página descreve a runtime atual de simulação. O foco está na composição por DI, na resolução de contexto, na seed, no ciclo temporal, na geração de leituras e na publicação RabbitMQ.

Não descreve um modelo físico completo de incêndio rural. A implementação atual gera leituras plausíveis e determinísticas a partir de parâmetros de cenário, sensores e ruído limitado; ainda não separa em módulos próprios a verdade física, o erro de medição e a falha de transporte.

## Componentes principais

@ref NatureProtector.Simulator.Host.Services.SimulationRunner é o hosted service que orquestra a execução. Ele não conhece detalhes de PostgreSQL nem de RabbitMQ; coordena contratos internos para resolver contexto, seed, geração, persistência de run e publicação.

@ref NatureProtector.Simulator.Host.Services.ScenarioContextFactory constrói o contexto em modo autónomo local. @ref NatureProtector.Simulator.Host.Services.PostgresSimulationContextSource constrói o contexto a partir do plano de controlo em PostgreSQL. @ref NatureProtector.Simulator.Host.Services.ReadingGenerationService transforma sensores e parâmetros de cenário em envelopes `SensorReadingProduced`. @ref NatureProtector.Simulator.Host.Publishing.RabbitMqReadingPublisher declara a topologia RabbitMQ e publica os eventos.

Quando o modo com plano de controlo está ativo, @ref NatureProtector.Simulator.Host.Services.PostgresSimulationRunStore persiste as transições da run. Em modo autónomo local, @ref NatureProtector.Simulator.Host.Services.NoOpSimulationRunStore preserva o contrato sem escrita relacional.

## Fluxo nominal

\startuml
title Fluxo nominal do simulador
autonumber
participant "SimulationRunner" as Runner
participant "ISimulationContextSource" as ContextSource
participant "SeedProvider" as SeedProvider
participant "ReadingGenerationService" as Generator
participant "ISimulationRunStore" as RunStore
participant "IReadingPublisher" as Publisher
database "PostgreSQL" as Postgres
queue "RabbitMQ" as Rabbit

Runner -> ContextSource : CreateAsync()
ContextSource --> Runner : SimulationContext
Runner -> SeedProvider : ResolveSeed() / CreateRandom()
Runner -> RunStore : Upsert Ready
RunStore -> Postgres : persistir quando ativo
Runner -> RunStore : Upsert Running
RunStore -> Postgres : persistir quando ativo
loop por ciclo
  Runner -> Generator : GenerateBatch(context, runId, cycle, eventTime, random)
  Generator --> Runner : envelopes SensorReadingProduced
  loop por envelope
    Runner -> Publisher : PublishAsync(envelope)
    Publisher -> Rabbit : publicar em np.events
  end
end
Runner -> RunStore : Upsert Completed, Cancelled ou Failed
RunStore -> Postgres : persistir quando ativo
\enduml

O tempo lógico de cada evento é calculado a partir de `StartTimestamp`, `IntervalSeconds` e índice do ciclo. Os timestamps reais de início e fim da run são registados separadamente quando existe store persistente.

## Modelo de execução

`src/NatureProtector.Simulator.Host/Program.cs` regista sempre os serviços principais e escolhe implementações conforme `Simulator:ControlPlaneEnabled`.

Com `ControlPlaneEnabled=true`, o simulador resolve área e cenário por `AreaId` ou `ControlPlaneAreaCode`, por `ScenarioId` ou `ControlPlaneScenarioCode`, lê apenas sensores ativos e reconstrói os parâmetros do cenário a partir de `ParametersJson`.

Com `ControlPlaneEnabled=false`, o contexto vem da configuração local e de eventual ficheiro de cenário gerado. Este modo continua útil para diagnóstico e testes, mas não valida a ponte bootstrap -> plano de controlo -> runtime.

## Decisões importantes

- A seed é resolvida uma vez por execução para tornar os valores reprodutíveis quando configurada.
- O `SimulationRunner` persiste `Ready`, `Running` e estado terminal quando o store PostgreSQL está ativo.
- O publisher RabbitMQ declara exchange, filas e bindings antes de publicar.
- A implementação atual publica apenas métricas de temperatura, humidade e vento. Sensores compostos não têm contrato de leitura suportado neste fluxo.
- Sensores indisponíveis podem gerar evento com `OperationalState=Invalid`; no consumidor atual esses eventos são rejeitados antes da pipeline de risco.

## Estado atual e limitações

O estado implementado suporta geração de eventos `SensorReadingProduced` com schema version `1.0`, payload partilhado e routing key `simulation.reading.produced`. O simulador consegue correr em modo autónomo ou apoiado pelo plano de controlo, e os testes fixam número de eventos, tempo lógico, cancelamento gracioso e separação entre tempo lógico e timestamps reais da run.

As limitações conhecidas são: não há publicação de famílias `ReadingAccepted`, `ReadingRejected` ou `ReadingNormalized`; não há contrato para sensores compostos; a simulação é deliberadamente simples face a um modelo físico completo; e o caminho com plano de controlo exige bootstrap PostgreSQL prévio.

## Pontos do repositório a consultar

- `src/NatureProtector.Simulator.Host/Program.cs`
- `src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`
- `src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`
- `src/NatureProtector.Simulator.Host/Context/ScenarioContextFactory.cs`
- `src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`
- `src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs`
- `tests/NatureProtector.Simulator.Host.Tests/Services/SimulationRunnerTests.cs`

## Ligações para páginas relacionadas

- Para a origem dos dados de controlo usados pelo simulador, consultar @ref control_plane_and_bootstrap.
- Para o consumidor dos eventos publicados, consultar @ref prevention_flow.
- Para a persistência das runs e do plano de controlo, consultar @ref persistence_model.
- Para testes que fixam o comportamento do simulador, consultar @ref tests_as_documentation.
