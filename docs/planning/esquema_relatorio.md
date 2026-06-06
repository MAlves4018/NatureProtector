# Esquema Fechado e Plano Operacional do Relatório Final

## 0. Objetivo deste documento

Este documento consolida a proposta final de estrutura, fontes, anexos, evidência e plano operacional para a produção do relatório final do **NatureProtector**.

Este documento é uma **proposta operacional de estrutura e produção do relatório final**, não é ainda o relatório final.

---

## 1. Decisões estruturais assumidas

| Tema | Decisão |
|---|---|
| Tipo de relatório | Projeto de engenharia data-driven com componente de investigação aplicada. |
| Tese central | O NatureProtector nasce de uma oportunidade observada em contexto real e evolui para uma baseline técnica capaz de simular cenários, processar eventos ambientais, calcular risco candidato, comparar esse risco com FWI/KBDI e produzir evidência operacional reprodutível. |
| Estrutura | Alinhada com o template: Introdução, Estado da Arte, Requisitos/Âmbito, Arquitetura, Estratégia de Implementação, Implementação, Avaliação de Risco, Validação Técnica, Discussão/Limitações, Trabalho Futuro e Conclusões. |
| Anexos | Usar anexos numerosos, curtos e especializados, organizados por grupos A–E. |
| Numeração dos anexos | Usar A0, A1, A2, B1, B2, C1, D1, E1, etc. |
| Documento de organização do repositório | Entra integralmente como anexo. |
| Posters | Entram como anexos completos, incluindo o poster de Data Analysis e o poster final de Projeto. |
| Requisitos | Síntese no corpo principal; matriz detalhada em B2 e rastreabilidade completa em D8. |
| Matriz requisito → funcionalidade → evidência | Fica completa no D8; é chamada no corpo principal para transmitir a rastreabilidade do trabalho feito. |
| V3 | Entra no Capítulo 7 como **Técnicas de Aprendizagem Automática**. A estrutura assume a V3 como fase integrante do relatório final, mas a escrita implica tarefas da V3 ainda não realizadas. |
| V4 | Fica como integração real: cloud/deploy, CI/CD, sensores físicos, integração dinâmica de áreas/sensores e operação externa. |
| Scenario C | Obrigatório para a comparação entre fluxo nominal e fluxo degradado. |
| Figuras obrigatórias | Não há imposição externa; a equipa decide com base no valor explicativo/evidencial. |
| Relatório beta | Pode ser reaproveitado como fonte histórica/parcial de texto, mas não como fonte de verdade final sobre o estado técnico. |

---

## 2. Estados editoriais usados no documento

| Estado | Significado |
|---|---|
| **Disponível** | Informação já existe e pode ser usada com revisão normal. |
| **Parcial** | Informação existe, mas precisa de complemento, atualização ou validação. |
| **Histórico** | Informação útil para explicar evolução, mas não deve ser usada como estado final da implementação. |
| **A validar** | Informação existe ou parece existir, mas precisa de confirmação final. |
| **A produzir** | Informação, evidência, figura ou texto ainda não existe e terá de ser criado. |
| **Dependente da V3** | Só pode ser fechado depois das tarefas de aprendizagem automática. |
| **Dependente da V4** | Só pode ser fechado depois de definir/prototipar a evolução real/cloud/CI-CD. |
| **Fonte a identificar** | Ainda não foi identificada fonte concreta. |

---

## 3. Assunções técnicas críticas e regras de coerência

Esta secção funciona como gate antes da escrita final dos capítulos técnicos. Nenhum claim técnico sensível deve ser fechado no relatório sem passar por estas regras.

### 3.1. Fronteira V1/V2

**Regra para o relatório**

A fronteira V1/V2 deve ser explícita.

- **V1**: baseline operacional — simulação, RabbitMQ, ingestão durável, Prevention Host, PostgreSQL, NP Score, projeções, API, UI, Run Orchestrator e cenários B/C.
- **V2**: camada metodológica de comparação/proveniência — FWI, KBDI e Portuguese Context Proxy.

**Regra editorial**

- Não tratar V2 como sistema externo separado se estiver integrada no runtime atual.
- Não esconder FWI/KBDI se aparecem na UI.
- Explicar que a UI final pode apresentar elementos V2 porque a entrega evoluiu para além da baseline V1 inicial.
- Legendar FWI/KBDI como comparação metodológica/proveniência, não como validação oficial.

**Onde aplicar**

- Capítulo 5;
- Capítulo 6;
- Capítulo 7;
- D5;
- D8.

**Estado:** regra editorial fechada; evidência final a validar.

---

### 3.2. Estado da V3

**Regra para o relatório**

A V3 fica no Capítulo 7 como **Técnicas de Aprendizagem Automática**.

A estrutura reserva espaço para esta componente como fase integrante do relatório final. A escrita desta secção depende de tarefas V3 ainda não realizadas. A versão final deve completar esta parte com os modelos, dados, métricas e resultados efetivamente obtidos.

**Regra editorial**

- Não remover a V3 da estrutura.
- Não escrever resultados de V3 antes de existirem.
- Distinguir objetivo, desenho técnico, implementação, métrica e resultado.
- Se a V3 não ficar fechada, escrever como estudo técnico/roadmap fundamentado e não como resultado.

**Onde aplicar**

- Capítulo 7;
- B6;
- E3;
- Capítulo 10.

**Estado:** dependente da V3.

---

### 3.3. UI como evidência visual, não fonte canónica

**Regra para o relatório**

A UI demonstra e comunica o estado do sistema. A fonte canónica da evidência deve ser backend, base de dados, logs ou pasta de evidência.

Cada screenshot importante deve indicar:

- o que a UI mostra;
- que requisito suporta;
- qual é a fonte canónica;
- se mostra V1, V2 ou V3;
- escala apresentada vs escala persistida, quando aplicável.

**Onde aplicar**

- D8;
- Capítulo 8;
- legendas das figuras;
- matriz requisito → funcionalidade → evidência.

**Estado:** regra editorial fechada.

---

## 4. Tese central e narrativa do relatório

### 4.1. Formulação principal

> O NatureProtector é um projeto de engenharia data-driven aplicado ao risco de incêndio rural. Nasce de uma oportunidade observada em contexto real e evolui para uma baseline técnica capaz de simular cenários, processar eventos ambientais, calcular risco candidato, comparar esse risco com FWI e KBDI, e produzir evidência operacional reprodutível. A solução foi estruturada para suportar evolução incremental, incluindo técnicas de aprendizagem automática e futura integração em ambientes reais.

### 4.2. Hierarquia de ênfase

1. Engenharia e arquitetura.
2. Investigação e fundamentação.
3. Execução e validação.
4. Evolução da ideia.
5. Metodologia de trabalho.
6. Potencial futuro.

### 4.3. Regra editorial

O relatório principal deve contar a história técnica de forma suficiente. Os anexos devem guardar:

- detalhe técnico;
- prova;
- rastreabilidade;
- evidência runtime;
- prints e diagramas;
- matrizes;
- entrevistas;
- decisões;
- documentos convertidos para LaTeX;
- referências extensas;
- dificuldades e alternativas exploradas.

---

## 5. Registo de fontes internas e evidência existente

### 5.1. Documentos de origem, contexto e empreendedorismo

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `NatureProtector.pdf` | PDF | Origem, visão, Erasmus, startup, Copernicus/EFFIS, problema inicial | Disponível | Usar como contexto, não como eixo principal do relatório de engenharia. Validar estatísticas antes de citar. |
| `TranscriptionOfInterviews.docx` | Word | Entrevistas e validação qualitativa do problema | Disponível | Usar síntese no relatório; transcrições completas ou excertos em anexo. |
| `BrainStormingGame.docx` | Word | Ideação inicial e dinâmica exploratória | Histórico | Usar apenas para origem/ideação. |
| `BusinessModelCanvas.docx` | Word | Modelo de negócio e startup | Histórico/Disponível | Usar em A1/A3, não no corpo técnico. |
| `ValuePreposition.docx` | Word | Proposta de valor | Histórico/Disponível | Usar em A1/A3, com peso moderado. |

