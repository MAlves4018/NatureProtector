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
- a estrutura de dados para `Proença-a-Nova` já existe e já produz artefactos curados;
- a baseline meteorológica já foi agregada e enriquecida com referência diária de índices;
- os `scenario_candidates` já estão limpos e enriquecidos;
- os cenários `A/B/C` já existem como manifestos executáveis;
- o `Simulator.Host` já consegue ler manifestos de cenário gerados;
- a validação local do `dotnet build` continua dependente do ambiente .NET instalado nesta máquina, por isso este documento evita tratar esse ponto como facto permanente.

### Leitura de progresso face ao roadmap

- `Fase 0`: parcial. Já existe documentação de navegação e leitura técnica, mas ainda faltam alguns artefactos-base, como o catálogo formal de eventos e a especificação consolidada da simulação.
- `Fase 1`: em aberto. A modularização alvo ainda não foi executada e continuam a existir resíduos de uma pipeline antiga dentro do `Simulator.Host`.
- `Fase 2`: em aberto. `PostgreSQL` ainda não é fonte de verdade em runtime.
- `Fase 3`: materialmente adiantada. A baseline de dados, os manifests e os cenários executáveis já existem no sistema de ficheiros.
- `Fase 4`: parcialmente adiantada. O simulador já trabalha com seed determinística e manifests, mas ainda não está separado nas três camadas pedidas pela investigação.

Isto confirma um estado misto: a frente de dados e cenários avançou antes de a frente arquitetural ficar fechada. O desvio fez sentido porque ajudou a clarificar tabelas, artefactos e necessidades reais da simulação, mas deve agora ser consolidado.

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

- leituras inválidas ainda podem entrar no caminho de risco, porque a distinção entre `accepted`, `rejected` e leituras apenas armazenadas continua em aberto;
- continuam a existir riscos de perda de mensagens por ausência de inbox durável, retry sério e `DLQ`;
- configuração e segredos continuam frágeis;
- a topologia de filas ainda não fecha o ciclo operacional;
- a persistência de estado crítico continua em memória;
- a API/backoffice continua esqueleto;
- a frente de dados e cenários avançou, mas a ligação formal ao plano de controlo e ao simulador em camadas continua por fechar.

## O Que Falta e Podemos Fazer Já

### Bloco A. Fechar a baseline técnica e o plano de controlo

- congelar o catálogo formal de eventos;
- fechar a lista mínima de tabelas `control` e `execution`;
- fechar a ligação entre datasets, cenários e runs;
- implementar `PostgreSQL` como fonte de verdade do plano de controlo.

### Bloco B. Fechar a ponte entre baseline, cenários e simulador

- gerar rede de sensores a partir da área e da grelha;
- criar perfis de sensor e perfis de instalação;
- separar o simulador em verdade física, erro de sensor e falha de transporte;
- ligar manifesto, seed e execução a uma `simulation_run` explícita.

### Bloco C. Fechar a ingestão

- distinguir estrutural / domínio / transitório;
- emitir `accepted`, `rejected` e `normalized`;
- implementar inbox durável, idempotência por `event_id`, retry e `DLQ`.

### Bloco D. Fechar risco, alertas e projeções

- consumir apenas leituras aceites e normalizadas;
- separar warning, alarm e recommendation como estágios explícitos;
- persistir projeções operacionais para UI e API.

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
2. implementar `PostgreSQL` do plano de controlo com base nos artefactos já curados;
3. gerar rede de sensores e refatorar o simulador para três camadas;
4. fechar ingestão: validação, inbox, idempotência, retry e `DLQ`;
5. fechar risco, alertas e projeções;
6. fechar API, dashboards e testes.

## Regra Prática Final

Se a pergunta for "o que fazemos já a seguir?", a resposta é:

1. fechar a baseline técnica e o desenho das tabelas;
2. implementar `PostgreSQL` do plano de controlo;
3. fechar rede de sensores e simulador em camadas;
4. fechar ingestão durável;
5. fechar risco, alertas e projeções.

Se a pergunta for "o que continua bloqueado de fora?", a resposta é:

- altitude;
- tree cover density;
- `ERA5-Land` oficial;
- `FIRMS`;
- `CEMS/EFFIS`;
- algumas camadas `ICNF`.
