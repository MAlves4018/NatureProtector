# Riscos e plano até dia 1

| Risco | Impacto | Mitigacao | Dono | Prazo |
|---|---|---|---|---|
| RBAC/roles incompleto | Pode limitar a demonstracao de perfis de utilizador e autorizacao. | Definir roles minimos ou declarar explicitamente fora da demo tecnica. | Backend/Frontend | Dia 1 |
| Relatorio atrasado | A apresentacao pode ficar sem narrativa consolidada. | Fechar outline curto com evidencias B/C, matriz de requisitos e riscos. | Equipa | Dia 1 |
| Demo instavel | Perde-se confianca se runtime local depender de passos manuais dificeis. | Usar specs fixas, run IDs recentes e smoke antes da apresentacao. | Backend | Antes da demo |
| Degradation profiles ainda limitados | C cobre missing readings, mas nao cobre ruido, stuck value, duplicate ou delays. | Implementar apenas P1 simples se houver tempo; manter o resto como roadmap. | Backend | Dia 1 |
| Score V1 e candidate parameter set, nao validacao cientifica | Risco metodologico de overclaim. | Dizer explicitamente que o score operacional e baseline candidata. | Equipa | Slides |
| UI/mapa/runtime monitor ainda com polish pendente | A demonstracao visual pode nao refletir todo o backend. | Curar uma vista B/C e screenshots antes da apresentacao. | Frontend | Dia 1 |
| Evidencia precisa de curadoria | Outputs espalhados podem confundir a mensagem. | Usar apenas `docs/evidence/progress-2026-05-22/` como pack principal. | Equipa | Antes da apresentacao |

## P0 até dia 1

- Garantir um smoke runtime repetivel para scenario B `none` e scenario C `missing-readings`.
- Fechar narrativa curta: baseline limpa, degradacao controlada, rastreabilidade por `SimulationRunId`.
- Confirmar visualmente Runtime Monitor/API com os IDs recentes.

## P1 até dia 1

- Implementar ou demonstrar apenas profiles P1 simples se nao colocarem a demo em risco: `noisy-readings`, `stuck-value` ou `duplicate-events`.
- Curar Grafana/Influx ou screenshots equivalentes.
- Definir estado claro de RBAC/roles.

## Fora de ambito ate dia 1

- Validacao cientifica/calibracao do score.
- Integracao meteorologica externa.
- Profiles compostos ou destrutivos.
- Refactors estruturais e alteracoes ao contrato RabbitMQ.