### 5.2. Documentos de requisitos, escopo e pesquisa

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `NP_DocumentoDeFechoDoEscopo.pdf` | PDF | Requisitos, escopo, critérios de aceitação, delimitação da baseline | Disponível | Mapear terminologia antiga para V1/V2/V3 quando necessário. |
| `pesquisa_incendios.pdf` | PDF | Pesquisa inicial, sensores, variáveis, enquadramento, termos técnicos | Disponível/Parcial | Validar referências externas antes de usar. |
| `PesquisaII.pdf` | PDF | Cadeia causal, fórmula V1, RiskInput, DailyCellState, FWI/KBDI, metodologia | Disponível | Distinguir proposta metodológica, candidate defaults e implementação real. |
| `pesquisa-ii-vs-implementation-state.md` | MD | Aderência entre pesquisa e implementação, gaps, estado técnico | Parcial/A validar | Usar como auditoria, mas atualizar com evidência final. |
| `project-completion-roadmap.md` | MD | Roadmap, V3/V4, evolução futura | Parcial/Dependente da V3/V4 | Não apresentar tarefas futuras como implementadas sem prova. |
| `Goal_Specification_WorldTree_MiguelAlves.pdf` | PDF | Hipótese ML/GNN, sensores como grafo, analogia do autocarro | Histórico/Dependente da V3 | Tratar como visão técnica exploratória. |

### 5.3. Documentos de arquitetura, implementação e operação

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `README.md` | MD | Porta de entrada do repositório, clone-to-run | Disponível | Fonte principal para setup geral. |
| `docs/setup/local-baseline-setup.md` | MD | Setup local, login, Run Orchestrator, troubleshooting | Disponível | Fonte principal de setup/demo. |
| `docs/NatureProtector-V1-overview.md` | MD | Visão consolidada da V1, fronteiras, validação técnica | Disponível | Rever claims fortes antes da versão final. |
| `Report.pdf` | PDF | Relatório beta, texto e estrutura reaproveitáveis | Histórico/Parcial | Não usar como fonte de verdade final sobre implementação ou validação. |
| `architecture.md` | MD | Arquitetura conceptual, blocos, drivers, fronteiras | Parcial/Histórico | Validar contra código, README atual e docs mais recentes. |
| `implementation.md` | MD | Pipeline, implementação, componentes internos | Parcial/Histórico | Confirmar nomes/classes/tabelas contra branch final. |
| `v1-implementation-map.md` | MD | Workstreams, planeamento, estratégia de execução | Histórico/Disponível | Usar no Cap. 5, não como prova de estado final. |
| `docs/contracts/event-catalog.md` | MD | Eventos e contratos | Disponível | Fonte forte para C2. |
| `docs/contracts/v1-vocabulary-map.md` | MD | Vocabulário técnico e fronteiras conceptuais | Disponível | Fonte forte para glossário e C2. |
| `docs/contracts/README.md` | MD | Contratos e fronteiras | Disponível | Fonte complementar. |
| `docs/architecture/scenario-run-orchestrator.md` | MD | Run Orchestrator, run-spec, parâmetros, seed, cenários | Disponível | Fonte principal para D6. |
| `docs/architecture/postgresql-architecture.md` | MD | PostgreSQL, schemas, papel da persistência | Disponível | Fonte principal para C4. |
| `docs/architecture/pipeline-influx-options.md` | MD | Influx como telemetria/observabilidade | Disponível | Fonte principal para Influx. |
| `docs/architecture/grafana-influx-dashboard-guide.md` | MD | Grafana/Influx dashboards | Parcial/A validar | Usar só se dashboards estiverem estáveis. |
| `organization-description.pdf` | PDF | Organização do repositório | Disponível/Parcial | Converter integralmente para LaTeX e validar se cobre estado final. |

### 5.4. Evidência técnica e runtime

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `docs/evidence/dev-runtime/*scenario-b*` | logs/evidência | scenario_b, run nominal, runtime | Histórico/A validar | Usar histórico; gerar evidência final antes de fechar relatório. |
| `docs/evidence/progress-2026-05-22/*scenario-b*` | evidência histórica | scenario_b | Histórico | Usar como evolução, não como prova final. |
| `docs/evidence/progress-2026-05-22/*scenario-c*` | evidência histórica | scenario_c | Histórico | Requer run C recente para validação final. |
| `docs/evidence/db-state/postgres-*` | DB exports | PostgreSQL schema/counts/samples | A validar | Exportar novamente no fecho final, depois das runs B/C. |
| `docs/evidence/db-state/influx-*` | DB exports | Influx health/schema/tables | A validar | Exportar novamente no fecho final; não incluir tokens completos. |
| `docs/evidence/*test*.txt` | outputs | testes | Histórico/A validar | Gerar build/test final. |
| `coveragereport_core/` | coverage | cobertura | A validar | Confirmar percentagem final antes de citar. |

### 5.5. Artefactos visuais

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `01-platform-context.png` | diagrama | contexto/plataforma | Disponível/A validar | Usar no Cap. 4 se atualizado. |
| `03-end-to-end-data-chain.png` | diagrama | cadeia de dados | Disponível/A validar | Validar nomes/fluxos. |
| `09-operational-pipeline-overview.png` | diagrama | pipeline operacional | Disponível/A validar | Cruzar com `ReadingRiskPipeline`. |
| `11-persistence-views.png` | diagrama | persistência/projeções | Disponível/A validar | Validar contra DB final. |
| `17-scenario-run-orchestrator.png` | diagrama | Run Orchestrator | Disponível | Usar em Cap. 6/8 ou D6. |
| `presentation/P07-DecisaoDeRisco.png` | diagrama | decisão de risco | Disponível/A validar | Usar no Cap. 7. |
| Screenshots UI finais | screenshots | evidência visual | A produzir | Capturar após runs finais. |

### 5.6. Diários e metodologia

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `MiguelAlves.md` | diário | metodologia, dificuldades, decisões, lições | Histórico | Extrair racional; não copiar notas brutas. |
| `NatureProtector.brain/*.md` | memória/auditoria | auditoria, readiness, P0/P1, decisões | Histórico/Complementar | Usar como apoio interno, não como prova primária. |
| `esquema_relatorio_natureprotector.md` | plano | estrutura e escrita | Fonte guia | Não é fonte de domínio. |

### 5.7. Bibliografia e referências

| Fonte | Tipo | Uso principal | Estado editorial | Risco / nota |
|---|---|---|---|---|
| `.bib` atual do relatório | BibTeX | citações e referências externas | Parcial/A validar | Limpar `TODO_*`, duplicados, entradas incompletas e referências em falta. |
| documentação oficial FWI/KBDI/IPMA/EFFIS | bibliografia | índices e enquadramento | Parcial | Confirmar chaves e metadados. |
| documentação oficial PostgreSQL/InfluxDB/Grafana/Docker/.NET | bibliografia técnica | estado da arte técnico, se necessário | Fonte a identificar | Só incluir se for realmente citada. |
| referências ML/GNN/wildfire prediction | bibliografia V3 | Cap. 2, Cap. 7, B6, E3 | A produzir | Necessário para V3. |
| referências CI/CD/cloud | bibliografia V4 | Cap. 10, E4 | A produzir | Necessário se V4 for detalhada. |

---

## 6. Estrutura fechada do relatório

### 6.1. Elementos pré-textuais

#### 6.1.1. Capa

**Conteúdo**

- título;
- autores;
- orientadores;
- instituição;
- curso;
- ano letivo.

**Fontes a consultar**

- template institucional;
- relatório beta;
- documento de organização.

**Estado:** Disponível.

---

#### 6.1.2. Resumo

**Conteúdo**

- problema;
- objetivo;
- solução;
- arquitetura;
- validação;
- principais resultados;
- limitações;
- trabalho futuro.

**Fontes a consultar**

- `Report.pdf`, apenas como fonte histórica/parcial de texto;
- `README.md`;
- `docs/NatureProtector-V1-overview.md`;
- evidência final B/C;
- build/test/coverage finais;
- estado final da V3.

**Estado:** Parcial. Só deve ser fechado no fim.

---

#### 6.1.3. Abstract

Mesma função do resumo, em inglês.

**Estado:** Parcial. Só deve ser fechado no fim.

---

#### 6.1.4. Utilização de Inteligência Artificial

**Conteúdo**

- ferramentas usadas: ChatGPT, Codex, NotebookLM;
- apoio à pesquisa, planeamento, escrita, auditoria e revisão;
- limites e validação humana;
- exemplos de falhas/limitações;
- responsabilidade final da equipa.

**Fontes a consultar**

- `MiguelAlves.md`;
- `NatureProtector.brain/*.md`;
- prompts e auditorias relevantes;
- respostas NotebookLM/Codex usadas para estrutura;
- A4;
- A6.

**Estado:** A produzir.

---

#### 6.1.5. Lista de acrónimos

**Conteúdo mínimo**

API, CFFDRS, CI/CD, DB, EFFIS, FWI, GNN, ICNF, IPMA, KBDI, ML, NP, PIR, RCM, UI, V1, V2, V3, V4.

**Fontes a consultar**

- relatório beta;
- `v1-vocabulary-map.md`;
- anexos de índices;
- glossário próprio.

**Estado:** Parcial/Disponível.

---

### 6.2. Capítulo 1 — Introdução

#### 1.1. Enquadramento geral

**O que deve dizer**

