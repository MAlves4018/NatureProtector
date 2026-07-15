# ADR HEALTH-01 — Readiness condicional e TLS do RabbitMQ Management

## Estado

**Implementado como proposta acumulada nas Fases 3C, 3D e 3E; ainda requer build e provas no workspace real.**

```text
CONDITIONAL_READINESS_IMPLEMENTED_NOT_PROVED
RABBITMQ_MANAGEMENT_TLS_IMPLEMENTED_NOT_PROVED
```

## Contexto

A Backoffice API expõe `/health`, `/health/live` e `/health/ready`, mas regista
apenas `AddHealthChecks()` sem o `ControlPlaneDatabaseHealthCheck` já existente.
Os três endpoints podem devolver 200 com PostgreSQL indisponível.

A Prevention regista apenas `PreventionReadinessHealthCheck`, cujo estado é
marcado ready depois de criar o consumer RabbitMQ. Quando
`PipelinePersistenceEnabled=true`, PostgreSQL é uma dependência funcional, mas
não participa na readiness.

Em cloud, a API recebe `RabbitMq__ManagementScheme=https`, porta 15671 e uma CA
privada. `RuntimeObservabilityService` constrói sempre uma URI `http://` e não
carrega a CA.

## Decisão

### 1. Liveness

Liveness responde apenas à pergunta: **o processo e o servidor HTTP estão
vivos?**

Falhas externas não devem provocar restart automático permanente.

| Componente | Dependências de liveness |
|---|---|
| Backoffice API | processo/HTTP |
| Prevention Host | processo/HTTP |

### 2. Readiness da Backoffice API

Quando `BackofficeApi:ControlPlaneEnabled=true`, readiness exige PostgreSQL.

Quando o control plane está explicitamente desativado, o check PostgreSQL é
`NotApplicable` e não bloqueia readiness.

RabbitMQ Management, Grafana e Influx são fontes de observabilidade. A sua
indisponibilidade degrada métricas, mas não torna toda a API unready.

### 3. Readiness da Prevention

Readiness exige:

- ligação/canal/consumer RabbitMQ operacional;
- PostgreSQL acessível quando `PipelinePersistenceEnabled=true`.

Quando persistence está desativada, PostgreSQL é `NotApplicable`.

InfluxDB só bloqueia readiness se uma configuração futura o declarar hard
dependency. Com o contrato atual de tolerância a erro de escrita, não bloqueia.

### 4. Transições

Readiness não é apenas um gate de startup. Deve responder a falhas e recuperação
em runtime:

- DB down depois de ready -> 503;
- DB recuperada -> 200;
- consumer/canal RabbitMQ perdido -> 503;
- consumer recriado -> 200;
- Management API down -> Backoffice ready continua 200;
- liveness permanece 200 durante falhas externas, salvo falha do processo.

Os checks devem usar timeout curto e cancellation token. Não podem bloquear uma
probe durante dezenas de segundos.

### 5. Tags e endpoints

Os registos devem usar tags explícitas:

```text
live
ready
```

Contrato:

```text
/health/live  -> apenas checks live
/health/ready -> apenas checks ready
/health       -> resumo compatível, sem ser usado como probe canónica cloud
```

### 6. RabbitMQ Management

A Management API usa opções tipadas separadas do transporte AMQP:

```text
ManagementScheme
ManagementHost (fallback controlado para HostName)
ManagementPort
ManagementUserName
ManagementPassword
ManagementCertificateAuthorityPath
```

Regras:

- schemes permitidos: `http`, `https`;
- `https` valida hostname e cadeia numa CA privada quando configurada;
- CA inexistente ou inválida falha cedo com mensagem segura;
- credenciais nunca aparecem em logs;
- deve existir um named/typed `HttpClient` próprio;
- o client usa timeout limitado;
- a conta Management deve ter apenas permissões de monitorização necessárias.

HTTP continua permitido apenas para desenvolvimento/Compose explicitamente
configurado.

## Consequências

### Positivas

- probes representam dependências funcionais reais;
- Kubernetes deixa de enviar trabalho para Prevention sem persistence;
- Backoffice não anuncia readiness falsa com control plane indisponível;
- observabilidade Management funciona com TLS e CA privada;
- falhas de observabilidade não derrubam serviços funcionais.

### Custos e limitações

- readiness pode oscilar durante falhas transitórias reais;
- é necessário provar recuperação, não apenas startup;
- a CA montada pode exigir restart para rotação, consoante a implementação do
  handler;
- `/health` histórico pode continuar menos estrito do que `/health/ready`.

## Alternativas rejeitadas

- incluir todas as integrações em readiness;
- reiniciar processos sempre que PostgreSQL ou RabbitMQ falham;
- continuar a usar `http://` na porta TLS;
- desativar validação de certificado;
- reutilizar credenciais administrativas do broker sem necessidade.

## Critérios de aceitação da implementação

- Backoffice live 200 e ready 503 com DB down e control plane ativo;
- Prevention live 200 e ready 503 com DB down e persistence ativa;
- readiness recupera automaticamente após recuperação das dependências;
- Management HTTPS usa a URI e CA configuradas;
- CA errada, hostname incorreto e certificado expirado falham;
- Management indisponível não altera readiness da Backoffice;
- probes cloud continuam a apontar para `/health/live` e `/health/ready`.


## Estado de implementação da Fase 3E

A proposta liga `ManagementScheme`, host, porta, credenciais e CA a `RabbitMqOptions`, usa um named `HttpClient`, desativa redirects, aplica timeout limitado e valida a CA privada com hostname preservado. O handler é renovado a cada dois minutos para permitir observar rotação do ficheiro montado. As credenciais Management separadas são suportadas; o manifest cloud pode continuar temporariamente a usar fallback para as credenciais AMQP até existir uma conta de monitorização dedicada.
