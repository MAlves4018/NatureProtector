# Runbook — G8.2 runtime qualification

## Pré-condições

1. G8.1 integrado no commit candidato.
2. Release G8.1 concluída para o mesmo SHA.
3. Projetos novos de platform, staging e production; nenhum recurso CN.
4. WIF G8.2 provisionado.
5. Bucket de evidence com versioning, public access prevention e retenção mínima de 365 dias.
6. Source adapters revistos e testados.
7. Environments GitHub e reviewers configurados.

## Ordem de execução

Para cada ação:

1. executar `G8.2 runtime probe`;
2. confirmar que o bundle foi atestado;
3. executar `G8.2 runtime qualification` em modo `ingest`;
4. preservar o run ID do ingest.

Os pilotos têm de ocorrer em três dias UTC distintos. O soak requer pelo menos 72 horas e intervalos máximos de cinco minutos entre amostras.

Depois de todas as ações:

1. executar `G8.2 runtime qualification` em modo `finalize`;
2. fornecer todos os run IDs de ingest, sem duplicados;
3. o workflow valida cada run pela API GitHub;
4. descarrega e verifica cada attestation;
5. agrega métricas;
6. sela o índice closed-world;
7. emite o pré-veredito;
8. arquiva a evidence;
9. verifica o recibo;
10. emite o veredito final e o review packet.

## Confirmações

- probe: `RUN_G82_<ACTION>`;
- ingest: `INGEST_G82_ACTION`;
- finalize: `FINALIZE_G82_QUALIFICATION`.

## Condições de bloqueio

- run, workflow, SHA, branch ou repository divergentes;
- attestation sem signer workflow/source digest/source ref esperados;
- manifesto diferente;
- ficheiros extra, em falta ou symlinks;
- pilotos repetidos;
- soak insuficiente ou com gaps excessivos;
- contagem `processed + failed + loss != produced`;
- SLO, DR, segurança, custo, operação, rollback ou cleanup abaixo da política;
- arquivo sem retenção/versioning/PAP;
- source adapter ausente.

## Estado final

`G82_FINAL_QUALIFICATION_PASSED` prova apenas a qualificação e o arquivo da release exata. Mantém `production_authorized=false` e `production_deployed=false`.