- enquadramento do risco de incêndio rural;
- necessidade de monitorização, prevenção e apoio à decisão;
- dificuldade de combinar dados ambientais, sensores, índices e evidência operacional;
- introdução breve ao NatureProtector.

**Fontes a consultar**

- `NatureProtector.pdf`;
- `pesquisa_incendios.pdf`;
- `PesquisaII.pdf`;
- `Report.pdf`, apenas como fonte histórica/parcial;
- `copernicus_effis`;
- `ec_jrc_effis`;
- `icnf_incendios_rurais`;
- `icnf_cartografia_incendios`.

**Informação ainda a produzir**

- confirmar dados/notícias específicas de incêndios de Castelo Branco 2024 se forem usados.

**Estado:** Disponível/Parcial.

---

#### 1.2. Motivação do projeto

**O que deve dizer**

- origem em contexto real;
- FSPT 2024/2025;
- incêndios perto de Castelo Branco;
- ideia de plataforma para centralizar monitorização/resposta/prevenção;
- referência curta ao Erasmus e à maturação da ideia.

**Fontes a consultar**

- `NatureProtector.pdf`;
- `BrainStormingGame.docx`;
- `BusinessModelCanvas.docx`;
- `ValuePreposition.docx`;
- notas sobre Salzburg IdeaUp;
- A1;
- A3.

**Estado:** Disponível.

---

#### 1.3. Problema abordado

**O que deve dizer**

- problema técnico: risco ambiental operacional com dados incompletos, sensores e índices;
- necessidade de simulação antes de sensores reais;
- necessidade de execução reprodutível;
- necessidade de comparar comportamento nominal e degradado.

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `PesquisaII.pdf`;
- `pesquisa_incendios.pdf`.

**Estado:** Disponível.

---

#### 1.4. Objetivos do projeto

**O que deve dizer**

- criar baseline técnica;
- simular leituras ambientais;
- processar eventos;
- persistir estado;
- calcular NP Score;
- integrar FWI/KBDI;
- comparar cenário B/C;
- preparar técnicas de aprendizagem automática.

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `docs/NatureProtector-V1-overview.md`;
- `project-completion-roadmap.md`.

**Estado:** Disponível/Dependente da V3 para fecho completo.

---

#### 1.5. Contribuições principais

**O que deve dizer**

- arquitetura distribuída local;
- pipeline durável;
- Run Orchestrator;
- score candidato;
- integração FWI/KBDI;
- evidência runtime;
- setup clone-to-run;
- preparação para aprendizagem automática.

**Fontes a consultar**

- `README.md`;
- `docs/setup/local-baseline-setup.md`;
- `docs/NatureProtector-V1-overview.md`;
- evidência final B/C.

**Estado:** Parcial/A validar.

---

### 6.3. Capítulo 2 — Estado da Arte e Enquadramento

#### 2.1. Incêndios rurais e sistemas de apoio à decisão

**O que deve dizer**

- diferença entre perigo, risco, suscetibilidade e previsão;
- sistemas de informação sobre incêndios;
- papel de dados ambientais e territoriais.

**Fontes a consultar**

- `pesquisa_incendios.pdf`;
- `PesquisaII.pdf`;
- `NatureProtector.pdf`;
- `copernicus_effis`;
- `ec_jrc_effis`;
- `icnf_incendios_rurais`;
- `icnf_cartografia_incendios`;
- `ghorbanzadeh_forest_fire_susceptibility`, se confirmado.

**Estado:** Disponível/Parcial.

---

#### 2.2. Dados territoriais e ambientais

**O que deve dizer**

- ocupação do solo;
- cartografia;
- dados meteorológicos;
- EFFIS/Copernicus;
- fontes públicas;
- limitações.

**Fontes a consultar**

- `NatureProtector.pdf`;
- `PesquisaII.pdf`;
- `dgt_cos`;
- `clms_corine_land_cover`;
- `copernicus_effis`;
- `ecmwf_era5`;
- `ecmwf_era5_land`.

**Estado:** Disponível/Parcial.

---

#### 2.3. Sensores e qualidade de dados

**O que deve dizer**

- sensores ambientais;
- temperatura, humidade, vento, precipitação;
- qualidade de dados;
- leituras completas/parciais/bloqueadas;
- distinção entre erro observacional e erro de pipeline.

**Fontes a consultar**

- `pesquisa_incendios.pdf`;
- `PesquisaII.pdf`;
- `wmo_no8`;
- `ioos_qc_flags`;
- `teh_sensor_data_quality`, se confirmado;
- datasheets relevantes se forem usados.

**Estado:** Disponível/Parcial.

---

#### 2.4. Índices de risco e perigo de incêndio

**O que deve dizer**

- FWI;
- KBDI;
- IPMA/PIR/RCM como enquadramento;
- diferença entre índice meteorológico, seca acumulada e classificação operacional.

**Fontes a consultar**

- `PesquisaII.pdf`;
- `nrcan_fwi_system`;
- `canadian_forest_service_cffdrs`;
- `ipma_fwi`;
- `ipma_pir_rcm`;
- `keetch_byram_1968`.

**Estado:** Disponível.

---

#### 2.5. Arquiteturas event-driven e processamento assíncrono

**O que deve dizer**

- RabbitMQ;
- AMQP;
- acknowledgements;
- retry;
- idempotência;
- separação entre ingestão e processamento.

**Fontes a consultar**

- `docs/contracts/event-catalog.md`;
- `docs/contracts/v1-vocabulary-map.md`;
- `rabbitmq_amqp_model`;
- `rabbitmq_consumer_acknowledgements`;
- `rabbitmq_negative_acknowledgements`;
- `richardson_idempotent_consumer`;
- `richardson_transactional_outbox`.

**Estado:** Disponível.

---

#### 2.6. Persistência e observabilidade

**O que deve dizer**

- PostgreSQL como persistência durável;
- InfluxDB como telemetria;
- Grafana como visualização;
- diferença entre fonte canónica e observabilidade.

**Fontes a consultar**

- `docs/architecture/postgresql-architecture.md`;
- `docs/architecture/pipeline-influx-options.md`;
- `docs/architecture/grafana-influx-dashboard-guide.md`, se validado;
- documentação oficial PostgreSQL;
- documentação oficial InfluxDB;
- documentação oficial Grafana.

**Estado:** Parcial.

---

#### 2.7. Técnicas de aprendizagem automática

**O que deve dizer**

- classificação;
- regressão;
- previsão;
- grafos/GNN;
- comparação com heurísticas;
- métricas.

**Fontes a consultar**

- `Goal_Specification_WorldTree_MiguelAlves.pdf`;
- `project-completion-roadmap.md`;
- `PesquisaII.pdf`;
- referências externas ML/GNN a recolher;
- `sklearn_metrics_scoring`;
- `fawcett_2006_roc`;
- `hyndman_athanasopoulos_forecasting`.

**Estado:** Dependente da V3.

---

### 6.4. Capítulo 3 — Requisitos, Âmbito e Momentos de Pesquisa

#### 3.1. Função do capítulo

Este capítulo define o objetivo, os requisitos, o âmbito e os momentos de pesquisa. Não deve explicar ainda o percurso detalhado de implementação; isso fica no Capítulo 5.

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `PesquisaII.pdf`;
- `pesquisa_incendios.pdf`;
- `Report.pdf`, apenas como fonte histórica/parcial.

**Estado:** Disponível.

---

#### 3.2. Requisitos funcionais

**O que deve dizer**

Síntese dos RF principais no corpo. Matriz completa em B2 e rastreabilidade em D8.

| ID | Requisito funcional |
|---|---|
| RF01 | Configurar área piloto. |
| RF02 | Representar sensores/nós. |
| RF03 | Simular leituras ambientais. |
| RF04 | Publicar eventos de sensores. |
| RF05 | Processar eventos recebidos. |
| RF06 | Validar/classificar qualidade das leituras. |
| RF07 | Calcular NP Score. |
| RF08 | Calcular/mostrar FWI. |
| RF09 | Calcular/mostrar KBDI. |
| RF10 | Executar cenários pelo Run Orchestrator. |
| RF11 | Comparar cenário nominal e cenário degradado. |
| RF12 | Expor evidência runtime na UI. |
| RF13 | Exportar/reproduzir evidência técnica. |

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `docs/NatureProtector-V1-overview.md`;
- D8.

**Estado:** Disponível/Parcial.

---

#### 3.3. Requisitos não funcionais

| ID | Requisito não funcional |
|---|---|
| RNF01 | Reprodutibilidade. |
| RNF02 | Rastreabilidade. |
| RNF03 | Auditabilidade. |
| RNF04 | Observabilidade. |
| RNF05 | Separação de responsabilidades. |
| RNF06 | Extensibilidade. |
| RNF07 | Tolerância a falhas/parcialidade. |
| RNF08 | Execução local documentada. |

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `docs/setup/local-baseline-setup.md`;
- `README.md`;
- `docs/contracts/*`.

