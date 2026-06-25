# Runbook — G8.2 independent review and authorization

## Separação de funções

Devem existir três identidades distintas:

- executor da qualificação;
- reviewer independente;
- authorizer humano.

O segundo operador e o rollback owner também têm de estar identificados. A ausência de pessoas atribuídas mantém `PRODUCTION_NO_GO`.

## Revisão independente

1. descarregar o review packet atestado;
2. verificar hashes e manifesto;
3. preencher um documento conforme `g8-2-independent-review.schema.json`;
4. usar severidades minúsculas do enum;
5. não aceitar findings high/critical abertos;
6. `ACCEPT` exige zero findings abertos e zero condições;
7. `ACCEPT_WITH_CONDITIONS` exige condições não vazias, datas futuras e `risk_reduction_only=true`;
8. assinar com namespace `natureprotector-g82-independent-review`;
9. submeter pelo workflow de governance;
10. verificar com o workflow de independent review.

## Pedido e decisão de autorização

O pedido só é criado quando o veredito final e o review estão aceites. O pedido liga diretamente:

- manifesto;
- evidence index;
- archive receipt;
- veredito final;
- review;
- review verdict.

O authorizer assina uma decisão conforme o schema com namespace `natureprotector-g82-production-authorization`. O máximo é 168 horas e nunca pode ultrapassar a validade pedida. A decisão deve indicar rollback owner e mantém `production_deployed=false`.

## Interpretação

`G82_PRODUCTION_AUTHORIZATION_VERIFIED` permite que uma fase posterior considere uma promoção. Não executa a promoção e não pode ser reutilizado para outro commit, manifesto, evidence, ambiente ou janela temporal.
