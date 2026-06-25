# G9 conflict and convergence register

| Conflict | Resolution | Status |
|---|---|---|
| Multiple development/staging/pilot Terraform roots | Removed; G8.1 reusable environment root is canonical | CLOSED_STATICALLY |
| Multiple RabbitMQ/GKE generations | Removed G6/G7 manifests; G8.1 Kubernetes base/overlays are canonical | CLOSED_STATICALLY |
| Multiple release and deployment workflows | Removed G3–G8 predecessors; G8.1 release/deploy/promote/teardown remain | CLOSED_STATICALLY |
| Vulnerable original G8 qualification workflows | Removed entirely; G8.2 chain is canonical | CLOSED_STATICALLY |
| Duplicate release/qualification contracts | Removed superseded G3–G8 contracts | CLOSED_STATICALLY |
| Local baseline versus cloud mode | Local remains default; cloud creation is explicit and disabled by default | CLOSED_STATICALLY |
| Public GitHub baseline versus owner archive | G9 uses the owner archive and records public repository identity; exact remote byte identity remains an owner/G10 gate | OPEN_OWNER_GATE |
| Runtime GCP proof | No projects exist before G10 by owner decision | BLOCKED_BY_SEQUENCE |
