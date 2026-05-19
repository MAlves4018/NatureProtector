# Nature Protector UI Design Spec v0.1

## Objetivo do documento

Este documento consolida a visão provisória da nova organização da UI do **Nature Protector**, antes de qualquer implementação.

O objetivo é transformar a aplicação numa interface que não mostre apenas resultados, mas que também represente claramente:

- o que o sistema faz;
- como os dados entram;
- como os eventos atravessam o backend;
- onde os dados ficam persistidos;
- como se calculam risco e alertas;
- como se compara uma run normal com uma run degradada;
- como a UI se relaciona com os diagramas, o relatório e a demo.

A UI deve funcionar como uma tradução operacional dos diagramas e da arquitetura, permitindo que um avaliador consiga fazer o paralelismo entre:

```text
Relatório -> Diagramas -> Backend -> UI -> Demo -> Evidência
```

---

## Princípio orientador

A UI deve espelhar os fluxos, gráficos e estados internos do sistema, mas em camadas de detalhe.

Não se pretende despejar todo o backend num único ecrã. O objetivo é organizar a informação em páginas e tabs que permitam uma leitura progressiva:

| Camada | Pergunta | Tipo de vista |
|---|---|---|
| 1. Contexto | O que estou a ver? | Área, run, cenário, modo, janela temporal |
| 2. Operação | Qual é o estado atual? | Risco, alertas, mapa, dashboards |
| 3. Fluxo | Como é que o sistema chegou aqui? | Pipeline, runtime chain, retries, failures |
| 4. Evidência | Como provo o que aconteceu? | Auditoria, comparação, timings, diagnostics |
| 5. Estrutura interna | Como isto está montado por dentro? | Modelo, proveniência, persistência, deployment, code mapping |

A navegação deve seguir a lógica:

```text
Área / Cenário -> Simulação -> Pipeline -> Risco / Alertas -> Evidência
```

---

## Decisões de linguagem

A UI atual já usa termos em inglês como `Runtime Monitor`, `Developer Runtime Control`, `Latest Run Audit`, `Diagnostics` e `Run Orchestrator`.

Por consistência, a navegação principal deve manter nomes em inglês:

- `Monitoring`
- `Scenario Lab`
- `Flow Explorer`
- `Evidence & Comparison`
- `Model & Provenance`

Evitar misturar português e inglês na navegação principal.

---

# 1. Mapa final de navegação

```text
Home
└── Area Selection

Workspace
├── Monitoring
│   ├── Overview
│   ├── Map & Cells
│   ├── Sensor Dashboards
│   ├── Area Risk
│   └── Alerts
│
├── Scenario Lab
│   ├── Run Orchestrator
│   ├── Scenario Definition
│   ├── Latest Run
│   └── Runtime State Control
│
├── Flow Explorer
│   ├── Runtime Chain
│   ├── Processing Pipeline
│   ├── Retry & Quarantine
│   ├── Persistence Views
│   ├── Deployment & Services
│   └── Nominal Flow
│
├── Evidence & Comparison
│   ├── Latest Run Audit
│   ├── Compare B vs C
│   ├── Run Timings
│   ├── Diagnostics
│   └── Export Evidence
│
└── Model & Provenance
    ├── Domain Model
    ├── Data Chain
    ├── Data Provenance
    ├── Territorial & Weather Context
    └── Code Mapping
```

---

# 2. Layout global

## 2.1 Top bar fixa

Em todas as páginas do Workspace deve existir uma top bar fixa com contexto global:

```text
Nature Protector | Area | Latest Run | Scenario | Status | Time Window | Theme | Refresh
```

Exemplo:

```text
Area: proenca-a-nova | Run: scenario_b | Status: Completed | Window: 30m
```

## 2.2 Navegação principal

Tabs horizontais principais:

```text
Monitoring | Scenario Lab | Flow Explorer | Evidence & Comparison | Model & Provenance
```

## 2.3 Elemento transversal recomendado

Em páginas técnicas, mostrar uma mini cadeia de fluxo:

```text
Scenario -> Inbox -> Processing -> Risk -> State -> Alerts -> UI
```

Isto ajuda o utilizador a perceber em que parte do sistema está.

## 2.4 Modo de visualização

Idealmente, no futuro, a UI pode ter dois modos:

| Modo | Objetivo |
|---|---|
| Demo | Menos ruído, foco em história, resultados e evidência |
| Developer | Mostra raw JSON, diagnostics, queries, limitações e detalhes técnicos |

Para o MVP, não é obrigatório implementar dois modos. Pode-se começar com tabs limpas e raw JSON colapsado.

---

# 3. Home

## Objetivo

A Home deve ser a entrada pública da aplicação. Não deve ser apenas um seletor de área. Deve explicar rapidamente:

- o que é o projeto;
- para que serve;
- quem participou;
- como entrar no painel de monitorização.

## Conteúdo proposto

### Hero section

```text
Nature Protector

Sistema de monitorização preventiva e simulação operacional para risco de incêndio florestal.

O Nature Protector permite simular cenários ambientais, processar leituras de sensores, calcular risco operacional, emitir alertas e analisar evidência runtime de forma rastreável.
```

### Cartões de valor

| Cartão | Texto |
|---|---|
| Simulação controlada | Execução de cenários com sensores, ciclos, seed e degradação configurável |
| Pipeline rastreável | Eventos, inbox, retries, quarantine, risco, projeções e alertas visíveis |
| Evidência para validação | Comparação de runs, auditoria e suporte ao relatório/demo |

### Participantes

```text
Projeto e Seminário, Licenciatura em Engenharia Informática e de Computadores

Autores:
- Miguel Alves
- Gabriel Mano

Orientadores:
- Nuno Leite
- Artur Ferreira
```

### Area selection

- Dropdown de área;
- botão `Enter Monitoring Panel`;
- toggle light/dark.

## Critério de layout

A Home deve caber num ecrã, ou ter scroll mínimo.

---

# 4. Matriz página / tab / widget

Esta matriz garante que a reorganização da UI não remove funcionalidades existentes.

## 4.1 Home

| Página | Tab / Secção | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Home | Hero | Logo, nome do projeto, subtítulo | Apresentar rapidamente o Nature Protector | Novo |
| Home | Project Summary | Texto curto sobre o objetivo do projeto | Explicar o que o sistema faz antes de entrar na app | Novo |
| Home | Value Cards | Simulação controlada, pipeline rastreável, evidência runtime | Mostrar valor do sistema em 3 blocos simples | Novo |
| Home | Participants | Autores, orientadores, curso, instituição | Dar contexto académico e equipa | Novo |
| Home | Area Selection | Dropdown da área | Selecionar área de monitorização | Existente |
| Home | Area Selection | Botão `Enter Monitoring Panel` | Entrar no workspace | Existente |
| Home | Global UI | Light/dark mode | Alternar tema | Existente |

## 4.2 Workspace, elementos globais

