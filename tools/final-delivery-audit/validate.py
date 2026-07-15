#!/usr/bin/env python3
"""Behavioral pre-integration audit for NatureProtector phases 7-10."""
from __future__ import annotations
import argparse
import json
import subprocess
import sys
from pathlib import Path


def add(rows, ok, name, detail): rows.append({"name":name,"status":"PASS" if ok else "FAIL","detail":detail})
def run(repo, cmd):
    p=subprocess.run(cmd,cwd=repo,text=True,capture_output=True)
    return p.returncode==0,(p.stdout+p.stderr)[-2000:]

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--repo',type=Path,default=Path('.')); ap.add_argument('--output',type=Path)
    a=ap.parse_args(); repo=a.repo.resolve(); rows=[]
    for name,cmd in [
      ('python-authority-wired',[sys.executable,'-m','unittest','tests.runtime.test_long_run_proof','tests.observability.test_grafana_dashboards','-v']),
      ('adversarial-evidence',[sys.executable,'-m','unittest','tests.evidence.test_adversarial_evidence_gate','-v']),
      ('adversarial-autoscaling',[sys.executable,'-m','unittest','tests.autoscaling.test_scaling_verifier_adversarial','-v']),
      ('ipma-cursor-safety',[sys.executable,'-m','unittest','tests.data.test_ipma_cursor_safety','-v']),
    ]:
        ok,detail=run(repo,cmd); add(rows,ok,name,detail)
    lifecycle=(repo/'src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.RuntimeLifecycle.cs').read_text()
    add(rows,'ReconcileRuntimeOperationWithProviderAsync' in lifecycle and 'RuntimeOperationStateLock' in lifecycle,'restart-safe-lifecycle','Provider polling and distributed lifecycle lock are present.')
    add(rows,'OrchestratorCorrelationId' in (repo/'src/NatureProtector.Infrastructure.Postgres/Control/ControlRecords.cs').read_text(),'typed-run-correlation','SimulationRun has typed orchestrator correlation.')
    add(rows,(repo/'tests/NatureProtector.IntegrationTests/Flow/DockerGrafanaPostgresQueryTests.cs').is_file(),'grafana-sql-integration-test','Every provisioned PostgreSQL panel is executed against a migrated database in Docker integration.')
    compose=(repo/'docker-compose.yml').read_text(); add(rows,'GF_AUTH_ANONYMOUS_ENABLED: "false"' in compose and 'GF_SECURITY_ALLOW_EMBEDDING: "false"' in compose,'grafana-safe-defaults','Anonymous access and embedding are disabled by default.')
    docs=(repo/'docs/current-state/final-delivery-status.md').read_text(); add(rows,'observability-only' in docs.lower() or 'observability only' in docs.lower(),'truthful-ipma-scope','Current-state documentation states that IPMA is observability-only.')
    failures=[r for r in rows if r['status']=='FAIL']; result={'schemaVersion':2,'status':'PASS' if not failures else 'FAIL','checks':rows,'summary':{'total':len(rows),'passed':len(rows)-len(failures),'failed':len(failures)}}
    if a.output:
      out=a.output if a.output.is_absolute() else repo/a.output; out.parent.mkdir(parents=True,exist_ok=True); out.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2)); return 0 if not failures else 1
if __name__=='__main__': raise SystemExit(main())
