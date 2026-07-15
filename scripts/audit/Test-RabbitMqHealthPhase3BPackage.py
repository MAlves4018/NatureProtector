#!/usr/bin/env python3
"""Offline structural checks for RabbitMQ health remediation Phase 3B."""

from __future__ import annotations

from pathlib import Path
import sys


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(root: Path, relative: str) -> str:
    path = root / relative
    require(path.is_file(), f"missing file: {relative}")
    return path.read_text(encoding="utf-8-sig")


def main() -> int:
    root = Path(__file__).resolve().parents[2]

    read(root, "scripts/audit/Invoke-RabbitMqHealthPhase3BValidation.ps1")

    definition = read(
        root,
        "src/NatureProtector.Shared/Messaging/RabbitMqQueueDefinition.cs",
    )
    require(
        'public const string PrimaryWorkQueue = "PrimaryWorkQueue";' in definition,
        "primary work queue role is missing",
    )
    require(
        'public const string AuxiliaryDiagnosticQueue = "AuxiliaryDiagnosticQueue";'
        in definition,
        "auxiliary diagnostic queue role is missing",
    )
    require(
        "bool BlocksRuntimeHealth" in definition,
        "queue definition does not expose blocking semantics",
    )

    options = read(root, "src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs")
    require("GetQueueDefinitions()" in options, "queue definitions are not exposed")
    require(
        "GetEnabledQueueDefinitions()" in options,
        "effective enabled queue definitions are not exposed",
    )
    require(
        "RabbitMqQueueRoles.PrimaryWorkQueue" in options
        and "RabbitMqQueueRoles.AuxiliaryDiagnosticQueue" in options,
        "RabbitMqOptions does not assign stable queue roles",
    )
    require(
        "Enabled: ObservabilityRawEnabled" in options,
        "raw enabled state is not represented in the queue definition",
    )

    contracts = read(
        root,
        "src/NatureProtector.Backoffice.Api/ControlPlane/Contracts/RuntimeObservabilityContracts.cs",
    )
    for field in [
        "string QueueRole",
        "bool Enabled",
        "bool ConsumerRequired",
        "bool BlocksRuntimeHealth",
    ]:
        require(field in contracts, f"RabbitMQ metric contract is missing {field}")

    service = read(
        root,
        "src/NatureProtector.Backoffice.Api/ControlPlane/Services/RuntimeObservabilityService.cs",
    )
    require(
        "options.GetQueueDefinitions()" in service,
        "RuntimeObservabilityService does not use configured queue definitions",
    )
    require(
        "NatureProtectorRabbitMqTopology.Bindings" not in service,
        "RuntimeObservabilityService still uses the static topology catalogue",
    )
    require(
        "IsBlockingRuntimeQueue" not in service,
        "RuntimeObservabilityService still infers blocking behavior from queue names",
    )
    require(
        "queue.BlocksRuntimeHealth" in service,
        "RabbitMQ health does not use explicit queue blocking semantics",
    )
    require(
        "RabbitMqQueueRoles.PrimaryWorkQueue" in service,
        "Prevention proxy does not locate the primary queue by role",
    )
    require(
        '"rabbitmq_disabled_queue_present"' in service,
        "legacy disabled queue drift is not surfaced",
    )
    require(
        'private const string RabbitMqSource = "RabbitMQ Management API";' in service,
        "management source remains incorrectly tied to HTTP",
    )

    unavailable = read(
        root,
        "src/NatureProtector.Backoffice.Api/ControlPlane/Services/UnavailableRuntimeObservabilityService.cs",
    )
    require(
        "GetQueueDefinitions()" in unavailable,
        "unavailable observability does not preserve configured topology roles",
    )
    require(
        "NatureProtectorRabbitMqTopology.Bindings" not in unavailable,
        "unavailable observability still uses static topology",
    )

    program = read(root, "src/NatureProtector.Backoffice.Api/Program.cs")
    require(
        ".GetSection(RabbitMqOptions.SectionName)" in program,
        "disabled control-plane composition does not pass RabbitMQ configuration",
    )

    tests = read(
        root,
        "tests/NatureProtector.Backoffice.Api.Tests/RuntimeObservabilityServiceTests.cs",
    )
    for test_name in [
        "UsesEffectiveTopologyAndQueueRoles_WhenRawIsDisabled",
        "ReportsDisabledDurableQueue_WhenItStillExists",
        "MarksEnabledRawQueueUnavailable_WhenItIsMissing",
        "KeepsPrimaryHealthy_WhenEnabledAuxiliaryMetricsAreUnavailable",
        "UsesPrimaryRoleForCustomQueueName",
        "DoesNotDegradeRabbitMq_ForEnabledAuxiliaryBacklog",
    ]:
        require(test_name in tests, f"missing focused observability test: {test_name}")

    openapi = read(
        root,
        "tests/NatureProtector.Backoffice.Api.Tests/OpenApiSemanticTests.cs",
    )
    require(
        'AssertSchemaProperty(queueMetric, "queueRole"' in openapi,
        "OpenAPI queueRole contract test is missing",
    )
    require(
        'AssertSchemaProperty(queueMetric, "blocksRuntimeHealth"' in openapi,
        "OpenAPI blocking-role contract test is missing",
    )

    runtime_types = read(root, "webUI/src/app/types/runtime.ts")
    require("queueRole:" in runtime_types, "webUI queue role type is missing")
    require("blocksRuntimeHealth: boolean" in runtime_types, "webUI blocking field is missing")

    technical_surfaces = read(root, "webUI/src/app/technicalSurfaces.ts")
    require(
        "findQueueByRole(rabbitMq, 'PrimaryWorkQueue')" in technical_surfaces,
        "webUI still locates ingestion by a hardcoded queue name",
    )
    require(
        "findQueueByRole(rabbitMq, 'AuxiliaryDiagnosticQueue')" in technical_surfaces,
        "webUI still locates raw by a hardcoded queue name",
    )
    require(
        "if (!queue.enabled)" in technical_surfaces,
        "disabled queues are not represented as non-ready UI evidence",
    )

    print("PHASE3B_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"PHASE3B_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