**Estado:** Disponível/Parcial.

---

#### 3.4. Âmbito da baseline

**O que deve dizer**

- V1: baseline técnica e NP Score;
- V2: FWI/KBDI/Portuguese Context Proxy;
- V3: aprendizagem automática;
- V4: integração real/cloud/CI-CD;
- dentro/fora de âmbito.

**Fontes a consultar**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- `v1-implementation-map.md`;
- `project-completion-roadmap.md`.

**Estado:** Disponível/Dependente da V3/V4 para fecho final.

---

#### 3.5. Os seis momentos de pesquisa

| Momento | Tema | Anexo principal | Estado |
|---|---|---|---|
| M1 | Origem, necessidade e definição do problema | B1 | Disponível |
| M2 | Requisitos, escopo e baseline | B2 | Disponível |
| M3 | Área piloto, dados e sensores | B3 | Disponível/Parcial |
| M4 | Fórmula NatureProtector V1 | B4 | Disponível |
| M5 | FWI, KBDI e V2 | B5 | Disponível |
| M6 | Machine Learning e V3 | B6 | Dependente da V3 |

---

### 6.5. Capítulo 4 — Arquitetura e Suporte de Desenvolvimento

#### 4.1. Visão geral da arquitetura

**O que deve dizer**

- arquitetura em blocos;
- separação de responsabilidades;
- Simulator Host;
- RabbitMQ;
- Prevention Host;
- Backoffice API;
- webUI;
- PostgreSQL;
- InfluxDB/Grafana.

**Fontes a consultar**

- `docs/NatureProtector-V1-overview.md`;
- `architecture.md`, com validação;
- `docs/contracts/*`;
- `docs/architecture/postgresql-architecture.md`;
- `docs/architecture/pipeline-influx-options.md`;
- código dos projetos principais;
- `01-platform-context.png`;
- `presentation/P06-Arquitetura3Blocos.png`;
- `03-end-to-end-data-chain.png`.

**Estado:** Disponível/A validar.

---

#### 4.2. Componentes principais

| Componente | Fontes principais | Estado |
|---|---|---|
| Simulator Host | `SimulationRunner.cs`, `ReadingGenerationService.cs`, `PostgresSimulationContextSource.cs`, `scenario-run-orchestrator.md` | Disponível |
| RabbitMQ | `NatureProtectorRabbitMqTopology.cs`, `RabbitMqOptions.cs`, `event-catalog.md` | Disponível |
| Prevention Host | `ReadingRiskPipeline.cs`, `PostgresReadingEventInbox.cs`, projection stores | Disponível |
| Backoffice API | controllers, runtime contracts, control plane services | Disponível |
| webUI | `Workspace.tsx`, `api.ts`, UI views | Disponível/A validar screenshots |
| PostgreSQL | `NatureProtectorControlDbContext.cs`, records, migrations, `postgresql-architecture.md` | Disponível |
| InfluxDB/Grafana | `Infrastructure.Influx`, scripts Influx, dashboard guide | Parcial/A validar |

---

#### 4.3. Suporte de desenvolvimento

**O que deve dizer**

- Docker Compose;
- scripts;
- setup local;
- testes;
- documentação;
- clone-to-run.

**Fontes a consultar**

- `README.md`;
- `docs/setup/local-baseline-setup.md`;
- `infra/scripts/up.ps1`;
- `scripts/postgres/bootstrap-control-plane.ps1`;
- `scripts/setup/Test-LocalBaseline.ps1`;
- `scripts/dev/start-local-runtime.ps1`.

**Estado:** Disponível.

---

### 6.6. Capítulo 5 — Estratégia de Implementação e Evolução do Âmbito

#### 5.1. Função do capítulo

Este capítulo não redefine os requisitos. Explica o percurso entre o âmbito definido e a implementação entregue, incluindo fases, decisões, mudanças de prioridade, validações intermédias e evolução da baseline.

**Fontes a consultar**

- `v1-implementation-map.md`;
- `project-completion-roadmap.md`;
- `MiguelAlves.md`;
- `NatureProtector.brain/*.md`;
- auditorias Codex/NotebookLM;
- `Report.pdf`, apenas como fonte histórica/parcial.

**Estado:** Disponível/Parcial.

---

#### 5.2. Estratégia geral de implementação

**O que deve dizer**

- planeamento incremental;
- prototipagem funcional;
- exploração controlada;
- validação por gates;
- ligação a fases V1/V2/V3/V4.

**Fontes a consultar**

- `MiguelAlves.md`;
- planos multifase anteriores;
- A4.

**Estado:** Disponível/Parcial.

---

#### 5.3. Evolução por fases

| Fase | Objetivo técnico | Principal implementação | Critério de fecho | Estado |
|---|---|---|---|---|
| V1 | Baseline e NP Score | pipeline + scoring + projeções | scenario_b com risk assessments | A validar com evidência final |
| V2 | Comparação FWI/KBDI | calculadores + proxy + UI | NP/FWI/KBDI visíveis e persistidos | A validar |
| V3 | Aprendizagem automática | modelos/features/métricas | comparação com heurística | Dependente da V3 |
| V4 | Integração real | cloud/CI-CD/sensores | plano/protótipo demonstrável | Dependente da V4 |

---

#### 5.4. Evolução metodológica V1 → V2

**O que deve dizer**

- a V1 representa a baseline operacional;
- a V2 acrescenta FWI, KBDI e Portuguese Context Proxy como camada metodológica de comparação/proveniência;
- a UI final pode apresentar elementos V2 porque a entrega evoluiu para além da baseline inicial;
- screenshots com FWI/KBDI devem ser interpretadas como evidência da evolução metodológica e da comparação, não como validação oficial;
- o relatório deve distinguir entre “baseline operacional” e “camada de comparação”.

**Fontes a consultar**

- `docs/NatureProtector-V1-overview.md`;
- `PesquisaII.pdf`;
- `MiguelAlves.md`;
- `SimpleRiskScoringService.cs`;
- `CanadianFireWeatherIndexCalculator.cs`;
- `CandidateKbdiCalculator.cs`;
- UI final;
- D5;
- D8.

**Estado:** Disponível/A validar.

---

#### 5.5. Metodologia de trabalho

**O que deve dizer**

- planeamento incremental;
- prototipagem;
- exploração controlada;
- planos próximos da implementação;
- IA como apoio;
- validação humana.

**Fontes a consultar**

- `MiguelAlves.md`;
- A4;
- A6;
- prompts/auditorias relevantes.

**Estado:** Disponível/Parcial.

---

### 6.7. Capítulo 6 — Implementação

#### 6.1. Simulação e geração de eventos

**Fontes a consultar**

- `src/NatureProtector.Simulator.Host`;
- `scenario-run-orchestrator.md`;
- `data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json`;
- `data/manifests/scenarios/proenca-a-nova/scenario_c.degraded-pipeline.json`;
- testes do Simulator Host;
- D6.

**Estado:** Disponível/A validar com runs finais.

---

#### 6.2. Pipeline de ingestão e processamento

**Fontes a consultar**

- `ReadingRiskPipeline.cs`;
- `PostgresReadingEventInbox.cs`;
- `PreventionWorker.cs`;
- `docs/doxygen/pages/prevention-flow.md`, se atualizado;
- `09-operational-pipeline-overview.png`;
- `10-pipeline-retry-and-quarantine-sequence.png`;
- C3.

**Estado:** Disponível/A validar.

---

#### 6.3. Persistência e identidade do `DailyCellState`

**O que deve dizer**

- PostgreSQL como fonte canónica;
- schemas `control`, `pipeline`, `projection`;
- InfluxDB como telemetria;
- `DailyCellState` como estado persistido por célula/dia/run;
- `SensorId` como metadado/contribuição, quando aplicável;
- diferença entre runtime PostgreSQL e implementações auxiliares InMemory/test doubles.

**Fontes a consultar**

- `postgresql-architecture.md`;
- `NatureProtectorControlDbContext.cs`;
- records/migrations;
- DB exports finais;
- `docs/architecture/pipeline-influx-options.md`;
- C4.

**Estado:** Disponível/A validar.

---

#### 6.4. API e UI

**Fontes a consultar**

- Backoffice API controllers;
- `Workspace.tsx`;
- `api.ts`;
- D8;
- screenshots finais;
- endpoints runtime.

**Estado:** Parcial/A produzir screenshots.

---

#### 6.5. Setup clone-to-run

**Fontes a consultar**

- `README.md`;
- `docs/setup/local-baseline-setup.md`;
- scripts de setup;
- evidência clone-to-run final.

