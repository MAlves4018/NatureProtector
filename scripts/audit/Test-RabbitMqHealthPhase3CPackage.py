#!/usr/bin/env python3
"""Offline structural validation for RabbitMQ/health phase 3C."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def require(path: str, *needles: str) -> None:
    target = ROOT / path
    if not target.is_file():
        raise AssertionError(f"missing file: {path}")

    content = target.read_text(encoding="utf-8")
    for needle in needles:
        if needle not in content:
            raise AssertionError(f"{path}: missing expected text: {needle}")


def forbid(path: str, *needles: str) -> None:
    target = ROOT / path
    content = target.read_text(encoding="utf-8")
    for needle in needles:
        if needle in content:
            raise AssertionError(f"{path}: forbidden stale text remains: {needle}")


def main() -> int:
    require(
        "src/NatureProtector.Backoffice.Api/Program.cs",
        "var healthChecks = builder.Services.AddHealthChecks();",
        "healthChecks.AddCheck<ControlPlaneDatabaseHealthCheck>",
        '"control-plane-postgres"',
        'tags: ["ready"]',
        "timeout: TimeSpan.FromSeconds(5)",
        'app.MapHealthChecks("/health/live", new HealthCheckOptions',
        "Predicate = _ => false",
        'app.MapHealthChecks("/health/ready", new HealthCheckOptions',
        'registration.Tags.Contains("ready")',
    )
    forbid(
        "src/NatureProtector.Backoffice.Api/Program.cs",
        'app.MapHealthChecks("/health/live");',
        'app.MapHealthChecks("/health/ready");',
    )
    require(
        "tests/NatureProtector.Backoffice.Api.Tests/BackofficeHealthRegistrationTests.cs",
        "Host_RegistersPostgresAsConditionalReadinessDependency",
        "Host_SeparatesProcessLivenessFromDependencyReadiness",
    )
    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeHealthOperationalAuditTests.cs",
        "BackofficeReadiness_BecomesUnhealthyAfterItsPostgresDatabaseIsDropped",
        "HttpStatusCode.ServiceUnavailable",
        "PHASE3C_BACKOFFICE_READINESS_REMEDIATED",
        "NP_RUN_BACKOFFICE_READINESS_PHASE3C",
    )
    forbid(
        "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeHealthOperationalAuditTests.cs",
        "PHASE1_BACKOFFICE_FALSE_READINESS_REPRODUCED",
        "CurrentBackofficeReadiness_RemainsHealthyAfterItsPostgresDatabaseIsDropped",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase1Reproduction.ps1",
        "PHASE3C_BACKOFFICE_READINESS_REMEDIATED",
        "PHASE1_CHARACTERIZATION_COMPLETE",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase3CValidation.ps1",
        "NP_RUN_BACKOFFICE_READINESS_PHASE3C",
        "BackofficeHealthRegistrationTests",
        "BackofficeReadiness_BecomesUnhealthyAfterItsPostgresDatabaseIsDropped",
        "PHASE3C_VALIDATION=PASS",
    )
    require(
        "src/NatureProtector.Backoffice.Api/README.md",
        "`/health/live` prova apenas que o processo HTTP está vivo",
        "`/health/ready` exige PostgreSQL",
    )

    print("PHASE3C_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"PHASE3C_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
