# NatureProtector.Prevention

Este projeto concentra a lógica de prevenção que, idealmente, deve poder sobreviver a mudanças de host e de infraestrutura. Hoje ele ainda é pequeno, mas já fixa a fronteira entre cálculo de risco e detalhes de execução.

## O que faz hoje

- Cria avaliações de risco simples a partir de uma métrica e de um valor.
- Agrega avaliações numa fotografia resumida por área.
- Fornece repositórios em memória para avaliações e snapshots.

## Conteúdo principal

- `Risk/SimpleRiskScoringService.cs`
  - transforma métricas como temperatura, humidade e vento num score normalizado
- `Risk/AreaRiskSnapshotService.cs`
  - agrega um conjunto de avaliações num snapshot único
- `Presistence/IRiskAssessmentRepository.cs`
- `Presistence/InMemoryRiskAssessmentRepository.cs`
- `Presistence/IAreaRiskSnapshotRepository.cs`
- `Presistence/InMemoryAreaRiskSnapshotRepository.cs`

## Observações importantes sobre o estado atual

- O scoring atual é deliberadamente simples e empírico. Serve para fechar o fluxo end-to-end da demonstração, não para representar ainda um motor de risco final.
- A persistência é toda em memória.
- Ainda não existe aqui política explícita de alerta, recomendação, histerese, cooldown ou projeções.
- O diretório físico ainda mantém a grafia `Presistence`, embora o namespace já use `Persistence`. A documentação deve refletir esta realidade para evitar confusão ao procurar ficheiros.

## Relação com o domínio e com o host

- O projeto depende de `NatureProtector.Core` para `RiskAssessment` e `AreaRiskSnapshot`.
- O projeto depende de `NatureProtector.Shared` porque o serviço de scoring atual recebe `SensorMetricType`.
- O `Prevention.Host` usa este projeto para o cálculo de risco e para a agregação por área.

## Estado de testes

Existe um projeto de teste em [../../tests/NatureProtector.Prevention.Tests](../../tests/NatureProtector.Prevention.Tests), mas ele ainda não acompanha o nível de cobertura que já existe no domínio central.
