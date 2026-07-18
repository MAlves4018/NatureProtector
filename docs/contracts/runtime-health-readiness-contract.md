# Contrato de liveness e readiness dos runtimes

## Estado

```text
IMPLEMENTED_NOT_PROVED
PHASES_3C_3D_3E
```

## Princípios

- **Liveness**: processo HTTP vivo.
- **Readiness**: capaz de aceitar o papel funcional configurado.
- Dependência desativada por configuração: `NotApplicable`.
- Fonte de observabilidade indisponível não é automaticamente dependência
  funcional indisponível.
- Falhas e recuperações em runtime devem refletir-se nas probes.

## Matriz normativa

| Componente | Dependência | Condição | Live | Ready |
|---|---|---|---:|---:|
| Backoffice | processo HTTP | sempre | sim | sim |
| Backoffice | PostgreSQL | `ControlPlaneEnabled=true` | não | sim |
| Backoffice | RabbitMQ Management | observabilidade ativa | não | não |
| Backoffice | Grafana | observabilidade ativa | não | não |
| Backoffice | InfluxDB | observabilidade ativa | não | não |
| Prevention | processo HTTP | sempre | sim | sim |
| Prevention | RabbitMQ consumer | sempre | não | sim |
| Prevention | PostgreSQL | `PipelinePersistenceEnabled=true` | não | sim |
| Prevention | InfluxDB | hard dependency futura | não | condicional |

## Status HTTP

| Estado | `/health/live` | `/health/ready` |
|---|---:|---:|
| processo saudável, dependências funcionais saudáveis | 200 | 200 |
| processo saudável, dependência ready indisponível | 200 | 503 |
| processo incapaz de responder | falha/5xx | falha/5xx |

## Transições obrigatórias

1. Ready -> PostgreSQL down -> Unready.
2. Unready -> PostgreSQL recovered -> Ready.
3. Ready -> RabbitMQ channel/consumer lost -> Unready.
4. Unready -> consumer recovered -> Ready.
5. RabbitMQ Management down -> métricas degraded, Backoffice readiness unchanged.
6. TLS/CA Management inválida -> collection error explícito, sem fallback silencioso para HTTP.

## Requisitos de implementação

- checks com tags `live` e `ready`;
- timeout limitado;
- cancellation token propagado;
- mensagens sem secrets;
- opções condicionais testadas;
- testes com processo publicado ou host real, não apenas mocks de método;
- Kubernetes e Compose usam os endpoints canónicos.

## Não-claims

Ready 200 não prova:

- processamento de uma reading end-to-end;
- ausência de backlog;
- capacidade sob carga;
- validade científica;
- disponibilidade prolongada;
- health de dashboards auxiliares.
