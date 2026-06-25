# G9 — Repository Convergence and Cloud Delivery Integration Candidate

## Purpose

G9 converges the cumulative cloud work onto the owner-provided NatureProtector baseline that corresponds structurally to the public `MAlves4018/NatureProtector` repository. It does not create GCP resources, change Git history or claim runtime proof.

## Canonical active cloud stack

Only the following implementation generations remain active:

- **G8.1** — production cloud architecture and Continuous Delivery;
- **G8.2** — runtime qualification, chain of custody, independent review and signed authorization.

Earlier G2–G8 Terraform roots, workflows, Kubernetes manifests, deployment scripts and duplicated contracts were removed from the integration candidate. Their decisions are represented by the surviving G8.1/G8.2 implementation and by the G9 removal inventory in the evidence bundle.

## Integration rules

- the local baseline remains the default supported execution mode;
- cloud resource creation remains disabled by default;
- no CN project, billing account or academic credit is referenced by deployable configuration;
- `.env.example`, shared event contracts, messaging semantics, risk scoring and existing migrations are preserved;
- the single added migration is limited to durable Cloud Run Job orchestration state;
- production authorization and production deployment remain false;
- projects `platform`, `staging` and `production` are created only after G10 and owner integration.

## Canonical flow

```text
Pull request
  -> engineering, security and cloud policy gates
  -> immutable image build and attestations
  -> staging deployment and functional verification
  -> production approval and canary/verified rollout
  -> one-week runtime qualification
  -> sealed evidence and immutable archive
  -> independent signed review
  -> separate signed authorization
```

## Scope boundary

G9 is an **integration candidate**, not a cloud execution. It proves that the cumulative change set has one active architecture, one active CD path and one active qualification path. G10 must package the semantic integration mission for Codex against the owner's canonical workspace.