| Página | Tab / Secção | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Todas | Top Bar | Nome do projeto | Identidade da aplicação | Existente |
| Todas | Top Bar | Área selecionada | Contexto global da área | Existente/parcial |
| Todas | Top Bar | Latest run summary | Mostrar run ativa/recente | Existente |
| Todas | Top Bar | Scenario code | Mostrar cenário atual | Existente |
| Todas | Top Bar | Status | Completed/running/error | Existente |
| Todas | Top Bar | Time window | 10m / 30m / 24h | Existente |
| Todas | Top Bar | Auto refresh / Refresh | Atualização manual/automática | Existente |
| Todas | Top Bar | Light/dark mode | Tema | Existente |
| Todas | Main Navigation | Monitoring | Área operacional | Novo como organização |
| Todas | Main Navigation | Scenario Lab | Execução e controlo de cenários | Novo como organização |
| Todas | Main Navigation | Flow Explorer | Pipeline/backend por baixo dos panos | Novo como organização |
| Todas | Main Navigation | Evidence & Comparison | Auditoria, comparação e evidência | Novo como organização |
| Todas | Main Navigation | Model & Provenance | Modelo, dados, proveniência e código | Novo como organização |

---

# 5. Monitoring

## Objetivo

Mostrar o estado operacional da área selecionada: mapa, sensores, risco e alertas.

## 5.1 Overview

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Monitoring | Overview | Current Area Risk card | Mostrar score/nível operacional da área | Existente |
| Monitoring | Overview | Active Alert card | Mostrar alerta ativo, severidade e estado | Existente |
| Monitoring | Overview | Freshness card | Mostrar fresh/stale/expired | Existente |
| Monitoring | Overview | Latest Run card | Mostrar run mais recente | Existente |
| Monitoring | Overview | Sensors card | Mostrar sensores selecionados/ativos | Existente/parcial |
| Monitoring | Overview | Last Update | Mostrar timestamp de refresh | Existente |
| Monitoring | Overview | Mini map preview | Dar contexto geográfico rápido | Existente, a reorganizar |
| Monitoring | Overview | Mini runtime chain | Ligar área ao estado runtime | Existente no Runtime Monitor |
| Monitoring | Overview | Alert summary | Mostrar alerta principal sem entrar em detalhe | Existente |
| Monitoring | Overview | Risk summary | Mostrar risco corrente e interpretação curta | Existente |

## 5.2 Map & Cells

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Monitoring | Map & Cells | Leaflet/map view | Mostrar área selecionada | Existente |
| Monitoring | Map & Cells | Area boundary | Delimitar área | Existente |
| Monitoring | Map & Cells | Grid/cells overlay | Mostrar células operacionais | Existente/parcial |
| Monitoring | Map & Cells | Sensor markers | Mostrar sensores na área | Existente/parcial |
| Monitoring | Map & Cells | Cell tooltip | Mostrar detalhe da célula | Novo/P1 |
| Monitoring | Map & Cells | Sensor tooltip | Mostrar sensor, métrica, estado, última leitura | Novo/P1 |
| Monitoring | Map & Cells | Cell risk overlay | Mostrar risco por célula, se disponível | P1/P2 |

## 5.3 Sensor Dashboards

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Monitoring | Sensor Dashboards | Temperature tab | Mostrar dashboard/gráfico de temperatura | Existente via Grafana |
| Monitoring | Sensor Dashboards | Humidity tab | Mostrar dashboard/gráfico de humidade | Existente via Grafana |
| Monitoring | Sensor Dashboards | Wind tab | Mostrar dashboard/gráfico de vento | Existente via Grafana |
| Monitoring | Sensor Dashboards | Sensor filter | Filtrar por sensor | Existente nos Grafana embeds |
| Monitoring | Sensor Dashboards | Time range control | Controlar janela temporal | Existente |
| Monitoring | Sensor Dashboards | Source note | Explicar que dados vêm da simulação/observabilidade | Novo |
| Monitoring | Sensor Dashboards | Grafana iframe/card | Reaproveitar dashboards atuais | Existente |

## 5.4 Area Risk

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Monitoring | Area Risk | Area risk chart | Mostrar evolução do risco da área | Existente |
| Monitoring | Area Risk | Current risk card | Mostrar score atual | Existente |
| Monitoring | Area Risk | Risk level card | Mostrar nível, por exemplo High | Existente |
| Monitoring | Area Risk | Snapshot table | Listar últimos snapshots | Existente via dados, talvez não UI |
| Monitoring | Area Risk | Assessment count | Mostrar número de assessments usados | Existente |
| Monitoring | Area Risk | Aggregation note | Explicar agregação e limitações | Novo |
| Monitoring | Area Risk | Recent vs persisted distinction | Distinguir risco recente de estado persistido | Novo, importante |

## 5.5 Alerts

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Monitoring | Alerts | Active alerts list | Mostrar alertas ativos | Existente |
| Monitoring | Alerts | Alert severity | Mostrar Warning/Alarm/High | Existente |
| Monitoring | Alerts | Alert status | Active/resolved | Existente |
| Monitoring | Alerts | Alert message | Mostrar explicação operacional | Existente/parcial |
| Monitoring | Alerts | TriggeredAt/ResolvedAt | Mostrar timestamps | Existente |
| Monitoring | Alerts | Recent alert transitions | Histórico de transições | Existente via diagnostics |
| Monitoring | Alerts | Link to run/snapshot | Ligar alerta à run/snapshot que o originou | P1 |

---

# 6. Scenario Lab

## Objetivo

Executar runs controladas e explicar cenários A/B/C.

## 6.1 Run Orchestrator

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Scenario Lab | Run Orchestrator | Scenario code select | Escolher scenario_a/b/c | Existente |
| Scenario Lab | Run Orchestrator | Sensor count input | Definir número de sensores | Existente |
| Scenario Lab | Run Orchestrator | Number of cycles input | Definir ciclos | Existente |
| Scenario Lab | Run Orchestrator | Interval seconds input | Definir intervalo | Existente |
| Scenario Lab | Run Orchestrator | Seed input | Definir seed | Existente |
| Scenario Lab | Run Orchestrator | Degradation profile select | Escolher none/missing-readings | Existente |
| Scenario Lab | Run Orchestrator | Timeout seconds input | Definir timeout | Existente |
| Scenario Lab | Run Orchestrator | Run label input | Identificar run | Existente |
| Scenario Lab | Run Orchestrator | collectEvidence checkbox | Gerar evidência | Existente |
| Scenario Lab | Run Orchestrator | waitForCompletion checkbox | Esperar fim da run | Existente |
| Scenario Lab | Run Orchestrator | allowParallelRun checkbox | Permitir/parar runs paralelas | Existente |
| Scenario Lab | Run Orchestrator | Start Run button | Lançar run | Existente |
| Scenario Lab | Run Orchestrator | Run request result | Mostrar resposta da run | Existente |
| Scenario Lab | Run Orchestrator | Selected sensors list | Mostrar sensores usados | Existente |
| Scenario Lab | Run Orchestrator | Evidence directory | Mostrar pasta de evidência | Existente |
| Scenario Lab | Run Orchestrator | Open Runtime Monitor button | Saltar para monitorização | Existente |

