# NatureProtector

NatureProtector é um repositório .NET 9 para suporte técnico à prevenção de incêndios florestais. Na fase atual, o foco está no módulo de prevenção e numa baseline local demonstrável que liga simulação, transporte de eventos, persistência durável, observabilidade e consulta operacional.

O repositório não deve ser lido como se toda a plataforma estivesse concluída. O estado atual corresponde a uma V1 executável e documentada da baseline de prevenção, centrada na área piloto de Proença-a-Nova e orientada pela implementação do repositório e pelos documentos referentes ao segundo momento de pesquisa.

## Escopo atual

- foco funcional no módulo de prevenção;
- área piloto Proença-a-Nova;
- baseline local demonstrável com `Simulator.Host`, `RabbitMQ`, `Prevention.Host`, `PostgreSQL`, `InfluxDB`, `Grafana` e `Backoffice.Api`;
- observabilidade inicial com OpenTelemetry e infraestrutura local de apoio;
- documentação narrativa, técnica e arquitetural em evolução controlada.

## Arquitetura atual

- `Simulator.Host`
  - resolve contexto, cria `simulation_runs` quando o control plane está ativo e publica leituras simuladas;
- `RabbitMQ`
  - barramento de eventos entre simulador e pipeline de prevenção;
- `Prevention.Host`
  - valida entrada, materializa inbox durável, processa retries e quarentena, calcula risco e atualiza projeções;
- `PostgreSQL`
  - fonte de verdade para `control`, estado durável da pipeline e projeções operacionais;
- `InfluxDB`
  - séries temporais e telemetria operacional;
- `Grafana`
  - observação local sobre InfluxDB;
- `Backoffice.Api`
  - superfície HTTP atual para leitura do plano de controlo e do estado operacional.

Não existe ainda, nesta baseline, uma web UI final integrada como parte estabilizada da entrega.

## Estado atual da implementação

### Já implementado

- plano de controlo em `PostgreSQL`;
- bootstrap da baseline local do plano de controlo;
- `Simulator.Host` ligado ao plano de controlo quando `ControlPlaneEnabled = true`;
- `simulation_runs` persistidas em `control.simulation_runs`;
- inbox durável em `pipeline.event_inbox`;
- rejeição antecipada de eventos inválidos;
- retry interno e quarentena persistida;
- persistência de leituras aceites, avaliações de risco, snapshots e projeções em `PostgreSQL`;
- API de leitura para configurações, áreas, sensores, cenários, runs e estado operacional;
- observabilidade inicial com `OpenTelemetry`, `InfluxDB`, `Grafana`, Doxygen, DocFX e Structurizr DSL.

### Parcialmente implementado

- semântica completa de `accepted`, `rejected` e `normalized` como superfície de eventos publicada;
- separação do simulador em camadas `TruthSnapshot`, `LocalObservation` e `OperationalEvent`;
- score operacional final e política final de alertas;
- agregação de área mais avançada;
- modo local mais explícito para reduzir ou desligar InfluxDB;
- cobertura de testes dos componentes runtime ainda desigual entre módulos.

### Experimental ou dependente de decisão

- `src/NatureProtector.AppHost/` com .NET Aspire;
- integração futura com collector de observabilidade;
- refinamento metodológico dependente dos documentos de referência do segundo momento de pesquisa.

## Como executar localmente

### Pré-requisitos

- `.NET SDK 9.0`
- Docker Desktop ou equivalente com `docker compose`
- PowerShell

### Configuração local

1. Criar `.env` a partir de `.env.example`.
2. Confirmar as credenciais locais para `RabbitMQ`, `PostgreSQL`, `InfluxDB` e `Grafana`.

### Levantar a baseline

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
````

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

### Validação mínima

* o bootstrap deve terminar sem erros;
* `Simulator.Host` deve publicar leituras;
* `Prevention.Host` deve materializar inbox, processamento e projeções;
* `Backoffice.Api` deve responder em `http://localhost:5254`.

## Como validar que o sistema está vivo

* `RabbitMQ`

  * abrir `http://localhost:15672` e confirmar a fila `np.ingestion.readings`;
* `PostgreSQL`

  * confirmar que o bootstrap populou `control.*` e que a runtime escreve em `pipeline.*` e `projection.*`;
* `InfluxDB`

  * confirmar que o contentor `np-influxdb` está operacional e que as escritas surgem nos logs e dashboards;
* `Grafana`

  * abrir `http://localhost:3000` e validar o datasource local;
* `Backoffice.Api`

  * testar `http://localhost:5254/api/control/configurations/active` e `http://localhost:5254/api/control/areas`;
* logs dos hosts

  * observar `Simulator.Host`, `Prevention.Host` e `Backoffice.Api` para fluxo nominal, retries e escrita de projeções.

## Build, testes e coverage

Para compilar a solução:

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

O relatório consolidado fica em `coveragereport_core/index.html`.

## Documentação relacionada

* [docs/README.md](docs/README.md)
* [docs/architecture/README.md](docs/architecture/README.md)
* [docs/architecture/implementation.md](docs/architecture/implementation.md)
* [docs/architecture/pipeline-influx-options.md](docs/architecture/pipeline-influx-options.md)
* [docs/doxygen/pages/mainpage.md](docs/doxygen/pages/mainpage.md)
* [docs/docfx/docfx.json](docs/docfx/docfx.json)
* [docs/structurizr/README.md](docs/structurizr/README.md)
* [docs/planning/project-completion-roadmap.md](docs/planning/project-completion-roadmap.md)

## Limitações conhecidas

* o score de risco continua preliminar e não deve ser lido como modelo metodológico final;
* o simulador ainda não separa completamente verdade física, observação local e falha de transporte;
* a semântica final de `accepted`, `rejected` e `normalized` ainda precisa de consolidação;
* `InfluxDB` continua a ser o principal candidato a gargalo local na baseline atual;
* `AppHost` e a camada Aspire permanecem experimentais e fora do caminho crítico da solução;
* a consolidação metodológica continua dependente dos documentos de referência do segundo momento de pesquisa.
