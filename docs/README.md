# Documentação do Projeto

Esta pasta guarda documentação transversal e relativamente estável sobre o projeto. O princípio que seguimos é simples:

- `docs/` deve concentrar conhecimento que interessa ao projeto como um todo.
- Cada módulo em `src/` deve ter o seu próprio `README.md` para explicar responsabilidade, dependências, configuração e estado atual.
- A documentação de dados mantém-se onde já faz sentido hoje, em [../data/README.md](../data/README.md) e [../scripts/data/README.md](../scripts/data/README.md).

Assim, evitamos dois extremos: nem tudo fica espalhado por pequenos ficheiros locais, nem tudo fica esmagado numa única pasta central sem contexto de proximidade ao código.

## Índice recomendado

- [../README.md](../README.md)
  - visão global do repositório, quickstart e estado atual
- [../src/README.md](../src/README.md)
  - mapa técnico dos projetos `.NET`
- [../tests/README.md](../tests/README.md)
  - estado atual da estratégia de testes
- [architecture/README.md](architecture/README.md)
  - leitura da arquitetura atual e ligação aos documentos de planeamento
- [planning/project-completion-roadmap.md](planning/project-completion-roadmap.md)
  - roadmap global e estrutura alvo
- [planning/pipeline-gap-and-dependency-map.md](planning/pipeline-gap-and-dependency-map.md)
  - lacunas, dependências e próximos passos
- [../data/README.md](../data/README.md)
  - organização da workspace de dados
- [../scripts/data/README.md](../scripts/data/README.md)
  - história completa da aquisição e curadoria de dados

## Regra prática para acrescentar documentação nova

- Se o conteúdo for arquitetural, transversal ou pouco volátil, devemos colocá-lo em `docs/`.
- Se o conteúdo explicar um módulo específico e viver ao ritmo desse código, devemos colocá-lo no `README.md` desse módulo.
- Se surgirem notas temporárias de desenvolvimento, devemos mantê-las perto do módulo a que dizem respeito e promovê-las para `docs/` quando estabilizarem.

## Fontes de enquadramento

Esta documentação foi escrita a partir do estado real do repositório e cruzada com os documentos de enquadramento do projeto:

- proposta de projeto;
- documento técnico de fecho do escopo do módulo;
- pesquisa técnica sobre simulação, sensores, índices e pipeline.

Os detalhes académicos continuam nesses documentos; aqui procuramos traduzir isso para decisões de código, estrutura e manutenção.
