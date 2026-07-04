export const DEFAULT_AREA = 'proenca-a-nova';
export const WINDOW_OPTIONS = [10, 30, 1440];
export const MAIN_TABS = [
  'Monitoring',
  'Scenario Lab',
  'Flow Explorer',
  'Evidence & Comparison',
  'Model & Provenance',
] as const;
export const MONITORING_TABS = ['Overview', 'Map & Cells', 'Sensor Dashboards', 'Area Risk', 'Alerts'] as const;
export const SCENARIO_TABS = [
  'Run Orchestrator',
  'Scenario Definition',
  'P3 Negative Pipeline',
  'Latest Run',
  'Runtime State Control',
] as const;
export const EVIDENCE_TABS = [
  'Latest Run Audit',
  'Controlled Validation',
  'Compare B vs C',
  'Run Timings',
  'Diagnostics',
  'Export Evidence',
] as const;
export const FLOW_TABS = [
  'Runtime Chain',
  'Processing Pipeline',
  'Retry & Quarantine',
  'Persistence Views',
  'Deployment & Services',
  'Nominal Flow',
] as const;
export const MODEL_TABS = [
  'Domain Model',
  'Data Chain',
  'Data Provenance',
  'V3 Readiness',
  'Territorial & Weather Context',
  'Code Mapping',
] as const;
export const DEGRADATION_PROFILE_OPTIONS = [
  'none',
  'missing-readings',
  'noise',
  'bias',
  'drift',
  'stuck-value',
  'outlier',
  'clipping/range',
  'lag/delay',
  'duplicate',
  'out-of-order',
];

export const DEGRADATION_PROFILE_DETAILS: Record<
  string,
  { label: string; status: string; detail: string; blocked?: boolean }
> = {
  none: { label: 'none', status: 'Baseline', detail: 'No simulator degradation profile is requested.' },
  'missing-readings': {
    label: 'missing-readings',
    status: 'P2 supported',
    detail: 'Observation stream drops readings so expected-vs-accepted can expose missing events.',
  },
  noise: {
    label: 'noise',
    status: 'P2 value profile',
    detail: 'Simulator value perturbation profile; not a pipeline fault.',
  },
  bias: {
    label: 'bias',
    status: 'P2 value profile',
    detail: 'Simulator value offset profile; not a scoring calibration claim.',
  },
  drift: { label: 'drift', status: 'P2 value profile', detail: 'Simulator gradual value change profile.' },
  'stuck-value': {
    label: 'stuck-value',
    status: 'P2 value profile',
    detail: 'Repeated value profile for observation degradation evidence.',
  },
  outlier: {
    label: 'outlier',
    status: 'P2 value profile',
    detail: 'Extreme value profile; validation evidence must decide whether runtime accepts or rejects it.',
  },
  'clipping/range': {
    label: 'clipping/range',
    status: 'P2 value profile',
    detail: 'Boundary/range degradation profile.',
  },
  'lag/delay': {
    label: 'lag/delay',
    status: 'P2 temporal profile',
    detail: 'Delayed observations; not the same as retry scheduling.',
  },
  duplicate: {
    label: 'duplicate idempotent replay',
    status: 'P2 idempotency',
    detail: 'Simulator duplicate replay. This is separate from P3 duplicate_payload_mismatch.',
  },
  'out-of-order': {
    label: 'out-of-order',
    status: 'Blocked/future',
    detail: 'Temporal semantics are blocked until safe classifier/window state is wired.',
    blocked: true,
  },
};

export const P3_CANONICAL = {
  runLabel: 'controlled-validation-p3-negative-pipeline-20260605-002',
  generatedAt: '2026-06-05',
  sidecar:
    'docs/evidence/controlled-validation/p3/20260605-130508-controlled-validation-p3-negative-pipeline-20260605-002/',
  queryPack: 'docs/evidence/ml/momento2/runs/20260605-150738/',
  summary:
    'docs/evidence/controlled-validation/p3/20260605-130508-controlled-validation-p3-negative-pipeline-20260605-002/summary.md',
  queryPackSql: 'tools/data-audit/postgres/11_controlled_validation_p3_negative_pipeline.sql',
};

