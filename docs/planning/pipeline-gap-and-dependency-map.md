# NatureProtector: Lacunas, Dependências e Próximos Passos

## Objetivo

Este documento responde a quatro perguntas práticas:

1. o que já está realmente fechado;
2. o que continua em falta;
3. o que podemos fazer já com o repositório atual;
4. o que continua bloqueado por fatores externos.

Não substitui o roadmap global. Funciona como um mapa operacional do estado atual do projeto.

## Estado Verificado no Repositório

### O que já ficou validado

- os pontos de entrada principais do simulador, da prevenção e da persistência já estão mapeados;
- a estrutura de dados para `Proença-a-Nova` já existe e já produz artefactos preparados;
- a baseline meteorológica já foi agregada e enriquecida com referência diária de índices;
- os `scenario_candidates` já estão limpos e enriquecidos;
- os cenários `A/B/C` já existem como manifestos executáveis;
- o `Simulator.Host` já consegue ler manifestos de cenário gerados e o plano de controlo em PostgreSQL;
- o `Prevention.Host` já trabalha com inbox durável, retries internos e quarentena persistida;
- o `Backoffice.Api` já expõe a primeira superfície real do plano de controlo e do estado operacional;
- o `dotnet build` e o `dotnet test` da solution já passam localmente.

### Leitura de progresso face ao roadmap

- `Fase 0`: parcial. Já existe documentação de navegação e leitura técnica, mas ainda faltam alguns artefactos-base, como o catálogo formal de eventos e a especificação consolidada da simulação.
- `Fase 1`: parcialmente fechada. A limpeza do legado mais problemático do simulador já foi feita, mas a modularização alvo ainda não foi executada.
- `Fase 2`: materialmente adiantada. `PostgreSQL` já serve o plano de controlo, a inbox durável e a primeira vaga de projeções operacionais.
- `Fase 3`: materialmente adiantada. A baseline de dados, os manifests e os cenários executáveis já existem no sistema de ficheiros.
- `Fase 4`: parcialmente adiantada. O simulador já trabalha com seed determinística, manifests e plano de controlo, mas ainda não está separado nas três camadas pedidas pela investigação.

### Artefactos-chave já existentes

- área e grelha:
  - [area.gpkg](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/area.gpkg)
  - [grid_1km.gpkg](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/grid_1km.gpkg)
- atributos por célula:
  - [cells_attributes.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/cells_attributes.parquet)
- referência meteorológica:
  - [weather_reference.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_reference.parquet)
  - [weather_daily_reference.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_daily_reference.parquet)
- histórico e candidatos:
  - [fire_history.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/fire_history.parquet)
  - [scenario_candidates.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.parquet)
- cenários executáveis:
  - [proenca-a-nova-scenarios.generated.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova-scenarios.generated.json)
  - [scenario_a.base.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_a.base.json)
  - [scenario_b.high-risk.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json)
  - [scenario_c.degraded-pipeline.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_c.degraded-pipeline.json)

## Problemas Iniciais da Pipeline e Estado Atual

- leituras inválidas ainda não têm o fecho semântico completo entre `accepted`, `rejected`, `normalized` e `accepted-for-storage-but-excluded-from-risk`;
- a topologia de filas ainda não fecha o ciclo operacional completo com DLQ externa explícita;
- configuração e segredos continuam frágeis;
- o estado operacional já é persistido, mas os alertas continuam simples e sem ciclo de vida rico;
- a frente de dados e cenários avançou, mas a ligação formal entre datasets, sensores, verdade física e simulador em camadas continua por fechar.

## O Que Falta e Podemos Fazer Já

### Bloco A. Fechar a baseline técnica e o plano de controlo

- congelar o catálogo formal de eventos;
- fechar a lista mínima de tabelas `control`, `pipeline` e `projection`;
- fechar a ligação entre datasets, cenários e runs;
- enriquecer a superfície HTTP do plano de controlo com operações mais fortes de backoffice.

### Bloco B. Fechar a ponte entre baseline, cenários e simulador

- gerar rede de sensores a partir da área e da grelha;
- criar perfis de sensor e perfis de instalação;
- separar o simulador em verdade física, erro de sensor e falha de transporte;
- ligar manifesto, seed e execução a uma `simulation_run` explícita.

### Bloco C. Fechar a ingestão

- distinguir estrutural / domínio / transitório;
- emitir `accepted`, `rejected` e `normalized`;
- completar a estratégia de retry, DLQ e replay operacional.

### Bloco D. Fechar risco, alertas e projeções

- consumir apenas leituras aceites e normalizadas;
- separar warning, alarm e recommendation como estágios explícitos;
- enriquecer o ciclo de vida dos alertas e as consultas agregadas operacionais.

### Bloco E. Fechar produto demonstrável

- expor configuração, cenários, risco e projeções por API;
- substituir dashboards apenas de setup por dashboards operacionais;
- reforçar testes de pipeline, integração, prevenção e cenários.

## O Que Falta Mas Não Podemos Fechar Sozinhos Já

- `altitude_m`, por bloqueio no `MDT25m` da `DGT`;
- `Tree Cover Density`, por autenticação e download do raster final;
- `CORINE`, por download manual ainda não integrado;
- `ERA5-Land` oficial, por autenticação `CDS`;
- `FIRMS` e `CEMS/EFFIS`, por acesso manual ou autenticado;
- camadas adicionais do `ICNF`, por disponibilidade prática dos downloads.

## Ordem Recomendada dos Próximos Passos

1. fechar a baseline técnica: catálogo de eventos, tabelas base e ligação dataset-cenário-run;
2. gerar rede de sensores e refatorar o simulador para três camadas;
3. fechar ingestão: validação, estados semânticos, retry, DLQ e replay;
4. fechar risco, alertas e projeções;
5. fechar API, dashboards e testes.

## Regra Prática Final

Se a pergunta for "o que fazemos já a seguir?", a resposta é:

1. fechar rede de sensores e o input canónico do simulador;
2. separar o simulador em verdade física, erro de sensor e falha de transporte;
3. fechar a semântica `accepted/rejected/normalized`;
4. enriquecer alertas, projeções e backoffice.

Se a pergunta for "o que continua bloqueado de fora?", a resposta é:

- altitude;
- tree cover density;
- `ERA5-Land` oficial;
- `FIRMS`;
- `CEMS/EFFIS`;
- algumas camadas `ICNF`.
