# ADR G10.1 — Paridade local/cloud e perfil de primeira qualificação

## Estado

**Aceite para remediação estática; deployment ainda não autorizado nem provado.**

## Decisão

1. O domínio, contratos, imagens, migrations e protocolo RabbitMQ são comuns aos modos local e cloud.
2. PostgreSQL local e Cloud SQL usam a mesma implementação Npgsql e o mesmo contrato `POSTGRES_*`; cloud exige configuração explícita e TLS.
3. O primeiro deployment usa o projeto canónico `staging` com um perfil efémero de qualificação. Não é criado um quarto projeto `dev` nem uma segunda aplicação cloud.
4. O perfil de qualificação reduz apenas propriedades não funcionais: escala mínima, réplicas, HA, retenção e proteção de eliminação. A produção preserva os guardrails production-ready.
5. InfluxDB permanece ativo no modo local. No primeiro perfil cloud fica explicitamente desativado, selecionando o `NoOpInfluxWriteService`, até existir ADR sobre provider, retenção, backup e custo.
6. Runtime evidence da API continua `FileSystem` apenas em Development/Evidence e `Disabled` em cloud. A prova cloud é produzida pela cadeia G8.2. Um `GcsRuntimeEvidenceSink` é uma evolução separada, não condição do primeiro deployment.
7. A billing account académica pode financiar projetos NatureProtector isolados; o projeto e os workloads CN não são reutilizados.
8. A identidade `MAlves4018/NatureProtector` e branch `master` permanece o contrato estático atual, mas repository ID e owner ID continuam gate humano antes de WIF.

## Consequências

- testes locais continuam representativos das regras e protocolos cloud;
- diferenças ficam concentradas em configuração, identidade, rede, TLS, escala e lifecycle;
- a primeira execução pode provar CD e fluxo funcional sem alegar HA/produção;
- qualquer apply continua bloqueado até cost plan, projetos, billing, WIF e teardown estarem confirmados.
