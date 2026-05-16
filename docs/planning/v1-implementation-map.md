# NatureProtector V1 — Mapa Consolidado de Plano e Implementação

## 1. Objetivo

Este documento consolida o antigo plano de implementação V1 e o antigo mapa `Proposal -> implementação`.

O objetivo é manter uma documentação de longo prazo que explique:

1. como o conteúdo do `Proposal` foi transformado em frentes técnicas executáveis;
2. que hierarquia de verdade foi usada para evitar confundir intenção documental com implementação real;
3. qual foi a ordem de execução definida;
4. que gaps foram identificados entre proposta, contratos, código, testes e evidência runtime;
5. que decisões, alternativas e limitações foram ponderadas durante a passagem da proposta para a implementação.

Este documento substitui os documentos intermédios:

* `docs/implementation/v1/v1-implementation-plan.md`;
* `docs/implementation/v1/v1-proposal-to-implementation-map.md`.

A documentação granular de tarefas não deve ser tratada como documentação de longo prazo. O histórico técnico relevante deve ficar consolidado aqui, em `docs/contracts/`, em `docs/architecture/`, em `docs/evidence/` e nos testes.

---

## 2. Âmbito

Este mapa cobre a frente V1 do subsistema de prevenção do NatureProtector, incluindo:

* contratos e vocabulário;
* simulador e cenários;
* pipeline de ingestão, validação, normalização e elegibilidade;
* classificadores e flags de qualidade;
* `RiskInput`;
* `DailyCellState`;
* `RiskAssessment`;
* alertas e projeções operacionais;
* API/Backoffice;
* evidência técnica/runtime;
* testes automáticos;
* orquestração reprodutível de runs.

Ficam fora deste documento:

* validação científica externa do modelo de risco;
* calibração final de pesos, thresholds ou índices;
* equivalência oficial com FWI, IPMA/PIR/RCM, EFFIS ou qualquer sistema externo;
* implementação final de FWI/KBDI, salvo como alvo futuro;
* documentação detalhada de prompts, tarefas temporárias ou scaffolding de execução.

---

## 3. Fontes consideradas

| Fonte | Papel | Observação |
|---|---|---|
| `Proposal` | Define intenção, requisitos-alvo e direção metodológica | Não prova implementação por si só |
| Código-fonte | Define o estado técnico real quando observado e testado | Prevalece sobre texto desatualizado |
| Contratos versionados | Definem compatibilidade e fronteiras externas | Não devem ser alterados sem estratégia explícita |
| Testes automáticos | Protegem comportamento esperado e regressões | São evidência técnica, não validação científica |
| Evidência runtime | Demonstra execução observável da baseline | Deve incluir limitações e contexto |
| Documentação técnica | Explica decisões e rastreabilidade | Deve acompanhar o código, não substituí-lo |

---

## 4. Hierarquia de verdade

A V1 foi planeada com uma hierarquia explícita para evitar sobredeclarações.

| Prioridade | Fonte de verdade | Regra operacional |
|---|---|---|
| 1 | Código executável e comportamento observado | Se divergir da documentação, o comportamento observado é o estado técnico atual |
| 2 | Contratos versionados | Definem fronteiras de compatibilidade e integração |
| 3 | Testes automáticos | Confirmam comportamento esperado quando consistentes com código e contratos |
| 4 | Evidência runtime | Demonstra execução em ambiente concreto, com limitações |
| 5 | `Proposal` e notas de planeamento | Definem intenção e direção, não implementação concluída |

Quando havia conflito entre o `Proposal` e o repositório, a regra foi marcar o ponto como gap, e não resolver por suposição.

---

## 5. Princípios de execução

A passagem da proposta para implementação seguiu estes princípios:

1. **Não assumir implementação a partir do texto**: uma secção do `Proposal` só passa a estado implementado quando existe código, teste ou evidência verificável.
2. **Separar intenção, contrato e runtime**: o que se pretende fazer, o que é contrato e o que corre em runtime são níveis diferentes.
3. **Preservar contratos externos**: mudanças em RabbitMQ, envelopes ou payloads externos exigem versão e migração explícitas.
4. **Evitar renames destrutivos**: conceitos internos podem evoluir sem quebrar contratos publicados.
5. **Separar input e output de risco**: `RiskInput` não deve conter `base_risk`, `adjusted_score`, `risk_level`, `alert_state` ou projeções.
6. **Preservar a semântica de bloqueio**: `Blocked` significa ausência de condições para calcular novo score válido, não risco zero.
7. **Separar validação técnica de validação científica**: testes, coverage e runtime provam funcionamento técnico, não calibração científica.
8. **Tratar parâmetros como candidatos**: pesos, thresholds, janelas temporais e normalizações são valores V1 candidatos, não constantes validadas cientificamente.
9. **Evoluir por fatias verificáveis**: cada frente deve ter critério de entrada, saída e evidência mínima.
10. **Documentar limitações**: estados parciais, gaps e adiamentos devem ficar explícitos.

---

## 6. Ordem de execução definida

A ordem de execução foi organizada em workstreams encadeados. A sequência não foi pensada como lista rígida de ficheiros, mas como dependências conceptuais e técnicas.

### WS0 — Baseline e evidência

**Objetivo:** congelar o estado inicial e criar uma base mínima de evidência.

**Razão:** antes de alterar contratos ou código, era necessário saber o que existia realmente no repositório.

**Entradas esperadas:**

* branch e commit;
* estado do repositório;
* comandos de build/test;
* inventário de ficheiros relevantes;
* evidência inicial em `docs/evidence/`.

**Saída esperada:**

* baseline registada;
* divergências iniciais identificadas;
* evidência mínima consultável.

---

### WS1 — Contratos e vocabulário

**Objetivo:** estabilizar nomes, fronteiras e contratos-alvo da V1.

**Razão:** sem vocabulário comum, a implementação poderia misturar conceitos como evento, leitura, observação, input de risco e assessment.

**Decisões principais:**

* `EventEnvelope<TPayload>` continua a ser envelope de transporte;
* `SensorReadingProduced` continua a ser o evento externo vivo do simulador para a pipeline;
* `OperationalEvent` deve ser tratado como camada interna, não substituição automática do contrato RabbitMQ;
* `TruthSnapshot` e `LocalObservation` permanecem conceitos planeados enquanto não forem implementados;
* `NormalizedReading` é a fronteira interna entre normalização e risco;
* `RiskInput` é a fronteira entre pipeline e motor de scoring.

**Saída esperada:**

* mapa de vocabulário;
* catálogo de eventos;
* distinção entre contratos reais, conceitos internos e conceitos planeados.

---

### WS2 — Simulador em camadas

**Objetivo:** preparar o simulador para gerar inputs controlados e reprodutíveis.

**Razão:** a V1 não deve ser descrita como “o simulador gera leituras e o sistema calcula risco” sem separar verdade física, observação, erro e transporte.

**Modelo-alvo ponderado:**

`ScenarioDefinition -> DailyCellState -> TruthSnapshot -> LocalObservation -> OperationalEvent`

**Decisões:**

* o simulador deve evoluir para camadas;
* erro observacional deve ser separado de falha de pipeline;
* cenários A/B/C devem permitir comparar condições limpas e degradadas;
* cenário C deve representar degradação operacional, não necessariamente novo clima.

**Estado prático da frente:**

* nem toda a cadeia foi implementada nesta fase;
* `OperationalEvent` foi introduzido como camada interna;
* `TruthSnapshot` e `LocalObservation` continuaram planeados.

---

### WS3 — Pipeline, classificadores e quality flags

**Objetivo:** fechar a taxonomia mínima de qualidade, classificadores e estados de decisão antes de scoring final.

**Razão:** a pipeline não deve calcular risco antes de saber se a leitura é válida, degradada, parcial ou bloqueada.

**Elementos ponderados:**

* `ClassifierResult`;
* `ClassifierStatus`;
* `ClassifierSeverity`;
* flags de qualidade;
* motivos auditáveis;
* distinção entre falha observacional e falha técnica de pipeline.

**Decisões:**

* classificadores devem produzir resultado auditável;
* flags devem acompanhar a leitura normalizada e a elegibilidade;
* qualidade não deve ser confundida com score final.

**Saída esperada:**

* leitura classificada;
* flags e severidade rastreáveis;
* base para elegibilidade.

---

### WS4 — Elegibilidade e `RiskInput`

**Objetivo:** definir quando uma leitura pode gerar input de risco.

