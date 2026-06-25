#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
from pathlib import Path

from g82_common import assert_commit, assert_qualification_id, parse_datetime, read_json, write_json

ACTIONS = {
    'pilot-1','pilot-2','pilot-3','soak-start','soak-observe','soak-finish','capacity',
    'security-rotation','incident-drill','collect-audit','cost-observation','second-operator',
    'rollback-drill','teardown-rehearsal'
}
ROLES = {'raw-measurement','execution-metadata','logs','metrics','audit','drill-result','cost-export','operator-evidence','manifest','other'}


def require(obj: dict, *names: str) -> None:
    missing=[n for n in names if n not in obj]
    if missing:
        raise ValueError('missing raw probe fields: '+', '.join(missing))


def histogram(samples: list[float]) -> dict:
    if not samples or any((not isinstance(v,(int,float)) or not math.isfinite(float(v)) or float(v)<0) for v in samples):
        raise ValueError('latency_samples_seconds must contain finite non-negative numbers')
    values=sorted(float(v) for v in samples)
    bounds=[0.1,0.25,0.5,1,2,5,10,20,30,60,120]
    if values[-1] > bounds[-1]:
        bounds.append(math.ceil(values[-1]))
    return {
        'unit':'seconds','count':len(values),'sum':round(sum(values),9),
        'buckets':[{'le':float(b),'cumulative_count':sum(1 for v in values if v<=b)} for b in bounds]
    }


def runtime_measurements(raw: dict, include_samples: bool=False) -> dict:
    require(raw,'produced_events','processed_events','failed_events','confirmed_message_loss',
            'availability_window_seconds','unavailable_seconds','latency_samples_seconds')
    produced=int(raw['produced_events']); processed=int(raw['processed_events']); failed=int(raw['failed_events']); loss=int(raw['confirmed_message_loss'])
    if produced <= 0 or min(processed,failed,loss) < 0 or processed+failed+loss != produced:
        raise ValueError('runtime event accounting must satisfy processed + failed + loss = produced > 0')
    window=float(raw['availability_window_seconds']); unavailable=float(raw['unavailable_seconds'])
    if window <= 0 or unavailable < 0 or unavailable > window:
        raise ValueError('invalid availability window')
    result={
        'produced_events':produced,'processed_events':processed,'failed_events':failed,
        'confirmed_message_loss':loss,'availability_window_seconds':window,
        'unavailable_seconds':unavailable,'latency_histogram':histogram(raw['latency_samples_seconds'])
    }
    if include_samples:
        require(raw,'sample_timestamps')
        stamps=[]
        for value in raw['sample_timestamps']:
            stamps.append(parse_datetime(value,field='sample_timestamps').isoformat().replace('+00:00','Z'))
        if len(stamps)<2 or len(set(stamps))!=len(stamps):
            raise ValueError('soak sample_timestamps require at least two unique values')
        result['sample_timestamps']=stamps
    return result


