# Testes

Esta pasta contém os projetos de teste da solução. O objetivo da suite atual é proteger a lógica de domínio mais estável, cobrir os caminhos críticos da baseline de prevenção já operacionais em runtime e manter rastreáveis as decisões técnicas da V1.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
  - domínio base: áreas, primitivas, leituras, risco, cenários, sensores e meteorologia;
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
  - scoring, snapshots, `NormalizedReading`, `OperationalEvent`, `ClassifierResult`, elegibilidade, `RiskInput`, `DailyCellState` e persistência in-memory do módulo de prevenção;
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
  - serialização, contratos e topologia de messaging;
- [NatureProtector.Simulator.Host.Tests](NatureProtector.Simulator.Host.Tests)
  - contexto, geração de leituras, publishers, runtime do simulador, `RunOverrides`, seleção determinística de sensores e suporte ao orquestrador de cenários;
- [NatureProtector.Prevention.Host.Tests](NatureProtector.Prevention.Host.Tests)
  - pipeline ativa, inbox, retries, quarentena, classificadores de falha, política de alertas V1, projeções operacionais e adaptadores PostgreSQL do host de prevenção;
- [NatureProtector.Infrastructure.Influx.Tests](NatureProtector.Infrastructure.Influx.Tests)
  - configuração, DI e write service de InfluxDB;
- [NatureProtector.Backoffice.Api.Tests](NatureProtector.Backoffice.Api.Tests)
  - arranque da API, endpoints do control plane, respostas de indisponibilidade, áreas, grelha, configurações, simulation runs e projeções operacionais;
- [NatureProtector.IntegrationTests](NatureProtector.IntegrationTests)
  - compatibilidade entre simulador e prevenção sem broker real.

## Como executar

Para compilar a solution antes da suite:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config
```

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

## Smoke B/C runtime

O smoke B/C executa a prova operacional reprodutivel da V1 quando a API e a infraestrutura local estao disponiveis. Ele nao substitui os testes unitarios nem recalcula risco; apenas orquestra runs e recolhe evidencia persistida.

Validacao sem executar HTTP/runtime:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\run-v1-bc-smoke.ps1 -DryRun
```

Execucao real, com `Backoffice.Api` em Development e PostgreSQL/RabbitMQ/Prevention/Simulator acessiveis:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\run-v1-bc-smoke.ps1 `
  -ApiBaseUrl http://localhost:5254 `
  -AreaCode proenca-a-nova
