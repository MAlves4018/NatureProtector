# ADR G10.3 — Bootstrap owner-side, budgets e plano de foundation

## Estado

**Aceite para integração. A execução cloud continua dependente do computador e da autorização do owner.**

## Contexto

A Fase G10.2 criou um preflight read-only e um bootstrap limitado a projetos
vazios. A revisão da execução identificou dois riscos adicionais:

1. o script de projetos aceitava `-Execute` mesmo quando o input mantinha as flags
   `create_projects` e `link_billing` a `false`;
2. ainda não existiam contratos executáveis para budgets, inventário pós-bootstrap
   e geração segura dos inputs Terraform da foundation.

O crédito académico é limitado. Budgets devem existir antes do data plane, mas são
alertas e não limites automáticos de consumo.

## Decisão

1. O bootstrap de projetos exige simultaneamente confirmação literal, `-Execute`,
   flags de execução no input e correspondência da conta `gcloud`.
2. `-WhatIf` não tenta descrever projetos inexistentes e produz evidence própria.
3. Budgets usam um contrato JSON separado, começam desativados e exigem confirmação
   literal. Não criam Pub/Sub nem automação de shutdown.
4. O inventário G10.3 é estritamente read-only e regista projetos, billing, APIs e
   recursos comuns por projeto.
5. O plano de state/platform foundation apenas gera `tfvars`, backend e comandos de
   validação. Todas as flags de criação permanecem `false`.
6. A criação do state bucket, WIF, Artifact Registry e service accounts continua
   dependente de um Terraform plan revisto e de nova autorização do owner.
7. Data plane, edge, produção e materialização de segredos permanecem proibidos.

## Consequências

- a intenção do input e a ação do script deixam de poder divergir silenciosamente;
- o owner pode provar que os projetos estão vazios antes de criar foundation;
- os budgets ficam repetíveis e idempotentes por `display_name`;
- nenhum package desta fase demonstra que projetos, budgets ou foundation foram
  realmente criados;
- deployment do NatureProtector continua não provado.