def main() -> int:
    ap=argparse.ArgumentParser()
    ap.add_argument('--source',required=True)
    ap.add_argument('--action',required=True,choices=sorted(ACTIONS))
    ap.add_argument('--qualification-id',required=True)
    ap.add_argument('--source-commit',required=True)
    ap.add_argument('--evidence-root',required=True)
    ap.add_argument('--output',required=True)
    args=ap.parse_args()
    root=Path(args.evidence_root).resolve(); source=Path(args.source).resolve(); source.relative_to(root)
    if source.is_symlink() or not source.is_file(): raise SystemExit('source must be a regular file inside evidence root')
    raw=read_json(source)
    if set(raw)-{'schema_version','phase','qualification_id','source_commit','action','started_at','finished_at','subject','facts','evidence_files','production_authorized','production_deployed'}:
        raise SystemExit('raw probe source contains unsupported properties')
    require(raw,'schema_version','phase','qualification_id','source_commit','action','started_at','finished_at','subject','facts','evidence_files','production_authorized','production_deployed')
    if raw['schema_version']!=2 or raw['phase']!='G8.2' or raw['action']!=args.action:
        raise SystemExit('raw probe source phase/action mismatch')
    if raw['qualification_id']!=assert_qualification_id(args.qualification_id) or raw['source_commit']!=assert_commit(args.source_commit):
        raise SystemExit('raw probe source binding mismatch')
    if raw['production_authorized'] is not False or raw['production_deployed'] is not False:
        raise SystemExit('probe source may not authorize or deploy production')
    started=parse_datetime(raw['started_at'],field='started_at'); finished=parse_datetime(raw['finished_at'],field='finished_at')
    if started>finished: raise SystemExit('probe finished_at precedes started_at')
    facts=raw['facts']; action=args.action
    if action.startswith('pilot-'):
        require(raw['subject'],'execution_id','simulation_run_id')
        measurements=runtime_measurements(facts)
    elif action=='soak-start':
        require(raw['subject'],'execution_id'); require(facts,'sample_started_at')
        measurements={'sample_started_at':parse_datetime(facts['sample_started_at'],field='sample_started_at').isoformat().replace('+00:00','Z')}
    elif action=='soak-observe':
        require(raw['subject'],'execution_id'); require(facts,'observed_at','healthy')
        measurements={'observed_at':parse_datetime(facts['observed_at'],field='observed_at').isoformat().replace('+00:00','Z'),'healthy':bool(facts['healthy'])}
    elif action=='soak-finish':
        require(raw['subject'],'execution_id'); measurements=runtime_measurements(facts,True)
    elif action=='capacity':
        require(facts,'required_peak_eps','measured_sustainable_eps','backlog_peak','drain_seconds')
        measurements={k:facts[k] for k in ('required_peak_eps','measured_sustainable_eps','backlog_peak','drain_seconds')}
        if float(measurements['required_peak_eps'])<=0 or float(measurements['measured_sustainable_eps'])<=0 or float(measurements['drain_seconds'])<0: raise ValueError('invalid capacity facts')
    elif action=='security-rotation':
        require(facts,'credential_rotation_passed','certificate_rotation_passed'); measurements={k:bool(facts[k]) for k in ('credential_rotation_passed','certificate_rotation_passed')}
    elif action=='incident-drill':
        keys=('regional_failover_seconds','pitr_rpo_seconds','pitr_restore_seconds','cross_region_promotion_passed','return_to_primary_passed','incident_drill_passed'); require(facts,*keys); measurements={k:facts[k] for k in keys}
    elif action=='collect-audit':
        keys=('data_access_audit_logs_enabled','artifact_attestations_verified','open_high_findings','open_critical_findings'); require(facts,*keys); measurements={k:facts[k] for k in keys}
    elif action=='cost-observation':
        keys=('observation_days','observed_cost_eur','forecast_monthly_eur','approved_monthly_eur','monthly_cost_approved'); require(facts,*keys); measurements={k:facts[k] for k in keys}
    elif action=='second-operator':
        require(facts,'second_operator_identity','runbook_passed'); measurements={'second_operator_identity':str(facts['second_operator_identity']),'runbook_passed':bool(facts['runbook_passed'])}
    elif action=='rollback-drill':
        require(facts,'rollback_proved','restored_release_digest'); measurements={'rollback_proved':bool(facts['rollback_proved']),'restored_release_digest':str(facts['restored_release_digest'])}
    elif action=='teardown-rehearsal':
        require(facts,'cleanup_rehearsal_proved','resources_remaining','environment_recreated'); measurements={'cleanup_rehearsal_proved':bool(facts['cleanup_rehearsal_proved']),'resources_remaining':int(facts['resources_remaining']),'environment_recreated':bool(facts['environment_recreated'])}
    else: raise AssertionError(action)
    refs=[]
    for item in raw['evidence_files']:
        if not isinstance(item,dict) or set(item)!={'path','role'} or item['role'] not in ROLES: raise ValueError('invalid evidence_files entry')
        path=(root/item['path']).resolve(); path.relative_to(root)
        if path.is_symlink() or not path.is_file(): raise ValueError(f"missing evidence file: {item['path']}")
        refs.append({'path':item['path'],'role':item['role']})
    if source.relative_to(root).as_posix() not in {r['path'] for r in refs}:
        refs.append({'path':source.relative_to(root).as_posix(),'role':'raw-measurement'})
    out={'started_at':raw['started_at'],'finished_at':raw['finished_at'],'subject':raw['subject'],'measurements':measurements,'evidence_files':refs}
    write_json(args.output,out); print(args.output); return 0

if __name__=='__main__': raise SystemExit(main())
