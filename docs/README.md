# Documentação do Projeto

Esta pasta guarda documentação transversal e relativamente estável sobre o projeto.

O princípio é simples:

- `docs/` concentra conhecimento que interessa ao projeto como um todo;
- cada módulo em `src/` deve ter o seu próprio `README.md` para explicar responsabilidade, dependências, configuração e estado atual;
- a documentação de dados mantém-se em [../data/README.md](../data/README.md) e [../scripts/data/README.md](../scripts/data/README.md).

Assim evitamos dois extremos: nem tudo fica espalhado por pequenos ficheiros locais, nem tudo fica esmagado numa única pasta central sem contexto de proximidade ao código.

## Índice recomendado

- [../README.md](../README.md)
  - visão global do repositório, quickstart e estado atual;
- [../src/README.md](../src/README.md)
  - mapa técnico dos projetos `.NET`;
- [../tests/README.md](../tests/README.md)
  - estado atual da estratégia de testes;
- [implementation/engineering-foundations.md](implementation/engineering-foundations.md)
  - gate M02 para build, testes, coverage, CI, infraestrutura local, auditoria frontend e health check;
- [implementation/ui-v2-foundation.md](implementation/ui-v2-foundation.md)
  - fundacao M03/M04 da UI v2 isolada, primeira vista read-only, area dinamica, runs, cenarios, simulacao autorizada, Data Status Strip, ajuda, PT/EN e validacao executada;
- [implementation/delivery-capacity-cutover-readiness.md](implementation/delivery-capacity-cutover-readiness.md)
  - handoff M06 de delivery/readiness local, medicoes API/web, simulacoes curtas, capacidade estimada, dependency/security findings e recomendacao de cutover;
- [architecture/README.md](architecture/README.md)
  - leitura da arquitetura atual e ligação aos documentos de planeamento;
- [architecture/repository-exploration-guide.md](architecture/repository-exploration-guide.md)
  - percurso recomendado para explorar o repositório, a documentação e os pontos críticos de código;
- [architecture/current-capabilities-and-how-to-run.md](architecture/current-capabilities-and-how-to-run.md)
  - visão operacional do que já pode ser corrido e observado hoje;
- [architecture/grafana-influx-dashboard-guide.md](architecture/grafana-influx-dashboard-guide.md)
  - guia prático para ligar o `Grafana` ao `InfluxDB`, descobrir tabelas e construir dashboards;
- [architecture/pipeline-influx-options.md](architecture/pipeline-influx-options.md)
  - estado atual da escrita temporal em `InfluxDB`, modos locais e impacto na pipeline;
- [architecture/postgresql-architecture.md](architecture/postgresql-architecture.md)
  - referência consolidada do papel atual do `PostgreSQL`, cobrindo `control`, `pipeline`, `projection`, bootstrap, runtime e API;
- [structurizr/README.md](structurizr/README.md)
  - modelo C4 em Structurizr DSL, com instruções de validação, exportação e abertura local;
- [planning/project-completion-roadmap.md](planning/project-completion-roadmap.md)
  - roadmap global e estrutura alvo;
- [planning/pipeline-gap-and-dependency-map.md](planning/pipeline-gap-and-dependency-map.md)
  - lacunas, dependências e próximos passos;
- [../data/README.md](../data/README.md)
  - organização da workspace de dados;
- [../scripts/data/README.md](../scripts/data/README.md)
  - história completa da aquisição e curadoria de dados.

Nota de leitura: os documentos de fase em `docs/planning/` continuam preservados como historial incremental do trabalho em `PostgreSQL`, mas a referência consolidada para o estado atual passou a ser [architecture/postgresql-architecture.md](architecture/postgresql-architecture.md). Para os comandos `dotnet` atuais, o caminho canónico passa pelo [../README.md](../README.md) e por [../tests/README.md](../tests/README.md).

## Regra prática para acrescentar documentação nova

- Se o conteúdo for arquitetural, transversal ou pouco volátil, deve ir para `docs/`.
- Se o conteúdo explicar um módulo específico e viver ao ritmo desse código, deve ir no `README.md` desse módulo.
- Se surgirem notas temporárias de desenvolvimento, devem ficar perto do módulo a que dizem respeito e ser promovidas para `docs/` quando estabilizarem.

## Fontes de enquadramento

Esta documentação foi escrita a partir do estado real do repositório e cruzada com os documentos de enquadramento do projeto:

- proposta e documentação de visão do projeto;
- documento técnico de fecho do escopo do módulo;
- pesquisa técnica sobre simulação, sensores, índices e pipeline.

Os detalhes académicos continuam nesses documentos. Aqui procuramos traduzir isso para decisões de código, estrutura, persistência e manutenção.