**Razão:** uma leitura persistida para auditoria não é necessariamente elegível para cálculo de risco.

**Estados definidos:**

* `CompleteEligible`;
* `PartialButUsable`;
* `Blocked`.

**Decisões:**

* `RiskInput` só deve nascer depois de validação, classificação, normalização e elegibilidade;
* `Blocked` não deve gerar score numérico novo;
* leituras parciais podem ser utilizáveis se preservarem informação mínima suficiente;
* `RiskInput` não deve conter resultados de scoring ou projeção.

**Saída esperada:**

* elegibilidade explícita;
* `RiskInput` pré-scoring;
* testes para complete, partial e blocked.

---

### WS5 — `DailyCellState`

**Objetivo:** introduzir estado diário por célula.

**Razão:** índices e variáveis com memória temporal não devem ser calculados apenas a partir de uma leitura instantânea.

**Função do `DailyCellState`:**

* guardar contexto diário;
* suportar precipitação diária;
* preservar estado antecedente;
* preparar base para FWI/KBDI futuros;
* manter proveniência e atualização temporal.

**Decisão:**

`DailyCellState` é suporte de contexto e memória, não score final.

---

### WS6 — `RiskAssessment` V1

**Objetivo:** alinhar o resultado de risco com a V1, preservando compatibilidade com campos legados.

**Problema identificado:**

A implementação existente usava essencialmente `RiskScore/RiskLevel`, enquanto o alvo V1 distinguia:

* `InputStatus`;
* `BaseRisk`;
* `AdjustedScore`;
* `RiskLevel`;
* compatibilidade com `RiskScore`.

**Decisões:**

* `BaseRisk` representa o risco antes de fatores de confiança/integridade;
* `AdjustedScore` representa o score operacional ajustado;
* `RiskScore` pode ser mantido como compatibilidade, espelhando `AdjustedScore`;
* `RiskAssessment` é resultado, não input;
* `Blocked` não deve ser convertido em assessment numérico com risco zero.

**Saída esperada:**

* assessment com semântica V1;
* compatibilidade controlada;
* testes de mapeamento e limites.

---

### WS7 — Agregação, `AlertState` e projeções operacionais

**Objetivo:** fechar a camada operacional após o cálculo de risco.

**Razão:** alertas e projeções devem consumir resultado já calculado, não recalcular risco.

**Decisões:**

* política interna com `None`, `Warning` e `Alarm`;
* thresholds candidatos:
  * `Warning` abre a partir de `0.60`;
  * `Warning` fecha abaixo de `0.50`;
  * `Alarm` abre a partir de `0.80`;
  * `Alarm` desce/fecha com histerese abaixo de `0.70`;
* cooldown e persistência mínima são práticas relevantes, mas nem todas precisam estar fechadas na mesma fatia;
* Backoffice/API deve expor estado persistido/projetado, sem recalcular scoring.

**Saída esperada:**

* projeções coerentes;
* estado de alerta testável;
* API expõe resultado operacional.

---

### WS8 — API, dashboards e evidência de demonstração

**Objetivo:** tornar a execução observável e demonstrável.

**Razão:** a V1 precisa ser auditável em runtime, não apenas testada em unidade.

**Elementos definidos:**

* queries de control plane;
* queries de pipeline;
* queries de projeções;
* script de recolha de evidência;
* validação da API;
* registo de limitações.

**Decisões:**

* evidência runtime é validação técnica, não validação científica;
* erros históricos devem ser separados de erros recentes;
* falhas de query por nomes errados não devem ser confundidas com bugs do sistema;
* Grafana/dashboard pode ser adiado se API e DB já provarem a baseline técnica.

---

### WS9 — Testes end-to-end e cenários

**Objetivo:** preparar validação técnica de cenários reprodutíveis.

**Razão:** a execução manual dispersa dificulta comparar runs, recolher evidência e repetir cenários.

**Decisão posterior associada:** criar um orquestrador de runs.

**Saída esperada:**

* execução por cenário;
* parâmetros explícitos;
* evidência por run;
* ligação futura ao Backoffice/site.

---

### O1/O1.2 — Orquestração reprodutível de runs

**Objetivo:** permitir correr cenários de forma controlada sem alterar manualmente CSV, scripts ou bootstrap.

**Componentes definidos:**

