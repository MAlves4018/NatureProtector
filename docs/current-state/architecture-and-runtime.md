---
id: NP-CURRENT-ARCH
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Architecture and Runtime

## Architectural shape

NatureProtector is split into a data/runtime plane, a user/control plane and an engineering operations plane.

- **Simulator.Host** resolves a scenario, produces deterministic or degraded readings and publishes `SensorReadingProduced` events.
- **RabbitMQ** transports reading events.
- **Prevention.Host** validates envelopes and semantics, materialises a durable inbox, applies retry/quarantine rules, evaluates eligibility and candidate risk, then updates durable projections.
- **PostgreSQL** is the main source of truth for control data, runs, inbox state, attempts, rejections, assessments and operational projections.
- **InfluxDB and Grafana** support temporal observability; they do not replace PostgreSQL authority.
- **Backoffice.Api** exposes runtime, identity, evidence and engineering-operation APIs.
- **webUI** provides role-aware task views and never receives provider credentials.
- **GitHub Actions and cloud runners** execute selected engineering operations outside the Web process.

## Runtime path

```text
Scenario/run specification
  -> Simulator.Host
  -> RabbitMQ event envelope
  -> PreventionWorker
  -> durable inbox
  -> semantic validation and normalisation
  -> quality and eligibility
  -> candidate scoring/comparison
  -> PostgreSQL logs and projections
  -> API/UI and temporal observability
```

## Failure semantics

The runtime differentiates transport failure, malformed envelopes, semantic invalidity, transient processing errors, exhausted retries, quarantine and projection failure. The intended safety property is: **absence or failure must not be silently converted into low risk**.

## Runtime orchestration

Local runs can be launched by the Backoffice run orchestrator. Cloud-oriented orchestration includes Cloud Run job abstractions and evidence sinks, but availability and proof depend on environment configuration. Simulation orchestration and engineering deployment operations share audit patterns but remain separate domain concepts.

## Deployment views

The current model contains three environments:

- **Local/development** - workstation services plus Docker Compose dependencies.
- **Staging** - isolated GCP project/environment used for qualification before production.
- **Production** - separately controlled target that requires verified release, staging evidence and explicit approval.

See the [diagram portfolio](../architecture/diagrams/current/README.md) and the updated [Structurizr model](../structurizr/workspace.dsl).