**Estado:** Disponível/A validar.

---

### 6.8. Capítulo 7 — Avaliação de Risco: NatureProtector, FWI, KBDI e Técnicas de Aprendizagem Automática

#### 7.1. Escala canónica do score

**O que deve dizer**

- o score canónico é normalizado em `0–1`;
- `Score100` é uma representação derivada;
- tabelas/DB e screenshots devem ser legendadas de forma consistente;
- valores como `0.448` são scores normalizados, não percentagens brutas.

**Fontes a consultar**

- `SimpleRiskScoringService.cs`;
- `RiskAssessment`/projection records;
- DB final;
- D1;
- D8.

**Estado:** fechado como regra editorial; evidência final a validar.

---

#### 7.2. NatureProtector Score

**O que deve dizer**

- score candidato;
- componentes;
- fórmula em duas etapas;
- escala normalizada;
- `Score100` como derivado;
- limitações.

**Fórmula recomendada**

```text
T = 0.50H + 0.30F + 0.20G
BaseRisk = 0.50M + 0.20D + 0.30T
```

**Fontes a consultar**

- `PesquisaII.pdf`;
- `docs/NatureProtector-V1-overview.md`;
- `SimpleRiskScoringService.cs`;
- `CandidateParameterSetV1.cs`;
- testes de scoring;
- D1.

**Estado:** disponível/a validar com evidência final.

---

#### 7.3. Fire Weather Index

**Fontes a consultar**

- `PesquisaII.pdf`;
- `CanadianFireWeatherIndexCalculator.cs`;
- testes FWI;
- D2;
- `nrcan_fwi_system`;
- `canadian_forest_service_cffdrs`;
- `ipma_fwi`.

**Estado:** Disponível/Parcial.

---

#### 7.4. KBDI

**Fontes a consultar**

- `PesquisaII.pdf`;
- `CandidateKbdiCalculator.cs`;
- testes KBDI;
- D3;
- `keetch_byram_1968`.

**Estado:** Disponível/Parcial.

---

#### 7.5. Portuguese Context Proxy

**Regra editorial**

O Portuguese Context Proxy deve ser apresentado como proxy candidato. Não deve ser apresentado como metodologia oficial IPMA/ICNF/RCM.

**Fontes a consultar**

- `IndexClassifications.cs`;
- `TerritorialRiskContext.cs`;
- `ipma_fwi`;
- `ipma_pir_rcm`;
- D4.

**Estado:** Disponível/Parcial.

---

#### 7.6. Comparação NP/FWI/KBDI

**Formulação segura**

> A comparação com FWI e KBDI é usada para análise metodológica, identificação de convergências/divergências e orientação de evolução do NP Score. Não constitui, por si só, validação científica final do índice.

**Fontes a consultar**

- D5;
- outputs de UI;
- `risk_assessment_log`;
- `daily_cell_state`;
- `wilks_statistical_methods`;
- `schober_2018_correlation`.

**Estado:** A validar.

---

#### 7.7. Técnicas de aprendizagem automática

**Fontes a consultar**

- `Goal_Specification_WorldTree_MiguelAlves.pdf`;
- `project-completion-roadmap.md`;
- `PesquisaII.pdf`;
- B6;
- E3;
- referências ML/GNN a recolher.

**Informação ainda a produzir**

- dataset/features finais;
- modelo(s);
- métricas;
- comparação com heurística.

**Estado:** Dependente da V3.

---

### 6.9. Capítulo 8 — Validação Técnica e Evidência em Tempo de Execução

#### 8.1. Estratégia de validação

**Fontes a consultar**

- `docs/setup/local-baseline-setup.md`;
- `docs/evidence/dev-runtime/*`;
- outputs finais build/test/coverage;
- D7;
- D8.

**Estado:** A validar/A produzir.

---

#### 8.2. Gate de coerência técnica antes da evidência final

Antes de capturar screenshots e fechar a validação, deve ser feita uma verificação curta:

| Tema | O que confirmar | Fonte |
|---|---|---|
| Escala NP Score | Persistido 0–1; `Score100` derivado | DB/código/UI |
| `DailyCellState` | Identidade por célula/dia/run no PostgreSQL | DB/código |
| H/F/G | Fórmula de `T` e uso de `T` no score | código/testes |
| V1/V2 | Que screenshots mostram baseline e que screenshots mostram extensão V2 | UI/docs |
| Scenario C | Run atual e comparação com B | DB/UI/evidence |
| Fonte canónica | Cada screenshot mapeado para DB/API/logs | D8 |

**Output esperado**

```text
Tema | Estado | Evidência | Impacto no relatório | Correção necessária
```

**Estado:** A produzir.

---

#### 8.3. Build e testes automatizados

**Fontes a consultar**

- `tests/*`;
- `tests/README.md`;
- coverage script;
- outputs finais;
- D7.

**Estado:** A produzir.

---

#### 8.4. Validação clone-to-run

**Fontes a consultar**

- `README.md`;
- `docs/setup/local-baseline-setup.md`;
- outputs finais de setup;
- C5.

**Estado:** A validar.

---

#### 8.5. Scenario B

**Fontes a consultar**

- run final B;
- SQL final;
- screenshots finais;
- D6;
- D8.

**Estado:** A validar/A produzir.

---

#### 8.6. Scenario C

**Fontes a consultar**

- run final C;
- comparação B/C;
- SQL final;
- screenshots finais;
- D6;
- D8.

**Nota**

Obrigatório para demonstrar comparação entre fluxo nominal e fluxo degradado.

**Estado:** A validar/A produzir.

---

#### 8.7. Evidência visual da UI

**Fontes a consultar**

- screenshots finais;
- componentes UI;
- endpoints;
- DB/projeções;
- D8.

**Estado:** A produzir.

---

#### 8.8. Estado das bases de dados

**Fontes a consultar**

- PostgreSQL exports finais após runs B/C;
- Influx exports finais após runs B/C;
- `postgresql-architecture.md`;
- `pipeline-influx-options.md`;
- C4.

**Estado:** A validar/A produzir.

---

### 6.10. Capítulo 9 — Discussão, Limitações e Dificuldades

#### 9.1. Limitações técnicas e metodológicas

**Fontes a consultar**

- `PesquisaII.pdf`;
- `pesquisa-ii-vs-implementation-state.md`;
- `docs/NatureProtector-V1-overview.md`;
- A6;
- D5.

**Estado:** Disponível/Parcial.

---

#### 9.2. Automação documental, memória operacional e custo de manutenção

**O que deve dizer**

- foi explorada a criação de documentação automática/memória operacional do repositório;
- objetivo: reduzir complexidade e obter visão quase em tempo real sobre estrutura, decisões, componentes e estado;
- limitações encontradas:
  - resultado pouco interativo;
  - pouca maleabilidade;
  - ausência de descoberta automática completa;
  - necessidade de configuração manual;
  - custo de manutenção superior ao retorno;
- consequência:
  - redução/abandono da abordagem;
  - reforço de documentação curada;
  - criação de anexos especializados;
  - utilização de auditorias;
  - uso de planos faseados e gates de validação.

**Fontes a consultar**

- `MiguelAlves.md`;
- `NatureProtector.brain/*.md`;
- A4;
- A6.

**Estado:** Disponível/Parcial.

---

#### 9.3. Dificuldades e alternativas exploradas

**Fontes a consultar**

- `MiguelAlves.md`;
- `NatureProtector.brain/*.md`;
- A6.

**Estado:** Disponível/Parcial.

---

#### 9.4. Lições aprendidas

**Fontes a consultar**

- diário;
- auditorias;
- evidência final;
- discussão com professores.

**Estado:** Parcial.

---

### 6.11. Capítulo 10 — Trabalho Futuro: V3 e V4

#### 10.1. Evolução da V3

**Fontes a consultar**

- B6;
- E3;
- `Goal_Specification_WorldTree_MiguelAlves.pdf`;
- `project-completion-roadmap.md`;
- referências ML/GNN.

**Estado:** Dependente da V3.

---

#### 10.2. Evolução V4

**Fontes a consultar**

- E4;
- roadmap;
- fontes CI/CD/cloud a recolher.

**Estado:** Dependente da V4.

---

#### 10.3. Evolução da validação

**Fontes a consultar**

- métricas;
- validação científica;
- dados reais;
- referências forecasting/ML.

**Estado:** Dependente da V3/V4.

---

### 6.12. Capítulo 11 — Conclusões

**O que deve dizer**

- síntese do trabalho;
- contribuição principal;
- limitações assumidas;
- valor académico;
- próximos passos.

**Fontes a consultar**

- todos os capítulos;
- validação final;
- estado final da V3;
- anexos principais.

**Estado:** Parcial. Só deve ser fechado no fim.

---

## 7. Plano de anexos

