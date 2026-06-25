# G8.1 Terraform state bootstrap

One-time owner-local bootstrap for the new, non-CN platform project after G10. It creates only the protected GCS bucket used by all later roots.

1. Run `terraform init -backend=false` and apply locally with the explicit owner confirmation, initially keeping the bootstrap state local and protected.
2. Re-run `terraform init -migrate-state` for this root using the newly created bucket and prefix `bootstrap/g8-1`.
3. Initialise the platform root with prefix `platform/g8-1`.
4. Initialise the reusable environment root with distinct prefixes `environments/staging/g8-1` and `environments/production/g8-1`.

The bucket is versioned, private, retained and protected from Terraform destruction. No billing account, project creation or secret value is stored in this root.
