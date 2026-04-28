@page persistence_model Modelo de persistência

# Modelo de persistência

## Objetivo da página

Explicar como a persistência atual está dividida entre PostgreSQL e InfluxDB, que responsabilidades cada schema assume e que componentes escrevem ou leem cada zona.

## Âmbito

Esta página cobre o modelo persistente usado pela runtime implementada: plano de controlo, inbox, tentativas, rejeições, quarentena, logs operacionais, projeções e séries temporais.

Não substitui as migrations nem documenta cada coluna. Para esse nível de detalhe, a fonte de verdade é @ref NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext e os records em `src/NatureProtector.Infrastructure.Postgres/`.

## Componentes principais

@ref NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext é o ponto central do modelo relacional. Ele agrega três grupos de schema: `control`, `pipeline` e `projection`.

O acesso ao plano de controlo é feito por @ref NatureProtector.Backoffice.Api.ControlPlane.Services.PostgresControlPlaneService, @ref NatureProtector.Simulator.Host.Services.PostgresSimulationContextSource e @ref NatureProtector.Simulator.Host.Services.PostgresSimulationRunStore.

O fluxo de prevenção usa @ref NatureProtector.Prevention.Host.Processing.PostgresReadingEventInbox para inbox, retries, rejeições e quarentena; usa repositórios PostgreSQL para leituras aceites, avaliações e snapshots; e usa @ref NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore para estado consultável por célula, por área e alertas simples.

@ref NatureProtector.Infrastructure.Influx.Services.InfluxWriteService escreve as medições temporais `accepted_readings`, `risk_assessments` e `area_risk_snapshots`.

## Modelo de persistência

\startuml
title Responsabilidades de persistência
package "Backoffice.Api" {
  [PostgresControlPlaneService]
}

package "Simulator.Host" {
  [PostgresSimulationContextSource]
  [PostgresSimulationRunStore]
}

package "Prevention.Host" {
  [PostgresReadingEventInbox]
  [PostgresAcceptedReadingRepository]
  [PostgresRiskAssessmentRepository]
  [PostgresAreaRiskSnapshotRepository]
  [PostgresAreaOperationalProjectionStore]
  [InfluxWriteService]
}

database "PostgreSQL\ncontrol + pipeline + projection" as Postgres
database "InfluxDB\nseries temporais operacionais" as Influx

[PostgresControlPlaneService] --> Postgres
[PostgresSimulationContextSource] --> Postgres
[PostgresSimulationRunStore] --> Postgres
[PostgresReadingEventInbox] --> Postgres
[PostgresAcceptedReadingRepository] --> Postgres
[PostgresRiskAssessmentRepository] --> Postgres
[PostgresAreaRiskSnapshotRepository] --> Postgres
[PostgresAreaOperationalProjectionStore] --> Postgres
[InfluxWriteService] --> Influx
\enduml

## Grupos de schema

`control` guarda a configuração e a topologia operacional: versões de configuração, áreas, contexto da área, células, perfis de sensor, redes, nós de sensor, cenários, runs de simulação, rule sets, artefactos de dataset e bindings entre cenários e datasets.

`pipeline` guarda a fronteira durável de processamento: inbox de eventos, tentativas, rejeições técnicas e eventos em quarentena.

`projection` guarda o estado operacional produzido pela prevenção: leituras aceites, avaliações de risco, snapshots agregados, estado por célula, estado por área e alertas.

InfluxDB guarda uma projeção temporal própria para observabilidade. Essa escrita é paralela à persistência operacional e não substitui PostgreSQL como fonte de consulta da API.

## Decisões importantes

- O modelo relacional é centralizado num único `DbContext` para tornar explícitos nomes de tabelas, índices, relações e conversores temporais.
- `DateTimeOffset` é normalizado por convenção para UTC na persistência PostgreSQL.
- `EventId` é único na inbox e no log de leituras aceites, apoiando deduplicação e rastreabilidade.
- Projeções atuais existem para leitura rápida pela API e evitam recalcular estado a partir dos logs a cada pedido.
- InfluxDB é usado para leitura temporal e dashboards; não decide retries, quarentena nem estado operacional.

## Estado atual e limitações

O estado implementado já cobre plano de controlo, pipeline durável, logs operacionais e projeções. A API lê diretamente os dados de `control` e `projection`, enquanto o simulador escreve runs em `control.simulation_runs` quando o modo com plano de controlo está ativo.

As limitações conhecidas são: não há mecanismo completo de manutenção operacional da inbox pela API; os alertas têm ciclo de vida simples; os eventos normalizados não são materializados como família própria; e InfluxDB não tem, nesta documentação manual, uma frente de consulta tão detalhada como o modelo PostgreSQL.

## Pontos do repositório a consultar

- `src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`
- `src/NatureProtector.Infrastructure.Postgres/Control/`
- `src/NatureProtector.Infrastructure.Postgres/Pipeline/`
- `src/NatureProtector.Infrastructure.Postgres/Projection/`
- `src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`
- `src/NatureProtector.Prevention.Host/Projection/PostgresAreaOperationalProjectionStore.cs`
- `src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`

## Ligações para páginas relacionadas

- Para a carga inicial de `control`, consultar @ref control_plane_and_bootstrap.
- Para os eventos que alimentam `pipeline` e `projection`, consultar @ref prevention_flow.
- Para a run que escreve em `control.simulation_runs`, consultar @ref simulator_flow.
- Para testes que exercitam persistência e contratos, consultar @ref tests_as_documentation.
