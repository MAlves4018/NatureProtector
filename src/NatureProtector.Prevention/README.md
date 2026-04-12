# NatureProtector.Prevention

Este projeto concentra a lógica de prevenção que, idealmente, deve poder sobreviver a mudanças de host e de infraestrutura. Hoje ele continua deliberadamente pequeno, mas já fixa a fronteira entre cálculo de risco e detalhes de execução.

## O que faz hoje

- cria avaliações de risco simples a partir de uma métrica e de um valor;
- agrega avaliações numa fotografia resumida por área;
- define as interfaces de persistência que o host usa para guardar assessments e snapshots;
- continua a fornecer implementações em memória úteis para testes e arranque simples.

## Conteúdo principal

- `Risk/SimpleRiskScoringService.cs`
  - transforma métricas como temperatura, humidade e vento num score normalizado
- `Risk/AreaRiskSnapshotService.cs`
  - agrega um conjunto de avaliações num snapshot único
- `Persistence/IRiskAssessmentRepository.cs`
- `Persistence/InMemoryRiskAssessmentRepository.cs`
- `Persistence/IAreaRiskSnapshotRepository.cs`
- `Persistence/InMemoryAreaRiskSnapshotRepository.cs`

## Observações importantes sobre o estado atual

- O scoring atual é deliberadamente simples e empírico. Serve para fechar o fluxo end-to-end da demonstração, não para representar ainda um motor de risco final.
- O projeto continua a transportar implementações em memória, mas o `Prevention.Host` já pode substituí-las por adapters duráveis em PostgreSQL.
- As interfaces de persistência já carregam informação suficiente para suportar idempotência operacional e histórico durável no host.
- O host usa hoje o snapshot de área como fotografia operacional do último assessment conhecido por sensor; o histórico completo continua disponível no repositório de assessments.
- Ainda não existe aqui política explícita de alerta, recomendação, histerese, cooldown ou projeções.

## Relação com o domínio e com o host

- O projeto depende de `NatureProtector.Core` para `RiskAssessment` e `AreaRiskSnapshot`.
- O projeto depende de `NatureProtector.Shared` porque o serviço de scoring atual recebe `SensorMetricType`.
- O `Prevention.Host` usa este projeto para o cálculo de risco, para a agregação por área e para as interfaces de persistência que depois adapta a memória ou PostgreSQL.

## Estado de testes

Existe um projeto de teste em [../../tests/NatureProtector.Prevention.Tests](../../tests/NatureProtector.Prevention.Tests), e nesta fase ele já acompanha a evolução das interfaces de persistência e dos repositórios em memória.
