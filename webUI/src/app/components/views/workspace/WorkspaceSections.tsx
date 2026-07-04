import { useMemo, useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  BarChart3,
  CloudRain,
  Clipboard,
  Clock,
  Database,
  Download,
  Map as MapIcon,
  Play,
  RefreshCw,
  RotateCcw,
  Search,
  Server,
  ShieldCheck,
} from 'lucide-react';
import { AreaMap } from '../../mainComponents/AreaMap';
import type {
  AreaCellResponse,
  ControlledValidationP3AvailabilityResponse,
  ControlledValidationP3RunRequest,
  ControlledValidationP3RunResponse,
  RuntimeAlertSummaryResponse,
  RuntimeDiagnosticDefinitionResponse,
  RuntimeDiagnosticResultResponse,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SensorNodeResponse,
} from '../../../types';
import {
  DEGRADATION_PROFILE_DETAILS,
  DEGRADATION_PROFILE_OPTIONS,
  MAIN_TABS,
  MODEL_ARTIFACTS,
  P3_CANONICAL,
  P3_CASES,
  SCENARIO_TABS,
  EVIDENCE_TABS,
  FLOW_TABS,
  VALIDATION_PHASE_ROWS,
} from './workspaceConstants';
import {
  AlertList,
  Badge,
  Banner,
  BarGraph,
  button,
  buildAttemptTimingSummary,
  buildCompareRows,
  buildEvidenceMarkdown,
  buildNominalFlowSteps,
  buildRuntimeChainDetails,
  buildSafeGrafanaAreaUrl,
  cardGrid,
  ChartPanel,
  CheckRow,
  CollapsibleJson,
  copyText,
  DiagnosticResult,
  downloadText,
  EmptyState,
  EventRows,
  FormGrid,
  formatDate,
  formatMaybeScore,
  formatMs,
  formatRiskRange,
  formatScore,
  groupDiagnostics,
  InfoCard,
  input,
  isBlockedDegradationProfile,
  isControlledValidationP3Run,
  KeyValues,
  labelStyle,
  LabeledInput,
  LabeledNumber,
  LabeledSelect,
  Metric,
  MetricGrid,
  NarrativeSummary,
  normalizeProfiles,
  Panel,
  panel,
  paragraph,
  p3CaseRows,
  p3EvidenceRows,
  ResetCounts,
  RiskLineChart,
  RuntimeChainStrip,
  RunDetails,
  RunRequestResult,
  SectionHeader,
  shortTime,
  SimpleTable,
  StatusCounts,
  statusTone,
  Tabs,
  toLegacyProfile,
  twoCol,
  ViewStack,
  type Colors,
} from './WorkspaceShared';

