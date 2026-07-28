# Fundação da UI v2 e expansão de capacidades

> **Estado histórico:** este documento descreve a implementação intermédia UI v2. A UI atual usa rotas diretas e `webUI/src/app`; consultar [`../current-state/roles-capabilities-and-ui.md`](../current-state/roles-capabilities-and-ui.md).


Esta página documenta a implementação UI v2 entregue pela M03 e expandida pela M04/M05, com hardening browser-auth posterior. É uma nota de implementação, não uma claim de produto.

A UI v2 é uma experiência protótipo isolada em `/ui-v2`. Mantém intactas as rotas beta existentes e não altera contratos RabbitMQ, projeções API públicas, schema/migrations de base de dados, semântica de alertas, scoring, JWT claims ou nomes de roles.

## Âmbito implementado

A M03 acrescentou a primeira fatia vertical:

- entrada demo que declara a fronteira de protótipo académico;
- vista contextual de risco read-only suportada pela runtime summary API existente;
- Data Status Strip para origem, freshness, completeness, coverage, eligibility, provenance, continuity e limitations;
- copy UI em português e inglês controlada client-side;
- ajuda contextual através do botão de ajuda e F1;
- testes de componentes orientados a acessibilidade e scan básico axe;
- isolamento por rota para manter a UI beta disponível.

A M04 expandiu essa fatia sem acrescentar contratos backend:

- seleção dinâmica de área a partir de `GET /api/control/areas`, com estado requested/resolved e sem fallback hardcoded de área;
- seleção de cenário a partir de `GET /api/control/areas/{areaCode}/scenarios`;
- seleção de run e contexto de run a partir de `GET /api/control/simulation-runs`, `GET /api/control/runtime/runs/{runId}`, audit e timing endpoints;
- revisão de pedido de simulação com configuração requested/resolved quando devolvida pela metadata runtime da run;
- UI de execução de simulação que chama `POST /api/control/runtime/runs` apenas quando o capability profile frontend o permite;
- ajuda browser-funcional integrada, substituindo o link M03 para docs do repositório que o Vite não servia como asset da app;
- testes backend de autorização a confirmar que `Pipeline` consegue ler runtime summary mas não consegue iniciar uma runtime run, enquanto `Sim` consegue iniciar uma.

A M05 reforçou a superfície técnica UI v2 sem acrescentar contratos backend:

- vista Pipeline/Observability suportada por runtime summary, run audit e run timing contracts existentes;
- estados explícitos `Not instrumented`, `Not confirmed`, `No evidence` e `Not available` para campos como queue backlog, broker health, publisher timestamps e latência por evento;
- vista QA que separa definição de teste, execução, resultado, referência de evidence e scope de coverage;
- vista Evidence/Limitations que lista evidence disponível e distingue claims suportadas de claims não suportadas pelo artifact;
- vista Admin proporcional que documenta ações sensíveis e enforcement backend sem expor reset destrutivo, P3 run ou diagnostic execution controls;
- vista experimental P3 que mantém P3 separado de scoring, alert semantics, schema e runtime principal do simulador;
- checklist de staging/demo readiness explícita sobre evidence visível no browser versus handoff/runtime evidence;
- testes focados para novos adapters, capabilities, claims/absence states e route wiring.

O hardening browser de 2026-06-16 acrescentou coverage Playwright contra o artifact UI construído para jornadas Anonymous, Admin, Sim e Pipeline, além de estados de falha login/session/API, estados degradados de runtime summary e download autenticado de evidence. Também expandiu coverage de regressão de acessibilidade para axe, skip link por teclado, ciclo de foco do diálogo de ajuda F1, Escape/focus restore, dark mode, viewport mobile e media settings `reduced-motion`. Um guardrail Vitest rejeita `console.*` na app browser quando as mensagens incluem termos sensíveis de user, token ou session. Estes testes browser usam uma fixture HTTP no boundary Playwright; validam comportamento UI, propagação de token, capability gating, regressões de console sensível e regressões de acessibilidade, não uma identity store externa live nem certificação WCAG.

## Mapeamento de superfícies

| Área | Ficheiros principais | Fonte de dados | Claim permitida |
| --- | --- | --- | --- |
| Entrada pública | `PublicLandingPage.tsx`, `ProductLandingView.tsx` | conteúdo local + área pública | protótipo académico/não operacional |
| Overview | `OverviewPage.tsx` | runtime context existente | orientação de navegação, sem nova semântica |
| Risk/Data | `RiskDataPage.tsx`, `outputContext.ts` | runtime summary/projeções existentes | leitura contextual, não recalculation |
| Runs | `RunsPage.tsx`, `coreContext.ts` | endpoints de simulation/runtime runs | seleção e detalhe de run quando disponíveis |
| Simulation | `SimulationPage.tsx` | runtime run API existente | execução apenas para capabilities autorizadas |
| Pipeline | `PipelinePage.tsx`, `technicalSurfaces.ts` | summary/audit/timings existentes | visibilidade técnica proporcional |
| Quality/Evidence | `QualityEvidencePage.tsx` | QA/evidence read models | evidence e limitações explícitas |
| Admin | `AdminPage.tsx` | capability profile + docs | orientação proporcional, sem ações destrutivas |
| P3 | `P3Page.tsx` | conteúdo experimental/documentado | P3 separado de runtime/scoring principal |

## Estados de ausência

A UI v2 deve mostrar ausência explicitamente:

- `Not available`: dado não disponível para o perfil, endpoint ou estado atual.
- `Not instrumented`: o sistema ainda não mede esse sinal.
- `Not confirmed`: existe configuração ou intenção, mas falta evidence runtime.
- `No evidence`: não há artifact ou referência verificável para a claim.
- `Unknown`: o sistema tentou observar, mas a observação falhou ou é inconclusiva.

Estes estados não são decoração; são parte do contrato de honestidade do protótipo. Não devem ser substituídos por zero, healthy ou sucesso implícito.

## Guardrails preservados

- Browser integration com fixture HTTP não é `FullStackE2E`.
- Startup/configuração OTLP não é prova de entrega para collector real.
- Microbenchmark B0 não é performance sistémica.
- Clean install estrutural não é instalação funcional completa.
- `Blocked` não é risk score 0.
- A UI v2 não muda contratos, scoring, alertas, migrations ou roles.
- A fronteira de segurança continua no backend; o frontend apenas condiciona UX.

## Validação associada

Validações relevantes desta frente:

```powershell
cd .\webUI
npm run typecheck
npm test -- src/app/ui-v2 src/app/services/api.test.ts
npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts
npm run build
npm run test:e2e
```

Quando os contratos runtime backend mudam, validar também:

```powershell
dotnet test .\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1
```

## Limitações atuais

- A UI v2 ainda é protótipo académico; não substitui a beta UI nem representa cutover de produto.
- A evidence browser usa fixture HTTP para Playwright; não prova identidade externa live.
- Alguns sinais de runtime continuam não instrumentados e devem permanecer visíveis como tal.
- P3 continua experimental e separado de scoring/alerting/runtime principal.
- Acessibilidade tem regressões automatizadas, mas não certificação formal WCAG.
