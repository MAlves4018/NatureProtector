# Testes

Esta pasta contém os projetos de teste da solução. O panorama atual continua assimétrico: o domínio central está bem coberto, mas os módulos de runtime e integração exigiram trabalho adicional para chegar a uma medição útil de coverage.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
- contém testes reais para áreas, primitivas, leituras, risco, cenários, sensores e meteorologia
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
- cobre scoring, snapshots e persistência in-memory do módulo de prevenção
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
- cobre serialização, contratos e topologia de messaging
- [NatureProtector.Simulator.Host.Tests](NatureProtector.Simulator.Host.Tests)
- cobre contexto, geração de leituras, publishers e código residual de ingestão
- [NatureProtector.Prevention.Host.Tests](NatureProtector.Prevention.Host.Tests)
- cobre pipeline ativa e comportamento principal do worker de prevenção
- [NatureProtector.Infrastructure.Influx.Tests](NatureProtector.Infrastructure.Influx.Tests)
- cobre configuração, DI, guard clauses e execução útil do write service
- [NatureProtector.Backoffice.Api.Tests](NatureProtector.Backoffice.Api.Tests)
- cobre o arranque mínimo da API
- [NatureProtector.IntegrationTests](NatureProtector.IntegrationTests)
- cobre compatibilidade entre simulador e prevenção sem broker real

## Como executar

Para correr todos os testes disponíveis, devemos executar:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1
```

Se quisermos focar apenas o domínio, devemos executar:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\tests\NatureProtector.Core.Tests\NatureProtector.Core.Tests.csproj --nologo -v minimal -m:1
```

## Cobertura

O repositório usa `coverlet.collector` nos projetos de teste e gera coverage com o collector `XPlat Code Coverage` em formato `cobertura`.

Para gerar um relatório consolidado em HTML e um resumo em texto, devemos executar:

```powershell
.\scripts\tests\generate-coverage-report.ps1
```

Isto:

- limpa `TestResults` antigos
- corre `dotnet test` com `coverage.runsettings`
- junta todos os `coverage.cobertura.xml`
- gera o relatório em `coveragereport_core`

## Resultados atuais

Na medição consolidada mais recente, gerada em `12/04/2026`, o projeto ficou com:

* `81.2%` de line coverage (`4094/5040`)
* `80.0%` de branch coverage (`997/1246`)
* `88.4%` de method coverage (`633/716`)

A geração do relatório foi concluída com `584` testes executados, todos com sucesso.

O relatório agregado cobre `7` assemblies, `85` classes e `71` ficheiros relevantes para a lógica aplicacional. O detalhe navegável fica em `coveragereport_core/index.html` e o resumo textual em `coveragereport_core/Summary.txt`.

Face à medição anterior, esta leitura mostra uma melhoria material da cobertura global, em especial no `Backoffice.Api` e no `Simulator.Host`. O caso mais evidente é o de `NatureProtector.Backoffice.Api.ControlPlane.Services.PostgresControlPlaneService`, que deixou de estar sem cobertura e passou para cobertura praticamente total. Em contrapartida, o principal bloco ainda fraco continua a ser o `NatureProtector.Prevention.Host`, sobretudo nos adaptadores PostgreSQL de persistência, inbox e projeções.

Por assembly, o estado mais recente ficou assim:

* `NatureProtector.Backoffice.Api`: `89.4%`
* `NatureProtector.Core`: `91.7%`
* `NatureProtector.Infrastructure.Influx`: `94.1%`
* `NatureProtector.Prevention`: `96.2%`
* `NatureProtector.Prevention.Host`: `50.8%`
* `NatureProtector.Shared`: `100%`
* `NatureProtector.Simulator.Host`: `92.9%`

Os principais hotspots que ainda justificam trabalho adicional são:

* `NatureProtector.Prevention.Host.Persistence.PostgresAcceptedReadingRepository`: `0%`
* `NatureProtector.Prevention.Host.Persistence.PostgresAreaRiskSnapshotRepository`: `0%`
* `NatureProtector.Prevention.Host.Processing.PostgresReadingEventInbox`: `0%`
* `NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore`: `0%`
* `NatureProtector.Prevention.Host.Persistence.PostgresRiskAssessmentRepository`: `26%`
* `NatureProtector.Backoffice.Api.ControlPlane.Contracts.GridCellResponse`: `20%`

Ao nível de risco por complexidade e baixa cobertura, o relatório destaca sobretudo métodos em `PostgresAreaOperationalProjectionStore`, `PostgresReadingEventInbox`, `DefaultProcessingFailureClassifier`, `PostgresRiskAssessmentRepository`, `RabbitMqReadingPublisher.EnsureChannel()` e o construtor de `NatureProtector.Core.Areas.GridCell`.

Alguns hotspots referidos anteriormente deixaram de ser válidos nesta medição. Em particular, `NatureProtector.Backoffice.Api.ControlPlane.Services.PostgresControlPlaneService` já não é um ponto fraco, `NatureProtector.Simulator.Host.Services.PostgresSimulationContextSource` já apresenta `85.4%`, `NatureProtector.Simulator.Host.Services.PostgresSimulationRunStore` está em `100%`, e a referência histórica a `NatureProtector.Prevention.Host.Validation.SimpleReadingValidator` continua a não se aplicar porque esse código já não existe na runtime atual.

## Filosofia de coverage

- o foco principal do relatório consolidado é a lógica de domínio, contratos, serialização, scoring e pipeline
- `Program.cs`, workers e bootstrap de hosting são tratados como infraestrutura de arranque e são excluídos do relatório final consolidado
- código gerado, `bin` e `obj` também são excluídos

## Relação com a documentação de planeamento

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) já antecipava a necessidade de aumentar a cobertura em contratos, simulação, pipeline e integração. Esta pasta mostra isso de forma concreta.
