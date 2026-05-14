# V1 Implementation Plan

## 1. Objetivo do Plano

Transformar o Proposal numa sequência de execução verificável, com critérios de entrada/saída e rastreabilidade explícita.

## 2. Fontes Usadas

| Fonte | Papel no plano | Localização | Última revisão | Responsável |
|---|---|---|---|---|
| Proposal principal | Base de requisitos e intenção | `C:/Users/Miguel/UNI/6sem/PS/Rand/Pesquisa2/Tex9/Pesquisa2/Proposal.pdf` |  |  |
| Mapa Proposal -> implementação | Backlog rastreável de gaps | `docs/implementation/v1/v1-proposal-to-implementation-map.md` |  |  |
| Evidências técnicas | Provas de execução/validação | `docs/evidence/README.md` |  |  |
| Contratos | Regras de compatibilidade | `docs/contracts/README.md` |  |  |

## 3. Hierarquia de fontes e regra de conflito

| Regra | Ação operacional |
|---|---|
| Código e testes prevalecem sobre documentação | Quando houver divergência, registar o comportamento observado no repo como estado atual auditado |
| Auditoria do repo define o estado real auditado | Marcar apenas o que tiver evidência verificável (ficheiro, linha, output, artefacto) |
| Proposal define contrato-alvo, não implementação atual | Tratar o Proposal como alvo de entrega, sem assumir que já está implementado |
| Roadmap ordena execução, mas não prova implementação | Usar roadmap para prioridade e sequência, não como evidência de conclusão |
| Conflito Proposal vs código | Marcar como gap na matriz `Proposal -> Repo -> Gap -> Tarefa`; não resolver automaticamente |

## 4. Hierarquia de Verdade

| Nível | Artefacto | Quando prevalece |
|---|---|---|
| 1 | Código + comportamento observado | Sempre que houver divergência com documentação |
| 2 | Contratos versionados | Em decisões de integração e compatibilidade |
| 3 | Testes automatizados | Para validar regressões e critérios de aceitação |
| 4 | Proposal e notas de planeamento | Para direção funcional e prioridade |

## 5. Workstreams

### WS0 - Baseline e evidência
- Objetivo: congelar baseline do estado atual e registar evidência executada mínima.
- Depende de: nenhum.
- Modelo/raciocínio recomendado para Codex: `low`.
- Não fazer ainda: alterações funcionais em contratos, pipeline ou API.
- Principais ficheiros prováveis: `docs/evidence/execution-evidence-YYYY-MM-DD.md`, `docs/implementation/v1/v1-proposal-to-implementation-map.md`.
- Testes esperados: checklist de execução preenchida; teste obrigatório de consistência documental (comandos listados vs evidências anexadas).
- Critério de saída: baseline e evidência inicial preenchidos e revisáveis.

### WS1 - Contratos e vocabulário
- Objetivo: definir contratos canónicos e vocabulário comum para entidades ausentes/divergentes.
- Depende de: WS0.
- Modelo/raciocínio recomendado para Codex: `medium`.
- Não fazer ainda: otimizações de pipeline ou ajustes de dashboard.
- Principais ficheiros prováveis: `docs/contracts/README.md`, contratos de domínio em `src/` (a confirmar em implementação).
- Testes esperados: testes obrigatórios de serialização/compatibilidade de contratos e mapeamento legado -> canónico.
- Critério de saída: contratos V1 definidos com estratégia de compatibilidade explícita.

### WS2 - Simulador em camadas
- Objetivo: estruturar simulador por camadas para gerar inputs controlados.
- Depende de: WS1.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: integração final com alertas e projeção agregada.
- Principais ficheiros prováveis: módulos de simulação em `src/` e testes em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório por camada (input sintético -> output esperado) e teste de integração curta entre camadas.
- Critério de saída: simulador gera cenários reproduzíveis para alimentar pipeline.

### WS3 - Pipeline, classificadores e quality flags
- Objetivo: fechar pipeline de processamento com `ClassifierResult` e `QualityFlags`.
- Depende de: WS1, WS2.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: decisão final de elegibilidade e scoring final de risco.
- Principais ficheiros prováveis: componentes de pipeline/classificação em `src/` e suites em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de classificação por cenário e teste obrigatório de composição/prioridade de flags.
- Critério de saída: pipeline produz saída classificada com flags de qualidade consistentes.

### WS4 - Elegibilidade e RiskInput
- Objetivo: aplicar estados de elegibilidade e expandir `RiskInput` para uso operacional.
- Depende de: WS3.
- Modelo/raciocínio recomendado para Codex: `medium`.
- Não fazer ainda: cálculo final de `RiskAssessment V1`.
- Principais ficheiros prováveis: contratos/serviços de elegibilidade e input de risco em `src/`, testes em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de decisão `CompleteEligible/PartialButUsable/Blocked` e teste obrigatório de validação de `RiskInput`.
- Critério de saída: `RiskInput` gerado apenas quando regras de elegibilidade estiverem satisfeitas.
- Estado atual: implementado na slice V1-007 com validação pendente (build/test ainda bloqueados por limitação ambiental de `NuGet.Config`).
- Nota: `QualityFlags` está parcial/temporário como `List<string>`; contrato canónico permanece pendente.