```

O script gera uma pasta `docs/evidence/runs/v1-bc-smoke-<timestamp>/` com:

* `summary.md`;
* `run-spec.resolved.json`;
* `run-b.json` e `run-c.json`;
* `audit-b.json` e `audit-c.json`;
* `runtime-summary.json`;
* `np-vs-fwi-kbdi.json`;
* `portuguese-context-proxy.json`;
* `kbdi-series-context.json`;
* `components.json`;
* `daily-cell-state.json`;
* `degradation-effects.json`;
* `b-vs-c.json`;
* `compare-b-vs-c.json`;
* diagnostics de qualidade, contexto diario, classes NP/FWI/KBDI, proxy portugues candidato e coverage/freshness.

O smoke valida que FWI/KBDI aparecem como calculados ou como Missing/Partial com limitation explicita. O `PortugueseContextRiskProxy` e candidato e nao deve ser apresentado como RCM/PIR/IPMA oficial. O KBDI e diario/acumulativo; quando falta historico antecedente, espera-se status/limitation de historico limitado em vez de leitura como calibrada.

Por defeito, a smoke recolhe evidencia via API e nao ativa `collectEvidence` no endpoint de arranque do `Simulator.Host`. Isto evita bloqueios em stdout/stderr de processos long-running. Se for necessario recolher tambem logs/evidencia do processo filho, usar `-CollectRuntimeProcessEvidence`.

Se a API ou Docker/PostgreSQL/RabbitMQ nao estiverem disponiveis, o script escreve `limitations.md` com uma mensagem objetiva. A execucao real continua a ser opcional/manual para nao tornar a suite `dotnet test` dependente de broker, base de dados ou processos long-running.

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

## Warnings conhecidos

A execução atual pode apresentar o warning `NU1902` associado ao pacote `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`. Este warning é conhecido e não bloqueia a execução da suite, mas deve continuar registado enquanto a dependência não for atualizada ou justificada.

## Resultados atuais

Atualizacao de `28/05/2026`: a medicao consolidada mais recente em `coveragereport_core/Summary.txt` reporta `87.6%` de line coverage, `76.8%` de branch coverage, `91.9%` de method coverage e `86.1%` de full method coverage. A queda face a medicoes anteriores e conhecida e vem sobretudo de DTOs, diagnostics, glue runtime/API, migrations e scripts de evidencia adicionados na frente V1. A prioridade imediata e estabilidade funcional dos indices NP/FWI/KBDI; testes adicionais devem focar `ControlRuntimeController`, diagnostics vazios/preenchidos, projection status e smoke B/C, sem criar testes artificiais apenas para inflar coverage.

Medição histórica de `16/05/2026`, antes da frente V1 de diagnostics/API:

* `97.6%` de line coverage (`6677/6837`);
* `90.1%` de branch coverage (`1549/1719`);
* `97.1%` de method coverage (`930/957`);
* `92.9%` de full method coverage (`890/957`).

O relatório agregado cobre `7` assemblies, `116` classes e `89` ficheiros relevantes para a lógica aplicacional. O detalhe navegável fica em `coveragereport_core/index.html` e o resumo textual em `coveragereport_core/Summary.txt`.

Por assembly, nessa medição histórica:

* `NatureProtector.Backoffice.Api`: `99.0%`
* `NatureProtector.Core`: `99.2%`
* `NatureProtector.Infrastructure.Influx`: `95.0%`
* `NatureProtector.Prevention`: `98.1%`
* `NatureProtector.Prevention.Host`: `96.7%`
* `NatureProtector.Shared`: `90.7%`
* `NatureProtector.Simulator.Host`: `97.3%`

A melhoria mais expressiva desta iteração ocorreu pela expansão sistemática de testes de domínio, validação, policy, pipeline, API, Influx configurável e orquestração do simulador. A cobertura global passou de `91.3%` para `97.6%` em line coverage, de `80.9%` para `90.1%` em branch coverage e de `93.4%` para `97.1%` em method coverage.

Os componentes prioritários ficaram assim:

* `NatureProtector.Backoffice.Api.Controllers.ControlAreasController`: `100%`
* `NatureProtector.Backoffice.Api.Controllers.ControlConfigurationsController`: `100%`
* `NatureProtector.Backoffice.Api.Controllers.ControlSimulationRunsController`: `100%`
* `NatureProtector.Backoffice.Api.ControlPlane.Controllers.ControlPlaneControllerBase`: `100%`
* `NatureProtector.Core.Risk.RiskAssessment`: `100%`
* `NatureProtector.Core.Risk.RiskCell`: `100%`
* `NatureProtector.Core.Areas.GridCell`: `100%`
* `NatureProtector.Core.Sensors.SensorDeployment`: `100%`
* `NatureProtector.Prevention.Risk.ClassifierResult`: `100%`
* `NatureProtector.Prevention.Risk.RiskEligibilityResult`: `100%`
* `NatureProtector.Prevention.Risk.RiskInput`: `100%`
* `NatureProtector.Prevention.Host.Processing.DefaultProcessingFailureClassifier`: `100%`
* `NatureProtector.Prevention.Host.Projection.V1AlertPolicy`: `100%`
* `NatureProtector.Prevention.Host.Configuration.PreventionHostOptionsValidator`: `100%`
* `NatureProtector.Infrastructure.Influx.Services.SafeInfluxWriteService`: `100%`
* `NatureProtector.Simulator.Host.Configuration.SimulatorOptionsValidator`: `100%`
* `NatureProtector.Simulator.Host.Services.SimulationRunner`: `100%`

Os hotspots que ainda justificam trabalho adicional são sobretudo caminhos de integração externa, observabilidade e branches técnicos:

* `NatureProtector.Shared.Observability.PostgresBootstrapTelemetry`: `0%`
* `NatureProtector.Shared.Observability.NatureProtectorObservabilityExtensions`: `89.6%`
* `NatureProtector.Simulator.Host.Publishing.RabbitMqReadingPublisher`: `85.5%`
* `NatureProtector.Infrastructure.Influx.Services.InfluxWriteService`: `86.2%`
* `NatureProtector.Prevention.Host.Processing.ReadingEventProcessingService`: `89.8%`
* `NatureProtector.Prevention.Host.Projection.InMemoryAreaOperationalProjectionStore`: `88.7%`
* `NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore`: `93.6%`
* `NatureProtector.Prevention.Risk.SimpleRiskScoringService`: `93.2%`

Estes valores não significam ausência de testes funcionais. Em vários casos, o que falta cobrir são branches de telemetry, `ActivitySource`, integração real com RabbitMQ/InfluxDB, fallback defensivo ou caminhos que exigiriam infraestrutura externa ou refactor específico para serem testados sem fragilidade.

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

## Vagas recentes de testes V1

Durante a consolidação da V1 foram adicionadas várias vagas de testes focadas em comportamento útil, não apenas em percentagem de coverage.

A primeira vaga reforçou componentes de domínio e policy:

* validações de `Area`, `GridCell` e `SensorDeployment`;
* invariantes de `ClassifierResult` e `RiskEligibilityResult`;
* semântica de `Blocked`, `PartialButUsable` e `CompleteEligible`;
* `V1AlertPolicy`, incluindo thresholds, Warning, Alarm e histerese;
* `DefaultProcessingFailureClassifier`;
* `ExpectedUniqueViolationDetector`;
* `SimulatorOptionsValidator`.

A segunda vaga reforçou API, infraestrutura leve e orquestração:

* endpoints do Backoffice para áreas, grelha, configurações e simulation runs;
* respostas `503 ProblemDetails` quando o control plane está indisponível;
* `SafeInfluxWriteService` com fakes, sem servidor InfluxDB real;
* `PostgresSimulationContextSource` com SQLite/in-memory;
* `SimulationRunner`, incluindo falha de publisher e transições de run;
* `SimulationRun` e transições de ciclo de vida;
* `ExpectedUniqueConstraint`.

A vaga final reforçou branches ainda úteis:

* validação de `PreventionHostOptionsValidator`;
* overloads e limites de `RiskAssessment`;
* tendência em `RiskCell`;
* invariantes de `DailyCellState`;
* normalização/fallback em `RiskInput`;
* duplicados e retry no `InMemoryReadingEventInbox`;
* parsing isolado de `InfluxDbSettingsLoader`;
* paths indisponíveis dos controllers Backoffice.

A suite passou a proteger de forma mais explícita a cadeia V1 entre leitura operacional, normalização, elegibilidade, input de risco, assessment, alertas, projeções, API e orquestração de runs.

## Filosofia de coverage

* o foco principal do relatório consolidado é a lógica aplicacional e o comportamento observável;
* `Program.cs`, workers e bootstrap de hosting continuam excluídos do relatório agregado;
* código gerado, `bin` e `obj` também continuam excluídos;
* o objetivo não é perseguir `100%` artificial, mas cobrir caminhos críticos, regressões relevantes e decisões de domínio;
* line coverage e method coverage elevados são úteis, mas branch coverage é tratado com mais cautela, porque muitos branches restantes pertencem a telemetry, integração externa, fallbacks defensivos ou wrappers de bibliotecas;
* não se pretende cobrir branches de `ActivitySource`, `Meter`, exporters OpenTelemetry, RabbitMQ real ou InfluxDB real com testes frágeis apenas para subir percentagem;
* código de observabilidade sem decisão funcional pode ser considerado limite conhecido ou candidato futuro a exclusão explícita, desde que justificado;
* código de domínio, pipeline, scoring, alert policy, contratos, normalização, elegibilidade e persistência com lógica própria não deve ser excluído por conveniência.

## Relação com o roadmap

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) continua a orientar as próximas vagas de testes.

As vagas recentes reduziram várias lacunas anteriores:

* semântica de `ClassifierResult`, quality flags e elegibilidade;
* separação entre `Blocked`, `PartialButUsable` e `CompleteEligible`;
* `RiskInput` como fronteira pré-scoring;
* `RiskAssessment` com `BaseRisk`, `AdjustedScore` e compatibilidade `RiskScore`;
* política interna de alertas V1 com `None`, `Warning`, `Alarm` e histerese;
* exposição de `alertState` e projeções pela `Backoffice.Api`;
* runtime do simulador com `RunOverrides` e orquestração por `run-spec.json`;
* recolha de evidência por run;
* modos configuráveis e tolerantes de escrita para InfluxDB.

Continuam como trabalho futuro:

* testes end-to-end mais completos com cenários canónicos;
* validação de integração real RabbitMQ/PostgreSQL/InfluxDB em ambiente controlado;
* testes da futura API/site para lançar e acompanhar runs;
* hardening de cancelamento, timeout e limpeza de runs;
* testes adicionais de projeções e processamento quando houver políticas finais de alertas/cooldown/persistência;
* validação externa e científica do modelo, que continua fora do âmbito dos testes unitários atuais.
