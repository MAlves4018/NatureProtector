# G8.1 — Edge, runtime e supply-chain trust model

## Princípio

Nenhum pedido, workload ou artifact é confiado apenas por estar dentro da cloud. A confiança é atribuída por identidade, origem, digest, política e evidence.

## Edge

- apenas Load Balancer externo recebe tráfego público;
- Cloud Run usa `internal-and-cloud-load-balancing`;
- Cloud Armor aplica WAF e limites antes do runtime;
- API aplica políticas por operação e identidade;
- quotas funcionais limitam runs, payloads e consultas;
- `429` inclui `Retry-After`;
- rate limits são afinados por load test, não escolhidos como constantes científicas.

## GitHub → Google Cloud

- OIDC/WIF, nunca JSON keys;
- providers separados por release, staging, produção e operações;
- binding ao repository ID, owner ID, branch, workflow e environment;
- provider e impersonation binding restringidos ao `workflow_ref` exato; repository ID, owner ID, branch e environment continuam validados;
- environments libertam a identidade apenas após regras de proteção.

## Runtime

- service account diferente por workload;
- API e frontend sem credenciais partilhadas;
- GKE usa Workload Identity Federation;
- pods non-root, seccomp, read-only e capabilities drop;
- NetworkPolicy default-deny;
- RabbitMQ apenas TLS;
- PostgreSQL apenas TLS e private IP;
- payloads de secrets nunca entram em Terraform state ou manifests.

## Supply chain

O artifact autorizado é uma referência por digest, ligada a:

- source SHA;
- workflow run;
- gates same-SHA;
- SBOM SPDX;
- provenance SLSA;
- scan com HIGH/CRITICAL igual a zero;
- assinatura Cosign verificada;
- release manifest com JSON Schema;
- attestation do manifesto.

Staging e produção consomem o mesmo digest. Tags são apenas auxiliares de localização e não são a identidade de release.

## Binary Authorization

GKE é declarado com Binary Authorization. A policy final deverá aceitar apenas imagens do registry platform com attestations autorizadas. Cloud Run deve usar a mesma disciplina de digest e validação no pipeline, mesmo quando a enforcement surface for diferente.

## Separação de autoridade

- release identity escreve artifacts, não promove produção;
- staging identity executa o ambiente de staging;
- production identity promove uma release já qualificada;
- operations identity executa teardown;
- G8.2 separará reviewer e authorizer humanos do executor.

## Falhas que bloqueiam

- action sem SHA;
- workflow/run/SHA divergentes;
- imagem sem digest;
- scan HIGH/CRITICAL;
- assinatura inválida;
- manifesto diferente entre staging e produção;
- evidence de staging sem attestation verificável;
- utilização de projeto CN;
- secret literal;
- target de produção sem approval;
- ausência de evidence no teardown;
- smoke funcional contra URL direta `run.app`;
- bootstrap de serviços apresentado como ambiente verificado.