export const P3_CASES = [
  {
    id: 'P3_REJECT_INVALID_JSON',
    category: 'Rejected',
    expected: 'invalid_json',
    status: 'matched',
    effect: 'pre-inbox rejection; no accepted/risk projection',
  },
  {
    id: 'P3_REJECT_MISSING_PAYLOAD',
    category: 'Rejected',
    expected: 'missing_payload',
    status: 'matched',
    effect: 'pre-inbox rejection; no accepted/risk projection',
  },
  {
    id: 'P3_REJECT_UNSUPPORTED_EVENT_TYPE',
    category: 'Rejected',
    expected: 'unsupported_event_type',
    status: 'matched',
    effect: 'pre-inbox rejection; no accepted/risk projection',
  },
  {
    id: 'P3_REJECT_UNSUPPORTED_SCHEMA_VERSION',
    category: 'Rejected',
    expected: 'unsupported_schema_version',
    status: 'matched',
    effect: 'pre-inbox rejection; no accepted/risk projection',
  },
  {
    id: 'P3_REJECT_INVALID_OPERATIONAL_STATE',
    category: 'Rejected',
    expected: 'invalid_operational_state',
    status: 'matched',
    effect: 'pre-inbox rejection; no accepted/risk projection',
  },
  {
    id: 'P3_QUARANTINE_SENSOR_NOT_FOUND',
    category: 'Quarantined',
    expected: 'sensor_not_found',
    status: 'matched',
    effect: 'terminal quarantine; no accepted/risk projection',
  },
  {
    id: 'P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH',
    category: 'Rejected',
    expected: 'duplicate_payload_mismatch',
    status: 'matched',
    effect: 'inbox-linked rejection after idempotency mismatch',
  },
  {
    id: 'P3_RETRY_TRANSIENT_THEN_SUCCESS',
    category: 'Retry',
    expected: 'retry_then_success',
    status: 'matched',
    effect: '2 attempts, 1 retry, 1 accepted/risk projection',
  },
  {
    id: 'P3_RETRY_EXHAUSTED_TO_QUARANTINE',
    category: 'Retry/quarantine',
    expected: 'retries_exhausted',
    status: 'matched',
    effect: '3 attempts, 2 retries, terminal quarantine',
  },
  {
    id: 'P3_PERMANENT_FAILURE_TO_QUARANTINE',
    category: 'Quarantined',
    expected: 'permanent_failure',
    status: 'matched',
    effect: '1 attempt, terminal quarantine',
  },
  {
    id: 'P3_QUARANTINE_SENSOR_INACTIVE',
    category: 'Quarantined',
    expected: 'sensor_inactive',
    status: 'blocked_needs_fixture',
    effect: 'requires safe fixture; no real sensor mutation',
  },
  {
    id: 'P3_QUARANTINE_SENSOR_AREA_MISMATCH',
    category: 'Quarantined',
    expected: 'sensor_area_mismatch',
    status: 'blocked_needs_fixture',
    effect: 'requires safe fixture; no real area/sensor mutation',
  },
];

export const P3_EVIDENCE_REFERENCES = [
  ['P3 runtime summary', P3_CANONICAL.summary, 'Canonical closure summary.'],
  ['P3 query pack manifest', `${P3_CANONICAL.queryPack}manifest.md`, 'Read-only PostgreSQL query pack manifest.'],
  [
    'P3 expected vs observed',
    `${P3_CANONICAL.queryPack}postgres/p3_expected_vs_observed.csv`,
    'Fault-case expected/observed status.',
  ],
  [
    'P3 rejected by fault case',
    `${P3_CANONICAL.queryPack}postgres/p3_rejected_by_fault_case.csv`,
    'Rejected path details, including pre-inbox rejections.',
  ],
  [
    'P3 quarantined by fault case',
    `${P3_CANONICAL.queryPack}postgres/p3_quarantined_by_fault_case.csv`,
    'Quarantine path details.',
  ],
  [
    'P3 retry paths',
    `${P3_CANONICAL.queryPack}postgres/p3_retry_paths_by_fault_case.csv`,
    'Retry then success and retry-to-quarantine evidence.',
  ],
  [
    'P3 unexpected accepted/risk',
    `${P3_CANONICAL.queryPack}postgres/p3_unexpected_accepted_or_risk.csv`,
    'Header-only in canonical P3 run: no unexpected accepted/risk rows.',
  ],
  [
    'P3 negative M5 traceability',
    `${P3_CANONICAL.queryPack}postgres/p3_negative_m5_traceability.csv`,
    'Negative-path traceability support.',
  ],
  ['P3 SQL', P3_CANONICAL.queryPackSql, 'Query pack source SQL.'],
];