### WS5 - DailyCellState
- Objetivo: consolidar estado diário por célula como base para etapas posteriores.
- Depende de: WS4.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: publicação de índices finais (`FWI/KBDI`) antes da consolidação diária estar pronta.
- Principais ficheiros prováveis: agregadores/estado diário em `src/`, testes de agregação em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de agregação diária determinística e teste obrigatório de comportamento com dados parciais.
- Critério de saída: `DailyCellState` disponível com regras de consistência verificadas.

### WS6 - RiskAssessment V1
- Objetivo: alinhar `RiskAssessment` ao contrato-alvo V1 com compatibilidade controlada.
- Depende de: WS5.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: alertas finais baseados em score novo sem validação completa.
- Principais ficheiros prováveis: modelos e cálculo de risco em `src/`, testes de mapeamento e cálculo em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de mapeamento legado -> V1 e teste obrigatório de cálculo (`input_status/base_risk/adjusted_score`).
- Critério de saída: `RiskAssessment V1` produzido e validado com cobertura mínima acordada.
- Estado atual: pendente/divergente; implementação atual continua baseada em `RiskScore/RiskLevel`.
- Decisão pendente ligada ao WS4/WS6: para `Blocked`, fechar contrato entre ausência de assessment numérico vs assessment com campos numéricos nulos.

### WS7 - Agregação, AlertState e OperationalProjection
- Objetivo: fechar agregação operacional e evoluir `AlertState`/`OperationalProjection` com base no `RiskAssessment V1`.
- Depende de: WS6.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: prometer alertas finais antes de validação de `RiskAssessment V1`.
- Principais ficheiros prováveis: projeções/alertas/agregadores em `src/`, testes de transição e projeção em `tests/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de transição de `AlertState` e teste obrigatório de projeção com dados incompletos controlados.
- Critério de saída: estado de alerta e projeção operacional consistentes com regras V1.

### WS8 - API, Grafana e evidência de demonstração
- Objetivo: expor resultados V1 em API/dashboards e recolher evidência demonstrável.
- Depende de: WS7.
- Modelo/raciocínio recomendado para Codex: `medium`.
- Não fazer ainda: mudanças arquiteturais fora do escopo V1.
- Principais ficheiros prováveis: endpoints/API, configurações de dashboard e `docs/evidence/` (a confirmar em implementação).
- Testes esperados: teste obrigatório de contrato de API e teste obrigatório de smoke da visualização principal.
- Critério de saída: demonstração ponta-a-ponta com evidência anexada.

### WS9 - Testes end-to-end A/B/C
- Objetivo: validar cenários E2E A/B/C com critérios claros de aprovação.
- Depende de: WS8.
- Modelo/raciocínio recomendado para Codex: `high`.
- Não fazer ainda: novas features fora dos cenários A/B/C.
- Principais ficheiros prováveis: suites E2E em `tests/` e evidências em `docs/evidence/` (a confirmar em implementação).
- Testes esperados: teste obrigatório E2E para cenário A, teste obrigatório E2E para cenário B e teste obrigatório E2E para cenário C.
- Critério de saída: 3 cenários E2E executados com resultados e limitações documentadas.

## 6. Definition of Ready (DoR)

| ID | Critério DoR | Evidência mínima | Cumprido (Sim/Não) | Notas |
|---|---|---|---|---|
| DOR-01 | Requisito mapeado a secção do Proposal | Referência explícita na matriz |  |  |
| DOR-02 | Gap confirmado no repo | Evidência objetiva (ficheiro/linha/comportamento) |  |  |
| DOR-03 | Critério de aceitação definido | Checklist testável |  |  |
| DOR-04 | Dependências identificadas | Lista de bloqueios e pré-requisitos |  |  |

## 7. Definition of Done (DoD)

| ID | Critério DoD | Evidência requerida | Validado por | Estado |
|---|---|---|---|---|
| DOD-01 | Implementação concluída conforme tarefa | Diff e descrição técnica |  |  |
| DOD-02 | Testes aplicáveis executados | Resultado de testes anexado |  |  |
| DOD-03 | Contratos não quebrados | Verificação de compatibilidade |  |  |
| DOD-04 | Evidências arquivadas | Registo em `docs/evidence/` |  |  |

## 8. Política de Modelo/Raciocínio para Codex

| Contexto | Modelo recomendado | Nível de raciocínio | Objetivo |
|---|---|---|---|
| Scaffolding documental | Codex | low | Velocidade e consistência estrutural |
| Mapeamento Proposal -> repo | Codex | medium | Melhor análise de rastreabilidade |
| Refatorações críticas/arquitetura | Codex | high | Reduzir risco de decisão |
| Tarefas repetitivas e mecânicas | Codex | low | Custo/tempo otimizados |

## 9. Como Escrever Prompts para Codex

### Template base

```text
Contexto:
- Branch atual:
- Ficheiros alvo:
- Restrições (ex.: não tocar em src/tests):

Objetivo:
- Resultado esperado em 1-2 frases.

Critérios de aceitação:
- [ ] Critério 1
- [ ] Critério 2

Entrega:
- Lista de ficheiros alterados/criados
- Resumo técnico curto
- Comandos de validação executados
```

### Boas práticas

- Pedir escopo explícito (o que pode e não pode ser alterado).
- Exigir evidência verificável em vez de afirmações genéricas.
- Solicitar resultados em formato checklist quando houver validação.

## 10. Ligação a Tarefas

Consultar `docs/implementation/v1/tasks/README.md` para convenções de decomposição e rastreabilidade de tarefas.