export function MonitoringOverview({
  colors,
  summary,
  run,
  audit,
  geoJSON,
  cells,
  sensorNodes,
  areaId,
}: {
  colors: Colors;
  summary: RuntimeSummaryResponse | null;
  run: RuntimeRunSummaryResponse | null;
  audit: RuntimeRunAuditResponse | null;
  geoJSON: any;
  cells: AreaCellResponse[];
  sensorNodes: SensorNodeResponse[];
  areaId: string;
}) {
  return (
    <ViewStack>
      <MetricGrid>
        <Metric
          colors={colors}
          title="Current Area Risk"
          value={formatScore(summary?.areaOperationalState?.aggregateRiskScore)}
          detail={summary?.areaOperationalState?.aggregateRiskLevel ?? 'No projection'}
          icon={<Activity size={18} />}
          tone="#be123c"
        />
        <Metric
          colors={colors}
          title="Active Alert"
          value={summary?.activeAlerts.length ?? 0}
          detail={summary?.areaOperationalState?.alertState ?? 'No active alert state'}
          icon={<AlertTriangle size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="Freshness"
          value={summary?.areaOperationalState?.freshnessStatus ?? 'Not available'}
          detail={
            summary?.freshness
              ? `${summary.freshness.freshCount}/${summary.freshness.staleCount}/${summary.freshness.expiredCount} cells fresh/stale/expired`
              : 'projection freshness'
          }
          icon={<Clock size={18} />}
          tone="#475569"
        />
        <Metric
          colors={colors}
          title="Coverage"
          value={summary?.areaOperationalState?.coverageStatus ?? 'Not available'}
          detail={summary?.areaOperationalState?.operationalStatusReason ?? 'projection coverage'}
          icon={<ShieldCheck size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Carry-forward"
          value={summary?.areaOperationalState?.carryForwardStatus ?? 'Not available'}
          detail={formatDate(summary?.areaOperationalState?.lastAssessmentTimestamp)}
          icon={<RefreshCw size={18} />}
          tone="#7c3aed"
        />
        <Metric
          colors={colors}
          title="Latest Run"
          value={run?.status ?? 'No run'}
          detail={run?.scenarioCode ?? 'Not available'}
          icon={<Play size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Sensors"
          value={cells.reduce((sum, cell) => sum + cell.sensorNodeCount, 0)}
          detail={`${cells.length} cells exposed`}
          icon={<MapIcon size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="Last Update"
          value={summary?.generatedAtUtc ? shortTime(summary.generatedAtUtc) : 'No data'}
          detail="runtime summary generatedAtUtc"
          icon={<RefreshCw size={18} />}
          tone="#7c3aed"
        />
      </MetricGrid>

      <div style={twoCol()}>
        <Panel colors={colors}>
          <SectionHeader
            title="Runtime Chain"
            subtitle="Scenario Run -> Event Inbox -> Processing Attempts -> Risk -> State -> Alerts -> API/UI"
          />
          <RuntimeChainStrip colors={colors} summary={summary} />
        </Panel>
        <Panel colors={colors}>
          <SectionHeader title="Risk and Alert Summary" />
          <p style={paragraph(colors)}>
            {summary?.areaOperationalState?.summary ?? 'No persisted area risk summary is exposed for this area.'}
          </p>
          <p style={paragraph(colors)}>
            {summary?.activeAlerts.length
              ? `${summary.activeAlerts.length} active alert(s) are currently exposed by projection.alert_state.`
              : 'No active alerts are currently exposed.'}
          </p>
          <KeyValues
            colors={colors}
            rows={[
              ['Expected events', audit?.expectedEvents ?? 'Not available'],
              ['Accepted readings', audit?.acceptedReadings ?? 'Not available'],
              ['Missing events', audit?.missingEvents ?? 'Not available'],
              ['Risk assessments', audit?.riskAssessments ?? 'Not available'],
            ]}
          />
        </Panel>
      </div>

      <Panel colors={colors}>
        <SectionHeader title="Mini Map Preview" subtitle="Boundary and cells are read from existing area endpoints." />
        <div
          style={{
            height: '340px',
            border: `1px solid ${colors.panelBorder}`,
            borderRadius: '8px',
            overflow: 'hidden',
          }}
        >
          {geoJSON ? (
            <AreaMap
              areaId={areaId}
              mapType="standard"
              showGrid={false}
              geoJSON={geoJSON}
              cells={cells}
              sensorNodes={sensorNodes}
            />
          ) : (
            <EmptyState colors={colors} text="Map data is not available." />
          )}
        </div>
      </Panel>
    </ViewStack>
  );
}

export function MapAndCells({
  colors,
  areaId,
  geoJSON,
  cells,
  sensorNodes,
}: {
  colors: Colors;
  areaId: string;
  geoJSON: any;
  cells: AreaCellResponse[];
  sensorNodes: SensorNodeResponse[];
}) {
  return (
    <ViewStack>
      <Panel colors={colors}>
        <SectionHeader
          title="Map & Cells"
          subtitle="Existing Leaflet map, area boundary, grid cells and sensor markers."
        />
        <div
          style={{
            height: '620px',
            border: `1px solid ${colors.panelBorder}`,
            borderRadius: '8px',
            overflow: 'hidden',
          }}
        >
          {geoJSON ? (
            <AreaMap
              areaId={areaId}
              mapType="standard"
              showGrid={false}
              geoJSON={geoJSON}
              cells={cells}
              sensorNodes={sensorNodes}
            />
          ) : (
            <EmptyState colors={colors} text="Map data is not available." />
          )}
        </div>
      </Panel>
      <CollapsibleJson colors={colors} title="Cells exposed by API" value={cells} />
    </ViewStack>
  );
}

export function SensorDashboards({
  colors,
  areaId,
  dashboardLinks,
}: {
  colors: Colors;
  areaId: string;
  dashboardLinks: string[];
}) {
  const sensorTabs = ['Temperature', 'Humidity', 'Wind'] as const;
  const [selected, setSelected] = useState<(typeof sensorTabs)[number]>('Temperature');
  const index = sensorTabs.indexOf(selected);
  const link = buildSafeGrafanaAreaUrl(dashboardLinks[index] ?? dashboardLinks[0] ?? null, areaId);
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Sensor Dashboards"
        subtitle="Only one Grafana embed is loaded by default to keep the view readable."
      />
      <Tabs values={sensorTabs} selected={selected} onSelect={setSelected} colors={colors} compact />
      {link ? (
        <div
          style={{
            height: '560px',
            border: `1px solid ${colors.panelBorder}`,
            borderRadius: '8px',
            overflow: 'hidden',
          }}
        >
          <iframe
            src={link}
            width="100%"
            height="100%"
            style={{ border: 0, display: 'block' }}
            title={`${selected} dashboard`}
            loading="lazy"
          />
        </div>
      ) : (
        <EmptyState colors={colors} text="Grafana dashboard not configured." />
      )}
    </Panel>
  );
}

export function AreaRiskView({
  colors,
  areaId,
  summary,
  dashboardLink,
}: {
  colors: Colors;
  areaId: string;
  summary: RuntimeSummaryResponse | null;
  dashboardLink: string | null;
}) {
  const grafanaUrl = buildSafeGrafanaAreaUrl(dashboardLink, areaId);
  return (
    <ViewStack>
      <MetricGrid>
        <Metric
          colors={colors}
          title="Current Area Score"
          value={formatScore(summary?.areaOperationalState?.aggregateRiskScore)}
          detail="aggregate projection / carry-forward"
          icon={<Activity size={18} />}
          tone="#be123c"
        />
        <Metric
          colors={colors}
          title="Latest NP Assessment"
          value={formatScore(summary?.scoreComponents?.npScore)}
          detail={`${summary?.scoreComponents?.npRiskClassLabel ?? summary?.scoreComponents?.npRiskClass ?? 'class n/a'}; ${summary?.scoreComponents?.parameterSetVersion ?? 'parameter set not exposed'}`}
          icon={<Activity size={18} />}
          tone="#be123c"
        />
        <Metric
          colors={colors}
          title="Risk Level"
          value={summary?.areaOperationalState?.aggregateRiskLevel ?? 'No data'}
          detail={summary?.areaOperationalState?.severity ?? 'Not available'}
          icon={<ShieldCheck size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="Assessment Count"
          value={summary?.areaOperationalState?.assessmentCount ?? 'Not available'}
          detail="persisted projection count"
          icon={<BarChart3 size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Freshness"
          value={summary?.areaOperationalState?.freshnessStatus ?? 'Not available'}
          detail={summary?.areaOperationalState?.carryForwardStatus ?? 'carry-forward not exposed'}
          icon={<Clock size={18} />}
          tone="#475569"
        />
        <Metric
          colors={colors}
          title="Coverage"
          value={summary?.areaOperationalState?.coverageStatus ?? 'Not available'}
          detail={summary?.areaOperationalState?.operationalStatusReason ?? 'coverage status'}
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="FWI"
          value={formatMaybeScore(summary?.indexComparison?.fireWeatherIndex)}
          detail={`${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? 'IPMA class n/a'}; near ${summary?.indexComparison?.fireWeatherNextIpmaClass ?? 'n/a'} (${formatMaybeScore(summary?.indexComparison?.fireWeatherThresholdDistanceToNextClass)})`}
          icon={<BarChart3 size={18} />}
          tone="#7c3aed"
        />
        <Metric
          colors={colors}
          title="KBDI"
          value={formatMaybeScore(summary?.indexComparison?.keetchByramDroughtIndex)}
          detail={`${summary?.indexComparison?.kbdiDrynessClassLabel ?? 'dryness class n/a'}; ${summary?.indexComparison?.kbdiAntecedentHistoryQuality ?? summary?.indexComparison?.kbdiCalculationStatus ?? 'status n/a'}`}
          icon={<BarChart3 size={18} />}
          tone="#6d28d9"
        />
        <Metric
          colors={colors}
          title="Portuguese Context Proxy"
          value={
            summary?.indexComparison?.portugueseContextRiskProxyLabel ??
            summary?.indexComparison?.portugueseContextRiskProxyClass ??
            'Not available'
          }
          detail={`FWI ${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? 'n/a'} x Territory ${summary?.indexComparison?.territorialHazardProxyClass ?? 'n/a'}`}
          icon={<ShieldCheck size={18} />}
          tone="#0f766e"
        />
        <Metric
          colors={colors}
          title="Precipitation 24h"
          value={formatMaybeScore(summary?.indexComparison?.dailyPrecipitationMillimeters)}
          detail={summary?.indexComparison?.provenance ?? 'daily reference not exposed'}
          icon={<CloudRain size={18} />}
          tone="#0369a1"
        />
        <Metric
          colors={colors}
          title="Recent Risk Rows"
          value={summary?.risk.recentCount ?? 0}
          detail={formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore)}
          icon={<Clock size={18} />}
          tone="#0891b2"
        />
      </MetricGrid>
      <Banner colors={colors} tone="#64748b">
        NP, FWI and KBDI values shown here are persisted backend projections/diagnostics. The frontend does not score,
        recalibrate indexes or claim scientific ground truth.
      </Banner>
      <Panel colors={colors}>
        <SectionHeader
          title="Score Components"
          subtitle="Read from persisted risk_assessment_log; frontend does not score."
        />
        <KeyValues
          colors={colors}
          rows={[
            [
              'BaseRisk / Adjusted',
              `${formatMaybeScore(summary?.scoreComponents?.baseRisk)} / ${formatMaybeScore(summary?.scoreComponents?.adjustedScore)}`,
            ],
            [
              'M / D / T',
              `${formatMaybeScore(summary?.scoreComponents?.meteorologyComponent)} / ${formatMaybeScore(summary?.scoreComponents?.droughtComponent)} / ${formatMaybeScore(summary?.scoreComponents?.territoryComponent)}`,
            ],
            [
              'H / F / G',
              `${formatMaybeScore(summary?.scoreComponents?.hazardComponent)} / ${formatMaybeScore(summary?.scoreComponents?.fuelComponent)} / ${formatMaybeScore(summary?.scoreComponents?.geomorphologyComponent)}`,
            ],
            [
              'C / I',
              `${formatMaybeScore(summary?.scoreComponents?.confidenceFactor)} / ${formatMaybeScore(summary?.scoreComponents?.integrityFactor)}`,
            ],
            ['Dominant driver', summary?.scoreComponents?.dominantDriver ?? 'Not available'],
            ['Calculation', summary?.scoreComponents?.calculationStatus ?? 'Not available'],
            [
              'Current Area vs Latest NP',
              'Area score is an aggregate projection. Latest NP assessment is the latest persisted risk_assessment_log row.',
            ],
            [
              'Precipitation 24h / provenance',
              `${formatMaybeScore(summary?.indexComparison?.dailyPrecipitationMillimeters)} / ${summary?.indexComparison?.provenance ?? 'Not available'}`,
            ],
            [
              'FWI calculated / reference',
              `${formatMaybeScore(summary?.indexComparison?.calculatedFireWeatherIndex)} / ${formatMaybeScore(summary?.indexComparison?.referenceFireWeatherIndex)}`,
            ],
            [
              'KBDI calculated / reference',
              `${formatMaybeScore(summary?.indexComparison?.calculatedKeetchByramDroughtIndex)} / ${formatMaybeScore(summary?.indexComparison?.referenceKeetchByramDroughtIndex)}`,
            ],
            [
              'Local FWI percentile',
              `${summary?.indexComparison?.localFwiPercentileStatus ?? 'Not available'}${summary?.indexComparison?.localFwiPercentileReason ? `: ${summary.indexComparison.localFwiPercentileReason}` : ''}`,
            ],
            [
              'Limitations',
              summary?.scoreComponents?.limitations ?? summary?.indexComparison?.limitations ?? 'None exposed',
            ],
          ]}
        />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader
          title="Recent Risk Scores"
          subtitle="Read from persisted risk_assessment_log values; frontend does not score."
        />
        <RiskLineChart colors={colors} summary={summary} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader
          title="Grafana Area Risk"
          subtitle="External dashboard link is shown only when configured as a valid Grafana URL."
        />
        {grafanaUrl ? (
          <>
            <a style={button(colors)} href={grafanaUrl} target="_blank" rel="noreferrer">
              Open Grafana area risk dashboard
            </a>
            <div
              style={{
                height: '560px',
                border: `1px solid ${colors.panelBorder}`,
                borderRadius: '8px',
                overflow: 'hidden',
              }}
            >
              <iframe
                src={grafanaUrl}
                width="100%"
                height="100%"
                style={{ border: 0, display: 'block' }}
                title={`area dashboard`}
                loading="lazy"
              />
            </div>
          </>
        ) : (
          <EmptyState colors={colors} text="Grafana area risk dashboard not configured." />
        )}
      </Panel>
      <Banner colors={colors} tone="#b45309">
        Recent risk rows and persisted area operational state may differ because projections can include carry-forward.
      </Banner>
    </ViewStack>
  );
}

export function AlertsView({ colors, alerts }: { colors: Colors; alerts: RuntimeAlertSummaryResponse[] }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title="Alerts" subtitle="Active alerts from projection.alert_state." />
      <AlertList colors={colors} alerts={alerts} detailed />
    </Panel>
  );
}

export function RunOrchestrator(props: {
  colors: Colors;
  scenarios: ScenarioResponse[];
  activeSensorCount: number;
  sensorCountTooHigh: boolean;
  runForm: RuntimeRunStartRequest;
  setRunForm: Dispatch<SetStateAction<RuntimeRunStartRequest>>;
  startRun: () => void;
  submittingRun: boolean;
  runResult: RuntimeRunStartResponse | null;
  runMessage: string | null;
  areaCode: string;
  setMainTab: Dispatch<SetStateAction<(typeof MAIN_TABS)[number]>>;
  setScenarioTab: Dispatch<SetStateAction<(typeof SCENARIO_TABS)[number]>>;
}) {
  const {
    colors,
    scenarios,
    activeSensorCount,
    sensorCountTooHigh,
    runForm,
    setRunForm,
    startRun,
    submittingRun,
    runResult,
    runMessage,
    areaCode,
    setMainTab,
    setScenarioTab,
  } = props;
  const activeProfiles = normalizeProfiles(runForm.degradationProfiles, runForm.degradationProfile);
  const scenarioCWithoutDegradation =
    runForm.scenarioCode === 'scenario_c' && activeProfiles.every((profile) => profile === 'none');
  const blockedProfiles = activeProfiles.filter(isBlockedDegradationProfile);
  const setDegradationProfile = (profile: string, checked: boolean) => {
    setRunForm((current) => {
      if (isBlockedDegradationProfile(profile) && checked) {
        return current;
      }
      const currentProfiles = normalizeProfiles(current.degradationProfiles, current.degradationProfile);
      let next = currentProfiles;
      if (profile === 'none') {
        next = checked ? ['none'] : [];
      } else {
        next = checked
          ? [...currentProfiles.filter((value) => value !== 'none'), profile]
          : currentProfiles.filter((value) => value !== profile);
      }
      if (next.length === 0) {
        next = ['none'];
      }
      return { ...current, degradationProfiles: next, degradationProfile: toLegacyProfile(next) };
    });
  };
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Run Orchestrator"
        subtitle="Starts Simulator.Host through the existing development control endpoint."
      />
      <FormGrid>
        <LabeledSelect
          colors={colors}
          label="scenario code"
          value={runForm.scenarioCode}
          options={scenarios.map((scenario) => ({
            value: scenario.code,
            label: `${scenario.code} - ${scenario.name}`,
          }))}
          onChange={(value) =>
            setRunForm((current) => {
              const currentProfiles = normalizeProfiles(current.degradationProfiles, current.degradationProfile);
              const nextProfiles =
                value === 'scenario_c' && currentProfiles.every((profile) => profile === 'none')
                  ? ['missing-readings']
                  : currentProfiles;
              return {
                ...current,
                scenarioCode: value,
                degradationProfile: toLegacyProfile(nextProfiles),
                degradationProfiles: nextProfiles,
                runLabel: `${value}-from-ui`,
              };
            })
          }
        />
        <LabeledNumber
          colors={colors}
          label="sensor count"
          value={runForm.sensorCount}
          max={activeSensorCount || undefined}
          onChange={(value) => setRunForm((current) => ({ ...current, sensorCount: value }))}
        />
        <LabeledNumber
          colors={colors}
          label="number of cycles"
          value={runForm.numberOfCycles}
          onChange={(value) => setRunForm((current) => ({ ...current, numberOfCycles: value }))}
        />
        <LabeledNumber
          colors={colors}
          label="interval seconds"
          value={runForm.intervalSeconds}
          onChange={(value) => setRunForm((current) => ({ ...current, intervalSeconds: value }))}
        />
        <LabeledNumber
          colors={colors}
          label="seed"
          value={runForm.seed}
          onChange={(value) => setRunForm((current) => ({ ...current, seed: value }))}
        />
        <LabeledNumber
          colors={colors}
          label="timeout seconds"
          value={runForm.timeoutSeconds}
          onChange={(value) => setRunForm((current) => ({ ...current, timeoutSeconds: value ?? 180 }))}
        />
        <LabeledInput
          colors={colors}
          label="run label"
          value={runForm.runLabel ?? ''}
          onChange={(value) => setRunForm((current) => ({ ...current, runLabel: value || null }))}
        />
      </FormGrid>
      <div style={{ marginTop: '10px' }}>
        <div style={labelStyle(colors)}>P2 observation degradation profiles</div>
        <div style={cardGrid()}>
          {DEGRADATION_PROFILE_OPTIONS.map((profile) => (
            <ProfileCheckCard
              key={profile}
              colors={colors}
              profile={profile}
              checked={activeProfiles.includes(profile)}
              onChange={(checked) => setDegradationProfile(profile, checked)}
            />
          ))}
        </div>
      </div>
      <div style={{ color: colors.textSecond, fontSize: '13px', marginTop: '10px' }}>
        Active sensors available: {activeSensorCount || 'Unknown'}; selected sensors requested:{' '}
        {runForm.sensorCount ?? 'all'}; active profiles: {activeProfiles.join(', ')}
      </div>
      {sensorCountTooHigh && (
        <Banner colors={colors} tone="#dc2626">
          sensorCount exceeds active sensors for this area.
        </Banner>
      )}
      {scenarioCWithoutDegradation && (
        <Banner colors={colors} tone="#b45309">
          scenario_c is intended for degraded/operational comparison. Select at least one degradation profile for a
          meaningful C run.
        </Banner>
      )}
      {blockedProfiles.length > 0 && (
        <Banner colors={colors} tone="#dc2626">
          Blocked profile(s): {blockedProfiles.join(', ')}. They are represented as future validation work and cannot be
          launched from this UI.
        </Banner>
      )}
      <Banner colors={colors} tone="#2563eb">
        These profiles alter simulator observations for P2-style evidence. P3 negative pipeline fault cases are
        represented separately and are not launched through this scenario endpoint.
      </Banner>
      <CheckRow
        colors={colors}
        label="collect evidence"
        checked={runForm.collectEvidence}
        onChange={(value) => setRunForm((current) => ({ ...current, collectEvidence: value }))}
      />
      <CheckRow
        colors={colors}
        label="wait for completion"
        checked={runForm.waitForCompletion}
        onChange={(value) => setRunForm((current) => ({ ...current, waitForCompletion: value }))}
      />
      <CheckRow
        colors={colors}
        label="allow parallel run"
        checked={runForm.allowParallelRun}
        onChange={(value) => setRunForm((current) => ({ ...current, allowParallelRun: value }))}
      />
      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '12px' }}>
        <button
          type="button"
          style={button(colors)}
          onClick={startRun}
          disabled={submittingRun || sensorCountTooHigh || blockedProfiles.length > 0}
        >
          <Play size={16} /> {submittingRun ? 'Submitting...' : 'Start Run'}
        </button>
        <button
          type="button"
          style={button(colors)}
          onClick={() => {
            setMainTab('Scenario Lab');
            setScenarioTab('Latest Run');
          }}
        >
          <ArrowRight size={16} /> Latest Run
        </button>
      </div>
      {(runMessage || runResult) && (
        <RunRequestResult
          colors={colors}
          result={runResult}
          request={runForm}
          message={runMessage}
          areaCode={areaCode}
        />
      )}
    </Panel>
  );
}