* `run-spec.json`;
* `scripts/scenarios/run-scenario.ps1`;
* exemplos de specs;
* `Simulator:RunOverrides:*`;
* `orchestratorCorrelationId`;
* pasta de evidência por run.

**Parâmetros controláveis:**

* `areaCode`;
* `scenarioCode`;
* `sensorCount`;
* `numberOfCycles`;
* `intervalSeconds`;
* `seed`;
* `degradationProfile`;
* `collectEvidence`;
* `waitForCompletion`;
* `timeoutSeconds`;
* `allowParallelRun`;
* `runLabel`.

**Decisões:**

* `run-spec` tem precedência sobre parâmetros do cenário e `appsettings`;
* seleção de sensores deve ser determinística com base em seed;
* `SimulationRun.MetadataJson` deve guardar valores pedidos e resolvidos;
* o estado da run deve ser lido de `control.simulation_runs`;
* a run deve produzir `summary.md`, `run-spec.resolved.json`, logs e evidência runtime;
* esta camada prepara futura integração no Backoffice/site.

---

## 7. Mapa consolidado `Proposal -> implementação`

A matriz abaixo consolida os principais itens que vieram do `Proposal` e foram convertidos em gaps, decisões ou ações técnicas.

| ID | Tema | Alvo V1 | Estado/observação no plano | Ação definida | Validação esperada |
|---|---|---|---|---|---|
| MAP-001 | Estado físico canónico | `TruthSnapshot` | Ausente como implementação confirmada | Manter como conceito-alvo do simulador em camadas | Teste futuro de contrato/serialização |
| MAP-002 | Observação local | `LocalObservation` | Ausente como implementação confirmada | Manter como camada planeada entre verdade física e evento | Teste futuro de mapeamento observacional |
| MAP-003 | Evento operacional | `OperationalEvent` | Parcial/introduzido como camada interna | Usar sem alterar contrato RabbitMQ externo | Teste de construção/mapeamento interno |
| MAP-004 | Leitura normalizada | `NormalizedReading` com flags/classificação | Existente, precisava de enriquecimento | Acrescentar qualidade/classificadores de forma aditiva | Testes de normalização e flags |
| MAP-005 | Input de risco | `RiskInput` completo e pré-scoring | Existente em versão mínima | Expandir sem incluir campos de resultado | Testes de validação e elegibilidade |
| MAP-006 | Avaliação de risco | `RiskAssessment` V1 | Divergência inicial com `RiskScore/RiskLevel` | Introduzir `BaseRisk`/`AdjustedScore` com compatibilidade | Testes de score, compatibilidade e limites |
| MAP-007 | Estado diário | `DailyCellState` | Ausente inicialmente | Criar estado diário por célula | Testes de invariantes e agregação |
| MAP-008 | Classificadores | `ClassifierResult` canónico | Planeado e depois consolidado | Integrar com elegibilidade e leitura | Testes de status, severidade e agregação |
| MAP-009 | Qualidade | `QualityFlags` canónico | Parcial, inicialmente como listas de strings | Evoluir para qualidade rastreável | Testes de composição/deduplicação |
| MAP-010 | Alertas | `AlertState` com transições | Existia alerta simples | Evoluir para `None/Warning/Alarm` com histerese | Testes de thresholds e transições |
| MAP-011 | Elegibilidade | `CompleteEligible/PartialButUsable/Blocked` | Planeado e consolidado | Fechar regra de blocked sem score zero | Testes de decisão de elegibilidade |
| MAP-012 | Projeção | `OperationalProjection` | Parcial | Completar campos operacionais mínimos | Testes de projeção/API |
| MAP-013 | Índices finais | FWI/KBDI | Ausentes como cálculo final | Adiar para fase posterior após estado diário e validação | Testes futuros quando implementados |
| MAP-014 | Evidência runtime | Relatório técnico reprodutível | Necessário para C7 | Criar script e queries reutilizáveis | Relatório em `docs/evidence/` |
| MAP-015 | Orquestração | Execução reprodutível por spec | Necessária para cenários controlados | Criar `run-spec` e orquestrador local | Run curta com evidência e `Completed` |
| MAP-016 | API/Backoffice | Exposição de estado operacional | Parcial | Expor projeções e `alertState` sem recalcular risco | Testes API e runtime |
| MAP-017 | Testes | Coverage e regressão | Necessário para estabilizar V1 | Expandir suite de testes úteis | Relatório coverage consolidado |

