# Arquitetura

Esta pasta concentra a documentação de arquitetura do projeto, com foco principal no subsistema de prevenção da plataforma NatureProtector.

## Conteúdo principal

- [architecture.md](architecture.md)
  - documento central, autocontido, com a leitura progressiva da arquitetura;
- [implementation.md](implementation.md)
  - documento central de onboarding técnico da implementação atual, guiado por vistas e alinhado com o código real da branch;
- [repository-exploration-guide.md](repository-exploration-guide.md)
  - guia de entrada para quem abre o repositório pela primeira vez e precisa de uma ordem de leitura concreta;
- [current-capabilities-and-how-to-run.md](current-capabilities-and-how-to-run.md)
  - documento operacional focado apenas no que já é possível correr, observar e demonstrar hoje;
- [grafana-influx-dashboard-guide.md](grafana-influx-dashboard-guide.md)
  - guia prático para datasource, queries, painéis e desenho dos dashboards sobre `InfluxDB`;
- [pipeline-influx-options.md](pipeline-influx-options.md)
  - referência do modo configurável de escrita em `InfluxDB`, incluindo `NoOp`, tolerância a falhas e batch por evento;
- [postgresql-architecture.md](postgresql-architecture.md)
  - referência consolidada do papel do `PostgreSQL` na arquitetura atual, unificando a leitura das fases 1 a 9;
- [diagrams/current/README.md](diagrams/current/README.md)
  - ponto de navegação canónico do portefólio de diagramas atual declarado no manifest; os assets canónicos foram restaurados e ficam `RESTORED_NOT_VISUALLY_VALIDATED` até smoke visual/revisão documental;
- [diagrams/](diagrams/)
  - ficheiros fonte em Draw.io, incluindo diagramas já importados para revisão e novos diagramas preparados para evolução;
- [images/](images/)
  - imagens PNG usadas no corpo do documento.

## Convenções

- `architecture.md` é a referência principal para leitura técnica, apresentação e apoio ao relatório.
- `implementation.md` é a referência principal para perceber a implementação atual sem partir diretamente para leitura dispersa do código.
- `postgresql-architecture.md` é a referência principal para perceber `control`, `pipeline` e `projection` sem ter de ler as fases históricas uma a uma.
- O portefólio canónico de diagramas é [diagrams/current/README.md](diagrams/current/README.md). A presença dos assets restaurados deve ser confirmada no filesystem antes de referenciar uma figura em relatório ou material externo, e a sua leitura visual continua separada da validação técnica/científica.
- Cada imagem em `images/` deve ter um ficheiro Draw.io homónimo em `diagrams/`, mas a presença real dos assets tem de ser confirmada no filesystem antes de referenciar a figura em relatório ou material externo.
- O ficheiro [diagrams/00-legacy-current-repo-architecture.drawio.xml](diagrams/00-legacy-current-repo-architecture.drawio.xml) foi preservado apenas como artefacto legado e não deve ser tratado como diagrama final.

## Nota de trabalho

Os ficheiros PNG desta pasta podem começar por existir como placeholders técnicos, enquanto os exports finais forem sendo produzidos a partir dos ficheiros Draw.io. O objetivo é que cada placeholder seja progressivamente substituído pelo respetivo export final sem alterar a estrutura do documento.