export function ProfileCheckCard({
  colors,
  profile,
  checked,
  onChange,
}: {
  colors: Colors;
  profile: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  const details = DEGRADATION_PROFILE_DETAILS[profile] ?? {
    label: profile,
    status: 'Profile',
    detail: 'Simulator degradation profile.',
  };
  const blocked = isBlockedDegradationProfile(profile);
  return (
    <label
      style={{
        ...panel(colors),
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        gap: '9px',
        alignItems: 'start',
        opacity: blocked ? 0.72 : 1,
        cursor: blocked ? 'not-allowed' : 'pointer',
      }}
    >
      <input
        type="checkbox"
        checked={checked && !blocked}
        disabled={blocked}
        onChange={(event) => onChange(event.target.checked)}
        style={{ marginTop: '3px' }}
      />
      <span>
        <span
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            gap: '8px',
            alignItems: 'center',
            flexWrap: 'wrap',
          }}
        >
          <strong style={{ color: colors.textPrimary }}>{details.label}</strong>
          <Badge colors={colors}>{details.status}</Badge>
        </span>
        <span style={{ ...paragraph(colors), display: 'block' }}>{details.detail}</span>
      </span>
    </label>
  );
}

export function P3NegativePipelinePanel(props: {
  colors: Colors;
  summary: RuntimeSummaryResponse | null;
  p3Availability: ControlledValidationP3AvailabilityResponse | null;
  p3RunRequest: ControlledValidationP3RunRequest;
  setP3RunRequest: Dispatch<SetStateAction<ControlledValidationP3RunRequest>>;
  p3RunResult: ControlledValidationP3RunResponse | null;
  p3RunMessage: string | null;
  submittingP3Run: boolean;
  startControlledValidationP3: () => void;
  setMainTab: Dispatch<SetStateAction<(typeof MAIN_TABS)[number]>>;
  setEvidenceTab: Dispatch<SetStateAction<(typeof EVIDENCE_TABS)[number]>>;
  setFlowTab: Dispatch<SetStateAction<(typeof FLOW_TABS)[number]>>;
}) {
  const {
    colors,
    summary,
    p3Availability,
    p3RunRequest,
    setP3RunRequest,
    p3RunResult,
    p3RunMessage,
    submittingP3Run,
    startControlledValidationP3,
    setMainTab,
    setEvidenceTab,
    setFlowTab,
  } = props;
  const latestRunLabel =
    summary?.latestRun?.orchestratorCorrelationId ?? summary?.latestRun?.scenarioCode ?? 'Not available';
  const executableCases = P3_CASES.filter((item) => item.status === 'matched').length;
  const blockedCases = P3_CASES.filter((item) => item.status === 'blocked_needs_fixture').length;
  const canRunP3 = Boolean(p3Availability?.available) && !submittingP3Run;
  return (
    <ViewStack>
      <MetricGrid>
        <Metric
          colors={colors}
          title="P3 status"
          value="Closed"
          detail={`canonical run ${P3_CANONICAL.runLabel}`}
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="Executable cases"
          value={`${executableCases}/10`}
          detail="all required executable P3 cases matched"
          icon={<Clipboard size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Blocked fixtures"
          value={blockedCases}
          detail="sensor_inactive and sensor_area_mismatch"
          icon={<AlertTriangle size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="Unexpected accepted/risk"
          value="0"
          detail="p3_unexpected_accepted_or_risk.csv has no data rows"
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
      </MetricGrid>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader
          title="P3 Negative Pipeline"
          subtitle="Dedicated controlled-validation endpoint; the normal Scenario Lab runtime endpoint is not used for P3 faults."
        />
        <KeyValues
          colors={colors}
          rows={[
            ['Canonical run label', P3_CANONICAL.runLabel],
            ['Sidecar', P3_CANONICAL.sidecar],
            ['Query pack', P3_CANONICAL.queryPack],
            ['Latest runtime label visible to UI', latestRunLabel],
            [
              'Endpoint availability',
              p3Availability
                ? `${p3Availability.available ? 'Available' : 'Blocked'} (${p3Availability.environment})`
                : 'Not loaded',
            ],
            ['Endpoint phase', p3Availability?.phase ?? 'Not loaded'],
          ]}
        />
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
            gap: '10px',
            marginTop: '12px',
          }}
        >
          <LabeledInput
            colors={colors}
            label="Run label"
            value={p3RunRequest.runLabel ?? ''}
            onChange={(value) => setP3RunRequest((current) => ({ ...current, runLabel: value }))}
          />
          <LabeledNumber
            colors={colors}
            label="Timeout seconds"
            value={p3RunRequest.timeoutSeconds}
            onChange={(value) => setP3RunRequest((current) => ({ ...current, timeoutSeconds: value ?? 300 }))}
          />
          <div style={panel(colors)}>
            <strong style={{ color: colors.textPrimary, display: 'block', marginBottom: '8px' }}>
              Execution flags
            </strong>
            <CheckRow
              colors={colors}
              label="Wait for completion"
              checked={p3RunRequest.waitForCompletion}
              onChange={(value) => setP3RunRequest((current) => ({ ...current, waitForCompletion: value }))}
            />
            <CheckRow
              colors={colors}
              label="Collect evidence"
              checked={p3RunRequest.collectEvidence}
              onChange={(value) => setP3RunRequest((current) => ({ ...current, collectEvidence: value }))}
            />
            <CheckRow
              colors={colors}
              label="Request audit after completion"
              checked={p3RunRequest.runAuditAfterCompletion}
              onChange={(value) => setP3RunRequest((current) => ({ ...current, runAuditAfterCompletion: value }))}
            />
          </div>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '12px' }}>
          <button
            type="button"
            style={{ ...button(colors), opacity: canRunP3 ? 1 : 0.65, cursor: canRunP3 ? 'pointer' : 'not-allowed' }}
            disabled={!canRunP3}
            onClick={startControlledValidationP3}
          >
            <Play size={16} /> {submittingP3Run ? 'Running P3...' : 'Run P3 suite'}
          </button>
          <button
            type="button"
            style={button(colors)}
            onClick={() => {
              setMainTab('Evidence & Comparison');
              setEvidenceTab('Controlled Validation');
            }}
          >
            <Search size={16} /> Evidence
          </button>
          <button
            type="button"
            style={button(colors)}
            onClick={() => {
              setMainTab('Flow Explorer');
              setFlowTab('Retry & Quarantine');
            }}
          >
            <ArrowRight size={16} /> Retry paths
          </button>
        </div>
        <Banner colors={colors} tone={p3Availability?.available ? '#2563eb' : '#b45309'}>
          {p3Availability?.message ??
            'P3 execution remains disabled until the backend confirms Development/Evidence availability.'}
        </Banner>
        <Banner colors={colors} tone="#b45309">
          This action is allowlisted: it does not accept raw JSON, routing keys, fault-case edits, sensor edits or area
          edits. Query-pack audit remains mandatory after runtime completion.
        </Banner>
        {p3RunMessage && (
          <Banner colors={colors} tone="#2563eb">
            {p3RunMessage}
          </Banner>
        )}
        {p3RunResult && <P3RunResult colors={colors} result={p3RunResult} />}
      </Panel>
      <Panel colors={colors}>
        <SectionHeader
          title="P3 Cases"
          subtitle="Executable cases are closed; fixture cases remain explicitly blocked."
        />
        <SimpleTable
          colors={colors}
          columns={['Fault case', 'Path', 'Expected code/path', 'Status', 'Projection expectation']}
          rows={p3CaseRows()}
        />
      </Panel>
    </ViewStack>
  );
}

