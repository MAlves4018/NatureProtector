# Nature Protector

O Nature Protector é uma plataforma modular de apoio à decisão para prevenção de incêndios florestais. Nesta fase, o repositório concentra-se sobretudo no módulo de prevenção: geração de leituras simuladas, transporte por eventos, cálculo preliminar de risco, persistência de telemetria e observabilidade de uma baseline local reproduzível.

Esta documentação foi alinhada com a proposta de projeto, com o documento técnico de fecho do escopo do módulo e com a pesquisa técnica sobre simulação e risco. Ainda assim, os documentos académicos continuam a ser a referência de enquadramento; o que aqui fazemos é traduzir esse enquadramento para a estrutura real do repositório.

## O que já existe no repositório

- Um modelo de domínio em `.NET 9` para áreas, grelha de risco, sensores, cenários, leituras, avaliações de risco, alertas e recomendações.
- Um `Simulator.Host` capaz de gerar leituras plausíveis e determinísticas a partir de configuração local ou de manifestos de cenário gerados.
- Um `Prevention.Host` capaz de consumir leituras via RabbitMQ, calcular risco simples e escrever leituras aceites e snapshots em InfluxDB.
- Uma baseline local com RabbitMQ, PostgreSQL, InfluxDB e Grafana via Docker Compose.
- Uma pipeline de dados já documentada para a área piloto de `Proença-a-Nova`, incluindo cenários `A/B/C`.
- Documentação de planeamento já produzida, que se mantém preservada e referenciada.

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

### 2. Compilar a solução

Para confirmar que a solução está consistente, devemos executar:

```powershell
dotnet build NatureProtector.sln
```

### 3. Executar os hosts atuais

Em terminais separados, podemos executar:

```powershell
dotnet run --project .\src\NatureProtector.Simulator.Host
dotnet run --project .\src\NatureProtector.Prevention.Host
```

Antes de arrancar o `Prevention.Host`, devemos garantir que a configuração de `InfluxDb` está completa, em particular o token. A baseline local de InfluxDB e Grafana ainda não está totalmente automatizada para esse ponto.

## Estado atual, sem maquilhagem

- `NatureProtector.Core` já define a linguagem comum do domínio e é o módulo mais estável do código.
- `NatureProtector.Shared` concentra hoje contratos de eventos, enums de leitura e topologia RabbitMQ, embora a documentação de planeamento já aponte para uma futura separação entre contratos e infraestrutura.
- `NatureProtector.Simulator.Host` já lê manifestos gerados em `data/manifests/scenarios`, mas ainda contém código residual de ingestão e validação que não faz parte do caminho ativo de arranque.
- `NatureProtector.Prevention.Host` consome leituras e calcula risco, mas ainda não implementa inbox durável, idempotência forte, normalização explícita nem publicação de estados `accepted/rejected/normalized`.
- `NatureProtector.Backoffice.Api` é ainda um esqueleto de ASP.NET Core, sem controladores funcionais.
- PostgreSQL já existe na baseline local, mas ainda não está integrado na runtime dos projetos `.NET`.

## Documentação existente que foi mantida

Alguns vestigios de documentação antes da normalização da mesma são:

- [docs/planning/project-completion-roadmap.md](docs/planning/project-completion-roadmap.md)
- [docs/planning/pipeline-gap-and-dependency-map.md](docs/planning/pipeline-gap-and-dependency-map.md)
- [data/README.md](data/README.md)
- [scripts/data/README.md](scripts/data/README.md)
