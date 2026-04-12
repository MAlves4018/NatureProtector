# NatureProtector.Core

Este projeto contém a linguagem comum do domínio. É aqui que definimos os conceitos que queremos preservar mesmo que a infraestrutura mude: áreas, limites, grelha territorial, grelha de risco, sensores, deployments, leituras, cenários, snapshots meteorológicos, alertas e recomendações.

## Papel do módulo

- Servir de base semântica para a solução.
- Concentrar validação de invariantes do domínio.
- Evitar dependências de RabbitMQ, ASP.NET Core, InfluxDB ou detalhes de runtime.

## O que existe aqui hoje

- `Areas/`
  - `Area`, `AreaContext` e `GridCell`
- `Primitives/`
  - `Boundaries`, `Location`, `RiskLevel` e `Severity`
- `Readings/`
  - `Reading` e `ReadingValues`
- `Risk/`
  - `RiskAssessment`, `AreaRiskSnapshot`, `RiskCell` e `RuleSet`
- `Scenarios/`
  - `Scenario`, `ScenarioParameters` e `SimulationRun`
- `Sensors/`
  - `Sensor`, `SensorProfile`, `SensorNetwork`, `SensorDeployment` e `SensorType`
- `Weather/`
  - `WeatherSnapshot` e `WindVector`
- `Communication/`
  - `Alert` e `Recommendation`

## Características do desenho atual

- A maior parte dos tipos é imutável depois da construção.
- As validações de intervalo e consistência acontecem logo nos construtores.
- O módulo trabalha com scores normalizados em `[0, 1]` e com níveis qualitativos derivados.
- O desenho favorece objetos de domínio pequenos e reutilizáveis, em vez de entidades cheias de detalhes de infraestrutura.
- A frente de PostgreSQL introduziu a distinção entre grelha territorial (`GridCell`) e grelha de risco (`RiskCell`).
- Os deployments de sensores passaram a ser modelados fora da entidade `Sensor`, para preservar um domínio mais limpo e mais estável.

## O que este módulo não deve fazer

- Não deve conhecer filas, exchanges, tokens, bases de dados ou controladores HTTP.
- Não deve assumir a forma final de persistência.
- Não deve ficar contaminado por DTOs de transporte quando esses DTOs pertencem a contratos de evento.

## Relação com os restantes projetos

- O `Simulator.Host` usa `Scenario`, `ScenarioParameters`, `Sensor`, `SensorProfile` e `SimulationRun`.
- O `Prevention` usa `RiskAssessment` e `AreaRiskSnapshot`.
- O `Infrastructure.Influx` usa tipos de risco e níveis qualitativos para escrever medições.
- O `Backoffice.Api` referencia este projeto para vir a expor a linguagem de domínio do plano de controlo.

## Estado atual

Este é o projeto mais maduro da solução. A cobertura de testes existente em [../../tests/NatureProtector.Core.Tests](../../tests/NatureProtector.Core.Tests) confirma isso e faz dele a melhor porta de entrada para perceber o vocabulário do sistema.

## Nota
Até o simulador, os datasets e o PostgreSQL estarem fechados este módulo pode continuar a sofrer alterações estruturais controladas.
