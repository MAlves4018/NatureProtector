#!/usr/bin/env python3
"""Offline structural checks for RabbitMQ health remediation Phase 3A."""

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

    read(root, "scripts/audit/Invoke-RabbitMqHealthPhase3AValidation.ps1")

    options = read(root, "src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs")
    require(
        "public bool ObservabilityRawEnabled { get; init; }" in options,
        "RabbitMqOptions does not expose ObservabilityRawEnabled",
    )
    require("GetQueueNames()" in options, "RabbitMqOptions does not expose effective queues")
    require(
        "if (!ObservabilityRawEnabled)" in options
        or (
            "Enabled: ObservabilityRawEnabled" in options
            and "GetEnabledQueueDefinitions()" in options
        ),
        "effective topology is not conditional on ObservabilityRawEnabled",
    )

    declarers = [
        "src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs",
        "src/NatureProtector.Simulator.Host/ControlledValidation/RabbitMqControlledValidationMessagePublisher.cs",
        "src/NatureProtector.Prevention.Host/PreventionWorker.cs",
    ]
    for relative in declarers:
        content = read(root, relative)
        require(
            "GetQueueNames()" in content,
            f"{relative} does not use the effective queue list",
        )
        require(
            "GetBindings()" in content,
            f"{relative} does not use the effective binding list",
        )
        require(
            "queue: _options.ObservabilityRawQueueName" not in content
            and "queue: options.ObservabilityRawQueueName" not in content,
            f"{relative} still declares the raw queue unconditionally",
        )

    compose = read(root, "docker-compose.g1.yml")
    require(
        compose.count('RabbitMq__ObservabilityRawEnabled: "false"') >= 3,
        "Compose does not keep raw disabled for all current runtime services",
    )

    simulator_dockerfile = read(root, "src/NatureProtector.Simulator.Host/Dockerfile")
    prevention_dockerfile = read(root, "src/NatureProtector.Prevention.Host/Dockerfile")
    require(
        "RabbitMq__ObservabilityRawEnabled=false" in simulator_dockerfile,
        "Simulator image does not default raw to false",
    )
    require(
        "RabbitMq__ObservabilityRawEnabled=false" in prevention_dockerfile,
        "Prevention image does not default raw to false",
    )

    options_tests = read(
        root,
        "tests/NatureProtector.Shared.Tests/Configuration/RabbitMqOptionsTests.cs",
    )
    require(
        "Assert.False(options.ObservabilityRawEnabled)" in options_tests,
        "default-disabled options test is missing",
    )
    require(
        "GetBindings_IncludesConfiguredRawQueue_WhenExplicitlyEnabled" in options_tests,
        "explicit-enable options test is missing",
    )

    publisher_tests = read(
        root,
        "tests/NatureProtector.Simulator.Host.Tests/Publishing/RabbitMqReadingPublisherBehaviorTests.cs",
    )
    controlled_tests = read(
        root,
        "tests/NatureProtector.Simulator.Host.Tests/Publishing/RabbitMqControlledValidationMessagePublisherBehaviorTests.cs",
    )
    prevention_tests = read(
        root,
        "tests/NatureProtector.Prevention.Host.Tests/Processing/PreventionWorkerTests.cs",
    )
    for name, content in [
        ("publisher", publisher_tests),
        ("controlled validation publisher", controlled_tests),
        ("prevention", prevention_tests),
    ]:
        require(
            "DeclareTopology_DeclaresOnlyPrimaryQueue_WhenRawIsDisabled" in content,
            f"{name} default-disabled topology test is missing",
        )
        require(
            "DeclareTopology_DeclaresRawQueue_WhenExplicitlyEnabled" in content,
            f"{name} explicit-enable topology test is missing",
        )

    docker_settings = read(
        root,
        "tests/NatureProtector.IntegrationTests/TestInfrastructure/DockerIntegrationSettings.cs",
    )
    require(
        "bool observabilityRawEnabled = false" in docker_settings,
        "Docker integration settings do not preserve the production default",
    )

    docker_publisher_tests = read(
        root,
        "tests/NatureProtector.IntegrationTests/Flow/DockerRabbitMqPublisherTests.cs",
    )
    require(
        "RabbitMqReadingPublisher_DoesNotCreateRawQueue_WhenDisabled_OnRealRabbitMq" in docker_publisher_tests,
        "real-broker default-disabled test is missing",
    )
    require(
        "observabilityRawEnabled: true" in docker_publisher_tests,
        "tests that intentionally inspect raw do not explicitly enable it",
    )

    phase1_compatibility = root / (
        "tests/NatureProtector.IntegrationTests/Flow/"
        "DockerRabbitMqOperationalAuditTests.cs"
    )
    if phase1_compatibility.exists():
        content = phase1_compatibility.read_text(encoding="utf-8-sig")
        require(
            content.count("observabilityRawEnabled: true") >= 4,
            "Phase 1 raw characterization tests were not explicitly opted in",
        )

    process_tests = root / (
        "tests/NatureProtector.IntegrationTests/Flow/"
        "DockerPublishedRuntimeProcessTests.cs"
    )
    if process_tests.exists():
        content = process_tests.read_text(encoding="utf-8-sig")
        require(
            '["RabbitMq__ObservabilityRawEnabled"] = '
            "rabbitMq.ObservabilityRawEnabled.ToString()" in content,
            "published-process environment does not propagate the new option",
        )

    print("PHASE3A_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"PHASE3A_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
