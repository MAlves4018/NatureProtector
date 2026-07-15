#!/usr/bin/env python3
"""Offline structural validation for RabbitMQ/health phase 3D."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def require(path: str, *needles: str) -> None:
    target = ROOT / path
    if not target.is_file():
        raise AssertionError(f"missing file: {path}")

    content = target.read_text(encoding="utf-8-sig")
    for needle in needles:
        if needle not in content:
            raise AssertionError(f"{path}: missing expected text: {needle}")


def forbid(path: str, *needles: str) -> None:
    target = ROOT / path
    content = target.read_text(encoding="utf-8-sig")
    for needle in needles:
        if needle in content:
            raise AssertionError(f"{path}: forbidden stale text remains: {needle}")


def main() -> int:
    require(
        "src/NatureProtector.Prevention.Host/Program.cs",
        "var healthChecks = builder.Services.AddHealthChecks()",
        "AddCheck<PreventionReadinessHealthCheck>",
        '"prevention-ready"',
        'tags: ["ready"]',
        "if (preventionHostOptions.PipelinePersistenceEnabled)",
        "healthChecks.AddCheck<PreventionDatabaseHealthCheck>",
        '"prevention-postgres"',
        "timeout: TimeSpan.FromSeconds(5)",
        'app.MapHealthChecks("/health/live", new HealthCheckOptions',
        "Predicate = _ => false",
        'app.MapHealthChecks("/health/ready", new HealthCheckOptions',
        'registration.Tags.Contains("ready")',
    )
    forbid(
        "src/NatureProtector.Prevention.Host/Program.cs",
        'app.MapHealthChecks("/health/ready");',
    )
    require(
        "src/NatureProtector.Prevention.Host/Health/PreventionDatabaseHealthCheck.cs",
        "IDbContextFactory<NatureProtectorControlDbContext>",
        "Database.CanConnectAsync(cancellationToken)",
        "PostgreSQL prevention readiness check failed.",
    )
    require(
        "tests/NatureProtector.Prevention.Host.Tests/Health/PreventionDatabaseHealthCheckTests.cs",
        "CheckHealthAsync_ReturnsHealthy_WhenDatabaseAcceptsConnections",
        "CheckHealthAsync_ReturnsUnhealthy_WhenFactoryCannotCreateContext",
    )
    require(
        "tests/NatureProtector.Prevention.Host.Tests/Health/PreventionHealthRegistrationTests.cs",
        "Host_RegistersRabbitMqAndConditionalPostgresReadinessChecks",
        "Host_SeparatesProcessLivenessFromDependencyReadiness",
    )
    require(
        "tests/NatureProtector.IntegrationTests/TestInfrastructure/TemporaryPostgresDatabase.cs",
        "public async Task RecreateAsync",
        'CREATE DATABASE "{DatabaseName}";',
        "Database.MigrateAsync(cancellationToken)",
    )
    require(
        "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeHealthOperationalAuditTests.cs",
        "PreventionReadiness_BecomesUnhealthyAfterPostgresDrops_AndRecoversAfterRecreation",
        "NP_RUN_PREVENTION_READINESS_PHASE3D",
        "HttpStatusCode.ServiceUnavailable",
        "database.RecreateAsync()",
        "PHASE3D_PREVENTION_READINESS_REMEDIATED",
        "PreventionReadiness_DoesNotRequirePostgres_WhenPersistenceIsDisabled",
        "PHASE3D_PREVENTION_IN_MEMORY_READINESS_PROVED",
    )
    forbid(
        "tests/NatureProtector.IntegrationTests/Flow/DockerPublishedRuntimeHealthOperationalAuditTests.cs",
        "CurrentPreventionReadiness_RemainsHealthyAfterItsPostgresDatabaseIsDropped",
        "PHASE1_PREVENTION_FALSE_READINESS_REPRODUCED",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase1Reproduction.ps1",
        "PHASE3D_PREVENTION_READINESS_REMEDIATED",
        "PHASE1_CHARACTERIZATION_COMPLETE",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase3DValidation.ps1",
        "NP_RUN_PREVENTION_READINESS_PHASE3D",
        "PreventionDatabaseHealthCheckTests",
        "PreventionHealthRegistrationTests",
        "FullyQualifiedName~PreventionReadiness_",
        "PHASE3D_VALIDATION=PASS",
    )
    require(
        "src/NatureProtector.Prevention.Host/README.md",
        "`/health/live` prova apenas que o processo da Prevention está vivo",
        "`/health/ready` exige o consumer RabbitMQ",
        "também exige PostgreSQL quando `PipelinePersistenceEnabled = true`",
    )

    print("PHASE3D_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"PHASE3D_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
