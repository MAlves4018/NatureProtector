# RBAC checkpoint

## Checklist RBAC

| Item | Estado | Evidência atual | Falta | Dono |
|---|---|---|---|---|
| Roles mínimos definidos | Em curso | Requisito registado na matriz `08-requirements-status.md`. | Confirmar nomes e permissões. | Backend/Frontend |
| Login/autenticação demonstrável | Não confirmado | Não validado nesta estabilização. | Confirmar fluxo real ou declarar fora da demo. | Frontend/Backend |
| Proteção de endpoints críticos | Não confirmado | Não validado nesta estabilização. | Listar endpoints e regra de acesso. | Backend |
| UI adapta navegação por role | Não confirmado | Não validado nesta estabilização. | Confirmar se o Runtime Monitor muda por role. | Frontend |
| Auditoria por utilizador | Por fazer | Rastreabilidade por `SimulationRunId` existe, mas não substitui auditoria de utilizador. | Definir eventos auditáveis. | Backend |
| Mensagem para apresentação | Em curso | Este checkpoint separa RBAC do runtime técnico. | Decidir se entra como demo ou risco. | Equipa |

## Perguntas ao colega

1. Quais são os roles mínimos que entram até dia 1?
2. Existe fluxo de login demonstrável ou vamos declarar RBAC como em curso?
3. Que endpoints precisam obrigatoriamente de autorização antes da demo?
4. A UI já esconde/mostra ações conforme role?
5. Há evidência concreta que possamos incluir no relatório?

## Estado para a apresentação de progresso

| Tópico | Estado para 2026-05-22 | Mensagem recomendada |
|---|---|---|
| RBAC/roles | Em curso | Requisito identificado; não usado como prova de runtime nesta apresentação. |
| Runtime técnico | Feito | B/C demonstram execução ponta a ponta sem depender de RBAC. |
| Auditoria técnica | Feito | `SimulationRunId`, metadata, inbox, attempts e risk logs dão rastreabilidade técnica. |
| Auditoria de utilizador | Por fazer | Diferente de rastreabilidade técnica; fica como trabalho até dia 1/futuro. |

## Mensagem curta para enviar ao colega

> Para a apresentação de progresso de 22/05 vou marcar RBAC/roles como "Em curso", salvo se tiveres evidência concreta de login, roles e proteção de endpoints. Consegues confirmar até ao fim do dia quais roles entram até dia 1, que endpoints ficam protegidos e se há screenshot/fluxo demonstrável? A parte runtime B/C já está validada independentemente de RBAC.
