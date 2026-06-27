# NatureProtector staging delivery platform

This Terraform root manages only the persistent delivery foundation required
for the ephemeral staging environment in the existing
`natureprotector-500518` project.

It reuses:

- the `np-releases` immutable Artifact Registry repository;
- the existing GitHub Workload Identity Federation configuration;
- `np-cd-release`;
- `np-cd-deploy`;
- the canonical Terraform state bucket;
- a dedicated G8.2 evidence bucket, isolated from Terraform state.

Execution is split into two ordered stages:

1. `create_delivery_control_plane=true` and
   `create_delivery_pipelines=false` creates the staging execution identity
   and grants the deployment authority required for the environment root.
2. After the staging runtime and private worker pool exist,
   `create_delivery_pipelines=true` creates only the staging targets and the
   three staging delivery pipelines.

No production resources, identities, targets, pipelines or automations are
managed by this root.

## State ownership boundary

This root is the sole owner of project-level Google Cloud API enablement and
project-level IAM bindings for `np-cd-deploy`. The environment root owns the
staging runtime resources, runtime service accounts and resource-level IAM
bindings on those accounts. The platform control-plane stage must be applied
before any environment apply.

## G8.2 evidence boundary

The platform root creates `np-g82-evidence-22505444922` only during the control-plane
stage. It is not a Terraform backend and it must never contain state files.
The bucket has uniform bucket-level access, public-access prevention,
versioning and a 365-day retention policy. `np-cd-deploy` receives
bucket-scoped object administration and metadata-view permissions only.