export function P3RunResult({ colors, result }: { colors: Colors; result: ControlledValidationP3RunResponse }) {
  return (
    <div style={{ ...panel(colors), marginTop: '12px', borderColor: result.auditRequired ? '#b45309' : '#059669' }}>
      <SectionHeader title="Latest P3 Request" subtitle={`${result.status} in ${result.environment}`} />
      <KeyValues
        colors={colors}
        rows={[
          ['Run label', result.runLabel],
          ['Phase', result.phase],
          ['Message count', String(result.messageCount)],
          ['Executable / blocked', `${result.executableCases} / ${result.blockedCases}`],
          ['Simulation run', result.run?.id ?? 'Not observed'],
          ['Evidence path', result.evidencePath ?? 'Not collected'],
          ['Query pack path', result.queryPackPath ?? 'Manual audit required'],
          ['Audit required', result.auditRequired ? 'Yes' : 'No'],
        ]}
      />
      {result.notes.length > 0 && (
        <SimpleTable colors={colors} columns={['Notes']} rows={result.notes.map((note) => [note])} />
      )}
    </div>
  );
}

export function ScenarioDefinition({ colors, scenarios }: { colors: Colors; scenarios: ScenarioResponse[] }) {
  const definitions = [
    ['scenario_a', 'Baseline/normal', 'Clean operational run', 'none', 'Stable readings and normal risk processing'],
    [
      'scenario_b',
      'High risk without degradation',
      'Compare against degraded scenario',
      'none',
      'High-risk inputs without missing readings',
    ],
    [
      'scenario_c',
      'High risk degraded with missing readings',
      'Demonstrate degradation handling',
      'missing-readings',
      'Fewer accepted readings with explicit missing events',
    ],
  ];
  return (
    <ViewStack>
      <div style={cardGrid()}>
        {definitions.map(([code, meaning, purpose, degradation, behavior]) => (
          <Panel key={code} colors={colors}>
            <SectionHeader title={code} subtitle={meaning} />
            <KeyValues
              colors={colors}
              rows={[
                ['Purpose', purpose],
                ['Expected degradation', degradation],
                ['Expected behavior', behavior],
                ['Default parameters', 'From scenario endpoint or run form defaults'],
              ]}
            />
          </Panel>
        ))}
      </div>
      <CollapsibleJson colors={colors} title="Scenario definitions exposed by API" value={scenarios} />
    </ViewStack>
  );
}

export function LatestRunView({ colors, run }: { colors: Colors; run: RuntimeRunSummaryResponse | null }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title="Latest Run" subtitle="Readable cards backed by control.simulation_runs metadata." />
      <RunDetails colors={colors} run={run} />
    </Panel>
  );
}

export function RuntimeStateControl({
  colors,
  confirm,
  setConfirm,
  resetRuntime,
  loading,
  resetResult,
}: {
  colors: Colors;
  confirm: string;
  setConfirm: (value: string) => void;
  resetRuntime: (dryRun: boolean) => void;
  loading: boolean;
  resetResult: RuntimeResetResponse | null;
}) {
  return (
    <Panel colors={colors} accent="#dc2626">
      <SectionHeader
        title="Runtime State Control"
        subtitle="Danger zone. Dry run first; real reset requires exact confirmation."
      />
      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        <button type="button" style={button(colors)} onClick={() => resetRuntime(true)} disabled={loading}>
          <Search size={16} /> Dry run reset
        </button>
        <input
          style={{ ...input(colors), width: '240px' }}
          value={confirm}
          onChange={(event) => setConfirm(event.target.value)}
          placeholder="RESET_RUNTIME_STATE"
        />
        <button
          type="button"
          style={{ ...button(colors), borderColor: '#dc2626', color: '#dc2626' }}
          onClick={() => resetRuntime(false)}
          disabled={loading || confirm !== 'RESET_RUNTIME_STATE'}
        >
          <RotateCcw size={16} /> Reset Runtime State
        </button>
      </div>
      {resetResult && <ResetCounts colors={colors} result={resetResult} />}
    </Panel>
  );
}

