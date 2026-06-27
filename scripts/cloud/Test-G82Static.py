#!/usr/bin/env python3
from __future__ import annotations

import json, re, sys
from pathlib import Path
import yaml
from jsonschema import Draft202012Validator, FormatChecker

from g8_state_evidence import load_required_json, validate_g8_state_document

ROOT=Path(__file__).resolve().parents[2]
errors=[]; checks=0

def check(ok: bool, msg: str):
    global checks
    checks+=1
    if not ok: errors.append(msg)

required=[
'.github/workflows/gcp-g8-2-runtime-probe.yml',
'.github/workflows/gcp-g8-2-runtime-qualification.yml',
'.github/workflows/gcp-g8-2-submit-signed-governance.yml',
'.github/workflows/gcp-g8-2-independent-review.yml',
'.github/workflows/gcp-g8-2-authorization-request.yml',
'.github/workflows/gcp-g8-2-authorization-verification.yml',
'.github/workflows/gcp-g8-2-policy.yml',
'scripts/cloud/g82_common.py','scripts/cloud/g82_governance.py','scripts/cloud/New-G82ProbeMeasurement.py',
'scripts/cloud/Invoke-G82RuntimeProbe.ps1','scripts/cloud/Verify-G82WorkflowRun.sh',
'scripts/cloud/New-G82ActionRecord.py','scripts/cloud/Aggregate-G82Qualification.py',
'scripts/cloud/New-G82EvidenceIndex.py','scripts/cloud/Verify-G82EvidenceIndex.py',
'scripts/cloud/Test-G82QualificationEvidence.py','scripts/cloud/Archive-G82Evidence.ps1',
'scripts/cloud/Test-G82IndependentReview.py','scripts/cloud/New-G82AuthorizationRequest.py',
'scripts/cloud/Test-G82Authorization.py','scripts/cloud/New-G82ReviewPacket.py',
'scripts/cloud/Validate-G82RunMetadata.py','scripts/cloud/Test-G82Adversarial.py',
'infra/gcp/qualification/g8-2-qualification-plan.json',
'docs/implementation/cloud/g8-2-qualification-evidence-integrity.md',
'docs/operations/g8-2-runtime-qualification-runbook.md',
'docs/operations/g8-2-independent-review-authorization-runbook.md',
'docs/security/g8-2-chain-of-custody.md','docs/evidence/g8-2-state.json',
]
schemas=sorted((ROOT/'infra/gcp/contracts').glob('g8-2-*.schema.json'))
for p in required: check((ROOT/p).is_file(),f'missing:{p}')
check(len(schemas)>=12,'insufficient-g82-schemas')

parsed_json = {}
for p in schemas+[ROOT/'infra/gcp/qualification/g8-2-qualification-plan.json',ROOT/'docs/evidence/g8-2-state.json']:
    result = load_required_json(p, ROOT)
    if result.error is not None:
        check(False, result.error)
        continue
    parsed_json[p] = result.data
    check(True,'')

state_path = ROOT/'docs/evidence/g8-2-state.json'
if state_path in parsed_json:
    for issue in validate_g8_state_document(parsed_json[state_path], 'G8.2'):
        check(False, issue)
for p in sorted((ROOT/'.github/workflows').glob('gcp-g8-2-*.yml')):
    try: yaml.safe_load(p.read_text(encoding='utf-8')); check(True,'')
    except Exception as e: check(False,f'yaml:{p.name}:{e}')
    text=p.read_text(encoding='utf-8')
    for m in re.finditer(r'uses:\s*([^\s#]+)',text):
        value=m.group(1)
        if not value.startswith('./'): check(bool(re.search(r'@[0-9a-f]{40}$',value)),f'unpinned-action:{p.name}:{value}')

# Every schema must be Draft 2020-12 and strict at the root.
for p in schemas:
    obj=parsed_json.get(p)
    if not isinstance(obj, dict):
        continue
    check(obj.get('$schema')=='https://json-schema.org/draft/2020-12/schema',f'schema-draft:{p.name}')
    check(obj.get('additionalProperties') is False,f'schema-not-closed:{p.name}')
    try: Draft202012Validator.check_schema(obj); check(True,'')
    except Exception as e: check(False,f'schema-invalid:{p.name}:{e}')

scope='\n'.join((ROOT/p).read_text(encoding='utf-8',errors='ignore') for p in required if (ROOT/p).is_file())
scope+='\n'+'\n'.join(p.read_text(encoding='utf-8',errors='ignore') for p in schemas)
for token in [
'G82_PRE_ARCHIVE_QUALIFICATION_PASSED','G82_FINAL_QUALIFICATION_PASSED',
'candidate_manifest_sha256','archive_receipt_sha256','final_qualification_verdict_sha256',
'production_authorized','production_deployed','additionalProperties',
'--signer-workflow','--source-digest','--source-ref',
'Validate-G82RunMetadata.py','Verify-G82EvidenceIndex.py','New-G82ProbeMeasurement.py',
'files_verified','resources_remaining','second_operator_identity',
'REVIEW_NAMESPACE','AUTHORIZATION_NAMESPACE'
]: check(token in scope,f'guardrail-missing:{token}')

# The converged G9 candidate removes the vulnerable G8 workflows entirely.
for name in ['gcp-g8-runtime-qualification.yml','gcp-g8-submit-signed-governance.yml','gcp-g8-independent-review.yml','gcp-g8-authorization-request.yml','gcp-g8-authorization-verification.yml']:
    check(not (ROOT/'.github/workflows'/name).exists(),f'legacy-workflow-present:{name}')

