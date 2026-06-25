# G8.1 platform control plane

This root defines the production delivery control plane in a new, non-course GCP project. It is deliberately disabled by default and must not be applied before G10 integration and owner approval.

It creates Artifact Registry, the evidence bucket, workflow-scoped WIF identities, Cloud Deploy targets and the API/frontend/Prevention pipelines. Staging is verified automatically; production requires approval and progressive delivery.

No billing account, project creation, secret payload or service-account key is stored here.


## Non-circular bootstrap order

1. Enable `create_delivery_control_plane` only. This creates the central registry, evidence store and workflow/Cloud Deploy execution identities.
2. Apply the staging and production environment roots with those identity outputs.
3. Feed the private worker-pool and dedicated GKE node service-account outputs back into this root, enable `create_delivery_pipelines`, and apply again.

This is a phased application of one canonical architecture, not a single-project/split-project alternative. No project or resource is created before G10 and explicit owner approval.
