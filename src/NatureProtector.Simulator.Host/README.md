# NatureProtector.Simulator.Host

Este projeto é o ponto de entrada da simulação. A sua responsabilidade atual é construir um contexto de execução, gerar leituras plausíveis com base num cenário e publicá-las como eventos.

## Fluxo ativo de execução

O caminho hoje ligado pelo `Program.cs` é este:

1. O host lê `RabbitMq` e `Simulator` de `appsettings.json`.
2. `GeneratedScenarioManifestLoader` pode sobrepor a configuração base quando `ScenarioManifestPath` está definido.
3. Se `ControlPlaneEnabled = false`, `ScenarioContextFactory` transforma a configuração em `Scenario`, `Sensor`, `SensorProfile` e `SimulationContext`.
4. Se `ControlPlaneEnabled = true`, `PostgresSimulationContextSource` lê área, cenário e sensores ativos do schema `control`.
5. `SeedProvider` fixa a seed efetiva da execução.
6. `SimulationRunner` percorre os ciclos configurados.
7. `ReadingGenerationService` gera um envelope por sensor e por ciclo.
8. `IReadingPublisher` publica cada envelope.
9. Quando o plano de controlo está ativo, `PostgresSimulationRunStore` persiste o ciclo de vida da run em `control.simulation_runs`.
10. Os envelopes continuam a usar tempo lógico (`event_time` a partir de `StartTimestamp`), enquanto `SimulationRun.StartedAt` e `EndedAt` passam a refletir o tempo real da execução.

## Ficheiros principais

- `Program.cs`
  - composição do host
- `Configuration/SimulatorOptions.cs`
  - contrato principal de configuração do simulador
- `Configuration/GeneratedScenarioManifestLoader.cs`
  - leitura de ficheiros de definição gerados em `data/manifests/scenarios`
- `Context/ScenarioContextFactory.cs`
- `Context/SimulationContext.cs`
- `Services/PostgresSimulationContextSource.cs`
- `Services/PostgresSimulationRunStore.cs`
- `Services/SeedProvider.cs`
- `Services/ReadingGenerationService.cs`
- `Services/SimulationRunner.cs`
- `Publishing/RabbitMqReadingPublisher.cs`
- `Publishing/ConsoleReadingPublisher.cs`

## Configuração relevante

### Secção `Simulator`

Inclui, entre outros:

- seed opcional;
- número de ciclos;
- intervalo entre ciclos;
- `AreaId`, `ScenarioId` e metadados do cenário;
- valores base de temperatura, humidade e vento;
- taxa de falha e ruído;
- lista de sensores;
- `ScenarioManifestPath` e `ScenarioManifestScenarioKey`;
- `ControlPlaneEnabled`, `ControlPlaneAreaCode` e `ControlPlaneScenarioCode`.

### Ficheiros de definição de cenário

O host já consegue ler os ficheiros gerados na cadeia de preparação de dados, por exemplo:

- [../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json](../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json)
- [../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json](../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json)

Na prática, o loader consome o bloco `simulator_options` e sobrepõe os campos que existem em `SimulatorOptions`. Campos extra do ficheiro de definição podem continuar no ficheiro sem serem ainda usados pelo host.

### Plano de controlo em PostgreSQL

Quando `ControlPlaneEnabled = true`, o host deixa de depender só de `appsettings` e de ficheiros de definição soltos para a topologia da simulação.

Nesse modo ele:

- resolve a área por `AreaId` ou `ControlPlaneAreaCode`;
- resolve o cenário por `ScenarioId` ou `ControlPlaneScenarioCode`;
- lê os `sensor_nodes` ativos e os respetivos `sensor_profiles`;
- usa `simulation_runs` para persistir o estado da execução.

Esse é o perfil local suportado por defeito do repositório. Quando o plano de controlo está ativo, o host deve ser tratado como dependente da baseline bootstrapada em PostgreSQL; o `ScenarioManifestPath` e a lista local de `Sensors` deixam de ser a fonte de verdade operacional.

## Tempo lógico vs. tempo real

O simulador trabalha hoje com duas linhas temporais diferentes e complementares:

- `event_time` em cada envelope representa o tempo lógico do cenário, calculado a partir de `StartTimestamp` e `IntervalSeconds`;
- `SimulationRun.StartedAt` e `SimulationRun.EndedAt` representam o tempo real em que a execução correu no host;
- `control.simulation_runs.logical_start_timestamp` guarda o instante lógico inicial usado para gerar os eventos.

Isto evita misturar o relógio do cenário com o relógio da máquina quando a run é consultada no plano de controlo.

## Sensores e métricas suportados hoje

No estado atual, o gerador suporta de forma explícita:

- `SensorType.Temperature`
- `SensorType.Humidity`
- `SensorType.Wind`

É importante notar que:

- `SensorType.Composite` não é suportado pelo contrato atual de leitura.
- `SensorType.WeatherStation` existe no domínio, mas o gerador atual também não o mapeia para um `SensorMetricType` publicável.
- O contrato partilhado já tem `WindDirection`, mas o gerador atual publica apenas vento como velocidade (`WindSpeed`).

## Publicação efetiva

O projeto regista dois publishers:

- `ConsoleReadingPublisher`
- `RabbitMqReadingPublisher`

Contudo, o `SimulationRunner` recebe uma única implementação de `IReadingPublisher`. Com o registo atual do container de DI, o caminho efetivo de execução fica alinhado com o publisher de RabbitMQ. O publisher de consola existe como alternativa útil para diagnóstico, mas não é o fluxo principal ligado pelo host.

## O que este host já fecha

- Execução determinística baseada em seed.
- Geração de envelopes comuns com `schema_version`, `event_id`, `correlation_id` e `event_time`.
- Publicação para RabbitMQ com mensagens persistentes.
- Integração com os cenários gerados na cadeia de preparação de dados.
- Integração opcional com o plano de controlo em PostgreSQL.

## Relação com o resto da solução

- Usa `NatureProtector.Core` para o modelo de cenário, sensores e runs.
- Usa `NatureProtector.Shared` para contratos e infraestrutura de mensagens.
- Produz os eventos que o [../NatureProtector.Prevention.Host/README.md](../NatureProtector.Prevention.Host/README.md) consome.