Cada anexo deve conter:

1. objetivo;
2. conteúdo;
3. fontes a consultar;
4. evidência associada;
5. figuras/screenshots;
6. estado;
7. dependências;
8. riscos.

### 7.1. Grupo A — Contexto, metodologia e responsabilidade

#### A0 — Mapa de Anexos e Guia de Leitura

**Objetivo**

Orientar o leitor nos anexos.

**Conteúdo**

- grupos A–E;
- lista de anexos;
- função de cada anexo;
- capítulos onde são chamados;
- estado: essencial/complementar/futuro.

**Estado:** A produzir.

---

#### A1 — Origem e evolução do NatureProtector

**Fontes**

- `NatureProtector.pdf`;
- `BrainStormingGame.docx`;
- `BusinessModelCanvas.docx`;
- `ValuePreposition.docx`;
- material Salzburg IdeaUp;
- fotos se existirem.

**Estado:** Disponível.

---

#### A2 — Entrevistas, consulta e validação inicial do problema

**Fontes**

- `TranscriptionOfInterviews.docx`;
- sínteses de entrevistas;
- lições retiradas.

**Estado:** Disponível.

---

#### A3 — Contributos das unidades curriculares e contexto Erasmus

**Fontes**

- documentos Erasmus;
- Data Analysis;
- Project Management;
- .NET industrial;
- New Business Models;
- Software Design Patterns.

**Estado:** Disponível/Parcial.

---

#### A4 — Metodologia de trabalho, planeamento e uso de IA

**Fontes**

- `MiguelAlves.md`;
- prompts;
- respostas Codex/NotebookLM;
- auditorias;
- planos multifase.

**Estado:** Disponível/Parcial.

---

#### A5 — Distribuição de responsabilidades e contributos individuais

**Fontes**

- diário;
- histórico de commits;
- divisão real de tarefas;
- informação da equipa.

**Estado:** A produzir.

---

#### A6 — Dificuldades, alternativas tentadas e lições aprendidas

**Fontes**

- `MiguelAlves.md`;
- `NatureProtector.brain`;
- notas sobre documentação automática/memória;
- decisões abandonadas.

**Estado:** Disponível/Parcial.

---

### 7.2. Grupo B — Momentos de pesquisa e escopo

#### B1 — Momento de Pesquisa 1: necessidades e definição do problema

**Fontes**

- `NatureProtector.pdf`;
- `pesquisa_incendios.pdf`;
- entrevistas;
- Copernicus/EFFIS.

**Estado:** Disponível.

---

#### B2 — Momento de Pesquisa 2: requisitos, escopo e delimitação da baseline

**Fontes**

- `NP_DocumentoDeFechoDoEscopo.pdf`;
- matriz RF/RNF;
- critérios de aceitação.

**Estado:** Disponível/Parcial.

---

#### B3 — Momento de Pesquisa 3: área piloto, dados e sensores

**Fontes**

- `PesquisaII.pdf`;
- `NatureProtector.pdf`;
- scripts/dados de área;
- poster Data Analysis.

**Estado:** Disponível/Parcial.

---

#### B4 — Momento de Pesquisa 4: fórmula NatureProtector V1

**Fontes**

- `PesquisaII.pdf`;
- `docs/NatureProtector-V1-overview.md`;
- scoring code/tests.

**Estado:** Disponível.

---

#### B5 — Momento de Pesquisa 5: FWI, KBDI e V2

**Fontes**

- `PesquisaII.pdf`;
- FWI/KBDI code/tests;
- referências externas FWI/KBDI/IPMA.

**Estado:** Disponível/Parcial.

---

#### B6 — Momento de Pesquisa 6: Machine Learning e V3

**Fontes**

- `Goal_Specification_WorldTree_MiguelAlves.pdf`;
- `project-completion-roadmap.md`;
- referências ML/GNN a recolher;
- resultados V3 quando existirem.

**Estado:** Dependente da V3.

---

### 7.3. Grupo C — Arquitetura, implementação e operação

#### C1 — Arquitetura técnica detalhada

**Fontes**

- `docs/NatureProtector-V1-overview.md`;
- `architecture.md` validado;
- diagramas;
- código.

**Estado:** Disponível/A validar.

---

#### C2 — Contratos, eventos e vocabulário técnico

**Fontes**

- `docs/contracts/*`;
- `NatureProtectorRabbitMqTopology.cs`;
- event envelope code/tests.

**Estado:** Disponível.

---

#### C3 — Pipeline de processamento, classificação e elegibilidade

**Fontes**

- `ReadingRiskPipeline.cs`;
- Prevention tests;
- pipeline diagrams.

**Estado:** Disponível/A validar.

---

#### C4 — Persistência e estado das bases de dados

**Fontes**

- `postgresql-architecture.md`;
- DB exports finais;
- Influx exports finais;
- migrations;
- código/records do `DailyCellState`.

**Conteúdo obrigatório**

- schemas `control`, `pipeline`, `projection`;
- estado das tabelas;
- identidade do `DailyCellState`;
- diferença entre PostgreSQL runtime e InMemory/test double;
- fonte canónica das projeções;
- export final após runs B/C.

**Estado:** A validar/A produzir.

---

#### C5 — Setup clone-to-run e operação local

**Fontes**

- `README.md`;
- `docs/setup/local-baseline-setup.md`;
- scripts;
- outputs finais setup.

**Estado:** Disponível/A validar.

---

#### C6 — Documento de organização do repositório

**Fontes**

- `organization-description.pdf`;
- versão LaTeX;
- README;
- estrutura final do repo.

**Estado:** Disponível/Parcial.

---

### 7.4. Grupo D — Índices, comparação, validação e UI

#### D1 — Índice NatureProtector V1

**Fontes**

- `PesquisaII.pdf`;
- `SimpleRiskScoringService.cs`;
- `CandidateParameterSetV1.cs`;
- tests.

**Conteúdo obrigatório**

- escala canónica 0–1;
- `Score100` como derivado;
- fórmula territorial em duas etapas;
- `T = 0.50H + 0.30F + 0.20G`;
- `BaseRisk = 0.50M + 0.20D + 0.30T`;
- `AdjustedScore`;
- papel de `StructuralHazardScore`, se aplicável;
- limitações.

**Estado:** Disponível/A validar.

---

#### D2 — Fire Weather Index

**Fontes**

- `PesquisaII.pdf`;
- `CanadianFireWeatherIndexCalculator.cs`;
- tests;
- `nrcan_fwi_system`;
- `canadian_forest_service_cffdrs`;
- `ipma_fwi`.

**Estado:** Disponível/Parcial.

---

#### D3 — KBDI

**Fontes**

- `CandidateKbdiCalculator.cs`;
- tests;
- `keetch_byram_1968`.

**Estado:** Disponível/Parcial.

---

#### D4 — Portuguese Context Proxy

**Fontes**

- `IndexClassifications.cs`;
- `TerritorialRiskContext.cs`;
- IPMA/PIR/RCM references.

**Estado:** Disponível/Parcial.

---

#### D5 — Comparação entre NP Score, FWI e KBDI

**Fontes**

- D1–D4;
- UI;
- DB;
- estatística/correlação;
- validação final.

**Conteúdo obrigatório**

- comparação metodológica;
- convergências/divergências;
- limites;
- não apresentar como validação científica final.

**Estado:** A validar.

---

#### D6 — Cenários, Run Orchestrator e evidência runtime

**Função**

Documentar a execução dos cenários e os resultados runtime.

**Fontes**

- `scenario-run-orchestrator.md`;
- scenario manifests;
- runs finais B/C;
- SQL;
- logs;
- evidence folder final.

**Conteúdo obrigatório**

- scenario_b;
- scenario_c;
- Run Orchestrator;
- parâmetros de execução;
- expected events;
- accepted readings;
- processing attempts;
- risk assessments;
- missing/degradation;
- comparison B/C;
- logs e SQL.

**Estado:** A validar/A produzir.

---

#### D7 — Testes, coverage, build e readiness beta

**Fontes**

- `tests/*`;
- build/test final;
- coverage final;
- readiness reviews.

**Estado:** A produzir.

---

#### D8 — Evidência Visual, UI e Matriz Requisito–Implementação–Demonstração

**Função**

Documentar como os resultados e funcionalidades aparecem na UI e como se ligam a requisitos, componentes, endpoints, backend, DB e fonte canónica.

**Diferença face ao D6**

- **D6** documenta execução e resultados runtime.
- **D8** documenta visualização, rastreabilidade e ligação requisito → UI → implementação → fonte canónica.

**Fontes**

- UI screenshots finais;
- `Workspace.tsx`;
- `api.ts`;
- API controllers;
- DB exports;
- D6/D7.

**Matriz obrigatória**