## 6.2 Scenario Definition

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Scenario Lab | Scenario Definition | Scenario A card | Explicar baseline/normal | Novo |
| Scenario Lab | Scenario Definition | Scenario B card | Explicar high risk sem degradação | Novo |
| Scenario Lab | Scenario Definition | Scenario C card | Explicar degraded/missing-readings | Novo |
| Scenario Lab | Scenario Definition | Scenario purpose | Mostrar objetivo do cenário | Novo |
| Scenario Lab | Scenario Definition | Degradation profile | Mostrar perfil esperado | Existente nos dados, novo na UI |
| Scenario Lab | Scenario Definition | Expected behaviour | Preparar demo e leitura de resultados | Novo |
| Scenario Lab | Scenario Definition | Default parameters | Mostrar sensors/cycles/interval/seed | Existente nos dados |
| Scenario Lab | Scenario Definition | Selected scenario details | Mostrar cenário selecionado no formulário | P1 |

## 6.3 Latest Run

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Scenario Lab | Latest Run | SimulationRunId card | Mostrar ID da run | Existente |
| Scenario Lab | Latest Run | ScenarioCode card | Mostrar cenário | Existente |
| Scenario Lab | Latest Run | Status card | Completed/running/error | Existente |
| Scenario Lab | Latest Run | StartedAt/EndedAt | Mostrar duração temporal | Existente |
| Scenario Lab | Latest Run | Duration card | Mostrar duração | Existente |
| Scenario Lab | Latest Run | Cycles card | Mostrar ciclos | Existente |
| Scenario Lab | Latest Run | Interval card | Mostrar intervalo | Existente |
| Scenario Lab | Latest Run | Seed card | Mostrar seed | Existente |
| Scenario Lab | Latest Run | Correlation card | Mostrar correlationId | Existente |
| Scenario Lab | Latest Run | Metadata status | Valid/invalid metadata | Existente |
| Scenario Lab | Latest Run | Requested overrides | Mostrar inputs pedidos | Existente |
| Scenario Lab | Latest Run | Resolved overrides | Mostrar valores finais | Existente |
| Scenario Lab | Latest Run | Selected sensors | Mostrar sensores usados | Existente |
| Scenario Lab | Latest Run | Raw metadata JSON | Mostrar JSON colapsado | Existente |

## 6.4 Runtime State Control

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Scenario Lab | Runtime State Control | Dry run reset button | Simular reset sem aplicar | Existente |
| Scenario Lab | Runtime State Control | Confirmation input | Evitar reset acidental | Existente |
| Scenario Lab | Runtime State Control | Reset Runtime State button | Limpar estado runtime | Existente |
| Scenario Lab | Runtime State Control | Reset result table | Mostrar before/after | Existente |
| Scenario Lab | Runtime State Control | Raw JSON | Mostrar resposta detalhada | Existente |
| Scenario Lab | Runtime State Control | Danger zone styling | Sinalizar risco da operação | Existente/parcial |

---

# 7. Flow Explorer

## Objetivo

Mostrar o backend por baixo dos panos: fluxo, pipeline, retry/quarantine, persistência e deployment.

## 7.1 Runtime Chain

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Runtime Chain | Flow strip | Mostrar cadeia Run -> Inbox -> Attempts -> Risk -> Cell State -> Alerts -> API | Existente no Runtime Monitor |
| Flow Explorer | Runtime Chain | Run block | Latest run, status | Existente |
| Flow Explorer | Runtime Chain | Inbox block | Processed/retry/quarantine count | Existente |
| Flow Explorer | Runtime Chain | Attempts block | Attempts count/outcome | Existente |
| Flow Explorer | Runtime Chain | Risk block | Assessments count | Existente |
| Flow Explorer | Runtime Chain | Cell State block | Cell states/projections updated | Existente |
| Flow Explorer | Runtime Chain | Alerts block | Active alert count/status | Existente |
| Flow Explorer | Runtime Chain | API block | Summary loaded/ok | Existente |
| Flow Explorer | Runtime Chain | Click to details | Clicar no bloco e abrir detalhe | P1 |
| Flow Explorer | Runtime Chain | Diagram alignment note | Explicar que espelha a cadeia runtime | Novo |

## 7.2 Processing Pipeline

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Processing Pipeline | Stage cards | Mostrar estágios internos | Novo/P1 |
| Flow Explorer | Processing Pipeline | Ingestion stage | Inbox/ingest | Novo/P1 |
| Flow Explorer | Processing Pipeline | Validation stage | Validação técnica/semântica | Novo/P1 |
| Flow Explorer | Processing Pipeline | Normalization stage | NormalizedReading | Novo/P1 |
| Flow Explorer | Processing Pipeline | Eligibility stage | Complete/partial/blocked | Novo/P1 |
| Flow Explorer | Processing Pipeline | Risk scoring stage | RiskInput/RiskAssessment | Novo/P1 |
| Flow Explorer | Processing Pipeline | Projection stage | Cell/area state | Novo/P1 |
| Flow Explorer | Processing Pipeline | Alert policy stage | Warning/Alarm | Novo/P1 |
| Flow Explorer | Processing Pipeline | Latest error per stage | Mostrar erro mais recente | P1 |
| Flow Explorer | Processing Pipeline | Stage count/duration | Mostrar count e timings quando existirem | P1/P2 |

## 7.3 Retry & Quarantine

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Retry & Quarantine | Attempts by Outcome chart | Mostrar succeeded/failed/retry/quarantined | Existente |
| Flow Explorer | Retry & Quarantine | Failed attempts table | Mostrar falhas por erro | Existente |
| Flow Explorer | Retry & Quarantine | Rejected by Code chart | Mostrar rejeições | Existente |
| Flow Explorer | Retry & Quarantine | Quarantined by Code chart | Mostrar quarantines | Existente |
| Flow Explorer | Retry & Quarantine | Latest Rejected list | Últimas rejeições | Existente |
| Flow Explorer | Retry & Quarantine | Latest Quarantined list | Últimas quarantines | Existente |
| Flow Explorer | Retry & Quarantine | Event failure timeline | Sequência por evento | P1 |
| Flow Explorer | Retry & Quarantine | Retry policy summary | Explicar max attempts/lease/retry | Novo/P1 |
| Flow Explorer | Retry & Quarantine | Raw event drawer | Ver payload/envelope | P1 |

## 7.4 Persistence Views

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Persistence Views | Runtime table counts | Counts por tabela | Existente em Diagnostics |
| Flow Explorer | Persistence Views | simulation_runs card | Runs guardadas | Existente |
| Flow Explorer | Persistence Views | event_inbox card | Eventos recebidos | Existente |
| Flow Explorer | Persistence Views | processing_attempts card | Attempts/retries | Existente |
| Flow Explorer | Persistence Views | rejected_events card | Rejeições | Existente |
| Flow Explorer | Persistence Views | quarantined_events card | Quarantines | Existente |
| Flow Explorer | Persistence Views | risk_assessment_log card | Assessments | Existente |
| Flow Explorer | Persistence Views | area_risk_snapshot_log card | Snapshots | Existente |
| Flow Explorer | Persistence Views | cell_operational_state card | Estado por célula | Existente |
| Flow Explorer | Persistence Views | area_operational_state card | Estado por área | Existente |
| Flow Explorer | Persistence Views | alert_state card | Alertas | Existente |
| Flow Explorer | Persistence Views | Latest row preview | Último registo por tabela | P1 |
| Flow Explorer | Persistence Views | Raw JSON / table drawer | Detalhes técnicos | P1 |

