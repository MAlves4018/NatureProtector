# Guia de manutenção da UI v2 para o owner

Última atualização: 2026-06-16

## Âmbito

Este guia documenta o contrato de manutenção recuperado da UI v2 após `UI-STRUCTURAL-RECOVERY-002`.

A UI v2 continua isolada em `/ui-v2`. As rotas beta continuam preservadas. Esta passagem não alterou contratos RabbitMQ, projeções API públicas, schema da base de dados, migrations, scoring, semântica de alertas, roles, JWT claims, integração runtime P3, comportamento reset/rebaseline, cutover de produção ou infraestrutura de observabilidade.

## Matriz de superfície de produto

| Perfil | Áreas visíveis da UI v2 | Oculto por desenho |
| --- | --- | --- |
| Público / signed out | Product landing, seletor de área, Data Status, ajuda, link de login | Risk score, pipeline, simulação, runs, QA, evidence, P3, admin |
| Pipeline | Overview, risk/data, runs, pipeline, quality/evidence, data status, ajuda | Execução de simulação, P3, admin |
| Sim | Overview, risk/data, runs, cenários, simulação, revisão requested/resolved, data status, ajuda | Internals pipeline/quality/evidence, P3, admin |
| Admin | Todas as superfícies UI v2 | Nenhuma ação destrutiva de reset é exposta |
| Role desconhecida | Demo e ajuda apenas | Todas as superfícies operacionais/técnicas |

A autorização backend continua a ser a fronteira de segurança. A matriz de perfis frontend é uma restrição de produto/UX e não deve ser tratada como substituto da autorização API.

## Estrutura atual

Ficheiros principais:

- `webUI/src/app/ui-v2/UiV2App.tsx`: shell pequeno/composição de provider, theme bridge, skip link, header, navegação e seleção de página.
- `webUI/src/app/ui-v2/state/UiV2Context.tsx`: orquestração frontend e leituras/escritas API existentes.
- `webUI/src/app/ui-v2/navigation/pageRegistry.ts`: page registry orientado a tarefas, derivado de capabilities.
- `webUI/src/app/ui-v2/navigation/UiV2Navigation.tsx`: renderer de navegação agrupada.
- `webUI/src/app/ui-v2/components/`: componentes reutilizáveis UI v2, como seleção de área, Data Status, detalhes técnicos, ajuda contextual e links de paridade beta.
- `webUI/src/app/ui-v2/pages/`: módulos de página public, overview, risk/data, runs, simulation, pipeline, quality/evidence, admin e P3.
- `webUI/src/app/ui-v2/content/`: mapeamento de labels técnicas, registry de tópicos de ajuda, inventário de paridade beta e conteúdo relacionado.
- `webUI/src/app/ui-v2/theme/ui-v2.css`: sistema visual light/dark da UI v2.
- `webUI/src/app/ui-v2/capabilities.ts`: matriz role-to-capability.
- `webUI/src/app/ui-v2/i18n.ts`: copy PT/EN.
- `webUI/src/app/ui-v2/coreContext.ts`: adapters read-model de area/scenario/run/simulation.
- `webUI/src/app/ui-v2/outputContext.ts`: read model contextual de risco.
- `webUI/src/app/ui-v2/technicalSurfaces.ts`: read models pipeline, QA, evidence, admin, P3 e readiness.
- `webUI/src/app/ui-v2/*.test.ts*`: coverage focada de regressão.

## Regras de manutenção

1. Não adicionar contratos backend para UI v2 sem alteração de missão explícita.
2. Não expor reset destrutivo, execução P3 ou execução de diagnósticos a partir da UI v2.
3. Não tratar dados ausentes como zero ou healthy.
4. Não apresentar `Blocked` como risk score 0.
5. Não transformar observabilidade configurada em prova de delivery para collector.
6. Não promover a fixture Playwright HTTP a prova FullStackE2E.
7. Não usar a matriz frontend de roles como fronteira de segurança.
8. Não remover a beta UI sem decisão separada.

## Validação esperada

Para alterações na UI v2:

```powershell
cd .\webUI
npm run typecheck
npm test -- src/app/ui-v2 src/app/services/api.test.ts
npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts
npm run build
```

Quando houver alteração de journeys browser ou capability gating:

```powershell
cd .\webUI
npm run test:e2e
```

Por defeito, `npm run test:e2e` executa Chromium. Para matriz local completa:

```powershell
cd .\webUI
$env:NP_PLAYWRIGHT_BROWSER_MATRIX='all'
npm run test:e2e
Remove-Item Env:\NP_PLAYWRIGHT_BROWSER_MATRIX
```

Para alterações em contratos runtime usados pela UI:

```powershell
dotnet test .\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1
```

## Evidence e limites

Os testes Playwright autenticados usam fixture HTTP controlada no boundary browser. Validam comportamento de UI, propagação de token, capability gating, regressões de console sensível e regressões de acessibilidade. Não provam uma identity store live externa, não substituem testes backend JWT/autorização e não constituem certificação WCAG.

As views técnicas mostram explicitamente `Not instrumented`, `Not confirmed`, `No evidence` e `Not available` quando a evidence runtime não existe. Esses estados são parte do contrato de honestidade da UI v2 e não devem ser escondidos com defaults visuais.

## Checklist de revisão antes de merge local

- A página alterada continua acessível pelo page registry correto.
- A capability necessária está em `capabilities.ts` e testada.
- O texto PT/EN está em `i18n.ts` ou no registry de conteúdo adequado.
- Estados loading/error/empty/unavailable continuam visíveis.
- Nenhum `console.*` browser contém termos sensíveis de utilizador, sessão, role, token, bearer, authorization, password ou credential.
- Não há imports diretos de `react-router` fora da camada permitida.
- Não há `process.env` nem acesso não público a `import.meta.env` no bundle browser.
- A UI continua a mostrar a fronteira académica/não operacional quando apropriado.
