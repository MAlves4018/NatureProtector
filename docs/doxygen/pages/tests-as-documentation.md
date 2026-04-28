@page tests_as_documentation Testes como documentação

# Testes como documentação

## Objetivo da página

Mostrar que testes ajudam a compreender o comportamento real do NatureProtector e como usá-los como documentação executável, sem transformar esta página num inventário completo da pasta `tests/`.

## Âmbito

Esta página aponta para testes que explicam contratos, decisões de runtime e limites atuais. O foco está nos testes que tornam observáveis os fluxos de simulador, prevenção, API, persistência e compatibilidade entre produtor e consumidor.

Não afirma que todos os comportamentos estejam cobertos por testes end-to-end com infraestrutura real. Alguns testes usam fakes ou repositórios em memória para isolar semântica.

## Componentes principais

Os testes de `NatureProtector.Core.Tests` fixam invariantes de domínio, como áreas, células, sensores, cenários, runs e risco. `NatureProtector.Shared.Tests` fixa contratos de mensagem, serialização JSON e constantes de topologia.

`NatureProtector.Simulator.Host.Tests` documenta validação de opções, resolução de contexto, geração de leituras, publishers e ciclo de vida de runs. `NatureProtector.Prevention.Host.Tests` documenta validação de envelope, `ack`, rejeição antes da inbox, retries, quarentena, pipeline e projeções. `NatureProtector.Backoffice.Api.Tests` documenta a ponte entre PostgreSQL e respostas HTTP. `NatureProtector.IntegrationTests` fecha compatibilidade entre envelopes produzidos pelo simulador e a pipeline da prevenção sem depender de um broker vivo.

## Fluxo de leitura recomendado

Para perceber o simulador, começar por `SimulationRunnerTests.cs`, `ReadingGenerationServiceTests.cs` e `PostgresSimulationContextSourceTests.cs`. Estes testes mostram tempo lógico, número de envelopes, cancelamento, resolução de contexto e limites das métricas suportadas.

Para perceber a prevenção, começar por `PreventionWorkerTests.cs`, `ReadingEventProcessingServiceTests.cs`, `InboxRetryWorkerTests.cs` e `ReadingRiskPipelineTests.cs`. Em conjunto, estes testes mostram a diferença entre rejeição antes da inbox, evento aceite para processamento, retry, quarentena e atualização de projeções.

Para perceber a API, ler `ControlPlaneApiTests.cs` e `PostgresControlPlaneServiceTests.cs`. Estes testes são a forma mais rápida de ver que dados vêm de `control.*`, que dados vêm de `projection.*` e que a ativação de configuração é a escrita confirmada.

Para perceber compatibilidade end-to-end sem infraestrutura externa, ler `SimulatorToPreventionCompatibilityTests.cs`.

## Decisões importantes documentadas por testes

- O simulador separa timestamps lógicos dos timestamps reais de ciclo de vida da run.
- Eventos com contrato inválido são rejeitados e recebem `ack`, sem entrar no processamento de risco.
- O `ack` de eventos válidos acontece depois da materialização na inbox.
- Duplicados não são reprocessados.
- Falhas transitórias agendam retry; falhas permanentes ou tentativas esgotadas resultam em quarentena.
- A pipeline aceite persiste leitura, avaliação, snapshot e escrita Influx.
- A compatibilidade entre `ReadingGenerationService` e `ReadingRiskPipeline` é exercitada sem RabbitMQ para isolar o contrato.

## Estado atual e limitações

O estado implementado tem uma bateria ampla que documenta domínio, contratos, hosts, API, Influx e integração curta. Os testes são particularmente úteis porque condensam fluxos longos em cenários pequenos.

As limitações conhecidas devem ser mantidas explícitas. Nem todos os testes usam PostgreSQL, RabbitMQ ou InfluxDB reais. A observabilidade visual por Grafana não é validada por estes testes. A existência de testes não substitui a leitura de `Program.cs`, `NatureProtectorControlDbContext` e serviços centrais quando a dúvida é de composição ou persistência real.

## Pontos do repositório a consultar

- `tests/NatureProtector.Simulator.Host.Tests/Services/SimulationRunnerTests.cs`
- `tests/NatureProtector.Simulator.Host.Tests/Services/ReadingGenerationServiceTests.cs`
- `tests/NatureProtector.Simulator.Host.Tests/Services/PostgresSimulationContextSourceTests.cs`
- `tests/NatureProtector.Prevention.Host.Tests/Processing/PreventionWorkerTests.cs`
- `tests/NatureProtector.Prevention.Host.Tests/Processing/ReadingEventProcessingServiceTests.cs`
- `tests/NatureProtector.Prevention.Host.Tests/Processing/ReadingRiskPipelineTests.cs`
- `tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiTests.cs`
- `tests/NatureProtector.IntegrationTests/Flow/SimulatorToPreventionCompatibilityTests.cs`

## Ligações para páginas relacionadas

- Para entender o bootstrap que a API pressupõe, consultar @ref control_plane_and_bootstrap.
- Para o comportamento nominal do simulador, consultar @ref simulator_flow.
- Para a pipeline de prevenção e falhas, consultar @ref prevention_flow.
- Para a persistência observada nos testes, consultar @ref persistence_model.
