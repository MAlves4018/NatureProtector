# NatureProtector

NatureProtector e um repositorio .NET 9 para suporte tecnico a prevencao de incendios florestais. Na fase atual, o foco esta no modulo de prevencao e numa baseline local demonstravel que liga simulacao, transporte de eventos, persistencia duravel, observabilidade e consulta operacional.

O repositorio nao deve ser lido como se toda a plataforma estivesse concluida. O estado atual corresponde a uma V1 executavel e documentada da baseline de prevencao, centrada na area piloto de Proenca-a-Nova e orientada pela implementacao do repositorio e pelos documentos referentes ao segundo momento de pesquisa.

## Escopo atual

- foco funcional no modulo de prevencao;
- area piloto Proenca-a-Nova;
- baseline local demonstravel com `Simulator.Host`, `RabbitMQ`, `Prevention.Host`, `PostgreSQL`, `InfluxDB`, `Grafana` e `Backoffice.Api`;
- observabilidade inicial com OpenTelemetry e infraestrutura local de apoio;
- documentacao narrativa, tecnica e arquitetural em evolucao controlada.

## Arquitetura atual

- `Simulator.Host`
  - resolve contexto, cria `simulation_runs` quando o control plane esta ativo e publica leituras simuladas;
- `RabbitMQ`
  - barramento de eventos entre simulador e pipeline de prevencao;
- `Prevention.Host`
  - valida entrada, materializa inbox duravel, processa retries e quarentena, calcula risco e atualiza projeccoes;
- `PostgreSQL`
  - fonte de verdade para `control`, estado duravel da pipeline e projeccoes operacionais;
- `InfluxDB`
  - series temporais e telemetria operacional;
- `Grafana`
  - observacao local sobre InfluxDB;
- `Backoffice.Api`
  - superficie HTTP atual para leitura do plano de controlo e do estado operacional.

Nao existe ainda, nesta baseline, uma web UI final integrada como parte estabilizada da entrega.

## Estado atual da implementacao

### Ja implementado

- plano de controlo em `PostgreSQL`;
- bootstrap da baseline local do plano de controlo;
- `Simulator.Host` ligado ao plano de controlo quando `ControlPlaneEnabled = true`;
- `simulation_runs` persistidas em `control.simulation_runs`;
- inbox duravel em `pipeline.event_inbox`;
- rejeicao antecipada de eventos invalidos;
- retry interno e quarentena persistida;
- persistencia de leituras aceites, avaliacoes de risco, snapshots e projeccoes em `PostgreSQL`;
- API de leitura para configuracoes, areas, sensores, cenarios, runs e estado operacional;
- observabilidade inicial com `OpenTelemetry`, `InfluxDB`, `Grafana`, Doxygen, DocFX e Structurizr DSL.

### Parcialmente implementado

- semantica completa de `accepted`, `rejected` e `normalized` como superficie de eventos publicada;
- separacao do simulador em camadas `TruthSnapshot`, `LocalObservation` e `OperationalEvent`;
- score operacional final e politica final de alertas;
- agregacao de area mais avancada;
- modo local mais explicito para reduzir ou desligar InfluxDB;
- cobertura de testes dos componentes runtime ainda desigual entre modulos.

### Experimental ou dependente de decisao

- `src/NatureProtector.AppHost/` com .NET Aspire;
- integracao futura com collector de observabilidade;
- refinamento metodologico dependente dos documentos de referencia do segundo momento de pesquisa.

## Como executar localmente

### Pre-requisitos

- `.NET SDK 9.0`
- Docker Desktop ou equivalente com `docker compose`
- PowerShell

### Configuracao local

1. Criar `.env` a partir de `.env.example`.
2. Confirmar as credenciais locais para `RabbitMQ`, `PostgreSQL`, `InfluxDB` e `Grafana`.

### Levantar a baseline

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
```

### Materializar o plano de controlo

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

### Arrancar os hosts

Em terminais separados:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Backoffice.Api
dotnet run --project .\src\NatureProtector.Prevention.Host
dotnet run --project .\src\NatureProtector.Simulator.Host
```

### Validacao minima

- o bootstrap deve terminar sem erros;
- `Simulator.Host` deve publicar leituras;
- `Prevention.Host` deve materializar inbox, processamento e projeccoes;
- `Backoffice.Api` deve responder em `http://localhost:5254`.

## Como validar que o sistema esta vivo

- `RabbitMQ`
  - abrir `http://localhost:15672` e confirmar a fila `np.ingestion.readings`;
- `PostgreSQL`
  - confirmar que o bootstrap populou `control.*` e que a runtime escreve em `pipeline.*` e `projection.*`;
- `InfluxDB`
  - confirmar que o contentor `np-influxdb` esta operacional e que as escritas surgem nos logs e dashboards;
- `Grafana`
  - abrir `http://localhost:3000` e validar o datasource local;
- `Backoffice.Api`
  - testar `http://localhost:5254/api/control/configurations/active` e `http://localhost:5254/api/control/areas`;
- logs dos hosts
  - observar `Simulator.Host`, `Prevention.Host` e `Backoffice.Api` para fluxo nominal, retries e escrita de projeccoes.

## Build, testes e coverage

Para compilar a solucao:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet build .\NatureProtector.sln --nologo -v minimal -m:1 --configfile NuGet.Config
```

Para correr todos os testes:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1
```

Para gerar coverage consolidada:

```powershell
.\scripts\tests\generate-coverage-report.ps1
```

O relatorio consolidado fica em `coveragereport_core/index.html`.

## Documentacao relacionada

- [docs/README.md](docs/README.md)
- [docs/architecture/README.md](docs/architecture/README.md)
- [docs/architecture/implementation.md](docs/architecture/implementation.md)
- [docs/architecture/code-and-design-review.md](docs/architecture/code-and-design-review.md)
- [docs/doxygen/pages/mainpage.md](docs/doxygen/pages/mainpage.md)
- [docs/docfx/docfx.json](docs/docfx/docfx.json)
- [docs/structurizr/README.md](docs/structurizr/README.md)
- [docs/planning/project-completion-roadmap.md](docs/planning/project-completion-roadmap.md)

## Limitacoes conhecidas

- o score de risco continua preliminar e nao deve ser lido como modelo metodologico final;
- o simulador ainda nao separa completamente verdade fisica, observacao local e falha de transporte;
- a semantica final de `accepted`, `rejected` e `normalized` ainda precisa de consolidacao;
- `InfluxDB` continua a ser o principal candidato a gargalo local na baseline atual;
- `AppHost` e a camada Aspire permanecem experimentais e fora do caminho critico da solucao;
- a consolidacao metodologica continua dependente dos documentos de referencia do segundo momento de pesquisa.