## 7.5 Deployment & Services

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Deployment & Services | API health card | Estado da API | Existente |
| Flow Explorer | Deployment & Services | PostgreSQL health card | Estado da BD | Existente |
| Flow Explorer | Deployment & Services | RabbitMQ card | exposed/not exposed | Existente |
| Flow Explorer | Deployment & Services | Prevention Host card | heartbeat/not exposed | Existente |
| Flow Explorer | Deployment & Services | Simulator Host card | on demand / last run | Parcial |
| Flow Explorer | Deployment & Services | InfluxDB card | observabilidade | P1 |
| Flow Explorer | Deployment & Services | Grafana card | dashboards disponíveis | P1 |
| Flow Explorer | Deployment & Services | Web UI card | frontend ok | Novo/P1 |
| Flow Explorer | Deployment & Services | Service limitations | Explicar o que não está exposto | Existente/parcial |

## 7.6 Nominal Flow

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Flow Explorer | Nominal Flow | Happy path timeline | Mostrar sequência esperada | Novo/P1 |
| Flow Explorer | Nominal Flow | Select scenario step | Início do fluxo | Novo |
| Flow Explorer | Nominal Flow | Start run step | Orquestração | Novo |
| Flow Explorer | Nominal Flow | Generate readings step | Simulador | Novo |
| Flow Explorer | Nominal Flow | Publish events step | RabbitMQ | Novo |
| Flow Explorer | Nominal Flow | Ingest inbox step | Prevention Host | Novo |
| Flow Explorer | Nominal Flow | Process risk step | Risk pipeline | Novo |
| Flow Explorer | Nominal Flow | Update projections step | PostgreSQL projections | Novo |
| Flow Explorer | Nominal Flow | Emit alerts step | Alert policy | Novo |
| Flow Explorer | Nominal Flow | Show UI step | API/UI | Novo |
| Flow Explorer | Nominal Flow | Collect evidence step | Evidence pack | Novo/P1 |

---

# 8. Evidence & Comparison

## 8.1 Latest Run Audit

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Evidence & Comparison | Latest Run Audit | Expected events card | Eventos esperados | Existente |
| Evidence & Comparison | Latest Run Audit | Accepted readings card | Leituras aceites | Existente |
| Evidence & Comparison | Latest Run Audit | Missing events card | Leituras em falta | Existente |
| Evidence & Comparison | Latest Run Audit | Rejected card | Rejeições | Existente |
| Evidence & Comparison | Latest Run Audit | Quarantined card | Quarantines | Existente |
| Evidence & Comparison | Latest Run Audit | Risk assessments card | Assessments | Existente |
| Evidence & Comparison | Latest Run Audit | Quality flags summary | Flags de qualidade | Existente/parcial |
| Evidence & Comparison | Latest Run Audit | Eligibility summary | Complete/partial/blocked | Existente/parcial |
| Evidence & Comparison | Latest Run Audit | Area snapshot card | Risco agregado | Existente |
| Evidence & Comparison | Latest Run Audit | Audit notes | Explicar que não recalcula risco | Existente |
| Evidence & Comparison | Latest Run Audit | Raw JSON | JSON colapsado | Existente |

## 8.2 Compare B vs C

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Evidence & Comparison | Compare B vs C | Scenario B card | Run B selecionada | Existente em diagnostics |
| Evidence & Comparison | Compare B vs C | Scenario C card | Run C selecionada | Existente em diagnostics |
| Evidence & Comparison | Compare B vs C | Comparison table | Comparar métricas B/C | Existente em diagnostics |
| Evidence & Comparison | Compare B vs C | Delta column | Mostrar diferença | Novo/P1 |
| Evidence & Comparison | Compare B vs C | Expected events row | Comparar eventos esperados | Existente |
| Evidence & Comparison | Compare B vs C | Accepted readings row | Comparar leituras aceites | Existente |
| Evidence & Comparison | Compare B vs C | Missing events row | Comparar missing | Existente |
| Evidence & Comparison | Compare B vs C | Risk assessments row | Comparar assessments | Existente |
| Evidence & Comparison | Compare B vs C | Rejected row | Comparar rejeições | Existente |
| Evidence & Comparison | Compare B vs C | Quarantined row | Comparar quarantines | Existente |
| Evidence & Comparison | Compare B vs C | Risk min/max/avg row | Comparar risco | Existente |
| Evidence & Comparison | Compare B vs C | Risk by metric rows | Comparar métrica a métrica | Existente |
| Evidence & Comparison | Compare B vs C | Narrative summary | Texto curto interpretativo | Novo/P1 |

## 8.3 Run Timings

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Evidence & Comparison | Run Timings | Run duration card | Duração total | Existente |
| Evidence & Comparison | Run Timings | Time to first assessment | Tempo até primeiro risk | P1 |
| Evidence & Comparison | Run Timings | Time to first alert | Tempo até alerta | P1 |
| Evidence & Comparison | Run Timings | Attempt duration table | Duração dos attempts | Parcial, via processing_attempts |
| Evidence & Comparison | Run Timings | Stage timing table | Tempo por stage | P1/P2 |
| Evidence & Comparison | Run Timings | Slowest events | Eventos mais lentos | P2 |
| Evidence & Comparison | Run Timings | Timing limitations note | Indicar o que ainda não é medido | Novo/P1 |

## 8.4 Diagnostics

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Evidence & Comparison | Diagnostics | Runtime table counts | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Active runs | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest runs | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Inbox by status | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Attempts by outcome | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Failed attempts by error | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest rejected events | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest quarantined events | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest run expected vs observed | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest run events by cycle | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Latest run risk by metric | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Area operational state | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Cell operational states | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Active alerts | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Recent alert transitions | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Scenario definition details | Quick query | Existente |
| Evidence & Comparison | Diagnostics | Compare latest B vs C | Deve ser mantido, mas promovido a tab própria | Existente |
| Evidence & Comparison | Diagnostics | Result table | Mostrar resultado da query | Existente |
| Evidence & Comparison | Diagnostics | Raw JSON | Resultado bruto colapsado | Existente |

## 8.5 Export Evidence

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Evidence & Comparison | Export Evidence | Copy audit JSON | Copiar JSON da run | Novo/P1 |
| Evidence & Comparison | Export Evidence | Export summary | Exportar resumo Markdown/JSON | P1/P2 |
| Evidence & Comparison | Export Evidence | Open evidence directory | Abrir pasta local de evidência | Existente/parcial |
| Evidence & Comparison | Export Evidence | Evidence files list | Listar ficheiros gerados | P1 |
| Evidence & Comparison | Export Evidence | Export B/C comparison | Guardar comparação | P1 |
| Evidence & Comparison | Export Evidence | Screenshot checklist | Checklist para demo/relatório | Novo/P2 |

