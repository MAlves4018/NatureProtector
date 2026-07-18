#!/usr/bin/env python3
"""Static verifier for the RabbitMQ/health phase-1 characterization package.

This verifier performs no network, Docker, .NET, database, or cloud operation.
It exists so the delta can be checked before Codex integrates it into another
working tree.
"""

from __future__ import annotations

import sys
from pathlib import Path


REQUIRED_FILES = {
    "tests/NatureProtector.IntegrationTests/Flow/DockerRabbitMqOperationalAuditTests.cs": [
        "PHASE1_RAW_GROWTH_REPRODUCED",
        "PHASE1_PARTIAL_NACK_REPRODUCED",
        "PHASE1_MANDATORY_PARTIAL_ROUTING_REPRODUCED",
        "PHASE1_MANDATORY_WRONG_DESTINATION_REPRODUCED",
        'Trait("Purpose", "OperationalAudit")',
    ],
    "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeHealthOperationalAuditTests.cs": [
        "PHASE3C_BACKOFFICE_READINESS_REMEDIATED",
        "PHASE3D_PREVENTION_READINESS_REMEDIATED",
        "database.DropAsync()",
        'Trait("Purpose", "OperationalAudit")',
    ],
    "tests/NatureProtector.IntegrationTests/TestInfrastructure/TemporaryRabbitMqVirtualHost.cs": [
        "SetQueuePolicyAsync",
        "ClearPolicyAsync",
        '["apply-to"] = "queues"',
    ],
    "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeProcessTests.cs": [
        "public sealed partial class DockerPublishedRuntimeProcessTests",
    ],
    "scripts/audit/Invoke-RabbitMqHealthPhase1Reproduction.ps1": [
        "NP_RUN_OPERATIONAL_AUDIT_PHASE1",
        '"Purpose=OperationalAudit"',
        "cloud_accessed = $false",
        "PHASE1_CHARACTERIZATION_COMPLETE",
    ],
}

FORBIDDEN_RUNNER_TOKENS = (
    "gcloud ",
    "terraform apply",
    "terraform destroy",
    "kubectl apply",
    "kubectl delete",
    "kubectl patch",
)


def find_repo_root(start: Path) -> Path:
    current = start.resolve()
    for candidate in (current, *current.parents):
        if candidate.joinpath("NatureProtector.sln").is_file():
            return candidate
    raise RuntimeError("NatureProtector.sln was not found above the verifier path")


def main() -> int:
    root = find_repo_root(Path(__file__).parent)
    failures: list[str] = []

    for relative_path, required_tokens in REQUIRED_FILES.items():
        path = root / relative_path
        if not path.is_file():
            failures.append(f"missing file: {relative_path}")
            continue

        text = path.read_text(encoding="utf-8")
        for token in required_tokens:
            if token not in text:
                failures.append(f"{relative_path}: missing token {token!r}")

    runner_path = root / "scripts/audit/Invoke-RabbitMqHealthPhase1Reproduction.ps1"
    if runner_path.is_file():
        lowered = runner_path.read_text(encoding="utf-8").lower()
        for token in FORBIDDEN_RUNNER_TOKENS:
            if token in lowered:
                failures.append(f"runner contains forbidden cloud/mutation token: {token!r}")

    if failures:
        print("PHASE1_PACKAGE_STATIC_CHECK=FAIL")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("PHASE1_PACKAGE_STATIC_CHECK=PASS")
    print(f"repository_root={root}")
    print(f"checked_files={len(REQUIRED_FILES)}")
    print("cloud_operations=none")
    return 0


if __name__ == "__main__":
    sys.exit(main())
