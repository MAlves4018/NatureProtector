#!/usr/bin/env python3
"""Offline structural validation for RabbitMQ/health phase 3G."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path: str) -> str:
    target = ROOT / path
    if not target.is_file():
        raise AssertionError(f"missing file: {path}")
    return target.read_text(encoding="utf-8-sig")


def require(path: str, *needles: str) -> str:
    content = read(path)
    for needle in needles:
        if needle not in content:
            raise AssertionError(f"{path}: missing expected text: {needle}")
    return content


def main() -> int:
    exceptions = require(
        "src/NatureProtector.Simulator.Host/Publishing/RabbitMqPublishExceptions.cs",
        "RabbitMqPublishDeliveryCertainty",
        "NotDeliveredToAnyQueue",
        "UnknownPossiblePartialDelivery",
        "RabbitMqUnroutableMessageException",
        "RabbitMqPublishOutcomeUnknownException",
        "PossiblePartialDelivery",
        "PrimaryQueueName",
    )
    if "exactly once" in exceptions.lower():
        raise AssertionError("publish exception contract must not claim exactly once")

    require(
        "src/NatureProtector.Simulator.Host/Publishing/RabbitMqPublishGuarantees.cs",
        "Exception? confirmFailure = null",
        "RabbitMqUnroutableMessageException",
        "RabbitMqPublishOutcomeUnknownException",
        "one or more queues may already have accepted",
        "preserve the same MessageId/EventId",
        "primaryQueueName",
    )
    require(
        "src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs",
        "_options.IngestionReadingsQueueName",
    )
    require(
        "src/NatureProtector.Simulator.Host/ControlledValidation/RabbitMqControlledValidationMessagePublisher.cs",
        "_options.IngestionReadingsQueueName",
    )

    require(
        "src/NatureProtector.Simulator.Host/Services/ISimulatorProcessExitCode.cs",
        "ISimulatorProcessExitCode",
        "Environment.ExitCode = 1",
    )
    require(
        "src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs",
        "ISimulatorProcessExitCode processExitCode",
        "processExitCode.MarkFailure()",
        "PossiblePartialDelivery={PossiblePartialDelivery}",
        "exception is RabbitMqPublishException",
    )
    require(
        "src/NatureProtector.Simulator.Host/Program.cs",
        "AddSingleton<ISimulatorProcessExitCode, EnvironmentSimulatorProcessExitCode>()",
    )
    require(
        "src/NatureProtector.Simulator.Host/ControlledValidation/ControlledValidationRunner.cs",
        "ISimulatorProcessExitCode processExitCode",
        "processExitCode.MarkFailure()",
        "PossiblePartialDelivery={PossiblePartialDelivery}",
    )
    require(
        "src/NatureProtector.Simulator.Host/ControlledValidation/ControlledValidationOrchestrator.cs",
        "run.Status == SimulationRunStatus.Running",
        "run.Fail(DateTimeOffset.UtcNow)",
        "CancellationToken.None",
    )

    require(
        "tests/NatureProtector.Simulator.Host.Tests/Publishing/RabbitMqPublishGuaranteesTests.cs",
        "WrapsConfirmFailure_AsAmbiguousPossiblePartialDelivery",
        "PrioritizesDefiniteBasicReturn_OverConfirmFailure",
        "RabbitMqPublishOutcomeUnknownException",
        "UnknownPossiblePartialDelivery",
    )
    require(
        "tests/NatureProtector.Simulator.Host.Tests/Services/SimulationRunnerTests.cs",
        "RecordingSimulatorProcessExitCode",
        "Assert.True(processExitCode.FailureMarked)",
        "Assert.False(processExitCode.FailureMarked)",
    )
    require(
        "tests/NatureProtector.Simulator.Host.Tests/ControlledValidation/ControlledValidationOrchestratorTests.cs",
        "PublisherFailure_MarksRegisteredRunFailed",
        "SimulationRunStatus.Failed",
        "Assert.NotNull(runStore.Records[2].EndedAt)",
    )
    require(
        "tests/NatureProtector.Simulator.Host.Tests/ControlledValidation/ControlledValidationRunnerTests.cs",
        "RecordingSimulatorProcessExitCode",
        "Assert.True(processExitCode.FailureMarked)",
        "Assert.False(processExitCode.FailureMarked)",
    )

    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerRabbitMqConsumerPipelineTests.cs",
        "public sealed partial class DockerRabbitMqConsumerPipelineTests",
        "bool observabilityRawEnabled = false",
    )
    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerRabbitMqPartialDeliveryEndToEndTests.cs",
        "NP_RUN_OPERATIONAL_AUDIT_PHASE3G",
        "PartialNack_PrimaryProcessesOnce_AndSameEventIdRetryIsIdempotent",
        "RabbitMqPublishOutcomeUnknownException",
        "AssertSingleProcessedEffectAsync",
        "AssertSingleInboxAttemptAsync",
        "PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED",
    )
    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimePartialDeliveryOperationalAuditTests.cs",
        "PublishedSimulator_PartialNack_ExitsNonZero_MarksRunFailed_WhilePrimaryProcessesOnce",
        "Assert.NotEqual(0, simulatorExitCode)",
        "SimulationRunStatus.Failed",
        "PossiblePartialDelivery=True",
        "PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED",
    )
    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerRabbitMqOperationalAuditTests.cs",
        "Assert.IsType<RabbitMqPublishOutcomeUnknownException>",
        "PossiblePartialDelivery",
    )

    contract = json.loads(read("config/operations/rabbitmq-health-contract.json"))
    if contract.get("status") != "IMPLEMENTED_NOT_PROVED_PHASE3G":
        raise AssertionError("contract status is not Phase 3G")
    semantics = contract["rabbitmq"]["delivery_semantics"]
    if semantics["ambiguous_certainty"] != "UnknownPossiblePartialDelivery":
        raise AssertionError("ambiguous delivery certainty is incorrect")
    if not semantics["retry_must_preserve_event_id"]:
        raise AssertionError("retry must preserve EventId")
    if semantics["automatic_publisher_retry_enabled"]:
        raise AssertionError("automatic publisher retry must remain disabled")
    if semantics["exactly_once_claim"]:
        raise AssertionError("exactly-once claim must remain false")
    if not semantics["simulator_failure_exit_code_must_be_nonzero"]:
        raise AssertionError("Simulator failure exit code contract is missing")

    require(
        "docs/operations/rabbitmq-partial-delivery-and-idempotency-evidence.md",
        "RabbitMqPublishOutcomeUnknownException",
        "retry must\nreuse the same `MessageId`",
        "Simulator exit code != 0",
        "PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED",
        "does not introduce an outbox",
    )
    require(
        "docs/decisions/ADR-RMQ-01-bounded-auxiliary-queue-and-topology-ownership.md",
        "## Nota de implementação Phase 3G",
        "IMPLEMENTED_NOT_PROVED_PHASE3G",
    )
    require(
        "docs/contracts/rabbitmq-runtime-topology-and-delivery-contract.md",
        "## Contrato Phase 3G",
        "UnknownPossiblePartialDelivery",
        "não afirma exactly-once global",
    )
    require(
        "docs/operations/rabbitmq-health-remediation-rollout-runbook.md",
        "## 15. Prova Phase 3G",
        "Invoke-RabbitMqHealthPhase3GValidation.ps1",
        "fault injection local e isolada",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase3GValidation.ps1",
        "PHASE3G_PACKAGE_STATIC_CHECK=PASS",
        "PHASE3G_TYPED_PUBLISH_OUTCOMES_AND_PROCESS_EXIT_PROVED",
        "PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED",
        "PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED",
        "PHASE3G_VALIDATION=PASS",
    )

    print("PHASE3G_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AssertionError, KeyError, json.JSONDecodeError) as exc:
        print(f"PHASE3G_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