---

# 9. Model & Provenance

## 9.1 Domain Model

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Model & Provenance | Domain Model | Entity cards | Mostrar entidades principais | Novo/P1 |
| Model & Provenance | Domain Model | ScenarioDefinition card | Explicar cenário | Novo |
| Model & Provenance | Domain Model | SimulationRun card | Explicar run | Novo |
| Model & Provenance | Domain Model | TruthSnapshot card | Explicar verdade simulada | Novo |
| Model & Provenance | Domain Model | LocalObservation card | Explicar observação local | Novo |
| Model & Provenance | Domain Model | OperationalEvent card | Explicar evento operacional | Novo |
| Model & Provenance | Domain Model | NormalizedReading card | Explicar leitura normalizada | Novo |
| Model & Provenance | Domain Model | DailyCellState card | Explicar estado diário | Novo |
| Model & Provenance | Domain Model | RiskInput card | Explicar input de risco | Novo |
| Model & Provenance | Domain Model | RiskAssessment card | Explicar assessment | Novo |
| Model & Provenance | Domain Model | AlertState card | Explicar alerta | Novo |
| Model & Provenance | Domain Model | OperationalProjection card | Explicar projeção | Novo |
| Model & Provenance | Domain Model | Detailed drawer | Mostrar mais detalhe | P2 |

## 9.2 Data Chain

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Model & Provenance | Data Chain | Conceptual flow | Mostrar cadeia V1 | Novo/MVP |
| Model & Provenance | Data Chain | ScenarioDefinition node | Início da cadeia | Novo |
| Model & Provenance | Data Chain | TruthSnapshot node | Verdade física | Novo |
| Model & Provenance | Data Chain | LocalObservation node | Observação degradável | Novo |
| Model & Provenance | Data Chain | OperationalEvent node | Evento transportado | Novo |
| Model & Provenance | Data Chain | NormalizedReading node | Leitura validada/normalizada | Novo |
| Model & Provenance | Data Chain | DailyCellState node | Estado diário | Novo |
| Model & Provenance | Data Chain | RiskInput node | Fronteira para scoring | Novo |
| Model & Provenance | Data Chain | RiskAssessment node | Resultado de risco | Novo |
| Model & Provenance | Data Chain | AreaRiskSnapshot node | Agregação/projeção | Novo |
| Model & Provenance | Data Chain | AlertState node | Estado de alerta | Novo |
| Model & Provenance | Data Chain | OperationalProjection node | UI/API | Novo |
| Model & Provenance | Data Chain | Persisted/transient badges | Mostrar o que é persistido | Novo/MVP |

## 9.3 Data Provenance

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Model & Provenance | Data Provenance | Simulated data card | Explicar origem simulada | Novo/MVP |
| Model & Provenance | Data Provenance | Scenario parameters card | Mostrar seed/cycles/interval/degradation | Novo/MVP |
| Model & Provenance | Data Provenance | Candidate parameters card | Explicar pesos/thresholds candidatos | Novo/MVP |
| Model & Provenance | Data Provenance | FWI/KBDI provenance card | Explicar ausente/importado/candidato | Novo/MVP |
| Model & Provenance | Data Provenance | Missing readings card | Explicar degradação | Novo/MVP |
| Model & Provenance | Data Provenance | Freshness/carry-forward card | Explicar estado persistido vs recente | Novo/MVP |
| Model & Provenance | Data Provenance | Limitations card | Limitações metodológicas | Novo/MVP |

## 9.4 Territorial & Weather Context

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Model & Provenance | Territorial & Weather Context | Area context card | Área e características | P1 |
| Model & Provenance | Territorial & Weather Context | Grid context card | Células/grelha | P1 |
| Model & Provenance | Territorial & Weather Context | Sensor context card | Sensores e métricas | P1 |
| Model & Provenance | Territorial & Weather Context | Weather variables card | Temperatura/humidade/vento | P1 |
| Model & Provenance | Territorial & Weather Context | Daily state card | DailyCellState/FWI/KBDI | P1 |
| Model & Provenance | Territorial & Weather Context | Territorial risk card | Contexto territorial | P1/P2 |

## 9.5 Code Mapping

| Página | Tab | Widgets / Componentes | Função | Estado |
|---|---|---|---|---|
| Model & Provenance | Code Mapping | Mapping table | Ligar conceito a código | Novo/MVP |
| Model & Provenance | Code Mapping | Scenario orchestration row | `SimulationRunner` | Novo |
| Model & Provenance | Code Mapping | Truth snapshot row | `TruthSnapshot` | Novo |
| Model & Provenance | Code Mapping | Local observation row | `LocalObservation` | Novo |
| Model & Provenance | Code Mapping | Event envelope row | `EventEnvelope<TPayload>` | Novo |
| Model & Provenance | Code Mapping | Prevention worker row | `PreventionWorker` | Novo |
| Model & Provenance | Code Mapping | Risk pipeline row | `ReadingRiskPipeline` | Novo |
| Model & Provenance | Code Mapping | Eligibility row | `RiskEligibilityService` | Novo |
| Model & Provenance | Code Mapping | Scoring row | `SimpleRiskScoringService` | Novo |
| Model & Provenance | Code Mapping | Alert policy row | `V1AlertPolicy` | Novo |
| Model & Provenance | Code Mapping | Daily state row | `DailyCellState` | Novo |
| Model & Provenance | Code Mapping | Projection store row | `PostgresAreaOperationalProjectionStore` | Novo |
| Model & Provenance | Code Mapping | File/class details drawer | Mostrar mais detalhe | P2 |

---

# 10. Matriz widget / fonte de dados / precisa backend?

Estados usados:

| Estado | Significado |
|---|---|
| Não | Pode ser feito só com frontend, dados já existentes ou conteúdo estático |
| Talvez | Depende de confirmar se o endpoint atual já devolve estes campos |
| Sim | Precisa de endpoint novo ou extensão clara do backend/API |

## 10.1 Home

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Project title | `Nature Protector` | Texto estático frontend | Não | MVP |
| Project subtitle | Descrição curta do projeto | Texto estático frontend | Não | MVP |
| Project summary | Objetivo do sistema | Texto estático frontend | Não | MVP |
| Value cards | Simulação, pipeline, evidência | Texto estático frontend | Não | MVP |
| Participants | Autores, orientadores, curso | Texto estático frontend/config | Não | MVP |
| Area selector | Lista de áreas | API/config atual de áreas | Talvez | MVP |
| Enter workspace button | Área selecionada | Estado frontend | Não | MVP |
| Light/dark toggle | Tema atual | localStorage/UI state | Não | MVP |

