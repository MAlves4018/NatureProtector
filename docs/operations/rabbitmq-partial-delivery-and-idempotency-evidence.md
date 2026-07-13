# RabbitMQ partial delivery and idempotency evidence

## Purpose

This document defines the Phase 3G proof for the case where a single RabbitMQ
publish is routed to more than one durable queue, one queue accepts the message,
another queue rejects it, and publisher confirms report failure.

The proof does not claim transactional atomicity across RabbitMQ queues. It
makes the ambiguity explicit and verifies that retrying the same logical event
does not duplicate durable Prevention effects.

## Delivery classifications

The Simulator publisher distinguishes two failure classes.

### `RabbitMqUnroutableMessageException`

`mandatory=true` produced a matching `basic.return`.

```text
certainty = NotDeliveredToAnyQueue
possiblePartialDelivery = false
```

No queue accepted the message under the requested exchange and routing key.

### `RabbitMqPublishOutcomeUnknownException`

Publisher confirms failed, timed out, or nacked without a matching
`basic.return`.

```text
certainty = UnknownPossiblePartialDelivery
possiblePartialDelivery = true
```

At least one destination may already have accepted the message. A retry must
reuse the same `MessageId`, which for readings is the envelope `EventId`.
Generating a new EventId changes the operation from retry to a second logical
event and defeats inbox idempotency.

## Runtime outcome

An unhandled publish failure must produce all of the following:

1. the `SimulationRun` transitions from `Running` to `Failed`;
2. `EndedAt` is persisted;
3. the Simulator process exits with a non-zero exit code;
4. logs include the concrete failure type and
   `PossiblePartialDelivery=true|false`;
5. no automatic publisher retry is performed inside the current synchronous
   publisher.

The absence of an automatic publisher retry is deliberate. The publisher
cannot know whether the primary queue accepted the event. Retry ownership must
preserve the EventId and remain observable.

## End-to-end fault injection

The Docker proof uses isolated PostgreSQL and RabbitMQ resources:

1. enable the auxiliary raw queue;
2. apply `max-length=1` and `overflow=reject-publish` only to the isolated raw
   queue;
3. fill the raw queue through the default exchange, without touching ingestion;
4. publish one reading through `np.events`;
5. observe `RabbitMqPublishOutcomeUnknownException`;
6. prove the primary ingestion queue still delivered and Prevention processed
   the event;
7. publish the exact same envelope/EventId again;
8. prove the inbox acknowledged the duplicate without a second processing
   attempt or duplicate projections.

Expected durable result for the target EventId:

```text
InboxEvents = 1, status Processed, attempt_count 1
ProcessingAttempts = 1, outcome Succeeded
AcceptedReadingLogs = 1
RiskAssessmentLogs = 1
AreaRiskSnapshotLogs = 1
CellOperationalStates = 1
AreaOperationalStates = 1
```

The published-process proof additionally requires:

```text
Simulator exit code != 0
SimulationRun status = Failed
SimulationRun EndedAt != null
primary durable effects = exactly one
raw queue retains only its capacity filler
```

## Commands

Static and unit validation:

```powershell
pwsh -NoProfile -File `
  .\scripts\audit\Invoke-RabbitMqHealthPhase3GValidation.ps1
```

Docker end-to-end proof:

```powershell
pwsh -NoProfile -File `
  .\scripts\audit\Invoke-RabbitMqHealthPhase3GValidation.ps1 `
  -IncludeDockerIntegration
```

Expected markers:

```text
PHASE3G_PACKAGE_STATIC_CHECK=PASS
PHASE3G_TYPED_PUBLISH_OUTCOMES_AND_PROCESS_EXIT_PROVED
PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED
PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED
PHASE3G_VALIDATION=PASS
```

## Boundaries

Phase 3G does not introduce an outbox, distributed transaction, publisher retry
loop, or exactly-once claim. It proves at-least-once handling with EventId-based
consumer idempotency for this failure path.

The proof does not close failures that occur after Prevention projection writes
but before inbox completion. That is a separate fault-injection path and remains
part of the broader transaction/idempotency investigation.


### Controlled Validation

The controlled-validation runner uses the same non-zero process outcome and the
orchestrator marks a registered Running simulation run as Failed when message
publication fails. This prevents a controlled-validation publish failure from
leaving `EndedAt = NULL`.
