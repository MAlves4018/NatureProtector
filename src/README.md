# Projetos de Código

Esta pasta contém os projetos `.NET` da solução. A forma mais segura de a ler é distinguir entre:

- módulos de domínio, que procuram ficar independentes de infraestrutura;
- módulos de infraestrutura, que adaptam o domínio a serviços concretos;
- hosts de execução, que ligam tudo e arrancam os workers ou a API.

## Mapa dos projetos

- [NatureProtector.Core/README.md](NatureProtector.Core/README.md)
  - linguagem comum do domínio: áreas, grelha de risco, sensores, leituras, cenários, clima, alertas e recomendações
- [NatureProtector.Shared/README.md](NatureProtector.Shared/README.md)
  - contratos de eventos, enums partilhados e topologia RabbitMQ usada hoje
- [NatureProtector.Prevention/README.md](NatureProtector.Prevention/README.md)
  - serviços de scoring simples e agregação de snapshots
- [NatureProtector.Infrastructure.Influx/README.md](NatureProtector.Infrastructure.Influx/README.md)
  - escrita de telemetria e risco em InfluxDB
- [NatureProtector.Prevention.Host/README.md](NatureProtector.Prevention.Host/README.md)
  - worker de consumo e pipeline atual de prevenção
- [NatureProtector.Simulator.Host/README.md](NatureProtector.Simulator.Host/README.md)
  - worker de simulação, geração de leituras e publicação
- [NatureProtector.Backoffice.Api/README.md](NatureProtector.Backoffice.Api/README.md)
  - API ASP.NET Core ainda em fase inicial

## Dependências atuais entre projetos

- `NatureProtector.Core`
  - não depende de outros projetos da solução
- `NatureProtector.Shared`
  - não depende de outros projetos da solução
- `NatureProtector.Prevention`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`
- `NatureProtector.Infrastructure.Influx`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`
- `NatureProtector.Simulator.Host`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`
- `NatureProtector.Prevention.Host`
  - depende de `NatureProtector.Prevention`, `NatureProtector.Shared` e `NatureProtector.Infrastructure.Influx`
- `NatureProtector.Backoffice.Api`
  - depende de `NatureProtector.Core` e `NatureProtector.Shared`

## Leitura recomendada

Para perceber rapidamente o estado do código, devemos ler por esta ordem:

1. [NatureProtector.Core/README.md](NatureProtector.Core/README.md)
2. [NatureProtector.Shared/README.md](NatureProtector.Shared/README.md)
3. [NatureProtector.Simulator.Host/README.md](NatureProtector.Simulator.Host/README.md)
4. [NatureProtector.Prevention/README.md](NatureProtector.Prevention/README.md)
5. [NatureProtector.Prevention.Host/README.md](NatureProtector.Prevention.Host/README.md)
6. [NatureProtector.Infrastructure.Influx/README.md](NatureProtector.Infrastructure.Influx/README.md)
7. [NatureProtector.Backoffice.Api/README.md](NatureProtector.Backoffice.Api/README.md)

## Tensões arquiteturais que devemos ter presentes

- `NatureProtector.Shared` ainda serve de módulo misto para contratos e RabbitMQ.
- `NatureProtector.Simulator.Host` já foi parcialmente limpo, mas ainda contém código residual fora do caminho principal de execução.
- `NatureProtector.Prevention.Host` faz processamento útil, mas ainda não representa a pipeline durável e idempotente desenhada nos documentos de planeamento.
- `NatureProtector.Backoffice.Api` já existe para fixar fronteira de produto, mas ainda não fecha o plano de controlo.
