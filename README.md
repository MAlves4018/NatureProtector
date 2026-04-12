# Nature Protector

O Nature Protector é uma plataforma modular de apoio à decisão para prevenção de incêndios florestais. Nesta fase, o repositório concentra-se sobretudo no módulo de prevenção: geração de leituras simuladas, transporte por eventos, cálculo preliminar de risco, persistência durável do plano de controlo em PostgreSQL, telemetria em InfluxDB e observabilidade de uma baseline local reproduzível.

Esta documentação foi alinhada com a proposta de projeto, com o documento técnico de fecho do escopo do módulo e com a pesquisa técnica sobre simulação e risco. Ainda assim, os documentos académicos continuam a ser a referência de enquadramento; o que aqui fazemos é traduzir esse enquadramento para a estrutura real do repositório.

## O que já existe no repositório

- Um modelo de domínio em `.NET 9` para áreas, grelha territorial, sensores, cenários, leituras, avaliações de risco, alertas e recomendações.
- Um `Simulator.Host` capaz de gerar leituras plausíveis e determinísticas a partir de configuração local, manifestos de cenário ou do plano de controlo em PostgreSQL.
- Um `Prevention.Host` capaz de consumir leituras via RabbitMQ, usar inbox durável, processar retries internos e materializar projeções operacionais em PostgreSQL.
- Um `Backoffice.Api` já ligado ao plano de controlo e à superfície operacional básica.
- Uma baseline local com RabbitMQ, PostgreSQL, InfluxDB e Grafana via Docker Compose.
- Uma pipeline de dados já documentada para a área piloto de `Proença-a-Nova`, incluindo cenários `A/B/C`.

## Como devemos ler o repositório

- O ponto de entrada global da documentação está em [docs/README.md](docs/README.md).
- O mapa técnico dos projetos de código está em [src/README.md](src/README.md).
- O estado atual dos testes está em [tests/README.md](tests/README.md).
- A documentação do workspace de dados está em [data/README.md](data/README.md).
- A explicação detalhada da pipeline de dados está em [scripts/data/README.md](scripts/data/README.md).

## Estrutura principal

```text
docs/    documentação transversal e relativamente estável
infra/   baseline local de infraestrutura e scripts operacionais
src/     projetos .NET da solução
tests/   projetos de teste
data/    artefactos de dados, manifests e saídas de runtime
scripts/ scripts auxiliares, com destaque para a pipeline de dados
```

## Arranque rápido

### 1. Levantar a baseline local

Para levantar a infraestrutura local, devemos executar:

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
```

Isto sobe RabbitMQ, PostgreSQL, InfluxDB e Grafana. O detalhe desta baseline está em [infra/README.md](infra/README.md).

### 2. Compilar e testar a solução

Para confirmar que a solução está consistente, devemos executar:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet build .\NatureProtector.sln --nologo -v minimal -m:1 --configfile NuGet.Config
dotnet test .\NatureProtector.sln --nologo -v minimal -m:1
```

O helper acima fixa `APPDATA`, `DOTNET_CLI_HOME` e as caches de `NuGet` dentro do repositório para evitar falsos negativos de restore/build quando o perfil global da maquina não está acessivel.

Neste workspace, `-m:1` fica assumido como caminho padrão para `dotnet build` e `dotnet test`, porque o caminho paralelo pode devolver um falso negativo sem erros de compilação.

### 3. Materializar o plano de controlo

Com a infraestrutura de pé, devemos executar:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

Isto aplica as migrations e semeia a configuração piloto de `Proença-a-Nova`, incluindo área, grelha, sensores, cenários e artefactos de dataset.

### 4. Executar os hosts atuais

Em terminais separados, podemos executar:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Backoffice.Api
dotnet run --project .\src\NatureProtector.Prevention.Host
dotnet run --project .\src\NatureProtector.Simulator.Host
```

Antes de arrancar o `Prevention.Host`, devemos garantir que a configuração de `InfluxDb` está resolvida. O perfil local passa agora a aceitar o token a partir da secção `InfluxDb` ou do `.env` do repositório.

O perfil local suportado por defeito e o perfil com plano de controlo ativo:

- `Simulator.Host` arranca com `ControlPlaneEnabled = true` e usa os sensores bootstrapados em PostgreSQL.
- `Prevention.Host` arranca com `PipelinePersistenceEnabled = true` e persiste inbox, logs e projeções em PostgreSQL.

Se quisermos correr uma demo totalmente standalone, devemos tratar isso como override explícito do operador: desligar `Simulator:ControlPlaneEnabled`, desligar `PreventionHost:PipelinePersistenceEnabled` e fornecer um conjunto local coerente de `AreaId`, `ScenarioId` e `Sensors` para o simulador.

## Estado atual, sem maquilhagem

- `NatureProtector.Core` já define a linguagem comum do domínio e é o módulo mais estável do código.
- `NatureProtector.Shared` concentra hoje contratos de eventos, enums de leitura e topologia RabbitMQ.
- `NatureProtector.Simulator.Host` já não transporta o legado de ingestão e validação de uma pipeline antiga; está focado em gerar e publicar leituras.
- `NatureProtector.Prevention.Host` já implementa inbox durável, retries internos e projeções básicas em PostgreSQL, mas continua com espaço para enriquecer classificação de falhas, alertas e normalização.
- `NatureProtector.Backoffice.Api` já expõe a superfície principal do plano de controlo e o estado operacional básico.
- PostgreSQL já está integrado na runtime dos projetos `.NET` para plano de controlo, inbox e projeções.

## Documentação existente que foi mantida

Continuam a existir documentos de planeamento que ajudam a perceber a evolução do projeto:

- [docs/planning/project-completion-roadmap.md](docs/planning/project-completion-roadmap.md)
- [docs/planning/pipeline-gap-and-dependency-map.md](docs/planning/pipeline-gap-and-dependency-map.md)
- [data/README.md](data/README.md)
- [scripts/data/README.md](scripts/data/README.md)