| Requisito | Funcionalidade | Vista UI | Screenshot | Frontend | API | Backend | DB/projeção | Fonte canónica | Escala persistida | Escala apresentada | Estado V1/V2/V3 | Capítulo | Anexo | Estado |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|

**Screenshots mínimos**

1. Área selecionada sem refresh.
2. Monitoring / Overview.
3. Monitoring / Area Risk com NP/FWI/KBDI.
4. Run Orchestrator antes da execução.
5. Resultado da run.
6. Latest Run Audit.
7. Run Timings.
8. Compare B vs C.
9. Model & Provenance.
10. Data Provenance.

**Estado:** A produzir.

---

### 7.5. Grupo E — Comunicação e futuro

#### E1 — Poster de Data Analysis e análise Copernicus/EFFIS

**Fontes**

- poster Data Analysis;
- `NatureProtector.pdf`;
- Copernicus/EFFIS.

**Estado:** Disponível/Parcial.

---

#### E2 — Poster final do projeto e material de apresentação

**Fontes**

- poster final;
- descrição do poster;
- material de apresentação.

**Estado:** Disponível/Parcial.

---

#### E3 — Roadmap V3: Machine Learning, GNN e modelos preditivos

**Fontes**

- B6;
- `Goal_Specification_WorldTree_MiguelAlves.pdf`;
- `project-completion-roadmap.md`;
- referências ML/GNN.

**Estado:** Dependente da V3.

---

#### E4 — Roadmap V4: integração real, cloud, CI/CD e operação externa

**Fontes**

- roadmap;
- CI/CD references;
- cloud/deploy references;
- plano futuro.

**Estado:** Dependente da V4.

---

## 8. Plano de figuras, diagramas e screenshots

### 8.1. Figuras existentes para corpo principal

| Capítulo | Figura | Função | Estado |
|---|---|---|---|
| 1 | `presentation/P01-project-one-picture.png` | visão simples do projeto | A validar |
| 4 | `01-platform-context.png` | contexto da plataforma | A validar |
| 4 | `presentation/P06-Arquitetura3Blocos.png` | arquitetura em três blocos | A validar |
| 4 | `03-end-to-end-data-chain.png` | cadeia fim-a-fim | A validar |
| 6 | `09-operational-pipeline-overview.png` | pipeline operacional | A validar |
| 6 | `11-persistence-views.png` | persistência/projeções | A validar |
| 6/8 | `17-scenario-run-orchestrator.png` | orquestração de cenários | Disponível |
| 7 | `presentation/P07-DecisaoDeRisco.png` | decisão de risco | A validar |
| 8 | `presentation/P08-RunReprodutivel.png` | run reprodutível | A validar |

### 8.2. Figuras novas a criar

| Figura nova | Função | Estado |
|---|---|---|
| Linha temporal do projeto | origem → Erasmus → V1/V2/V3 | A produzir |
| Seis momentos de pesquisa | mostrar M1–M6 | A produzir |
| Metodologia híbrida | planeamento + prototipagem + exploração controlada | A produzir |
| Comparação NP/FWI/KBDI | inputs, outputs, limitações | A produzir |
| V3 sensores como grafo | hipótese ML/GNN | Dependente da V3 |
| V4 integração real | cloud/CI-CD/sensores | Dependente da V4 |
| Requisito → funcionalidade → UI → evidência | rastreabilidade | A produzir |
| Mapa de anexos | guia de leitura | A produzir |

### 8.3. Regras de legenda

Cada figura deve ter uma legenda orientada por argumento.

Exemplo fraco:

> Arquitetura do sistema.

Exemplo forte:

> Arquitetura em três blocos da baseline local, evidenciando a separação entre simulação, processamento de risco e consulta/controlo operacional.

Para screenshots, a legenda deve indicar:

- requisito suportado;
- funcionalidade demonstrada;
- fonte canónica;
- escala persistida/apresentada, se aplicável;
- se a imagem pertence a V1, V2 ou V3.

---

## 9. Plano de citações e referências

### 9.1. Tipos de fonte

| Tipo | Uso |
|---|---|
| Referência externa | Fundamentar conceitos gerais, índices, metodologias, tecnologias. |
| Documento interno | Explicar decisões, escopo, pesquisa e evolução do projeto. |
| Evidência técnica | Provar execução, resultados, DB, testes e runtime. |
| Evidência visual | Mostrar UI, outputs e funcionalidades. |
| Código/testes | Provar implementação e comportamento técnico. |

### 9.2. Referências por capítulo

| Capítulo | Referências principais | Lacunas |
|---|---|---|
| 1 | EFFIS, ICNF, documentos internos | dados Castelo Branco 2024 se usados |
| 2 | FWI, KBDI, IPMA, sensores, RabbitMQ | PostgreSQL/Influx/Grafana, ML/GNN |
| 3 | `NP_DocumentoDeFechoDoEscopo`, pesquisas internas | metodologia formal de requisitos se necessário |
| 4 | RabbitMQ, idempotência, docs internos | Docker/.NET/PostgreSQL/Influx se citados |
| 5 | fontes internas/metodologia | Agile/prototipagem se formalizado |
| 6 | código, docs internos, evidência | docs oficiais se explicar tecnologias |
| 7 | NP interno, FWI, KBDI, IPMA, ML | GNN/wildfire ML |
| 8 | evidência técnica | métricas/coverage se discutido formalmente |
| 9 | limitações, estatística/correlação | IA no desenvolvimento, se necessário |
| 10 | ML/GNN, CI/CD/cloud | várias referências em falta |
| 11 | sem novas citações | — |

### 9.3. Bibliografia a limpar

A bibliografia atual deve ser revista para:

- remover `TODO_*` quando as chaves finais estiverem estáveis;
- eliminar duplicados FWI/CFFDRS;
- eliminar duplicados EFFIS/Copernicus;
- confirmar referências incompletas;
- acrescentar GNN/ML;
- acrescentar PostgreSQL/InfluxDB/Grafana/Docker/.NET, se necessário;
- acrescentar CI/CD/cloud se o Cap. 10/E4 for detalhado.

---

## 10. Mapa de reaproveitamento de documentos e evidência

| Documento/Evidência | Usar em | Conteúdo reaproveitável | Estado | Risco |
|---|---|---|---|---|
| `NatureProtector.pdf` | Cap. 1, A1, E1 | origem, Copernicus, startup | Disponível | validar estatísticas |
| `TranscriptionOfInterviews.docx` | A2, B1 | entrevistas e lições | Disponível | sintetizar |
| `NP_DocumentoDeFechoDoEscopo.pdf` | Cap. 3, B2 | requisitos, escopo, critérios | Disponível | mapear terminologia |
| `pesquisa_incendios.pdf` | Cap. 2, B1 | pesquisa inicial | Disponível | validar refs |
| `PesquisaII.pdf` | Cap. 2, 7, B4, B5 | fórmula, índices, metodologia | Disponível | separar proposta de implementação |
| `Report.pdf` | pré-textuais, Cap. 1, Cap. 3, revisão textual | texto beta e organização anterior | Histórico/Parcial | não usar como fonte final de implementação |
| `architecture.md` | Cap. 4, C1 | arquitetura | Parcial | pode estar desatualizado |
| `implementation.md` | Cap. 6, C3 | implementação | Parcial | validar contra código |
| `v1-implementation-map.md` | Cap. 5 | estratégia/workstreams | Histórico/Disponível | não usar como estado final |
| `scenario-run-orchestrator.md` | D6, Cap. 8 | Run Orchestrator | Disponível | validar parâmetros |
| `postgresql-architecture.md` | C4 | schemas e persistência | Disponível | validar DB final |
| `project-completion-roadmap.md` | Cap. 10, E3/E4 | futuro | Dependente | não apresentar como feito |
| `Goal_Specification...pdf` | B6/E3 | V3/GNN | Dependente | exploratório |
| `organization-description.pdf` | C6 | organização repo | Disponível/Parcial | atualizar |
| `.bib` atual | todos | referências | Parcial | limpar TODOs e duplicados |
| `docs/evidence/dev-runtime/*` | Cap. 8/D6 | runtime | Histórico/A validar | gerar final |
| `docs/evidence/db-state/*` | C4/D6 | DB state | A validar | gerar final após B/C |
| screenshots UI | D8/Cap. 8 | prova visual | A produzir | obrigatório para relatório visual |

---

## 11. Plano de Produção do Relatório e dos Anexos

### 11.1. Ordem técnica de produção de evidência