export function LatestRunAuditView({ colors, audit }: { colors: Colors; audit: RuntimeRunAuditResponse | null }) {
  if (!audit) {
    return (
      <Panel colors={colors}>
        <EmptyState colors={colors} text="No latest run audit is available." />
      </Panel>
    );
  }
  const p3LikeRun = isControlledValidationP3Run(audit.run);

  return (
    <ViewStack>
      {p3LikeRun && (
        <Banner colors={colors} tone="#7c3aed">
          This latest run appears to be part of controlled validation P3. The generic run audit is not the full P3
          expected-vs-observed report; use Controlled Validation for the canonical query pack.
        </Banner>
      )}
      <MetricGrid>
        <Metric
          colors={colors}
          title="Expected events"
          value={audit.expectedEvents ?? 'Not available'}
          detail="run overrides x cycles"
          icon={<Clipboard size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Accepted readings"
          value={audit.acceptedReadings}
          detail="accepted_reading_log"
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="Missing events"
          value={audit.missingEvents ?? 'Not available'}
          detail="expected - accepted"
          icon={<AlertTriangle size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="Risk assessments"
          value={audit.riskAssessments}
          detail="risk_assessment_log"
          icon={<Activity size={18} />}
          tone="#0891b2"
        />
        <Metric
          colors={colors}
          title="Rejected"
          value={audit.rejected}
          detail="pipeline.rejected_events"
          icon={<AlertTriangle size={18} />}
          tone="#dc2626"
        />
        <Metric
          colors={colors}
          title="Quarantined"
          value={audit.quarantined}
          detail="pipeline.quarantined_events"
          icon={<AlertTriangle size={18} />}
          tone="#ea580c"
        />
      </MetricGrid>
      <div style={twoCol()}>
        <Panel colors={colors}>
          <SectionHeader title="Quality Summary" />
          <StatusCounts colors={colors} rows={audit.qualityFlagsSummary} />
        </Panel>
        <Panel colors={colors}>
          <SectionHeader title="Eligibility Summary" />
          <StatusCounts colors={colors} rows={audit.eligibilitySummary} />
        </Panel>
      </div>
      <Panel colors={colors}>
        <SectionHeader
          title="NP vs FWI vs KBDI"
          subtitle="Values are read from persisted backend projections and diagnostics; the UI does not calculate indexes."
        />
        <KeyValues
          colors={colors}
          rows={[
            [
              'NP score / base / adjusted',
              `${formatMaybeScore(audit.scoreComponents?.npScore)} / ${formatMaybeScore(audit.scoreComponents?.baseRisk)} / ${formatMaybeScore(audit.scoreComponents?.adjustedScore)}`,
            ],
            [
              'M / D / T',
              `${formatMaybeScore(audit.scoreComponents?.meteorologyComponent)} / ${formatMaybeScore(audit.scoreComponents?.droughtComponent)} / ${formatMaybeScore(audit.scoreComponents?.territoryComponent)}`,
            ],
            [
              'H / F / G',
              `${formatMaybeScore(audit.scoreComponents?.hazardComponent)} / ${formatMaybeScore(audit.scoreComponents?.fuelComponent)} / ${formatMaybeScore(audit.scoreComponents?.geomorphologyComponent)}`,
            ],
            [
              'C / I',
              `${formatMaybeScore(audit.scoreComponents?.confidenceFactor)} / ${formatMaybeScore(audit.scoreComponents?.integrityFactor)}`,
            ],
            [
              'FWI raw / normalized / status',
              `${formatMaybeScore(audit.indexComparison?.fireWeatherIndex)} / ${formatMaybeScore(audit.indexComparison?.normalizedFireWeatherIndex)} / ${audit.indexComparison?.fireWeatherCalculationStatus ?? 'Not available'}`,
            ],
            [
              'FWI IPMA / EFFIS',
              `${audit.indexComparison?.fireWeatherIpmaClassLabel ?? audit.indexComparison?.fireWeatherIpmaClass ?? 'Not available'} / ${audit.indexComparison?.fireWeatherEffisClass ?? 'Not available'}`,
            ],
            [
              'KBDI raw / normalized / status',
              `${formatMaybeScore(audit.indexComparison?.keetchByramDroughtIndex)} / ${formatMaybeScore(audit.indexComparison?.normalizedKeetchByramDroughtIndex)} / ${audit.indexComparison?.kbdiCalculationStatus ?? 'Not available'}`,
            ],
            [
              'KBDI dryness / antecedent',
              `${audit.indexComparison?.kbdiDrynessClassLabel ?? audit.indexComparison?.kbdiDrynessClass ?? 'Not available'} / ${audit.indexComparison?.kbdiAntecedentHistoryQuality ?? 'Not available'}`,
            ],
            [
              'Portuguese Context Proxy',
              `${audit.indexComparison?.portugueseContextRiskProxyLabel ?? audit.indexComparison?.portugueseContextRiskProxyClass ?? 'Not available'}; territory ${audit.indexComparison?.territorialHazardProxyClass ?? 'n/a'}`,
            ],
            ['Dominant driver', audit.scoreComponents?.dominantDriver ?? 'Not available'],
            ['Parameter set', audit.scoreComponents?.parameterSetVersion ?? 'Not available'],
            ['Limitations', audit.scoreComponents?.limitations ?? audit.indexComparison?.limitations ?? 'None exposed'],
          ]}
        />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Audit Notes" />
        <ul style={{ margin: 0, paddingLeft: '18px', color: colors.textSecond, lineHeight: 1.7 }}>
          {audit.areaSnapshot && (
            <li>
              Area snapshot: {audit.areaSnapshot.aggregateRiskLevel} {audit.areaSnapshot.aggregateRiskScore} with{' '}
              {audit.areaSnapshot.assessmentCount} assessment(s).
            </li>
          )}
          {audit.limitations.map((item) => (
            <li key={item.code}>{item.message}</li>
          ))}
        </ul>
        <CollapsibleJson colors={colors} title="Raw audit JSON" value={audit} />
      </Panel>
    </ViewStack>
  );
}

export function ControlledValidationEvidenceView({ colors }: { colors: Colors }) {
  return (
    <ViewStack>
      <MetricGrid>
        <Metric
          colors={colors}
          title="P0/P1/P2/P3 evidence"
          value="Present"
          detail="controlled validation query pack and sidecar summaries"
          icon={<Database size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="P3 executable cases"
          value="10 matched"
          detail="negative pipeline required cases"
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="P3 blocked cases"
          value="2 fixtures"
          detail="sensor_inactive; sensor_area_mismatch"
          icon={<AlertTriangle size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="P3 positive leakage"
          value="0"
          detail="no unexpected accepted/risk rows"
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
      </MetricGrid>
      <Panel colors={colors} accent="#2563eb">
        <SectionHeader
          title="Controlled Validation Status"
          subtitle="Evidence is represented from local query packs and summaries; the UI does not execute SQL or recalculate risk."
        />
        <SimpleTable colors={colors} columns={['Phase/artifact', 'Status', 'Meaning']} rows={VALIDATION_PHASE_ROWS} />
      </Panel>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader title="P3 Expected vs Observed" subtitle={`Canonical run: ${P3_CANONICAL.runLabel}`} />
        <SimpleTable
          colors={colors}
          columns={['Fault case', 'Path', 'Expected code/path', 'Status', 'Projection expectation']}
          rows={p3CaseRows()}
        />
        <Banner colors={colors} tone="#b45309">
          `sensor_inactive` and `sensor_area_mismatch` are not treated as runtime failures. They remain blocked until
          safe fixtures exist.
        </Banner>
      </Panel>
      <Panel colors={colors}>
        <SectionHeader
          title="Evidence References"
          subtitle="Paths are local repository artifacts generated by controlled validation and read-only query packs."
        />
        <SimpleTable
          colors={colors}
          columns={['Artifact', 'Path', 'Purpose', 'Action']}
          rows={p3EvidenceRows(colors)}
        />
      </Panel>
    </ViewStack>
  );
}

export function CompareBvsC({ colors, compare }: { colors: Colors; compare: RuntimeDiagnosticResultResponse | null }) {
  const rows = useMemo(() => buildCompareRows(compare), [compare]);
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Compare B vs C"
        subtitle="Promoted from diagnostics; uses persisted rows and does not recalculate risk."
      />
      <SimpleTable colors={colors} columns={['Metric', 'Scenario B', 'Scenario C', 'Delta']} rows={rows} />
      <NarrativeSummary colors={colors} rows={rows} />
      {compare?.limitations.length ? (
        <ul style={{ color: colors.textSecond }}>
          {compare.limitations.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : null}
      <CollapsibleJson colors={colors} title="Raw comparison JSON" value={compare ?? 'Not available'} />
    </Panel>
  );
}

export function RunTimings({
  colors,
  run,
  summary,
  audit,
  timings,
  timingsMessage,
}: {
  colors: Colors;
  run: RuntimeRunSummaryResponse | null;
  summary: RuntimeSummaryResponse | null;
  audit: RuntimeRunAuditResponse | null;
  timings: RuntimeRunTimingSummaryResponse | null;
  timingsMessage: string | null;
}) {
  const attemptTimings = buildAttemptTimingSummary(summary);
  const attempts = timings?.attempts;
  const stageRows =
    timings?.stages.map((item) => [
      item.stage,
      item.outcome,
      item.errorCode ?? 'None',
      item.count,
      formatDate(item.firstStartedAt),
      formatDate(item.lastFinishedAt),
      item.minDurationMs == null ? 'Not exposed' : formatMs(item.minDurationMs),
      item.avgDurationMs == null ? 'Not exposed' : formatMs(item.avgDurationMs),
      item.maxDurationMs == null ? 'Not exposed' : formatMs(item.maxDurationMs),
    ]) ??
    attemptTimings.rows.map((row) => [row[0], row[1], 'Not exposed', row[2], row[3], row[4], row[5], row[6], row[7]]);
  const sourceDetail = timings ? 'Read-only DB timing endpoint' : 'Runtime summary fallback';

  return (
    <ViewStack>
      {timingsMessage && (
        <Banner colors={colors} tone="#b45309">
          {timingsMessage}
        </Banner>
      )}
      <MetricGrid>
        <Metric
          colors={colors}
          title="Run duration"
          value={
            timings?.runDurationMs == null
              ? run?.durationSeconds == null
                ? 'Not available'
                : `${Math.round(run.durationSeconds)}s`
              : formatMs(timings.runDurationMs)
          }
          detail={`${formatDate(timings?.startedAt ?? run?.startedAt)} -> ${formatDate(timings?.endedAt ?? run?.endedAt)}`}
          icon={<Clock size={18} />}
          tone="#2563eb"
        />
        <Metric
          colors={colors}
          title="Time to first inbox"
          value={timings?.timeToFirstInboxMs == null ? 'Not exposed' : formatMs(timings.timeToFirstInboxMs)}
          detail={formatDate(timings?.firstInboxReceivedAt)}
          icon={<Database size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="Time to first risk"
          value={
            timings?.timeToFirstRiskAssessmentMs == null ? 'Not exposed' : formatMs(timings.timeToFirstRiskAssessmentMs)
          }
          detail={formatDate(timings?.firstRiskAssessmentCreatedAt)}
          icon={<BarChart3 size={18} />}
          tone="#0891b2"
        />
        <Metric
          colors={colors}
          title="Time to first alert"
          value={timings?.timeToFirstAlertMs == null ? 'Not exposed' : formatMs(timings.timeToFirstAlertMs)}
          detail={formatDate(timings?.firstAlertTriggeredAt)}
          icon={<AlertTriangle size={18} />}
          tone="#dc2626"
        />
        <Metric
          colors={colors}
          title="First processing attempt"
          value={
            timings?.firstProcessingAttemptStartedAt
              ? shortTime(timings.firstProcessingAttemptStartedAt)
              : attemptTimings.firstStarted
                ? shortTime(attemptTimings.firstStarted)
                : 'Not exposed'
          }
          detail={sourceDetail}
          icon={<Activity size={18} />}
          tone="#7c3aed"
        />
        <Metric
          colors={colors}
          title="Last processing attempt"
          value={
            timings?.lastProcessingAttemptFinishedAt
              ? shortTime(timings.lastProcessingAttemptFinishedAt)
              : attemptTimings.lastFinished
                ? shortTime(attemptTimings.lastFinished)
                : 'Not exposed'
          }
          detail="FinishedAt when exposed"
          icon={<Clock size={18} />}
          tone="#0891b2"
        />
        <Metric
          colors={colors}
          title="Attempt count"
          value={attempts?.attemptCount ?? summary?.pipeline.attemptsRecent ?? 'No data'}
          detail={sourceDetail}
          icon={<Server size={18} />}
          tone="#475569"
        />
        <Metric
          colors={colors}
          title="Successful attempts"
          value={attempts?.successfulAttempts ?? attemptTimings.successfulAttempts ?? 'Not exposed'}
          detail="Grouped by persisted outcome"
          icon={<ShieldCheck size={18} />}
          tone="#059669"
        />
        <Metric
          colors={colors}
          title="Failed attempts"
          value={attempts?.failedAttempts ?? attemptTimings.failedAttempts}
          detail="failed/retry outcomes"
          icon={<AlertTriangle size={18} />}
          tone="#dc2626"
        />
        <Metric
          colors={colors}
          title="Quarantined attempts"
          value={attempts?.quarantinedAttempts ?? attemptTimings.quarantinedAttempts}
          detail="quarantined outcome"
          icon={<AlertTriangle size={18} />}
          tone="#ea580c"
        />
        <Metric
          colors={colors}
          title="Avg attempt duration"
          value={
            (attempts?.avgDurationMs ?? attemptTimings.avgDurationMs) == null
              ? 'Not exposed'
              : formatMs((attempts?.avgDurationMs ?? attemptTimings.avgDurationMs)!)
          }
          detail="Calculated only when StartedAt/FinishedAt exist"
          icon={<BarChart3 size={18} />}
          tone="#b45309"
        />
        <Metric
          colors={colors}
          title="Max attempt duration"
          value={
            (attempts?.maxDurationMs ?? attemptTimings.maxDurationMs) == null
              ? 'Not exposed'
              : formatMs((attempts?.maxDurationMs ?? attemptTimings.maxDurationMs)!)
          }
          detail={timings ? 'All attempts associated with run' : 'Latest failed attempts subset'}
          icon={<Clock size={18} />}
          tone="#be123c"
        />
      </MetricGrid>
      <Panel colors={colors}>
        <SectionHeader
          title="Attempt timing summary"
          subtitle={
            timings
              ? 'Uses pipeline.processing_attempts rows associated with this SimulationRunId.'
              : 'Uses processing attempt fields currently exposed by runtime summary fallback.'
          }
        />
        {stageRows.length > 0 ? (
          <SimpleTable
            colors={colors}
            columns={[
              'Stage',
              'Outcome',
              'Error',
              'Count',
              'First started',
              'Last finished',
              'Min duration',
              'Avg duration',
              'Max duration',
            ]}
            rows={stageRows}
          />
        ) : (
          <EmptyState colors={colors} text="Attempt-level timings not exposed by current diagnostics." />
        )}
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Run evidence timing context" subtitle="Available runtime/audit timestamps and counts." />
        <KeyValues
          colors={colors}
          rows={[
            ['SimulationRunId', run?.id ?? 'Not available'],
            ['Scenario', run?.scenarioCode ?? 'Not available'],
            ['Created', formatDate(run?.createdAt)],
            ['Started', formatDate(run?.startedAt)],
            ['Finished', formatDate(run?.endedAt)],
            ['Expected events', audit?.expectedEvents ?? 'Not available'],
            ['Accepted readings', audit?.acceptedReadings ?? 'Not available'],
            ['Risk assessments', audit?.riskAssessments ?? 'Not available'],
          ]}
        />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Timing limitations" subtitle="The endpoint is read-only and does not parse local logs." />
        {timings?.limitations.length ? (
          <ul style={{ color: colors.textSecond, margin: 0 }}>
            {timings.limitations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        ) : (
          <Banner colors={colors} tone="#b45309">
            Logger stopwatch timings are emitted in logs but are not structurally associated with SimulationRunId yet. A
            future evidence summary should expose those elapsed timings without frontend log parsing.
          </Banner>
        )}
      </Panel>
    </ViewStack>
  );
}

export function DiagnosticsView(props: {
  colors: Colors;
  diagnostics: RuntimeDiagnosticDefinitionResponse[];
  selectedDiagnostic: string;
  diagnosticResult: RuntimeDiagnosticResultResponse | null;
  executeDiagnostic: (id?: string) => void;
  loading: boolean;
}) {
  const { colors, diagnostics, selectedDiagnostic, diagnosticResult, executeDiagnostic, loading } = props;
  const groups = groupDiagnostics(diagnostics);
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: '14px' }}>
      <Panel colors={colors}>
        <SectionHeader title="Diagnostics" subtitle="All quick queries remain available." />
        {Object.entries(groups).map(([group, items]) => (
          <details key={group} open={group === 'Runs'} style={{ marginBottom: '10px' }}>
            <summary style={{ cursor: 'pointer', fontWeight: 800 }}>{group}</summary>
            <div style={{ display: 'grid', gap: '6px', marginTop: '8px' }}>
              {items.map((item) => (
                <button
                  type="button"
                  key={item.id}
                  style={button(colors, selectedDiagnostic === item.id)}
                  onClick={() => executeDiagnostic(item.id)}
                  disabled={loading}
                >
                  <Search size={14} /> {item.title}
                </button>
              ))}
            </div>
          </details>
        ))}
      </Panel>
      <Panel colors={colors}>
        <SectionHeader
          title={diagnosticResult?.title ?? 'Diagnostic result'}
          subtitle={diagnosticResult?.description ?? 'Choose a diagnostic to load data.'}
        />
        <DiagnosticResult colors={colors} result={diagnosticResult} />
      </Panel>
    </div>
  );
}

export function ExportEvidence({
  colors,
  audit,
  compare,
  summary,
}: {
  colors: Colors;
  audit: RuntimeRunAuditResponse | null;
  compare: RuntimeDiagnosticResultResponse | null;
  summary: RuntimeSummaryResponse | null;
}) {
  const evidence = {
    summary: summary ?? 'Not available',
    latestRunAudit: audit ?? 'Not available',
    compareBvsC: compare ?? 'Not available',
  };
  const markdown = buildEvidenceMarkdown(audit, compare, summary);
  return (
    <Panel colors={colors}>
      <SectionHeader title="Export Evidence" subtitle="Frontend-only export helpers; no backend changes." />
      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        <button
          type="button"
          style={button(colors)}
          onClick={() => copyText(JSON.stringify(audit ?? 'Not available', null, 2))}
        >
          <Clipboard size={16} /> Copy audit JSON
        </button>
        <button
          type="button"
          style={button(colors)}
          onClick={() => downloadText('natureprotector-summary.json', JSON.stringify(evidence, null, 2))}
        >
          <Download size={16} /> Export summary JSON
        </button>
        <button
          type="button"
          style={button(colors)}
          onClick={() => downloadText('natureprotector-summary.md', markdown)}
        >
          <Download size={16} /> Export summary Markdown
        </button>
        <button
          type="button"
          style={button(colors)}
          onClick={() =>
            downloadText('natureprotector-b-vs-c.json', JSON.stringify(compare ?? 'Not available', null, 2))
          }
        >
          <Download size={16} /> Export B/C comparison
        </button>
      </div>
      <CollapsibleJson colors={colors} title="Export preview JSON" value={evidence} />
    </Panel>
  );
}

export function RuntimeChainView({
  colors,
  summary,
  onNavigate,
}: {
  colors: Colors;
  summary: RuntimeSummaryResponse | null;
  onNavigate: (target: 'retry' | 'risk' | 'state' | 'alerts' | 'services') => void;
}) {
  const chain = buildRuntimeChainDetails(summary);
  return (
    <ViewStack>
      <Panel colors={colors}>
        <SectionHeader
          title="Runtime Chain"
          subtitle="Scenario Run -> Event Inbox -> Processing Attempts -> Risk -> State -> Alerts -> API/UI"
        />
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '10px' }}>
          {chain.map((item) => (
            <div
              key={item.label}
              style={{ ...panel(colors), borderLeft: `4px solid ${item.tone}`, minHeight: '154px' }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: '8px' }}>
                <strong>{item.label}</strong>
                <Badge colors={colors}>{item.source}</Badge>
              </div>
              <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '8px' }}>{item.count}</div>
              <div style={paragraph(colors)}>Status: {item.status}</div>
              <div style={paragraph(colors)}>Last update: {item.lastUpdate}</div>
              <div style={paragraph(colors)}>Latest error: {item.latestError}</div>
              {item.navigate && (
                <button
                  type="button"
                  style={{ ...button(colors), marginTop: '8px' }}
                  onClick={() => onNavigate(item.navigate!)}
                >
                  Open related view
                </button>
              )}
            </div>
          ))}
        </div>
      </Panel>
      <CollapsibleJson colors={colors} title="Runtime summary JSON" value={summary ?? 'No data'} />
    </ViewStack>
  );
}