## 10.2 Global Workspace

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Selected area | `areaId`, `areaName` | Estado frontend + API áreas | Talvez | MVP |
| Latest run indicator | `simulationRunId`, `scenarioCode`, `status` | runtime summary / latest runs | Talvez | MVP |
| Scenario indicator | `scenarioCode`, `scenarioName` | latest run / audit | Talvez | MVP |
| Status indicator | completed/running/error | latest run / runtime summary | Talvez | MVP |
| Time window selector | `10m`, `30m`, `24h` | Estado frontend | Não | MVP |
| Refresh button | trigger reload | Frontend | Não | MVP |
| Auto refresh toggle | bool | Frontend | Não | P1 |
| Theme toggle | light/dark | Frontend/localStorage | Não | MVP |
| Main navigation tabs | páginas principais | Frontend | Não | MVP |
| Mini chain strip | counts/status por bloco | runtime summary/diagnostics | Talvez | MVP/P1 |

## 10.3 Monitoring

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Current Area Risk card | `AggregateRiskScore`, `AggregateRiskLevel`, `Severity` | `area_operational_state` via API/runtime summary | Talvez | MVP |
| Active Alert card | `AlertCode`, `Severity`, `Status`, `Message` | `alert_state` via API/runtime summary | Talvez | MVP |
| Freshness card | last update, stale/fresh status | runtime summary / area operational state | Talvez | MVP |
| Latest Run card | latest `SimulationRunId`, scenario, status | latest runs endpoint / audit | Talvez | MVP |
| Sensors card | selected/active sensor count | run metadata / resolved overrides | Talvez | MVP |
| Last Update | timestamp do último refresh | Frontend + API timestamp | Não/Talvez | MVP |
| Mini map preview | boundary/cells/sensors | mapa atual/config atual | Não/Talvez | MVP |
| Mini runtime chain | run -> inbox -> risk -> alerts | runtime summary | Talvez | P1 |
| Risk summary text | interpretação curta do risco | frontend derivado do risk level | Não | MVP |
| Alert summary text | interpretação curta do alerta | frontend derivado do alert state | Não | MVP |
| Map component | base map | componente atual | Não | MVP |
| Area boundary | coordenadas da área | config/API atual | Talvez | MVP |
| Grid/cells overlay | cell geometry | config/API atual | Talvez | MVP |
| Sensor markers | sensor coordinates, names, metric | config/API atual | Talvez | MVP |
| Sensor tooltip | sensor name, metric, latest reading | API readings/projection | Talvez/Sim | P1 |
| Cell tooltip | cell id, state, risk | `cell_operational_state` / projection API | Talvez | P1 |
| Cell risk overlay | risk by cell | `cell_operational_state` | Talvez | P1 |
| Temperature dashboard | Grafana URL / panel id | Grafana embed atual | Não | P1 |
| Humidity dashboard | Grafana URL / panel id | Grafana embed atual | Não | P1 |
| Wind dashboard | Grafana URL / panel id | Grafana embed atual | Não | P1 |
| Area risk chart | snapshots over time | `area_risk_snapshot_log` endpoint | Talvez | MVP/P1 |
| Active alerts list | active alert rows | `alert_state` endpoint/runtime summary | Talvez | MVP |
| Alert transitions | historical transitions | alert_state history if retained | Talvez/Sim | P1 |

## 10.4 Scenario Lab

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Scenario select | scenario codes | static config / manifest endpoint | Não/Talvez | MVP |
| Sensor count input | number | frontend form | Não | MVP |
| Cycles input | number | frontend form | Não | MVP |
| Interval input | seconds | frontend form | Não | MVP |
| Seed input | int | frontend form | Não | MVP |
| Degradation profile select | `none`, `missing-readings` | static config | Não | MVP |
| Run label input | string | frontend form | Não | MVP |
| collectEvidence checkbox | bool | frontend form | Não | MVP |
| waitForCompletion checkbox | bool | frontend form | Não | MVP |
| allowParallelRun checkbox | bool | frontend form | Não | MVP |
| Start Run button | request payload | existing orchestration endpoint | Não, se endpoint existe | MVP |
| Run request result | response object | orchestration endpoint response | Não/Talvez | MVP |
| SimulationRunId display | `simulationRunId` | orchestration response | Não/Talvez | MVP |
| Selected sensors list | resolved selected sensor names | orchestration response / run audit | Talvez | MVP |
| Evidence directory | path | orchestration response | Talvez | P1 |
| Scenario A/B/C cards | purpose, expected behavior | static content from docs/manifests | Não | MVP |
| Latest run fields | id, scenario, status, timestamps, metadata | latest run endpoint/audit | Talvez | MVP |
| Runtime reset controls | dry run, confirmation, reset response | existing reset endpoint | Talvez | MVP |

## 10.5 Flow Explorer

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Chain strip | status/count per block | runtime summary | Talvez | MVP |
| Scenario Run block | latest run status | latest run/runtime summary | Talvez | MVP |
| Inbox block | processed/retry/quarantined counts | event_inbox diagnostics | Talvez | MVP |
| Attempts block | succeeded/failed/retry counts | processing_attempts diagnostics | Talvez | MVP |
| Risk block | risk assessment count | risk_assessment_log diagnostics | Talvez | MVP |
| State block | cell/area state counts | projection diagnostics | Talvez | MVP |
| Alerts block | active alert count | alert_state diagnostics | Talvez | MVP |
| Processing Pipeline stage cards | stage counts, errors, timings | processing_attempts + diagnostics | Talvez/Sim | P1 |
| Attempts by Outcome | outcome/count | processing_attempts diagnostics | Talvez | MVP |
| Failed attempts by error | errorCode/message/count | processing_attempts | Talvez | MVP |
| Rejected by Code | reasonCode/count | rejected_events | Talvez | MVP |
| Quarantined by Code | quarantineCode/count | quarantined_events | Talvez | MVP |
| Latest Rejected | latest rows | rejected_events | Talvez | MVP |
| Latest Quarantined | latest rows | quarantined_events | Talvez | MVP |
| Runtime table counts | table/count | existing diagnostics | Talvez | P1 |
| Persistence latest row preview | latest row per table | generic diagnostics endpoint | Talvez/Sim | P1 |
| API health | ok/error | current health summary | Talvez | P1 |
| RabbitMQ status | exposed/unknown/ok | health endpoint | Sim/Talvez | P1 |
| Prevention Host status | heartbeat/unknown | heartbeat endpoint/log not currently exposed | Sim | P1 |
| Happy path timeline | step definitions | static + audit/runtime data | Não/Talvez | P1 |