| Ordem | Validação | Ações/comandos | Evidência produzida | Suporta | Estado |
|---:|---|---|---|---|---|
| 1 | Estado Git | `git status --short --branch`, `git diff --stat`, verificar `.env` | higiene do repo | setup/anexos | A fazer |
| 2 | Pré-requisitos | `Test-LocalPrerequisites.ps1` | output prereqs | C5 | A fazer |
| 3 | Infra local | `.\infra\scripts\up.ps1` | containers ativos | C5/C4 | A fazer |
| 4 | Bootstrap PostgreSQL | `bootstrap-control-plane.ps1` | área/grid/sensores/cenários | C4/D6 | A fazer |
| 5 | Baseline infra | `Test-LocalBaseline.ps1 -InfrastructureOnly` | 0 falhas | C5/D7 | A fazer |
| 6 | Export inicial PostgreSQL | queries/scripts DB | schema/tabelas/counts iniciais | C4 | Opcional |
| 7 | Export inicial Influx | health/tables/columns | estado inicial Influx | C4 | Opcional |
| 8 | Frontend deps | `cd webUI; npm ci; cd ..` | deps instaladas | C5 | A fazer |
| 9 | Build | `dotnet build`, `npm run build` | logs build | D7 | A fazer |
| 10 | Testes | `dotnet test` | total/pass/fail | D7 | A fazer |
| 11 | Coverage | script coverage | summary coverage | D7 | A fazer |
| 12 | Gate de coerência técnica | verificar escala, DailyCellState, H/F/G, V1/V2, fonte canónica | tabela de coerência | Cap. 6/7/8/D1/C4/D8 | A fazer |
| 13 | Runtime | `start-local-runtime.ps1 -OpenBrowser -ForceRestart` | launcher summary/logs | C5/D6 | A fazer |
| 14 | Login/UI base | login `admin/admin123` | screenshot | D8 | A fazer |
| 15 | Run scenario_b | Run Orchestrator | run result | D6/D8 | A fazer |
| 16 | SQL B | queries B | attempts/assessments | D6 | A fazer |
| 17 | Screenshots B | Overview, Area Risk, Latest Run Audit | prova visual | D8 | A fazer |
| 18 | Run scenario_c | Run Orchestrator | run result C | D6/D8 | A fazer |
| 19 | SQL C | queries C | missing/degraded evidence | D6 | A fazer |
| 20 | Compare B vs C | UI comparison | screenshot + export | D8 | A fazer |
| 21 | Provenance | Model/Data Provenance screenshots | limitações/proveniência | D8/Cap. 9 | A fazer |
| 22 | Export final PostgreSQL | queries/scripts DB após B/C | schema/counts/samples finais | C4/D6/D8 | A fazer |
| 23 | Export final Influx | health/tables/columns após B/C | estado final Influx | C4/D6 | A fazer |
| 24 | Arquivar evidência | guardar outputs com data | evidence final | anexos | A fazer |

**Regra:** os exports iniciais são opcionais. Os exports finais após `scenario_b` e `scenario_c` são obrigatórios se forem citados no relatório.

---

### 11.2. Ordem de escrita do relatório

| Ordem | Tarefa | Capítulos/anexos | Dependências | Output |
|---:|---|---|---|---|
| 1 | Consolidar inventário de fontes | Todos | respostas NotebookLM/Codex | mapa final de fontes |
| 2 | Escrever origem/contexto | Cap. 1, A1, A2, A3, B1 | documentos internos | texto base |
| 3 | Escrever Estado da Arte | Cap. 2 | refs externas | secções teóricas |
| 4 | Escrever requisitos/âmbito | Cap. 3, B2 | fecho de escopo | RF/RNF |
| 5 | Escrever arquitetura | Cap. 4, C1, C2 | docs/código/diagramas | arquitetura textual |
| 6 | Escrever estratégia | Cap. 5, A4, A6 | diário/planos | metodologia/percurso |
| 7 | Escrever implementação | Cap. 6, C3, C4, C5, C6 | código/docs | descrição técnica |
| 8 | Escrever avaliação de risco V1/V2 | Cap. 7, D1–D5 | pesquisa/refs/código | índices |
| 9 | Produzir evidência final | Cap. 8, D6–D8 | plano técnico | SQL/screenshots |
| 10 | Escrever validação | Cap. 8 | evidência final | resultados |
| 11 | Completar V3 | Cap. 7/10, B6/E3 | tarefas V3 | secção ML |
| 12 | Escrever V4 | Cap. 10/E4 | roadmap | futuro |
| 13 | Escrever discussão/limitações | Cap. 9/A6 | validação + diário | discussão crítica |
| 14 | Escrever conclusão | Cap. 11 | tudo anterior | fecho |
| 15 | Escrever resumo/abstract | pré-textuais | relatório quase final | síntese |
| 16 | Escrever IA/acrónimos | pré-textuais | material final | secções finais |
| 17 | Limpar bibliografia | refs | bib atual + novas refs | `.bib` final |
| 18 | Converter anexos para LaTeX | todos os anexos | textos finais | anexos integrados |
| 19 | Revisão LaTeX/editorial | tudo | PDF final | qualidade formal |
| 20 | Revisão final | tudo | professores/equipa | versão final |

---

### 11.3. Sequência mínima absoluta

Se o tempo apertar, executar pelo menos:

1. Git status.
2. `Test-LocalPrerequisites.ps1`.
3. `up.ps1`.
4. `bootstrap-control-plane.ps1`.
5. `Test-LocalBaseline.ps1 -InfrastructureOnly`.
6. `npm ci`.
7. `dotnet build`.
8. `dotnet test`.
9. `npm run build`.
10. gate de coerência técnica.
11. `start-local-runtime.ps1`.
12. login UI.
13. run `scenario_b`.
14. SQL B.
15. screenshots B.
16. run `scenario_c`.
17. SQL C.
18. Compare B vs C.
19. screenshots finais.
20. export final PostgreSQL/Influx.
21. bibliografia limpa.
22. revisão LaTeX.

---

## 12. Riscos de escrita e mitigação

| Risco | Mitigação |
|---|---|
| Estado da Arte demasiado grande | Focar apenas no que justifica decisões técnicas. Detalhe vai para anexos. |
| Duplicação Cap. 3/Cap. 5 | Cap. 3 define objetivo; Cap. 5 explica percurso até ao objetivo. |
| V3 parecer feita antes de estar | Estrutura inclui V3, mas plano marca dependência de tarefas ainda não realizadas. |
| UI parecer fonte canónica | D8 deve indicar fonte canónica em DB/backend. |
| Escala 0–1 vs 0–100 inconsistente | Definir 0–1 como escala canónica e `Score100` como derivado. |
| `DailyCellState` mal descrito | Descrever identidade PostgreSQL por célula/dia/run e distinguir InMemory/test double. |
| H/F/G mal formulado | Mostrar H/F/G como decomposição de `T`, não como soma direta no score final. |
| FWI/KBDI parecerem validação final | Formular como comparação metodológica. |
| Portuguese Proxy parecer oficial | Chamar proxy candidato, não metodologia institucional. |
| Documentos antigos contaminarem relatório | Marcar `architecture.md`/`implementation.md` como parciais e validar contra código. |
| Citações adicionadas tarde | Usar plano de citações desde o início. |
| Anexos parecerem dispersos | Criar A0 e chamar anexos explicitamente no corpo. |
| Falta de evidência visual | Capturar screenshots obrigatórios e D8. |
| LaTeX com problemas formais | Criar gate de revisão editorial/LaTeX. |

---

## 13. Quality gate LaTeX e integridade editorial

Antes da submissão final, verificar:

- páginas em branco indesejadas;
- acrónimos duplicados;
- citações quebradas;
- entradas `TODO_*` na bibliografia;
- figuras sem legenda;
- legendas demasiado genéricas;
- anexos não chamados no corpo;
- chamadas para anexos inexistentes;
- tabelas sem fonte;
- screenshots sem fonte canónica;
- inconsistência entre escala apresentada e escala persistida;
- numeração dos anexos;
- lista de figuras;
- lista de tabelas;
- índice;
- links internos;
- captions orientadas por argumento;
- nomes V1/V2/V3/V4 usados de forma consistente.

---

## 14. Critérios de fecho do documento final

O relatório só deve ser considerado pronto quando:

- capítulos principais estiverem escritos;
- anexos essenciais estiverem convertidos para LaTeX;
- bibliografia estiver sem `TODO_*`;
- V3 estiver escrita conforme estado real;
- scenario_b e scenario_c tiverem evidência final;
- screenshots principais estiverem capturados;
- D8 estiver preenchido;
- DB exports finais estiverem arquivados depois das runs B/C;
- build/test/coverage final estiverem registados;
- escala do NP Score estiver consistente;
- `DailyCellState` estiver descrito de acordo com o runtime final;
- H/F/G estiver formulado sem overclaim;
- claims NP/FWI/KBDI/Portuguese Proxy estiverem formulados com cuidado;
- conclusão e resumo estiverem atualizados com resultados finais.
