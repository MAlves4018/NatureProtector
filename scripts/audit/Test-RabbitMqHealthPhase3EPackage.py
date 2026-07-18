#!/usr/bin/env python3
"""Offline structural validation for RabbitMQ/health phase 3E."""
from __future__ import annotations
import json
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
    content = (ROOT / path).read_text(encoding="utf-8-sig")
    for needle in needles:
        if needle in content:
            raise AssertionError(f"{path}: forbidden stale text remains: {needle}")

def main() -> int:
    require(
        "src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs",
        "ManagementScheme",
        "ManagementHost",
        "ManagementPort",
        "ManagementUserName",
        "ManagementPassword",
        "ManagementCertificateAuthorityPath",
        "ManagementAllowInsecureHttp",
        "ManagementTimeoutSeconds",
        "GetEffectiveManagementHost",
    )
    require(
        "src/NatureProtector.Backoffice.Api/Configuration/RabbitMqManagementOptionsValidator.cs",
        "ManagementScheme must be either 'http' or 'https'",
        "ManagementAllowInsecureHttp=true",
        "PrivateCertificateAuthorityValidator.Create",
        "could not be loaded or validated",
    )
    require(
        "src/NatureProtector.Backoffice.Api/Configuration/RabbitMqManagementHttpClient.cs",
        'ClientName = "RabbitMqManagement"',
        "AllowAutoRedirect = false",
        "ServerCertificateCustomValidationCallback",
        "SetHandlerLifetime(TimeSpan.FromMinutes(2))",
        '"/api/queues"',
    )
    require(
        "src/NatureProtector.Backoffice.Api/Program.cs",
        "AddRabbitMqManagementHttpClient(builder.Configuration)",
    )
    require(
        "src/NatureProtector.Backoffice.Api/ControlPlane/Services/RuntimeObservabilityService.cs",
        "IOptions<RabbitMqOptions>",
        "RabbitMqManagementHttpClient.BuildQueuesUri(options)",
        "CreateClient(RabbitMqManagementHttpClient.ClientName)",
        "GetEffectiveManagementUserName()",
        "GetEffectiveManagementPassword()",
        "CreateClient();",
    )
    forbid(
        "src/NatureProtector.Backoffice.Api/ControlPlane/Services/RuntimeObservabilityService.cs",
        'new Uri($"http://{hostName}',
        'GetValue<int?>("RabbitMq:ManagementPort")',
        "CreateClient(nameof(RuntimeObservabilityService))",
    )
    require(
        "tests/NatureProtector.Backoffice.Api.Tests/RabbitMqManagementHttpClientTests.cs",
        "Dedicated_handler_accepts_private_ca_and_matching_hostname",
        "Dedicated_handler_rejects_wrong_private_ca",
        "Dedicated_handler_rejects_hostname_mismatch",
        "Dedicated_handler_rejects_expired_leaf_certificate",
        "TemporaryHttpsServer",
    )
    require(
        "tests/NatureProtector.Backoffice.Api.Tests/RabbitMqManagementOptionsValidatorTests.cs",
        "Validate_accepts_https_with_a_loadable_private_ca",
        "Validate_rejects_http_without_explicit_insecure_opt_in",
        "Validate_fails_closed_when_https_private_ca_is_missing",
    )
    require(
        "docker-compose.g1.yml",
        "RabbitMq__ManagementScheme: http",
        'RabbitMq__ManagementAllowInsecureHttp: "true"',
    )
    require(
        "infra/gcp/cloud-deploy/g8-1/api/service.yaml",
        "RabbitMq__ManagementScheme, value: https",
        "RabbitMq__ManagementCertificateAuthorityPath",
        "RabbitMq__ManagementTimeoutSeconds",
    )
    json.loads((ROOT / "src/NatureProtector.Backoffice.Api/appsettings.Development.json").read_text())
    print("PHASE3E_PACKAGE_STATIC_CHECK=PASS")
    return 0

if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"PHASE3E_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
