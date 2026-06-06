# Manifesto de Auditoria de Dados - Momento 2

## Identificação

- Data/hora:
- Tipo de auditoria: PostgreSQL / InfluxDB / ambos
- Ambiente:
- Operador:

## Scripts corridos

| Script | Estado | Output |
|---|---|---|

## Ligação usada

Connection string redigida ou database/bucket:

```text

```

Tokens ou passwords completos não devem ser registados.

## Ficheiros gerados

| Ficheiro | Descrição |
|---|---|

## Notas de segurança

- Queries read-only.
- Sem dumps completos.
- Sem tokens gravados.

## Limitações

- A evidência representa apenas o estado da base no momento da execução.
- Contagens pequenas não suportam treino robusto.
- M3 depende de dados negativos suficientes.
- M5 de falhas depende de rejeições/quarentenas/retries e correlação.

