# NatureProtector

Esta documentação Doxygen é a homepage técnica do repositório NatureProtector. O seu papel é ligar a leitura gerada a partir do código às páginas manuais que explicam a arquitetura implementada, os fluxos de runtime, o modelo persistente e os testes que fixam comportamento.

## Objetivo da página

Orientar uma leitura técnica inicial sem substituir o código como fonte de verdade. A página ajuda a perceber que partes do sistema existem hoje, onde começam os fluxos principais e que páginas consultar para aprofundar cada tema.

## Âmbito

O âmbito documentado aqui é a implementação atual do subsistema de prevenção e da baseline local que o suporta:

- `NatureProtector.Postgres.Bootstrap` materializa a configuração inicial no PostgreSQL.
- `NatureProtector.Backoffice.Api` expõe endpoints de leitura sobre o plano de controlo e as projeções operacionais, com uma escrita limitada para ativar uma configuração.
- `NatureProtector.Simulator.Host` resolve contexto, gera leituras simuladas e publica eventos `SensorReadingProduced`.
- `NatureProtector.Prevention.Host` consome esses eventos, valida-os, usa uma inbox durável quando configurado, calcula risco, atualiza projeções e escreve séries temporais em InfluxDB.
- Os testes em `tests/` funcionam como documentação executável das regras, contratos e bifurcações principais.

Ficam fora desta homepage os detalhes de preparação de dados, desenho de dashboards e exploração histórica completa. Esses temas continuam documentados em páginas específicas de `docs/architecture/`, `data/README.md`, `infra/README.md` e `scripts/data/README.md`.

## Componentes principais

O sistema atual organiza-se em quatro blocos práticos. O primeiro é o domínio e os contratos: `NatureProtector.Core` contém conceitos de áreas, células, sensores, cenários, risco e runs; `NatureProtector.Shared` contém envelope, payloads, serialização e convenções RabbitMQ. O segundo é a runtime: simulador, prevenção, API e bootstrap. O terceiro é a persistência: PostgreSQL para plano de controlo, inbox e projeções; InfluxDB para séries temporais operacionais. O quarto é a validação: testes unitários e de integração que documentam comportamento observável.

Páginas manuais principais:

- @subpage control_plane_and_bootstrap
- @subpage simulator_flow
- @subpage prevention_flow
- @subpage persistence_model
- @subpage tests_as_documentation

Documentação complementar fora desta navegação curta:

- `docs/architecture/implementation.md` é o documento longo de onboarding técnico da implementação atual.
- `docs/architecture/architecture.md` é a narrativa arquitetural mais ampla, incluindo diferenças entre estado atual, arquitetura-alvo e evolução futura.
- `docs/architecture/current-capabilities-and-how-to-run.md` é o guia operacional para correr a baseline local.
- `docs/architecture/postgresql-architecture.md` aprofunda o papel de PostgreSQL nos schemas `control`, `pipeline` e `projection`.

## Fluxo principal

O caminho nominal da demonstração local é:

1. O bootstrap carrega a baseline da área piloto para PostgreSQL.
2. O simulador resolve área, cenário e sensores a partir do plano de controlo quando `ControlPlaneEnabled=true`.
3. O simulador publica leituras no RabbitMQ com routing key `simulation.reading.produced`.
4. O host de prevenção consome a fila `np.ingestion.readings`, valida o envelope e rejeita mensagens inválidas antes da pipeline.
5. Eventos válidos são materializados na inbox, recebem `ack` no broker e seguem para a pipeline de risco.
6. A pipeline persiste leitura aceite, avaliação de risco, snapshot agregado, projeções operacionais e medições em InfluxDB.
7. A API lê `control.*` e `projection.*` para expor configuração, topologia, runs, estado operacional e alertas simples.

## Decisões importantes

- PostgreSQL é a fonte persistente do plano de controlo e do estado operacional consultável.
- RabbitMQ desacopla produção e consumo de leituras.
- InfluxDB é usado para observabilidade temporal, não como fonte de decisão de negócio.
- O simulador tem modo com plano de controlo e modo autónomo local; o modo por omissão atual usa o plano de controlo.
- A prevenção tem modo persistente e modo em memória; o modo persistente ativa inbox, retries, quarentena e projeções em PostgreSQL.
- O `ack` ao broker acontece depois de o evento ficar materializado na inbox, não depois de toda a pipeline terminar.

## Estado atual e limitações

O estado implementado já suporta uma cadeia local coerente: bootstrap, API, simulador, RabbitMQ, prevenção, PostgreSQL, InfluxDB e Grafana como apoio de observabilidade. O comportamento suportado inclui geração determinística por seed, publicação de leituras de temperatura, humidade e vento, rejeição técnica de mensagens inválidas, retries, quarentena, projeções por célula e por área, e alertas simples quando o risco agregado é alto ou superior.

As limitações conhecidas também devem ser lidas como parte da arquitetura atual. O simulador ainda não separa em módulos autónomos a verdade física, o erro de medição e a falha de transporte. Os eventos `ReadingAccepted`, `ReadingRejected` e `ReadingNormalized` existem como nomes partilhados, mas não são publicados como famílias autónomas no fluxo vivo. Os dashboards ainda são uma frente de apoio e não uma consola operacional madura. A API é sobretudo de leitura; a escrita confirmada é a ativação de configuração.

## Pontos do repositório a consultar

- `src/NatureProtector.Postgres.Bootstrap/Program.cs`
- `src/NatureProtector.Backoffice.Api/Program.cs`
- `src/NatureProtector.Simulator.Host/Program.cs`
- `src/NatureProtector.Prevention.Host/Program.cs`
- `src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`
- `src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`
- `tests/NatureProtector.IntegrationTests/Flow/SimulatorToPreventionCompatibilityTests.cs`

## Ligações para páginas relacionadas

- Para bootstrap e API de controlo, consultar @ref control_plane_and_bootstrap.
- Para geração e publicação de leituras, consultar @ref simulator_flow.
- Para consumo, validação, retries, quarentena e projeções, consultar @ref prevention_flow.
- Para schemas e responsabilidades de persistência, consultar @ref persistence_model.
- Para comportamento demonstrado por testes, consultar @ref tests_as_documentation.
