#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml

try:
    import hcl2
except ImportError:  # pragma: no cover
    hcl2 = None

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
checks = 0


def check(ok: bool, message: str) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(message)


required = [
    'README.md',
    'docs/implementation/cloud/g9-repository-convergence.md',
    'docs/operations/g9-preintegration-runbook.md',
    'docs/evidence/g9-convergence-state.json',
    'docs/evidence/g9-conflict-register.md',
    'docs/history/cloud-evolution-summary.md',
    'infra/gcp/integration/g9-canonical-paths.json',
    'scripts/cloud/Test-G81Static.py',
    'scripts/cloud/Test-G82Static.py',
    'scripts/cloud/Test-G82Adversarial.py',
    'scripts/cloud/Test-G103Static.py',
]
for item in required:
    check((ROOT / item).is_file(), f'missing:{item}')

for dirname in [
    'g8-1-state-bootstrap', 'g8-1-platform', 'g8-1-environment'
]:
    check((ROOT / 'infra/gcp/terraform' / dirname).is_dir(), f'missing-terraform-root:{dirname}')

for dirname in [
    'bootstrap', 'dev', 'staging', 'g5-runtime', 'g6-pilot',
    'g7-identity-bootstrap', 'g7-production-candidate'
]:
    check(not (ROOT / 'infra/gcp/terraform' / dirname).exists(), f'legacy-terraform-root:{dirname}')

legacy_workflows = [
    'gcp-build-images.yml', 'gcp-deploy-dev.yml', 'gcp-foundations-plan.yml',
    'gcp-foundations-validation.yml', 'gcp-g5-distributed-chain.yml',
    'gcp-g6-cleanup.yml', 'gcp-g6-pilot-hardening.yml',
    'gcp-g6-pilot-infrastructure.yml', 'gcp-g6-soak.yml',
    'gcp-g7-deploy.yml', 'gcp-g7-disaster-recovery.yml',
    'gcp-g7-infrastructure.yml', 'gcp-g7-operations.yml',
    'gcp-g8-runtime-qualification.yml', 'gcp-g8-submit-signed-governance.yml',
    'gcp-g8-independent-review.yml', 'gcp-g8-authorization-request.yml',
    'gcp-g8-authorization-verification.yml', 'gcp-promote-staging.yml',
    'gcp-staging-plan.yml'
]
for name in legacy_workflows:
    check(not (ROOT / '.github/workflows' / name).exists(), f'legacy-workflow:{name}')

check(not (ROOT / 'infra/gcp/kubernetes/g6').exists(), 'legacy-kubernetes:g6')
check(not (ROOT / 'infra/gcp/kubernetes/g7').exists(), 'legacy-kubernetes:g7')
check((ROOT / 'infra/gcp/kubernetes/g8-1').is_dir(), 'missing-kubernetes:g8-1')

canonical = json.loads((ROOT / 'infra/gcp/integration/g9-canonical-paths.json').read_text(encoding='utf-8'))
for workflow in canonical['active_workflows']:
    check((ROOT / '.github/workflows' / workflow).is_file(), f'missing-active-workflow:{workflow}')

for path in sorted((ROOT / '.github/workflows').glob('*.yml')):
    try:
        yaml.safe_load(path.read_text(encoding='utf-8'))
        check(True, '')
    except Exception as exc:  # noqa: BLE001
        check(False, f'yaml:{path.name}:{exc}')
    text = path.read_text(encoding='utf-8')
    for match in re.finditer(r'uses:\s*([^\s#]+)', text):
        value = match.group(1)
        if value.startswith('./'):
            continue
        check(bool(re.search(r'@[0-9a-f]{40}$', value)), f'unpinned-action:{path.name}:{value}')

for path in [
    ROOT / 'docs/evidence/g9-convergence-state.json',
    ROOT / 'infra/gcp/integration/g9-canonical-paths.json',
]:
    try:
        json.loads(path.read_text(encoding='utf-8'))
        check(True, '')
    except Exception as exc:  # noqa: BLE001
        check(False, f'json:{path.relative_to(ROOT)}:{exc}')

hcl_files = sorted((ROOT / 'infra/gcp/terraform').rglob('*.tf'))
check(hcl2 is not None, 'hcl2-module-missing')
if hcl2 is not None:
    for path in hcl_files:
        try:
            with path.open('r', encoding='utf-8') as handle:
                hcl2.load(handle)
            check(True, '')
        except Exception as exc:  # noqa: BLE001
            check(False, f'hcl:{path.relative_to(ROOT)}:{exc}')

# The academic billing account may be referenced only by the owner-controlled
# G10.2 bootstrap contract. The CN project and its runtime identifiers remain
# forbidden everywhere in deployable configuration.
deployable = []
for base in [ROOT / '.github/workflows', ROOT / 'infra/gcp', ROOT / 'scripts/cloud']:
    for path in base.rglob('*'):
        if not path.is_file():
            continue
        if path.name.startswith('Test-'):
            continue
        deployable.append(path)

for path in deployable:
    relative = path.relative_to(ROOT).as_posix()
    content = path.read_text(encoding='utf-8', errors='ignore')
    lowered = content.lower()
    for forbidden in ['cn2526-t4-g04', 'cn2526-t4-g04-billacc']:
        check(forbidden not in lowered, f'academic-runtime-identifier:{relative}:{forbidden}')

approved_billing_paths = {
    'infra/gcp/contracts/g10-2-bootstrap-input.schema.json',
    'infra/gcp/contracts/g10-2-bootstrap-input.example.json',
    'infra/gcp/contracts/g10-3-budget-input.schema.json',
    'infra/gcp/contracts/g10-3-budget-input.example.json',
    'scripts/cloud/Invoke-G102OwnerGate.ps1',
}
for path in deployable:
    relative = path.relative_to(ROOT).as_posix()
    content = path.read_text(encoding='utf-8', errors='ignore')
    if '0109B8-93144E-B93C1C'.lower() in content.lower():
        check(relative in approved_billing_paths, f'academic-billing-id-outside-approved-contract:{relative}')

# The local baseline and sensitive domain boundaries remain present.
for item in [
    '.env.example', 'docker-compose.yml', 'NatureProtector.sln',
    'src/NatureProtector.Shared/Contracts', 'src/NatureProtector.Shared/Messaging',
    'src/NatureProtector.Core/Risk'
]:
    check((ROOT / item).exists(), f'boundary-missing:{item}')

state = json.loads((ROOT / 'docs/evidence/g9-convergence-state.json').read_text(encoding='utf-8'))
check(state.get('cloud_provisioned') is False, 'state-cloud-provisioned')
check(state.get('production_authorized') is False, 'state-production-authorized')
check(state.get('production_deployed') is False, 'state-production-deployed')
check(state.get('cn_resources_allowed') is False, 'state-cn-resources-allowed')

result = {
    'phase': 'G9',
    'status': 'PASS' if not errors else 'FAIL',
    'checks_total': checks,
    'checks_failed': len(errors),
    'errors': errors,
    'classification': 'INTEGRATION_CANDIDATE',
    'cloud_provisioned': False,
    'production_authorized': False,
    'production_deployed': False,
}
print(json.dumps(result, indent=2))
sys.exit(1 if errors else 0)
