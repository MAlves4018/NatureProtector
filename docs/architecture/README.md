# Arquitetura

Esta pasta guarda a leitura arquitetural do projeto e os artefactos que ajudam a ligar a intenção do módulo ao estado real do código.

## Artefactos principais

- [natureprotector-current-architecture.drawio.xml](natureprotector-current-architecture.drawio.xml)
  - diagrama editável da arquitetura atual
- [../planning/project-completion-roadmap.md](../planning/project-completion-roadmap.md)
  - estrutura alvo, backlog e ordem recomendada de evolução
- [../planning/pipeline-gap-and-dependency-map.md](../planning/pipeline-gap-and-dependency-map.md)
  - lacunas verificadas e próximas prioridades

## Leitura curta da arquitetura atual

Hoje, o fluxo técnico principal do repositório é este:

1. O [../../src/NatureProtector.Simulator.Host/README.md](../../src/NatureProtector.Simulator.Host/README.md) constrói um contexto de simulação a partir de `appsettings.json` ou de um manifesto de cenário gerado.
2. O simulador gera eventos `SensorReadingProduced` com envelope comum definido em [../../src/NatureProtector.Shared/README.md](../../src/NatureProtector.Shared/README.md).
3. Esses eventos seguem por RabbitMQ para a fila `np.ingestion.readings`.
4. O [../../src/NatureProtector.Prevention.Host/README.md](../../src/NatureProtector.Prevention.Host/README.md) consome essas leituras, calcula risco simples e produz snapshots agregados por área.
5. O [../../src/NatureProtector.Infrastructure.Influx/README.md](../../src/NatureProtector.Infrastructure.Influx/README.md) escreve leituras aceites, avaliações de risco e snapshots de área em InfluxDB.
6. O [../../src/NatureProtector.Backoffice.Api/README.md](../../src/NatureProtector.Backoffice.Api/README.md) já existe na solução, mas continua numa fase de esqueleto.

## O que a arquitetura já fecha

- Existe separação entre domínio, contratos partilhados, simulação, prevenção e infraestrutura Influx.
- RabbitMQ já funciona como mecanismo de desacoplamento entre produção e consumo de leituras.
- A pipeline de dados já produz artefactos de cenário que o simulador consegue ler.
- A baseline local de infraestrutura já é reprodutível com Docker Compose.

## O que ainda está em transição

- `NatureProtector.Shared` continua a juntar contratos e detalhes de RabbitMQ.
- `Simulator.Host` ainda guarda código residual de ingestão/validação que não faz parte do arranque atual.
- `Prevention.Host` ainda não implementa a pipeline durável que a documentação de planeamento descreve.
- PostgreSQL já está presente na baseline local, mas ainda não serve o plano de controlo em runtime.

## Como usar esta pasta

Para perceber o desenho atual, devemos começar pelo diagrama e cruzá-lo com o [../../src/README.md](../../src/README.md). Para perceber a direção de evolução, devemos seguir depois para os documentos de `planning/`.
