# Testes

Esta pasta contém os projetos de teste da solução. O objetivo da suite atual é duplo: proteger a lógica de domínio mais estável e cobrir os caminhos críticos da baseline de prevenção que já estão operacionais em runtime.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
  - domínio base: áreas, primitivas, leituras, risco, cenários, sensores e meteorologia;
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
  - scoring, snapshots e persistência in-memory do módulo de prevenção;
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
  - serialização, contratos e topologia de messaging;
- [NatureProtector.Simulator.Host.Tests](NatureProtector.Simulator.Host.Tests)
  - contexto, geração de leituras, publishers e runtime do simulador;
- [NatureProtector.Prevention.Host.Tests](NatureProtector.Prevention.Host.Tests)
  - pipeline ativa, inbox, retries, quarentena e adaptadores PostgreSQL do host de prevenção;
- [NatureProtector.Infrastructure.Influx.Tests](NatureProtector.Infrastructure.Influx.Tests)
  - configuração, DI e write service de InfluxDB;
- [NatureProtector.Backoffice.Api.Tests](NatureProtector.Backoffice.Api.Tests)
  - arranque mínimo da API e serviço de controlo;
- [NatureProtector.IntegrationTests](NatureProtector.IntegrationTests)
  - compatibilidade entre simulador e prevenção sem broker real.

## Como executar

Para correr todos os testes disponíveis:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1
````

Para focar apenas a `Prevention.Host`:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\tests\NatureProtector.Prevention.Host.Tests\NatureProtector.Prevention.Host.Tests.csproj --nologo -v minimal
```

## Coverage

O repositório usa `coverlet.collector` e agrega os resultados com `reportgenerator`.

Para gerar o relatório consolidado:

```powershell
.\scripts\tests\generate-coverage-report.ps1
```

Este comando:

* limpa `TestResults` antigos;
* corre `dotnet test` com `coverage.runsettings`;
* agrega todos os `coverage.cobertura.xml`;
* gera HTML e `Summary.txt` em `coveragereport_core`.

## Resultados atuais

Na medição consolidada mais recente, gerada em `28/04/2026`, o projeto ficou com:

* `91.0%` de line coverage (`5037/5530`);
* `82.3%` de branch coverage (`1133/1376`);
* `91.8%` de method coverage (`680/740`).

O relatório agregado cobre `7` assemblies, `91` classes e `74` ficheiros relevantes para a lógica aplicacional. O detalhe navegável fica em `coveragereport_core/index.html` e o resumo textual em `coveragereport_core/Summary.txt`.

Por assembly, o estado atual é:

* `NatureProtector.Backoffice.Api`: `89.5%`
* `NatureProtector.Core`: `91.7%`
* `NatureProtector.Infrastructure.Influx`: `75.8%`
* `NatureProtector.Prevention`: `96.2%`
* `NatureProtector.Prevention.Host`: `91.0%`
* `NatureProtector.Shared`: `90.6%`
* `NatureProtector.Simulator.Host`: `92.9%`

A melhoria mais expressiva desta iteração ocorreu em `NatureProtector.Prevention.Host`, que subiu de cerca de `52%` para `91%` depois da cobertura dos adaptadores PostgreSQL e da pipeline durável.

Os componentes prioritários dessa vaga ficaram assim:

* `NatureProtector.Prevention.Host.Persistence.PostgresAcceptedReadingRepository`: `100%`
* `NatureProtector.Prevention.Host.Persistence.PostgresAreaRiskSnapshotRepository`: `100%`
* `NatureProtector.Prevention.Host.Persistence.PostgresRiskAssessmentRepository`: `100%`
* `NatureProtector.Prevention.Host.Processing.PostgresReadingEventInbox`: `98%`
* `NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore`: `88.8%`

Os hotspots que ainda justificam trabalho adicional são:

* `NatureProtector.Prevention.Host.Processing.DefaultProcessingFailureClassifier`: `58.4%`
* `NatureProtector.Infrastructure.Influx.Services.InfluxWriteService`: `65.4%`
* `NatureProtector.Backoffice.Api.ControlPlane.Contracts.GridCellResponse`: `20%`
* construtores e primitivas específicas em `NatureProtector.Core.Areas` e `NatureProtector.Core.Sensors`

## Nota sobre provider de teste

Os testes de persistência da `Prevention.Host` usam `SQLite` in-memory porque permitem validar comportamento relacional sem depender de serviços externos. Há, no entanto, uma limitação importante: `SQLite` não traduz `ORDER BY` sobre `DateTimeOffset` da mesma forma que `PostgreSQL`.

Por isso, o comportamento de ordenação crítica nos adaptadores PostgreSQL ficou protegido por testes e por uma abordagem segura no código que mantém a semântica em runtime sem esconder a diferença entre providers.

## Nota sobre InfluxDB em testes

Os testes do repositório não dependem de um servidor InfluxDB real para validar a pipeline operacional.

Nesta fase, a cobertura de `NatureProtector.Infrastructure.Influx` e da `Prevention.Host` valida:

* modo `NoOp` quando `InfluxDb:Enabled=false`;
* tolerância a falhas quando `InfluxDb:FailPipelineOnWriteError=false`;
* comportamento estrito quando `InfluxDb:FailPipelineOnWriteError=true`;
* ativação ou desativação por measurement para `accepted_readings`, `risk_assessments` e `area_risk_snapshots`.

Isto reforça a decisão arquitetural atual: `PostgreSQL` permanece o estado durável da pipeline e `InfluxDB` é observabilidade temporal configurável.

## Filosofia de coverage

* o foco principal do relatório consolidado é a lógica aplicacional e o comportamento observável;
* `Program.cs`, workers e bootstrap de hosting continuam excluídos do relatório agregado;
* código gerado, `bin` e `obj` também continuam excluídos;
* o objetivo não é perseguir `100%` artificial, mas cobrir caminhos críticos e regressão relevante.

## Relação com o roadmap

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) continua a orientar as próximas vagas de testes, nomeadamente:

* semântica completa de rejeição, retry e quarentena;
* modos locais e falhas em `InfluxDB`;
* casos canónicos end-to-end;
* superfície da `Backoffice.Api` e comportamento sem dados.