## 10.6 Evidence & Comparison

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Expected events | expectedEvents | audit endpoint / diagnostic | Talvez | MVP |
| Accepted readings | acceptedReadings | audit endpoint / event_inbox processed | Talvez | MVP |
| Missing events | expected - accepted | frontend if expected/accepted exist | Não/Talvez | MVP |
| Rejected | rejected count | audit endpoint/diagnostics | Talvez | MVP |
| Quarantined | quarantined count | audit endpoint/diagnostics | Talvez | MVP |
| Risk assessments | count | risk_assessment_log/audit | Talvez | MVP |
| Quality flags summary | flags/count | if persisted in audit | Talvez/Sim | P1 |
| Eligibility summary | complete/partial/blocked | if persisted in audit | Talvez/Sim | P1 |
| Area snapshot | aggregateRiskScore/level/count | area_risk_snapshot_log | Talvez | MVP |
| Raw audit JSON | response | audit endpoint | Talvez | P1 |
| Scenario B/C cards | latest B/C run | compare diagnostic | Talvez | MVP |
| Compare B/C table | expected, accepted, missing, risk, rejected, quarantined | compare diagnostic | Talvez | MVP |
| Delta column | B-C difference | frontend calculation | Não | MVP |
| Narrative summary | generated text from values | frontend calculation/static template | Não | P1 |
| Run duration | startedAt/endedAt | simulation_runs | Talvez | P1 |
| Time to first assessment | run started + first assessment timestamp | risk_assessment_log | Talvez | P1 |
| Time to first alert | run started + triggeredAt | alert_state | Talvez | P1 |
| Attempt duration table | StartedAt/FinishedAt per attempt | processing_attempts | Talvez | P1 |
| Export summary JSON | audit/comparison data | frontend download | Não | P1 |
| Export summary Markdown | audit/comparison data | frontend generated markdown | Não | P1 |
| Evidence files list | file list | backend filesystem endpoint | Sim | P2 |

## 10.7 Model & Provenance

| Widget | Dados necessários | Fonte provável | Precisa backend? | Prioridade |
|---|---|---|---|---|
| Entity cards | name/description/status | static config | Não | P1 |
| Conceptual flow | ordered chain nodes | static config | Não | MVP |
| Persisted/transient badges | per node status | static config | Não | MVP |
| Runtime count badges | optional count per node | runtime summary/diagnostics | Talvez | P1 |
| Simulated data card | explanation | static | Não | MVP |
| Scenario parameters card | seed/cycles/interval/degradation | latest run metadata | Talvez | MVP |
| Candidate parameters card | weights/threshold notes | static config | Não | MVP |
| FWI/KBDI provenance card | absent/imported/calculated | DailyCellState/fire index fields | Talvez | MVP/P1 |
| Missing readings card | missing count/degradation profile | audit/compare | Talvez | MVP |
| Freshness/carry-forward card | explanation + timestamps | static + area state | Talvez | MVP |
| Limitations card | text | static | Não | MVP |
| Area context card | area name/id | config/API | Talvez | P1 |
| Daily state card | daily cell state values | daily_cell_state endpoint/diagnostic | Talvez/Sim | P1 |
| Territorial risk card | H/F/G/T context | if implemented/exposed | Sim/Talvez | P2 |
| Code mapping table | concept -> class/file | static config | Não | MVP |
| Search/filter | frontend | frontend | Não | P2 |

---

# 11. Funcionalidades atuais e destino final

| Funcionalidade atual | Destino final |
|---|---|
| Home com seleção de área | Home -> Area Selection |
| Light/dark mode | Top bar global + Home |
| Grafana dashboards | Monitoring -> Sensor Dashboards |
| Mapa com células | Monitoring -> Map & Cells |
| Area risk chart | Monitoring -> Area Risk |
| Runtime Monitor cards | Flow Explorer -> Runtime Chain / Monitoring -> Overview |
| Current/latest run details | Scenario Lab -> Latest Run |
| Runtime chain | Flow Explorer -> Runtime Chain |
| Inbox by Status | Flow Explorer -> Retry & Quarantine ou Diagnostics |
| Attempts by Outcome | Flow Explorer -> Retry & Quarantine |
| Rejected by Code | Flow Explorer -> Retry & Quarantine |
| Quarantined by Code | Flow Explorer -> Retry & Quarantine |
| Risk Scores | Monitoring -> Area Risk / Flow Explorer -> Runtime Chain |
| Active Alerts | Monitoring -> Alerts |
| Latest Rejected | Flow Explorer -> Retry & Quarantine |
| Latest Quarantined | Flow Explorer -> Retry & Quarantine |
| Failed Attempts | Flow Explorer -> Retry & Quarantine |
| Observability Limitations | Model & Provenance -> Data Provenance ou Flow Explorer -> Deployment & Services |
| Developer health cards | Flow Explorer -> Deployment & Services |
| Latest Run Audit | Evidence & Comparison -> Latest Run Audit |
| Diagnostics buttons | Evidence & Comparison -> Diagnostics |
| Compare latest B vs C | Evidence & Comparison -> Compare B vs C |
| Run Orchestrator | Scenario Lab -> Run Orchestrator |
| Runtime State Control | Scenario Lab -> Runtime State Control |
| Reset Runtime State | Scenario Lab -> Runtime State Control |
| Raw JSON | Mantido, mas colapsado em cada página/tab relevante |

---

# 12. Matriz diagramas -> UI

| Diagrama | Página | Tab | Widget principal | Prioridade |
|---|---|---|---|---|
| `01-platform-context.drawio` | Flow Explorer | Deployment & Services | Service cards | P1 |
| `02-prevention-subsystem-landscape.drawio` | Model & Provenance | Code Mapping | module mapping | MVP/P1 |
| `03-end-to-end-data-chain.drawio` | Model & Provenance | Data Chain | conceptual flow | MVP |
| `04-data-curation-and-provenance.drawio` | Model & Provenance | Data Provenance | provenance cards | MVP |
| `05-territorial-and-weather-context.drawio` | Model & Provenance | Territorial & Weather Context | context cards | P1 |
| `06-scenario-construction.drawio` | Scenario Lab | Scenario Definition | scenario cards | MVP |
| `07-simulator-layered-architecture.drawio` | Scenario Lab / Model | Scenario Definition / Code Mapping | simulator layer cards | P1 |
| `08-simulation-sequence-happy-path.drawio` | Flow Explorer | Nominal Flow | happy path timeline | P1 |
| `09-operational-pipeline-overview.drawio` | Flow Explorer | Processing Pipeline | pipeline stage cards | MVP/P1 |
| `10-pipeline-retry-and-quarantine-sequence.drawio` | Flow Explorer | Retry & Quarantine | retry/quarantine table + sequence | MVP |
| `11-persistence-views.drawio` | Flow Explorer | Persistence Views | persistence table cards | P1 |
| `12-runtime-deployment-local-baseline.drawio` | Flow Explorer | Deployment & Services | service health | P1 |
| `13-code-mapping-prevention-slice.drawio` | Model & Provenance | Code Mapping | class/service mapping | MVP |
| `14-domain-model-simplified.drawio` | Model & Provenance | Domain Model | entity cards | P1 |
| `15-domain-model-detailed.drawio` | Model & Provenance | Domain Model | detailed entity drawer | P2 |
| `16-v1-runtime-chain.drawio` | Flow Explorer | Runtime Chain | live runtime chain | MVP |
| `17-scenario-run-orchestrator.drawio` | Scenario Lab | Run Orchestrator | run form + result | MVP |
| `implementation-rejection-retry-quarantine.drawio` | Flow Explorer | Retry & Quarantine | failure sequence | MVP |
| `implementation-data-and-scripts-flow.drawio` | Model & Provenance | Data Provenance | scripts/data flow | P1 |
| `implementation-prevention-nominal-flow.drawio` | Flow Explorer | Nominal Flow | nominal flow timeline | P1 |