export function ProcessingPipeline({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const stages = [
    ['Ingestion', `${summary?.pipeline.inboxRecent ?? 0} recent inbox rows`, 'pipeline.event_inbox'],
    ['Validation', `${summary?.pipeline.rejectedRecent ?? 0} recent rejected`, 'pipeline.rejected_events'],
    ['Normalization', 'Not exposed', 'normalized reading stage is internal'],
    ['Eligibility', 'Audit summary only', 'eligibility aggregate is not persisted'],
    ['Risk Scoring', `${summary?.risk.recentCount ?? 0} recent assessments`, 'projection.risk_assessment_log'],
    ['Projection', `${summary?.cellOperationalStateCount ?? 0} cell states`, 'projection.cell_operational_state'],
    ['Alert Policy', `${summary?.activeAlerts.length ?? 0} active alerts`, 'projection.alert_state'],
  ];
  return (
    <div style={cardGrid()}>
      {stages.map(([title, status, detail]) => (
        <InfoCard key={title} colors={colors} title={title} status={status} detail={detail} />
      ))}
    </div>
  );
}

export function RetryQuarantine({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  return (
    <ViewStack>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader
          title="P3 Retry, Rejected and Quarantine Paths"
          subtitle="Canonical controlled-validation interpretation; live charts below still come from the current runtime summary window."
        />
        <div style={cardGrid()}>
          <InfoCard
            colors={colors}
            title="Retry then success"
            status="matched"
            detail="P3_RETRY_TRANSIENT_THEN_SUCCESS: 2 attempts, 1 retry, 1 accepted/risk projection."
          />
          <InfoCard
            colors={colors}
            title="Retry exhausted"
            status="matched"
            detail="P3_RETRY_EXHAUSTED_TO_QUARANTINE: 3 attempts, 2 retries, terminal quarantine, no accepted/risk projection."
          />
          <InfoCard
            colors={colors}
            title="Permanent failure"
            status="matched"
            detail="P3_PERMANENT_FAILURE_TO_QUARANTINE: 1 attempt, terminal quarantine."
          />
          <InfoCard
            colors={colors}
            title="Pre-inbox rejected"
            status="5 matched"
            detail="Invalid JSON, missing payload, unsupported type/version and invalid operational state never become accepted readings."
          />
        </div>
        <SimpleTable
          colors={colors}
          columns={['Fault case', 'Path', 'Expected code/path', 'Status', 'Projection expectation']}
          rows={p3CaseRows().filter(
            (row) =>
              String(row[1]).includes('Retry') ||
              String(row[1]).includes('Quarantined') ||
              String(row[1]).includes('Rejected'),
          )}
        />
      </Panel>
      <div style={cardGrid()}>
        <ChartPanel colors={colors} title="Attempts by Outcome">
          <BarGraph
            data={(summary?.pipeline.attemptsByOutcomeAndError ?? []).map((item) => ({
              name: item.errorCode ? `${item.outcome}/${item.errorCode}` : item.outcome,
              value: item.count,
            }))}
            color="#7c3aed"
          />
        </ChartPanel>
        <ChartPanel colors={colors} title="Failed Attempts by Error">
          <BarGraph
            data={(summary?.pipeline.attemptsByOutcomeAndError ?? [])
              .filter((item) => item.errorCode || !/success|completed|accepted/i.test(item.outcome))
              .map((item) => ({ name: item.errorCode ?? item.outcome, value: item.count }))}
            color="#b45309"
          />
        </ChartPanel>
        <ChartPanel colors={colors} title="Rejected by Code">
          <BarGraph
            data={(summary?.pipeline.rejectedByCode ?? []).map((item) => ({ name: item.code, value: item.count }))}
            color="#dc2626"
          />
        </ChartPanel>
        <ChartPanel colors={colors} title="Quarantined by Code">
          <BarGraph
            data={(summary?.pipeline.quarantinedByCode ?? []).map((item) => ({ name: item.code, value: item.count }))}
            color="#ea580c"
          />
        </ChartPanel>
      </div>
      <div style={twoCol()}>
        <Panel colors={colors}>
          <SectionHeader title="Latest Rejected" />
          <EventRows
            colors={colors}
            rows={(summary?.pipeline.latestRejected ?? []).map((item) => [
              item.rejectionCode,
              item.rejectionReason,
              formatDate(item.rejectedAt),
            ])}
            empty="No recent rejected events."
          />
        </Panel>
        <Panel colors={colors}>
          <SectionHeader title="Latest Quarantined" />
          <EventRows
            colors={colors}
            rows={(summary?.pipeline.latestQuarantined ?? []).map((item) => [
              item.quarantineCode,
              item.quarantineReason,
              formatDate(item.quarantinedAt),
            ])}
            empty="No recent quarantined events."
          />
        </Panel>
        <Panel colors={colors}>
          <SectionHeader title="Latest Failed Attempts" />
          <EventRows
            colors={colors}
            rows={(summary?.pipeline.latestFailedAttempts ?? []).map((item) => [
              item.errorCode ?? item.outcome,
              `${item.stage} / attempt ${item.attemptNumber} / ${item.errorMessage ?? 'No error message'}`,
              `${formatDate(item.startedAt)} -> ${formatDate(item.finishedAt)}`,
            ])}
            empty="No recent failed attempts."
          />
        </Panel>
      </div>
      <Banner colors={colors} tone="#64748b">
        Retry and quarantine are backend pipeline concerns: invalid events may be rejected before inbox persistence;
        failed processing attempts may retry; terminal poison cases are quarantined. Counts here come from persisted
        pipeline summaries and diagnostics.
      </Banner>
    </ViewStack>
  );
}

export function PersistenceViews({
  colors,
  tableCounts,
}: {
  colors: Colors;
  tableCounts: RuntimeDiagnosticResultResponse | null;
}) {
  const tables = [
    'control.simulation_runs',
    'pipeline.event_inbox',
    'pipeline.processing_attempts',
    'pipeline.rejected_events',
    'pipeline.quarantined_events',
    'projection.risk_assessment_log',
    'projection.area_risk_snapshot_log',
    'projection.cell_operational_state',
    'projection.area_operational_state',
    'projection.alert_state',
  ];
  const countFor = (tableName: string) => {
    const [schema, table] = tableName.split('.');
    const row = tableCounts?.rows.find((item) => item.schema === schema && item.table === table);
    return row?.count ?? 'Not exposed yet';
  };
  return (
    <div style={cardGrid()}>
      {tables.map((table) => (
        <InfoCard
          key={table}
          colors={colors}
          title={table}
          status={countFor(table)}
          detail="Runtime/persistence view"
        />
      ))}
    </div>
  );
}

export function DeploymentServices({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const services = [
    ['Web UI', 'Loaded', 'Current browser app'],
    ['Backoffice API', summary ? 'Reachable' : 'Unknown', 'runtime summary endpoint'],
    ['PostgreSQL', summary ? 'Reachable through API' : 'Unknown', 'No direct health endpoint in UI'],
    ['RabbitMQ', 'Not exposed', 'No management adapter'],
    ['Prevention Host', 'Not exposed', 'No heartbeat endpoint'],
    ['Simulator Host', summary?.currentRun ? 'Run active' : 'Unknown', 'Observed through simulation_runs'],
    ['InfluxDB', 'Not exposed', 'No health endpoint in UI'],
    ['Grafana', 'Unknown', 'Embeds are attempted when dashboards load'],
  ];
  return (
    <div style={cardGrid()}>
      {services.map(([title, status, detail]) => (
        <InfoCard key={title} colors={colors} title={title} status={status} detail={detail} />
      ))}
    </div>
  );
}

export function NominalFlow({
  colors,
  summary,
  audit,
  runResult,
}: {
  colors: Colors;
  summary: RuntimeSummaryResponse | null;
  audit: RuntimeRunAuditResponse | null;
  runResult: RuntimeRunStartResponse | null;
}) {
  const steps = buildNominalFlowSteps(summary, audit, runResult);
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Nominal Flow"
        subtitle="Semi-live timeline inferred from persisted runtime summary, latest run audit and latest UI run response."
      />
      <div style={{ display: 'grid', gap: '8px' }}>
        {steps.map((step, index) => (
          <div
            key={step.name}
            style={{
              ...panel(colors),
              display: 'grid',
              gridTemplateColumns: '42px 150px 1fr',
              gap: '10px',
              alignItems: 'center',
              borderLeft: `4px solid ${statusTone(step.status)}`,
            }}
          >
            <Badge colors={colors}>{String(index + 1).padStart(2, '0')}</Badge>
            <div>
              <strong>{step.name}</strong>
              <div style={{ color: statusTone(step.status), fontSize: '12px', fontWeight: 800 }}>{step.status}</div>
            </div>
            <div style={paragraph(colors)}>{step.evidence}</div>
          </div>
        ))}
      </div>
      <Banner colors={colors} tone="#64748b">
        Statuses marked as Done or Partial are frontend inferences from exposed counts and timestamps; they are not a
        separate backend workflow state machine.
      </Banner>
    </Panel>
  );
}

export function DomainModel({ colors }: { colors: Colors }) {
  return (
    <ViewStack>
      <Banner colors={colors} tone="#2563eb">
        This page maps report concepts to implementation and UI evidence. It separates conceptual domain language from
        persisted runtime artifacts and visible UI widgets.
      </Banner>
      <div style={cardGrid()}>
        {MODEL_ARTIFACTS.map((item) => (
          <InfoCard
            key={item.concept}
            colors={colors}
            title={item.concept}
            status={item.status}
            detail={`${item.persistence}; ${item.uiEvidence}`}
          />
        ))}
      </div>
    </ViewStack>
  );
}

export function DataChain({ colors }: { colors: Colors }) {
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Data Chain"
        subtitle="Conceptual-to-runtime chain with implementation, persistence and UI visibility state."
      />
      <SimpleTable
        colors={colors}
        columns={['Node', 'Status', 'Persistence', 'UI evidence', 'Code reference']}
        rows={MODEL_ARTIFACTS.map((item) => [item.concept, item.status, item.persistence, item.uiEvidence, item.code])}
      />
    </Panel>
  );
}

export function DataProvenance({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const cards = [
    [
      'Simulated data',
      'Runtime evidence is generated by controlled scenarios. It is not presented as field validation of real wildfire prediction.',
    ],
    [
      'Scenario parameters',
      'Scenario code, cycles, seed, sensor count and degradation profile define the operational experiment.',
    ],
    [
      'Candidate parameter set',
      'Risk score V1 is a candidate operational parameter set, useful for comparison and traceability, not final scientific calibration.',
    ],
    [
      'FWI/KBDI provenance',
      'FWI/KBDI-like context is treated as provenance/candidate context unless a validated scientific calibration is exposed.',
    ],
    [
      'Missing readings',
      'Missing readings are represented by degradation settings plus expected-vs-accepted audit arithmetic.',
    ],
    [
      'Freshness/carry-forward',
      'Persisted projections can carry forward state; UI labels distinguish recent risk rows from current area state.',
    ],
    [
      'Limitations',
      'Unknown or unavailable facts remain explicitly marked as Not exposed, Not available, Not instrumented or No data.',
    ],
  ];
  return (
    <ViewStack>
      <div style={cardGrid()}>
        {cards.map(([title, detail]) => (
          <InfoCard key={title} colors={colors} title={title} status="Provenance" detail={detail} />
        ))}
      </div>
      <Panel colors={colors} accent="#0891b2">
        <SectionHeader
          title="NP vs FWI/KBDI"
          subtitle="Persisted comparison/provenance values; no frontend scoring or scientific validation claim."
        />
        <KeyValues
          colors={colors}
          rows={[
            ['Parameter set', summary?.scoreComponents?.parameterSetVersion ?? 'Not available'],
            ['NP adjusted score', formatMaybeScore(summary?.scoreComponents?.adjustedScore)],
            [
              'FWI / normalized',
              `${formatMaybeScore(summary?.indexComparison?.fireWeatherIndex)} / ${formatMaybeScore(summary?.indexComparison?.normalizedFireWeatherIndex)}`,
            ],
            [
              'FWI IPMA class',
              `${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? 'Not available'}; next ${summary?.indexComparison?.fireWeatherNextIpmaClass ?? 'n/a'}`,
            ],
            [
              'KBDI / normalized',
              `${formatMaybeScore(summary?.indexComparison?.keetchByramDroughtIndex)} / ${formatMaybeScore(summary?.indexComparison?.normalizedKeetchByramDroughtIndex)}`,
            ],
            [
              'KBDI dryness',
              `${summary?.indexComparison?.kbdiDrynessClassLabel ?? 'Not available'}; ${summary?.indexComparison?.kbdiAntecedentHistoryQuality ?? 'antecedent n/a'}`,
            ],
            [
              'Portuguese Context Proxy',
              summary?.indexComparison?.portugueseContextRiskProxyLabel ??
                summary?.indexComparison?.portugueseContextRiskProxyClass ??
                'Not available',
            ],
            ['Local FWI percentile', summary?.indexComparison?.localFwiPercentileStatus ?? 'Not available'],
            [
              'FWI/KBDI status',
              `${summary?.indexComparison?.fireWeatherCalculationStatus ?? 'FWI n/a'}; ${summary?.indexComparison?.kbdiCalculationStatus ?? 'KBDI n/a'}`,
            ],
            [
              'Limitations',
              summary?.scoreComponents?.limitations ?? summary?.indexComparison?.limitations ?? 'None exposed',
            ],
          ]}
        />
      </Panel>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader
          title="RBAC readiness note"
          subtitle="Conceptual role-based visibility plan; not security enforcement."
        />
        <SimpleTable
          colors={colors}
          columns={['Role', 'Future UI access']}
          rows={[
            ['Viewer', 'Monitoring; basic Model & Provenance'],
            ['Analyst', 'Monitoring; Evidence; Compare B/C'],
            ['Operator', 'Scenario Lab; Run Orchestrator'],
            ['Developer', 'Flow Explorer; Diagnostics; Raw JSON'],
            ['Admin', 'Runtime State Control; Reset; future user/role management'],
          ]}
        />
        <Banner colors={colors} tone="#b45309">
          Role-based visibility can be applied to tabs and actions, but backend authorization is required for
          enforcement. Frontend visibility is not security.
        </Banner>
      </Panel>
    </ViewStack>
  );
}

export function V3ReadinessView({ colors }: { colors: Colors }) {
  const artifacts = [
    ['Controlled validation P3 summary', P3_CANONICAL.summary, 'Negative pipeline closure summary.'],
    ['Query pack manifest', `${P3_CANONICAL.queryPack}manifest.md`, 'Read-only extraction inventory.'],
    [
      'M3 label support',
      `${P3_CANONICAL.queryPack}postgres/p3_m3_label_support.csv`,
      'Label support/readiness evidence only.',
    ],
    [
      'M5 negative traceability',
      `${P3_CANONICAL.queryPack}postgres/p3_negative_m5_traceability.csv`,
      'Traceability across negative paths.',
    ],
    [
      'Unexpected accepted/risk guard',
      `${P3_CANONICAL.queryPack}postgres/p3_unexpected_accepted_or_risk.csv`,
      'Guards against positive projections for negative cases.',
    ],
  ];
  return (
    <ViewStack>
      <Banner colors={colors} tone="#7c3aed">
        V3 here means data audit and controlled-validation readiness. It does not mean trained ML, shadow runtime,
        dataset publication, GNN implementation or final scientific calibration.
      </Banner>
      <Panel colors={colors} accent="#2563eb">
        <SectionHeader title="Validation Readiness" subtitle="Current evidence state by phase and milestone." />
        <SimpleTable
          colors={colors}
          columns={['Phase/milestone', 'State', 'Evidence boundary']}
          rows={VALIDATION_PHASE_ROWS}
        />
      </Panel>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader title="V3 Artifacts" subtitle="Repository evidence paths; not frontend-generated data." />
        <SimpleTable colors={colors} columns={['Artifact', 'Path', 'Use']} rows={artifacts} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Blocked/Future Work" />
        <SimpleTable
          colors={colors}
          columns={['Item', 'State', 'Reason']}
          rows={[
            [
              'sensor_inactive',
              'blocked_needs_fixture',
              'Requires a safe fixture instead of mutating nominal sensors.',
            ],
            [
              'sensor_area_mismatch',
              'blocked_needs_fixture',
              'Requires a safe two-area/controlled event fixture instead of changing real relationships.',
            ],
            [
              'P3 UI generation',
              'not wired',
              'Requires guarded Development/Evidence backend endpoint with allowlist and evidence reporting.',
            ],
            ['ML/GNN', 'not implemented', 'Out of scope for this controlled-validation UI representation mission.'],
          ]}
        />
      </Panel>
    </ViewStack>
  );
}

export function TerritorialContext({
  colors,
  cells,
  sensors,
  summary,
}: {
  colors: Colors;
  cells: AreaCellResponse[];
  sensors: SensorNodeResponse[];
  summary: RuntimeSummaryResponse | null;
}) {
  return (
    <div style={cardGrid()}>
      <InfoCard
        colors={colors}
        title="Area context"
        status={summary?.areaCode ?? 'Not available'}
        detail="Selected workspace area"
      />
      <InfoCard
        colors={colors}
        title="Grid context"
        status={`${cells.length} cells`}
        detail="Read from grid-cells endpoint"
      />
      <InfoCard
        colors={colors}
        title="Sensor context"
        status={`${sensors.length} sensors`}
        detail={`${sensors.filter((item) => item.isActive).length} active`}
      />
      <InfoCard colors={colors} title="Weather variables" status="Not exposed" detail="Use dashboards when available" />
      <InfoCard
        colors={colors}
        title="Daily state"
        status="Not exposed"
        detail="No dedicated daily state endpoint in this UI"
      />
      <InfoCard
        colors={colors}
        title="Territorial risk"
        status={summary?.areaOperationalState?.aggregateRiskLevel ?? 'Not available'}
        detail="Projection-backed operational risk"
      />
    </div>
  );
}

export function CodeMapping({ colors }: { colors: Colors }) {
  const rows = [
    [
      'Scenario orchestration',
      'Implemented',
      'control.simulation_runs',
      'Scenario Lab / Latest Run',
      'SimulationRunner',
    ],
    ['SimulationRun', 'Implemented', 'Persisted', 'Top bar; Latest Run; Run Timings', 'SimulationRun'],
    ['TruthSnapshot', 'Implemented', 'Transient', 'Model only', 'TruthSnapshot'],
    ['LocalObservation', 'Implemented', 'Transient', 'Model only', 'LocalObservation'],
    [
      'EventEnvelope',
      'Implemented',
      'pipeline.event_inbox',
      'Runtime Chain / Persistence Views',
      'EventEnvelope<TPayload>',
    ],
    ['PreventionWorker', 'Implemented', 'Runtime service', 'Flow Explorer', 'PreventionWorker'],
    [
      'ReadingRiskPipeline',
      'Implemented',
      'Processing attempts / projections',
      'Processing Pipeline',
      'ReadingRiskPipeline',
    ],
    [
      'RiskEligibilityService',
      'Partial UI evidence',
      'Aggregate audit only',
      'Latest Run Audit',
      'RiskEligibilityService',
    ],
    [
      'SimpleRiskScoringService',
      'Implemented',
      'projection.risk_assessment_log',
      'Area Risk / Evidence',
      'SimpleRiskScoringService',
    ],
    ['DailyCellState', 'Implemented', 'Projection/carry-forward state', 'Territorial Context', 'DailyCellState'],
    ['RiskAssessment', 'Implemented', 'projection.risk_assessment_log', 'Area Risk chart', 'RiskAssessment'],
    [
      'AreaRiskSnapshot',
      'Implemented',
      'projection.area_risk_snapshot_log',
      'Latest Run Audit / Area Risk',
      'AreaRiskSnapshot',
    ],
    ['V1AlertPolicy', 'Implemented', 'projection.alert_state', 'Alerts', 'V1AlertPolicy'],
    [
      'Projection store',
      'Implemented',
      'projection.*',
      'Monitoring / Persistence Views',
      'PostgresAreaOperationalProjectionStore',
    ],
  ];
  return (
    <Panel colors={colors}>
      <SectionHeader
        title="Code Mapping"
        subtitle="Report concepts mapped to implementation state, persistence and visible UI evidence."
      />
      <SimpleTable colors={colors} columns={['Concept', 'Status', 'Persistence', 'UI evidence', 'Code']} rows={rows} />
    </Panel>
  );
}