# Qualification/governance may verify and archive but may not deploy production.
for p in sorted((ROOT/'.github/workflows').glob('gcp-g8-2-*.yml')):
    text=p.read_text(encoding='utf-8').lower()
    for forbidden in ['gcloud run services update-traffic','gcloud deploy rollouts approve','kubectl apply','terraform apply','production_deployed=true']:
        check(forbidden not in text,f'deployment-command-in-g82:{p.name}:{forbidden}')

# No academic CN identifiers or broad IAM roles in G8.2 deployable scope.
for forbidden in ['-'.join(['0109b8','93144e','b93c1c']),'cn2526-t4-g04','roles/owner','roles/editor','google_service_account_key']:
    check(forbidden not in scope.lower(),f'forbidden:{forbidden}')

# Runtime launch is explicit and default-off.
options=(ROOT/'src/NatureProtector.Backoffice.Api/RuntimeOrchestration/RuntimeOrchestrationOptions.cs').read_text()
ext=(ROOT/'src/NatureProtector.Backoffice.Api/RuntimeOrchestration/RuntimeOrchestrationServiceCollectionExtensions.cs').read_text()
ctrl=(ROOT/'src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs').read_text()
svc=(ROOT/'infra/gcp/cloud-deploy/g8-1/api/service.yaml').read_text()
check('AllowRemoteLaunch' in options,'remote-launch-option-missing')
check('AllowRemoteLaunch=true requires Mode=CloudRunJob' in ext,'remote-launch-validation-missing')
check('!_environment.IsDevelopment()' in ctrl and 'StatusCodes.Status403Forbidden' in ctrl and 'only available in Development' in ctrl,'controller-remote-launch-gate-missing')
check('RuntimeOrchestration__AllowRemoteLaunch, value: "true"' in svc,'cloud-remote-launch-opt-in-missing')

# G8.2 runtime workflows use the canonical staging WIF boundary.
probe_workflow = (
    ROOT / '.github/workflows/gcp-g8-2-runtime-probe.yml'
).read_text(encoding='utf-8')

qualification_workflow = (
    ROOT / '.github/workflows/gcp-g8-2-runtime-qualification.yml'
).read_text(encoding='utf-8')

for workflow_name, workflow_text in [
    ('gcp-g8-2-runtime-probe.yml', probe_workflow),
    ('gcp-g8-2-runtime-qualification.yml', qualification_workflow),
]:
    check(
        'environment: staging' in workflow_text,
        f'wif-environment-missing:{workflow_name}',
    )
    check(
        'id-token: write' in workflow_text,
        f'wif-id-token-permission-missing:{workflow_name}',
    )
    check(
        'workload_identity_provider: ${{ vars.WIF_PROVIDER }}'
        in workflow_text,
        f'wif-provider-missing:{workflow_name}',
    )
    check(
        'service_account: ${{ vars.DEPLOY_SERVICE_ACCOUNT }}'
        in workflow_text,
        f'wif-service-account-missing:{workflow_name}',
    )

check(
    "-ProjectId '${{ vars.GCP_PROJECT_ID }}'"
    in probe_workflow,
    "probe-project-uses-standard-variable",
)
check(
    "GCP_STAGING_PROJECT_ID" not in probe_workflow,
    "legacy-probe-project-variable",
)
for token, message in [
    (
        "GCP_G81_STAGING_CLUSTER_NAME",
        "probe-cluster-variable-missing",
    ),
    (
        "GCP_G81_STAGING_CLOUD_SQL_INSTANCE",
        "probe-cloud-sql-variable-missing",
    ),
    (
        "GCP_G82_SCC_PARENT",
        "probe-scc-variable-missing",
    ),
    (
        "if [[ \"$ACTION\" == collect-audit ]]",
        "probe-scc-action-gate-missing",
    ),
]:
    check(
        token in probe_workflow,
        message,
    )

for token, message in [
    (
        "GCP_G82_EVIDENCE_BUCKET",
        "qualification-evidence-variable-missing",
    ),
    (
        "EVIDENCE_BUCKET: ${{ vars.GCP_G82_EVIDENCE_BUCKET }}",
        "qualification-evidence-preflight-missing",
    ),
]:
    check(
        token in qualification_workflow,
        message,
    )

for legacy_token in [
    'GCP_G82_PROBE_WIF_PROVIDER',
    'GCP_G82_PROBE_SERVICE_ACCOUNT',
    'GCP_G82_QUALIFICATION_WIF_PROVIDER',
    'GCP_G82_QUALIFICATION_SERVICE_ACCOUNT',
]:
    check(
        legacy_token not in probe_workflow
        and legacy_token not in qualification_workflow,
        f'legacy-g82-wif-variable:{legacy_token}',
    )

platform_identity = (
    ROOT / 'infra/gcp/terraform/g8-1-platform/identity.tf'
).read_text(encoding='utf-8')

for legacy_identity in [
    'gha-np-g82-probe',
    'gha-np-g82-qualify',
]:
    check(
        legacy_identity not in platform_identity,
        f'legacy-g82-wif-identity:{legacy_identity}',
    )

if errors:
    print(json.dumps({'phase':'G8.2','status':'FAIL','checks_total':checks,'checks_failed':len(errors),'errors':errors},indent=2))
    raise SystemExit(1)
print(json.dumps({'phase':'G8.2','status':'PASS','checks_total':checks,'checks_passed':checks,'checks_failed':0,'production_authorized':False,'production_deployed':False},indent=2))