export const VALIDATION_PHASE_ROWS = [
  [
    'P0 runtime',
    'Closed prerequisite',
    'Rejected and quarantine paths, mismatch/sensor_not_found and no positive projections were proven before P1/P3.',
  ],
  [
    'P1 retry/failure',
    'Evidence present',
    'retry_transitions, retry_then_success, retry_to_quarantine, processing_faults_by_case and p1_expected_vs_observed are in the query pack.',
  ],
  [
    'P2 degradation',
    'Evidence present with limitations',
    'Observation degradation profiles are separate from P3 pipeline faults; out-of-order remains blocked/future.',
  ],
  [
    'P3 negative pipeline',
    'Closed for executable cases',
    '10 required cases matched; sensor_inactive and sensor_area_mismatch are blocked_needs_fixture.',
  ],
  [
    'M3 label support',
    'Audit artifact',
    'Negative labels are query-pack/readiness evidence, not ML training completion.',
  ],
  [
    'M5 traceability',
    'Ready for negative paths',
    'Negative-path traceability files are present for controlled validation evidence.',
  ],
];

export const MODEL_ARTIFACTS = [
  {
    concept: 'ScenarioDefinition',
    status: 'Implemented',
    persistence: 'Persisted',
    uiEvidence: 'Scenario Definition / Run Orchestrator',
    code: 'ScenarioDefinition',
  },
  {
    concept: 'TruthSnapshot',
    status: 'Implemented',
    persistence: 'Transient',
    uiEvidence: 'Not exposed',
    code: 'TruthSnapshot',
  },
  {
    concept: 'LocalObservation',
    status: 'Implemented',
    persistence: 'Transient',
    uiEvidence: 'Not exposed',
    code: 'LocalObservation',
  },
  {
    concept: 'OperationalEvent',
    status: 'Implemented',
    persistence: 'pipeline.event_inbox',
    uiEvidence: 'Runtime Chain / Persistence Views',
    code: 'EventEnvelope<TPayload>',
  },
  {
    concept: 'NormalizedReading',
    status: 'Partial UI evidence',
    persistence: 'accepted_reading_log',
    uiEvidence: 'Latest Run Audit',
    code: 'ReadingRiskPipeline',
  },
  {
    concept: 'DailyCellState',
    status: 'Implemented',
    persistence: 'projection.cell_operational_state',
    uiEvidence: 'Freshness / Territorial Context',
    code: 'DailyCellState',
  },
  {
    concept: 'RiskInput',
    status: 'Implemented',
    persistence: 'Transient',
    uiEvidence: 'Not exposed',
    code: 'RiskEligibilityService',
  },
  {
    concept: 'RiskAssessment',
    status: 'Implemented',
    persistence: 'projection.risk_assessment_log',
    uiEvidence: 'Area Risk / Latest Run Audit',
    code: 'RiskAssessment',
  },
  {
    concept: 'AreaRiskSnapshot',
    status: 'Implemented',
    persistence: 'projection.area_risk_snapshot_log',
    uiEvidence: 'Latest Run Audit / Area Risk',
    code: 'AreaRiskSnapshot',
  },
  {
    concept: 'AlertState',
    status: 'Implemented',
    persistence: 'projection.alert_state',
    uiEvidence: 'Monitoring / Alerts',
    code: 'V1AlertPolicy',
  },
  {
    concept: 'OperationalProjection',
    status: 'Implemented',
    persistence: 'projection.*',
    uiEvidence: 'Monitoring / Flow Explorer',
    code: 'PostgresAreaOperationalProjectionStore',
  },
];
