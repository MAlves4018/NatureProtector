---
title: "NatureProtector - Suplemento de Atualização do Estado do Projeto"
author: "Miguel Alves"
date: "28 de junho de 2026"
lang: pt-PT
---

# Finalidade e autoridade

Este suplemento regista a diferença factual entre o estado do relatório identificado como **R13** e o snapshot do repositório que inclui o Unified Operations Control Plane e o novo sistema documental. É um instrumento de integração, não substitui automaticamente o relatório de 379 páginas e não constitui evidence de que staging ou produção foram concluídos.

O relatório fornecido posiciona corretamente o NatureProtector como plataforma experimental auditável e distingue implementação, verificação estática, resultado vigente, execução histórica, tentativa bloqueada, reprodução e validação. O repositório atual estende essa disciplina às operações de engenharia.

# Síntese da atualização

O repositório passou a conter policies de capabilities no servidor; as roles `QA`, `Operations` e `ReleaseApprover`; as vistas Mission Control, Quality Runs, Evidence Explorer, Deployments, Cloud Resources, Approvals e administração de utilizadores/roles; catálogo fechado de operações; registos, confirmação e aprovação; wrappers de GitHub Actions; callback autenticado e classificação de artifacts.

Foi também criada uma camada documental canónica com current state, tutorial/how-to/reference/explanation, modelo Structurizr, portfolio visual com source/render/sidecar, manifests, inventários, validação de links e compêndio de estudo.

![Arquitetura atual de containers](../../architecture/diagrams/current/render/container-architecture-a4.png)

# Integração arquitetural

A arquitetura do relatório deve agora distinguir três planos:

1. processamento runtime e dados;
2. API, UI e plano de controlo;
3. operações de engenharia, runners e evidence.

O browser permanece fora da fronteira de confiança do provider. Seleciona um identificador de operação do catálogo; o backend autoriza e regista; runners especializados detêm a identidade necessária.

![Ciclo de uma operação](../../architecture/diagrams/current/render/operations-lifecycle-a4.png)

# Roles e separação de poderes

O modelo atual acrescenta `QA`, `Operations` e `ReleaseApprover`. `Admin` gere utilizadores, roles e configuração da aplicação, mas não recebe automaticamente capacidade de deployment de produção ou destroy. A separação continua relevante mesmo quando o owner académico acumula roles, porque solicitação, confirmação e aprovação ficam registadas como passos distintos.

![Roles e jornadas da UI](../../architecture/diagrams/current/render/roles-ui-journeys-a4.png)

# Qualidade e evidence

As operações de qualidade e evidence são selecionadas num catálogo limitado e despachadas para workflows autorizados. Um status de sucesso do provider não é suficiente para classificar um claim como provado. Devem existir artifacts esperados, referência do snapshot, ambiente, produtor, run ID e SHA-256, e o âmbito da evidence tem de corresponder à afirmação.

![Fluxo de qualidade e evidence](../../architecture/diagrams/current/render/quality-evidence-flow-a4.png)

# Cloud e deployment

A implementação inclui Terraform, GKE/Autopilot, Cloud Deploy, Artifact Registry, WIF e workflows de release/deployment. A formulação factual continua a ser: a implementação source está avançada, mas os artifacts fornecidos não provam uma signed release para o head final, um deployment de staging qualificado ou produção.

![Deployment e promoção](../../architecture/diagrams/current/render/deployment-and-promotion-a4.png)

# Fronteira científica e operacional

A evolução de engenharia não altera as limitações científicas. FWI, KBDI, o score NatureProtector e o proxy de contexto português continuam componentes candidatos de comparação técnica; não são apresentados como métodos oficiais, calibrados ou validados para utilização operacional.

![Proveniência e autoridade](../../architecture/diagrams/current/render/data-provenance-authority-a4.png)

# Sistema documental e portfolio visual

A documentação corrente passa a ter:

- entrada canónica em `docs/index.md`;
- documentos de current state por tema;
- classificação `CURRENT`, `HISTORICAL`, `PLANNED`, `GENERATED`, `EVIDENCE`, `EXPERIMENTAL` e `SUPERSEDED`;
- inventário e manifest documental;
- validação de links e triples source/render/sidecar;
- modelo Structurizr com ambientes e dynamic views;
- dez diagramas factualmente reconciliados em variantes web, A4 e 16:9;
- compêndio pessoal e referência rápida da defesa;
- portal HTML pesquisável.

# Mapa de integração no relatório

| Destino | Alteração necessária |
|---|---|
| Capítulo 4 | Acrescentar Operations Control Plane, fronteiras runner/provider, capabilities e vistas de ambiente |
| Capítulo 5 | Acrescentar ciclo de convergência documental e governação de fontes |
| Capítulo 6 | Acrescentar páginas da UI, catálogo de operações, workflows e callback |
| Capítulo 8 | Acrescentar dispatch fechado de quality/evidence e regra de promoção por artifacts com hash |
| Capítulo 9 | Discutir separação entre administração da aplicação e autoridade de release |
| Capítulo 10 | Retirar a Operations UI já implementada do trabalho futuro; manter proof runtime/cloud e validação científica |
| Capítulo 11 | Alargar a contribuição de auditabilidade sem reclamar produção provada |
| Anexo E | Atualizar componentes, endpoints, roles e estrutura do repositório |
| Anexo H | Atualizar quality/evidence e gates de execução ainda abertos |
| Anexo I | Atualizar administração e capabilities |
| Anexo J | Atualizar cloud/CD e a fronteira factual de proof |
| Anexo K | Substituir diagramas selecionados R13 por figuras atuais e screenshots atuais quando existirem |

# Controlos de linguagem

Usar: `implementado`, `verificado estaticamente`, `execução identificada`, `evidence histórica`, `snapshot atual`, `parcial`, `bloqueado`, `candidato`, `experimental`.

Não usar sem proof atual: `production-ready`, `live`, `real-time`, `validado`, `calibrado`, `alerta oficial`, `sistema operacional`, `dados reais`.

# Requisito editorial remanescente

A fonte editável do relatório atual de 379 páginas não foi incluída nos inputs. Uma operação editorial futura deve inserir a adenda LaTeX e as alterações por capítulo na fonte real, recompilar o relatório e executar revisão visual, bibliográfica e de rastreabilidade. Este pacote não altera silenciosamente um PDF sem a respetiva fonte.
