# Testes

Esta pasta contem os projetos de teste da solucao. O panorama atual continua assimetrico: o dominio central esta bem coberto, mas os modulos de runtime e integracao exigiram trabalho adicional para chegar a uma medicao util de coverage.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
  - contem testes reais para areas, primitivas, leituras, risco, cenarios, sensores e meteorologia
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
  - cobre scoring, snapshots e persistencia in-memory do modulo de prevencao
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
  - cobre serializacao, contratos e topologia de messaging
- [NatureProtector.Simulator.Host.Tests](NatureProtector.Simulator.Host.Tests)
  - cobre contexto, geracao de leituras, publishers e codigo residual de ingestao
- [NatureProtector.Prevention.Host.Tests](NatureProtector.Prevention.Host.Tests)
  - cobre pipeline ativa e comportamento principal do worker de prevencao
- [NatureProtector.Infrastructure.Influx.Tests](NatureProtector.Infrastructure.Influx.Tests)
  - cobre configuracao, DI, guard clauses e execucao util do write service
- [NatureProtector.Backoffice.Api.Tests](NatureProtector.Backoffice.Api.Tests)
  - cobre o arranque minimo da API
- [NatureProtector.IntegrationTests](NatureProtector.IntegrationTests)
  - cobre compatibilidade entre simulador e prevencao sem broker real

## Como executar

Para correr todos os testes disponiveis, devemos executar:

```powershell
dotnet test NatureProtector.sln
```

Se quisermos focar apenas o dominio, devemos executar:

```powershell
dotnet test .\tests\NatureProtector.Core.Tests\NatureProtector.Core.Tests.csproj
```

## Cobertura

O repositorio usa `coverlet.collector` nos projetos de teste e gera coverage com o collector `XPlat Code Coverage` em formato `cobertura`.

Para gerar um relatorio consolidado em HTML e um resumo em texto, devemos executar:

```powershell
.\scripts\tests\generate-coverage-report.ps1
```

Isto:

- limpa `TestResults` antigos
- corre `dotnet test` com `coverage.runsettings`
- junta todos os `coverage.cobertura.xml`
- gera o relatorio em `coveragereport_core`

## Resultados atuais

Na ultima medicao consolidada do relatorio filtrado, gerada em `06/04/2026`, o projeto ficou com:

- `97.9%` de line coverage (`2604/2658`)
- `95.2%` de branch coverage (`779/818`)
- `99.4%` de method coverage (`360/362`)

O relatorio agregado cobre `6` assemblies, `50` classes e `49` ficheiros relevantes para a logica aplicacional. O detalhe navegavel fica em `coveragereport_core/index.html` e o resumo textual em `coveragereport_core/Summary.txt`.

Por assembly, o estado mais recente ficou assim:

- `NatureProtector.Core`: `98.6%`
- `NatureProtector.Infrastructure.Influx`: `95.7%`
- `NatureProtector.Prevention`: `100%`
- `NatureProtector.Prevention.Host`: `100%`
- `NatureProtector.Shared`: `100%`
- `NatureProtector.Simulator.Host`: `96.3%`

Os principais hotspots que ainda justificam trabalho adicional sao:

- `NatureProtector.Simulator.Host.Publishing.RabbitMqReadingPublisher`: `84.1%`
- `NatureProtector.Core.Scenarios.SimulationRun`: `85.5%`
- `NatureProtector.Infrastructure.Influx.Services.InfluxWriteService`: `95.5%`
- `NatureProtector.Simulator.Host.Services.ReadingGenerationService`: `95.1%`
- `NatureProtector.Prevention.Host.Validation.SimpleReadingValidator`: `94.7%`

## Filosofia de coverage

- o foco principal do relatorio consolidado e a logica de dominio, contratos, serializacao, scoring e pipeline
- `Program.cs`, workers e bootstrap de hosting sao tratados como infraestrutura de arranque e sao excluidos do relatorio final consolidado
- codigo gerado, `bin` e `obj` tambem sao excluidos

## Relacao com a documentacao de planeamento

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) ja antecipava a necessidade de aumentar a cobertura em contratos, simulacao, pipeline e integracao. Esta pasta mostra isso de forma concreta.