---

## 8. Sequência de dependências

A ordem definida não foi arbitrária. Cada frente desbloqueava a seguinte.

```text
Baseline/evidência
  -> contratos e vocabulário
  -> simulador em camadas
  -> classificadores e quality flags
  -> elegibilidade
  -> RiskInput
  -> DailyCellState
  -> RiskAssessment
  -> AlertState/projeções
  -> API/evidência runtime
  -> E2E/cenários
  -> orquestração reprodutível
```

A lógica foi:

1. não calcular score antes de existir input elegível;
2. não criar alertas finais antes de existir assessment coerente;
3. não expor API como fonte de verdade antes de ter projeções persistidas;
4. não declarar evidência runtime antes de ter queries e scripts reprodutíveis;
5. não automatizar cenários antes de ter parâmetros e run metadata rastreáveis.

---

## 9. Decisões ponderadas e alternativas

### 9.1 `OperationalEvent` interno vs contrato externo novo

**Alternativa considerada:** substituir diretamente o contrato RabbitMQ por um evento operacional canónico.

**Decisão:** não alterar o contrato externo nesta fase.

**Razão:** `EventEnvelope<SensorReadingProducedPayload>` e `SensorReadingProduced` já eram fronteira viva. Alterá-los implicaria risco de compatibilidade. O `OperationalEvent` foi tratado como adaptação interna.

---

### 9.2 `TruthSnapshot` e `LocalObservation` imediatos vs planeados

**Alternativa considerada:** implementar já toda a cadeia física/observacional do simulador.

**Decisão:** manter `TruthSnapshot` e `LocalObservation` como conceitos metodológicos planeados, não pré-requisito imediato.

**Razão:** a V1 precisava primeiro de estabilizar pipeline, elegibilidade, scoring e evidência técnica. A cadeia completa do simulador pode evoluir depois sem quebrar contratos.

---

### 9.3 `Blocked` como score zero vs ausência de novo assessment

**Alternativa considerada:** representar `Blocked` como score `0`.

**Decisão:** rejeitada.

**Razão:** score zero significa risco baixo; `Blocked` significa ausência de condições para calcular risco válido. Misturar ambos destruiria a semântica operacional.

---

### 9.4 `RiskInput` com campos de resultado vs fronteira pré-scoring

**Alternativa considerada:** incluir `base_risk`, `adjusted_score`, `risk_level` ou `alert_state` no input.

**Decisão:** rejeitada.

**Razão:** input e resultado precisam de fronteira clara. O motor de risco deve consumir `RiskInput` e produzir `RiskAssessment`.

---

### 9.5 Alertas a recalcular na API vs consumir projeção

**Alternativa considerada:** recalcular alertas ou risco no Backoffice/API.

**Decisão:** rejeitada.

**Razão:** a API deve expor estado operacional persistido/projetado. Recalcular no Backoffice criaria divergência entre pipeline e leitura externa.

---

### 9.6 Coverage máximo vs coverage saudável

**Alternativa considerada:** perseguir `100%` de coverage.

**Decisão:** rejeitada como meta rígida.

**Razão:** alguns ramos restantes pertencem a observabilidade, `ActivitySource`, integração RabbitMQ/Influx real ou wrappers técnicos. O objetivo é cobrir comportamento útil, não criar testes frágeis para melhorar métricas.

---

### 9.7 Orquestração manual vs `run-spec`

**Alternativa considerada:** continuar a alterar scripts, CSV ou parâmetros manualmente para cada cenário.

**Decisão:** criar orquestração por `run-spec.json`.

**Razão:** runs precisam ser reprodutíveis, auditáveis e preparadas para futura integração no site/API.

---

## 10. Critérios de entrada e saída

### Definition of Ready

| ID | Critério | Evidência mínima |
|---|---|---|
| DOR-01 | Requisito ligado ao `Proposal` ou decisão V1 | Referência no mapa ou documentação técnica |
| DOR-02 | Gap confirmado no repo | Evidência objetiva: ficheiro, teste, output ou ausência verificada |
| DOR-03 | Critério de aceitação definido | Teste, query, checklist ou evidência esperada |
| DOR-04 | Dependências identificadas | Ordem de workstreams ou bloqueios explícitos |
| DOR-05 | Risco de contrato avaliado | Confirmação se altera ou não RabbitMQ/API/schema |

