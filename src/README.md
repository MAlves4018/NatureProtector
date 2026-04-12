# Projetos de Código

Esta pasta contém os projetos `.NET` da solução. A forma mais segura de a ler é distinguir entre:

- módulos de domínio, que procuram ficar independentes de infraestrutura;
- módulos de infraestrutura, que adaptam o domínio a serviços concretos;
- hosts de execução, que ligam tudo e arrancam os workers ou a API.

## Mapa dos projetos

- [NatureProtector.Core/README.md](NatureProtector.Core/README.md)
  - linguagem comum do domínio: áreas, grelha territorial, grelha de risco, sensores, leituras, cenários, clima, alertas e recomendações
- [NatureProtector.Shared/README.md](NatureProtector.Shared/README.md)
  - contratos de eventos, enums partilhados e topologia RabbitMQ usada hoje
- [NatureProtector.Prevention/README.md](NatureProtector.Prevention/README.md)
  - serviços de scoring simples e agregação de snapshots
- [NatureProtector.Infrastructure.Influx/README.md](NatureProtector.Infrastructure.Influx/README.md)
  - escrita de telemetria e risco em InfluxDB
- [NatureProtector.Infrastructure.Postgres/README.md](NatureProtector.Infrastructure.Postgres/README.md)
  - base do plano de controlo, da inbox durável e dos logs/projeções operacionais em PostgreSQL
- [NatureProtector.Postgres.Bootstrap/README.md](NatureProtector.Postgres.Bootstrap/README.md)
  - utilitário de arranque para materializar o schema `control`, importar a área piloto e gerar a rede de sensores piloto
- [NatureProtector.Prevention.Host/README.md](NatureProtector.Prevention.Host/README.md)
  - worker de consumo, inbox durável, retries internos e pipeline atual de prevenção
- [NatureProtector.Simulator.Host/README.md](NatureProtector.Simulator.Host/README.md)
  - worker de simulação, geração de leituras e publicação
- [NatureProtector.Backoffice.Api/README.md](NatureProtector.Backoffice.Api/README.md)
  - API ASP.NET Core com primeira superfície HTTP do control plane e do estado operacional

## Dependências atuais entre projetos

- `NatureProtector.Core`
  - não depende de outros projetos da solução
- `NatureProtector.Shared`
  - não depende de outros projetos da solução
- `NatureProtector.Prevention`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`
- `NatureProtector.Infrastructure.Influx`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`
- `NatureProtector.Infrastructure.Postgres`
  - depende de `NatureProtector.Core`
- `NatureProtector.Simulator.Host`
  - depende de `NatureProtector.Core`, `NatureProtector.Shared` e opcionalmente de `NatureProtector.Infrastructure.Postgres`
- `NatureProtector.Postgres.Bootstrap`
  - depende de `NatureProtector.Infrastructure.Postgres`
- `NatureProtector.Prevention.Host`
  - depende de `NatureProtector.Prevention`, `NatureProtector.Shared`, `NatureProtector.Infrastructure.Influx` e `NatureProtector.Infrastructure.Postgres`
- `NatureProtector.Backoffice.Api`
  - depende de `NatureProtector.Core`, `NatureProtector.Shared` e `NatureProtector.Infrastructure.Postgres`

## Leitura recomendada

Para perceber rapidamente o estado do código, devemos ler por esta ordem:

1. [NatureProtector.Core/README.md](NatureProtector.Core/README.md)
2. [NatureProtector.Shared/README.md](NatureProtector.Shared/README.md)
3. [NatureProtector.Simulator.Host/README.md](NatureProtector.Simulator.Host/README.md)
4. [NatureProtector.Prevention/README.md](NatureProtector.Prevention/README.md)
5. [NatureProtector.Prevention.Host/README.md](NatureProtector.Prevention.Host/README.md)
6. [NatureProtector.Infrastructure.Influx/README.md](NatureProtector.Infrastructure.Influx/README.md)
7. [NatureProtector.Infrastructure.Postgres/README.md](NatureProtector.Infrastructure.Postgres/README.md)
8. [NatureProtector.Postgres.Bootstrap/README.md](NatureProtector.Postgres.Bootstrap/README.md)
9. [NatureProtector.Backoffice.Api/README.md](NatureProtector.Backoffice.Api/README.md)

## Tensões arquiteturais que devemos ter presentes

- `NatureProtector.Shared` ainda serve de módulo misto para contratos e RabbitMQ.
- `NatureProtector.Infrastructure.Postgres` já cobre `control`, `pipeline` e uma vaga útil de `projection`, incluindo logs duráveis e projeções por célula, mas ainda não fecha consultas agregadas operacionais mais ricas.
- `NatureProtector.Postgres.Bootstrap` já consegue importar área, grelha, cenários, catálogo de datasets e sensores piloto, mas depende de um PostgreSQL local ativo.
- `NatureProtector.Simulator.Host` já foi limpo do legado de ingestão e validação e está focado no caminho principal de execução.
- `NatureProtector.Prevention.Host` já faz commit durável antes do `ack`, agenda retries internos, move falhados para quarentena, persiste logs operacionais duráveis e atualiza a projeção por área e por célula, mas ainda não fecha alertas ricos.
- `NatureProtector.Backoffice.Api` já fecha a leitura do plano de controlo e da primeira superfície operacional por área e célula, mas ainda não fecha a experiência completa de backoffice.
