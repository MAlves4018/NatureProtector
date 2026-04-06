# NatureProtector.Simulator.Host

Este projeto é o ponto de entrada da simulação. A sua responsabilidade atual é construir um contexto de execução, gerar leituras plausíveis com base num cenário e publicá-las como eventos.

## Fluxo ativo de execução

O caminho hoje ligado pelo `Program.cs` é este:

1. O host lê `RabbitMq` e `Simulator` de `appsettings.json`.
2. `GeneratedScenarioManifestLoader` pode sobrepor a configuração base quando `ScenarioManifestPath` está definido.
3. `ScenarioContextFactory` transforma a configuração em `Scenario`, `Sensor`, `SensorProfile` e `SimulationContext`.
4. `SeedProvider` fixa a seed efetiva da execução.
5. `SimulationRunner` percorre os ciclos configurados.
6. `ReadingGenerationService` gera um envelope por sensor e por ciclo.
7. `IReadingPublisher` publica cada envelope.

## Ficheiros principais

- `Program.cs`
  - composição do host
- `Configuration/SimulatorOptions.cs`
  - contrato principal de configuração do simulador
- `Configuration/GeneratedScenarioManifestLoader.cs`
  - leitura de manifestos gerados em `data/manifests/scenarios`
- `Context/ScenarioContextFactory.cs`
- `Context/SimulationContext.cs`
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
- `ScenarioManifestPath` e `ScenarioManifestScenarioKey`.

### Secção `Prevention`

O `appsettings.json` deste projeto ainda contém uma secção `Prevention`, mas o `Program.cs` atual não a usa. Devemos lê-la como configuração residual de uma fase intermédia do projeto, não como parte do fluxo ativo do simulador.

### Manifestos de cenário

O host já consegue ler os ficheiros gerados na pipeline de dados, por exemplo:

- [../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json](../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json)
- [../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json](../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json)

Na prática, o loader consome o bloco `simulator_options` e sobrepõe os campos que existem em `SimulatorOptions`. Campos extra do manifesto podem continuar no ficheiro sem serem ainda usados pelo host.

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
- Integração com os cenários gerados na pipeline de dados.

## Código residual que ainda existe aqui

Este projeto ainda contém ficheiros de ingestão, persistência e validação que não pertencem ao caminho ativo atual:

- `ReadingIngestionWorker.cs`
- `Configuration/PreventionOptions.cs`
- `Presistence/*`
- `Validation/*`

Além disso, esses ficheiros usam o namespace `NatureProtector.Prevention.Host`, o que confirma que devem ser lidos como resíduo de uma fase anterior ou intermédia de refatoração.

## Relação com o resto da solução

- Usa `NatureProtector.Core` para o modelo de cenário, sensores e runs.
- Usa `NatureProtector.Shared` para contratos e mensageria.
- Produz os eventos que o [../NatureProtector.Prevention.Host/README.md](../NatureProtector.Prevention.Host/README.md) consome.