---

# 13. Priorização MVP

## 13.1 MVP obrigatório para demo

| Área | Implementar |
|---|---|
| Home | Project info, participants, area selection, theme |
| Monitoring | Overview, Map & Cells, Alerts |
| Scenario Lab | Run Orchestrator, Latest Run, Runtime State Control |
| Flow Explorer | Runtime Chain, Retry & Quarantine |
| Evidence & Comparison | Latest Run Audit, Compare B vs C, Diagnostics |
| Model & Provenance | Data Chain, Code Mapping, Data Provenance |

## 13.2 P1, importante mas pode vir depois do primeiro layout

| Área | Implementar |
|---|---|
| Monitoring | Sensor Dashboards reorganizados, Area Risk detalhado |
| Flow Explorer | Processing Pipeline, Persistence Views, Deployment & Services, Nominal Flow |
| Evidence | Run Timings, Export Evidence |
| Model | Domain Model, Territorial & Weather Context |

## 13.3 P2, polish

| Área | Implementar |
|---|---|
| UI geral | Demo/Developer mode |
| UI geral | Drawers laterais para raw JSON |
| UI geral | Highlight automático da cadeia ativa |
| Evidence | Download Markdown/PDF de evidence pack |
| Model | Mermaid/diagram viewer integrado |

---

# 14. Backend necessário, resumo

## 14.1 Pode ser feito sem backend novo

| Área | Itens |
|---|---|
| Home | hero, project summary, participants, value cards |
| Navigation | páginas/tabs/top bar |
| Scenario Definition | cards A/B/C estáticos |
| Model & Provenance | Data Chain, Data Provenance, Code Mapping estáticos |
| Evidence | delta B/C, narrative summary, export JSON/Markdown local |
| UI polish | tabs, drawers, raw JSON colapsado, light/dark |

## 14.2 Deve reaproveitar backend atual

| Área | Itens |
|---|---|
| Scenario Lab | Run Orchestrator, Latest Run, Runtime State Control |
| Evidence | Latest Run Audit, Compare B vs C, Diagnostics |
| Flow Explorer | Runtime Chain, Retry & Quarantine |
| Monitoring | Area Risk, Alerts, Map & Cells |

## 14.3 Provavelmente precisa backend novo ou extensão

| Área | Widget / Tab | Motivo |
|---|---|---|
| Flow Explorer | Processing Pipeline stage-level | Nem todos os estágios parecem persistidos separadamente |
| Flow Explorer | Deployment & Services | Precisa health real de RabbitMQ, Prevention Host, Influx, Grafana |
| Flow Explorer | Persistence Views com latest row/schema | Precisa endpoint genérico ou diagnostics novos |
| Evidence | Run Timings avançado | Precisa queries/timestamps agregados |
| Evidence | Evidence files list | Precisa acesso à pasta/ficheiros |
| Monitoring | Sensor/cell tooltips ricos | Precisa endpoint consolidado por sensor/célula |
| Model | Territorial risk context | Depende de expor contexto territorial real |
| Model | DailyCellState detalhado por área/célula | Precisa API se ainda só existir em DB |

---

# 15. Ordem segura de implementação

## Iteração 1, sem backend novo

Objetivo: criar navegação e tabs, preservando funcionalidades.

1. Criar layout global com top bar e tabs principais.
2. Criar Home enriquecida.
3. Mover componentes existentes para:
   - Monitoring;
   - Scenario Lab;
   - Flow Explorer;
   - Evidence & Comparison;
   - Model & Provenance.
4. Não alterar runtime/backend.
5. Não remover diagnostics.
6. Não remover raw JSON, apenas colapsar.

## Iteração 2, demo MVP

Objetivo: demo clara.

1. Monitoring Overview.
2. Scenario Lab Run Orchestrator.
3. Flow Explorer Runtime Chain.
4. Evidence Compare B vs C.
5. Model Data Chain.

## Iteração 3, detalhe técnico

Objetivo: mostrar backend por baixo dos panos.

1. Retry & Quarantine.
2. Persistence Views.
3. Processing Pipeline.
4. Deployment & Services.
5. Code Mapping.

## Iteração 4, polish

1. Reduzir scroll.
2. Drawers para raw JSON.
3. Loading/empty/error states.
4. Modo demo/developer.
5. Cores e labels consistentes.
6. Melhorar responsive/full screen.

---

# 16. Critérios de aceitação para implementação

1. A UI mantém todas as funcionalidades existentes.
2. A Home passa a incluir descrição do projeto e participantes.
3. A navegação principal tem 5 áreas:
   - `Monitoring`;
   - `Scenario Lab`;
   - `Flow Explorer`;
   - `Evidence & Comparison`;
   - `Model & Provenance`.
4. O Runtime Monitor deixa de depender de scroll longo para os blocos principais.
5. `Compare B vs C` fica visível como tab própria.
6. `Run Orchestrator` fica numa área própria.
7. `Runtime State Control` fica isolado como danger/control area.
8. `Flow Explorer` mostra a cadeia viva do backend.
9. Raw JSON e diagnostics ficam colapsados ou em tabs específicas.
10. Não há alterações ao comportamento runtime.
11. Não há alteração de endpoints salvo se explicitamente necessário.
12. Build da webUI passa.
13. A UI continua a funcionar em light/dark mode.
14. Não se remove Grafana, mapa, run orchestrator, diagnostics, reset, latest run audit ou compare B/C.
15. A demo consegue seguir a narrativa:

```text
Home -> Scenario Lab -> Flow Explorer -> Monitoring -> Evidence & Comparison -> Model & Provenance
```

---

# 17. Dados a confirmar antes da prompt final para Codex

Antes de gerar a prompt final de implementação, recolher:

```powershell
rg -n "HttpGet|HttpPost|Route|runtime|audit|summary|diagnostics|runs|reset|orchestrator" src/NatureProtector.Backoffice.Api
```

```powershell
rg -n "fetch|axios|api\.|Runtime|Audit|Diagnostics|Orchestrator|Grafana|Area|Alert|Risk" webUI/src
```

```powershell
tree .\webUI\src\app /F
```

Isto permite escrever uma prompt para o Codex com nomes reais de componentes, endpoints e ficheiros.

---

# 18. Veredito

A reorganização proposta não remove funcionalidades. Ela faz quatro coisas:

1. Torna a UI mais alinhada com os diagramas e com o relatório.
2. Promove funcionalidades críticas para a demo, como `Run Orchestrator`, `Runtime Chain`, `Latest Run Audit` e `Compare B vs C`.
3. Isola funcionalidades técnicas ou perigosas, como reset runtime, raw JSON e diagnostics.
4. Cria espaço para mostrar o backend por baixo dos panos sem sobrecarregar a vista operacional principal.

A primeira implementação deve ser sobretudo reorganização frontend, sem alterações de backend. Backend novo só deve ser considerado depois de confirmar exatamente que widgets não conseguem ser alimentados pelos endpoints atuais.
