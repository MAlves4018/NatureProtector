# Phase 7 — CI/CD workflow convergence

This phase removes duplicate standard validation from the automatic staging path, centralizes the three standard staging operations, and makes the signed release artifact hand-off explicit.

## Authorities

- `ci.yml` is the automatic standard validation authority for pushes to `master`.
- `cd-staging.yml` starts after a successful `CI` workflow run; manual dispatch still executes `_validate.yml` itself.
- `_staging-operation.yml` owns common checkout, toolchain, cloud authentication, and staging operation dispatch.
- `_release.yml` uploads `standard-cd-release`; `_deploy.yml` and `_qualify.yml` download that exact artifact before reading the manifest.

## Preserved boundaries

The G8.1 production policy, promotion, and controlled teardown workflows remain independent. They do not call the standard staging operation workflow and retain their dedicated environments, confirmations, identities, and evidence rules.

## Validation

Run:

```bash
python tools/workflow-audit/validate.py --repo .
```

The validator checks workflow count, local call targets, external action pinning, trigger convergence, artifact hand-off, staging wrapper contracts, and production policy boundaries.