### Definition of Done

| ID | Critério | Evidência requerida |
|---|---|---|
| DOD-01 | Implementação concluída conforme objetivo | Diff e descrição técnica |
| DOD-02 | Testes aplicáveis executados | Output de `dotnet test` |
| DOD-03 | Coverage atualizado quando aplicável | `coveragereport_core/Summary.txt` |
| DOD-04 | Contratos externos preservados ou versionados | Verificação de compatibilidade |
| DOD-05 | Evidência runtime arquivada quando aplicável | Ficheiros em `docs/evidence/` |
| DOD-06 | Limitações documentadas | Secção explícita de limitações |
| DOD-07 | Documentação sincronizada | `docs/contracts`, `docs/architecture`, `docs/planning`, `tests/README.md` ou diário atualizados |

---

## 11. Evidência esperada por tipo de frente

| Frente | Evidência mínima |
|---|---|
| Contratos/vocabulário | Markdown em `docs/contracts/`, testes de compatibilidade se houver código |
| Pipeline/elegibilidade | Testes unitários e de integração de pipeline |
| RiskInput/RiskAssessment | Testes de domínio, limites e compatibilidade |
| Alertas/projeções | Testes de policy, stores e API |
| Runtime C7 | Script de recolha, queries, relatório runtime |
| Orquestração | `run-spec.resolved.json`, `summary.md`, logs e relatório por run |
| Coverage | `dotnet test`, script de coverage e `Summary.txt` |
| Documentação | Atualização de mapas, arquitetura e diário |

---

## 12. Estado consolidado da frente V1

No fim desta sequência, a frente V1 passou de intenção documental para uma base técnica mais auditável.

Ficaram consolidados:

* vocabulário e fronteiras principais;
* distinção entre contratos reais e conceitos planeados;
* `OperationalEvent` como camada interna;
* classificação e elegibilidade mais explícitas;
* `RiskInput` como fronteira pré-scoring;
* `DailyCellState` como estado diário por célula;
* `RiskAssessment` com `BaseRisk`, `AdjustedScore` e compatibilidade `RiskScore`;
* regra `Blocked != risco zero`;
* política interna de alertas V1 com histerese;
* projeções e `alertState` expostos pela API;
* recolha de evidência runtime;
* orquestração de cenários por `run-spec`;
* reforço amplo da suite de testes e coverage.

Continuaram como limites ou trabalho futuro:

* implementação completa de `TruthSnapshot` e `LocalObservation`;
* cálculo final FWI/KBDI;
* calibração científica externa;
* validação multiárea;
* dashboards finais;
* orquestração pelo Backoffice/site;
* integração real controlada com RabbitMQ/Influx/PostgreSQL em testes E2E dedicados;
* política operacional completa de cooldown/persistência, se for assumida como requisito final.

---

## 13. Localização recomendada dos documentos

A organização de longo prazo recomendada é:

```text
docs/
  architecture/
    v1-scenario-run-orchestrator.md

  contracts/
    README.md
    event-catalog.md
    v1-vocabulary-map.md

  evidence/
    ...

  planning/
    v1-implementation-map.md
```

O diretório `docs/implementation/v1/tasks/` foi útil como apoio temporário de execução, mas não deve ser mantido como documentação principal de longo prazo. O conteúdo relevante deve estar consolidado neste mapa, nos contratos, na arquitetura e na evidência.

---

## 14. Próximos passos

1. Manter este documento como mapa consolidado de planeamento e execução V1.
2. Atualizar links antigos que apontem para `docs/implementation/v1/`.
3. Remover documentação granular temporária que já esteja consolidada.
4. Garantir que `docs/contracts/` reflete contratos reais e conceitos planeados.
5. Manter `docs/architecture/v1-scenario-run-orchestrator.md` como documento operacional do orquestrador.
6. Guardar evidência runtime e coverage em `docs/evidence/`.
7. Atualizar o diário e `tests/README.md` quando forem feitas novas vagas relevantes.
8. Tratar FWI/KBDI, dashboards finais e orquestração via site/API como frentes futuras separadas.
