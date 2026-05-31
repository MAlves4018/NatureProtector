import { useCallback, useEffect, useMemo, useState } from "react";
import type { Dispatch, ReactNode, SetStateAction } from "react";
import { useNavigate, useParams } from "react-router-dom";
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
  Moon,
  Play,
  RefreshCw,
  RotateCcw,
  Search,
  Server,
  ShieldCheck,
  Sun,
} from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { AreaMap } from "../mainComponents/AreaMap";
import { api } from "../../services/api";
import {
  AreaCellResponse,
  AreaResponse,
  RuntimeAlertSummaryResponse,
  RuntimeDiagnosticDefinitionResponse,
  RuntimeDiagnosticResultResponse,
  RuntimeProcessingAttemptResponse,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SensorNodeResponse,
} from "../../types";
import { getColors } from "../../utils/utils";
import { useToken } from "../../context/TokenContext";
import { LoggedOutBlock } from "../components/LoggedOutBlock";

type Colors = ReturnType<typeof getColors>;

const DEFAULT_AREA = "proenca-a-nova";
const WINDOW_OPTIONS = [10, 30, 1440];
const MAIN_TABS = ["Monitoring", "Scenario Lab", "Flow Explorer", "Evidence & Comparison", "Model & Provenance"] as const;
const MONITORING_TABS = ["Overview", "Map & Cells", "Sensor Dashboards", "Area Risk", "Alerts"] as const;
const SCENARIO_TABS = ["Run Orchestrator", "Scenario Definition", "Latest Run", "Runtime State Control"] as const;
const EVIDENCE_TABS = ["Latest Run Audit", "Compare B vs C", "Run Timings", "Diagnostics", "Export Evidence"] as const;
const FLOW_TABS = ["Runtime Chain", "Processing Pipeline", "Retry & Quarantine", "Persistence Views", "Deployment & Services", "Nominal Flow"] as const;
const MODEL_TABS = ["Domain Model", "Data Chain", "Data Provenance", "Territorial & Weather Context", "Code Mapping"] as const;
const DEGRADATION_PROFILE_OPTIONS = [
  "none",
  "missing-readings",
  "noise",
  "bias",
  "drift",
  "stuck-value",
  "outlier",
  "clipping/range",
  "lag/delay",
  "duplicate",
  "out-of-order",
];

const MODEL_ARTIFACTS = [
  { concept: "ScenarioDefinition", status: "Implemented", persistence: "Persisted", uiEvidence: "Scenario Definition / Run Orchestrator", code: "ScenarioDefinition" },
  { concept: "TruthSnapshot", status: "Implemented", persistence: "Transient", uiEvidence: "Not exposed", code: "TruthSnapshot" },
  { concept: "LocalObservation", status: "Implemented", persistence: "Transient", uiEvidence: "Not exposed", code: "LocalObservation" },
  { concept: "OperationalEvent", status: "Implemented", persistence: "pipeline.event_inbox", uiEvidence: "Runtime Chain / Persistence Views", code: "EventEnvelope<TPayload>" },
  { concept: "NormalizedReading", status: "Partial UI evidence", persistence: "accepted_reading_log", uiEvidence: "Latest Run Audit", code: "ReadingRiskPipeline" },
  { concept: "DailyCellState", status: "Implemented", persistence: "projection.cell_operational_state", uiEvidence: "Freshness / Territorial Context", code: "DailyCellState" },
  { concept: "RiskInput", status: "Implemented", persistence: "Transient", uiEvidence: "Not exposed", code: "RiskEligibilityService" },
  { concept: "RiskAssessment", status: "Implemented", persistence: "projection.risk_assessment_log", uiEvidence: "Area Risk / Latest Run Audit", code: "RiskAssessment" },
  { concept: "AreaRiskSnapshot", status: "Implemented", persistence: "projection.area_risk_snapshot_log", uiEvidence: "Latest Run Audit / Area Risk", code: "AreaRiskSnapshot" },
  { concept: "AlertState", status: "Implemented", persistence: "projection.alert_state", uiEvidence: "Monitoring / Alerts", code: "V1AlertPolicy" },
  { concept: "OperationalProjection", status: "Implemented", persistence: "projection.*", uiEvidence: "Monitoring / Flow Explorer", code: "PostgresAreaOperationalProjectionStore" },
];

export function Workspace({ isDark, setIsDark }: { isDark: boolean; setIsDark: Dispatch<SetStateAction<boolean>> }) {
  const { token, user } = useToken();
  const colors = getColors(isDark);
  const navigate = useNavigate();
  const { areaCode: areaCodeParam } = useParams<{ areaCode: string }>();
  const [areaCode, setAreaCode] = useState(areaCodeParam || DEFAULT_AREA);
  const [areas, setAreas] = useState<AreaResponse[]>([]);
  const [mainTab, setMainTab] = useState<(typeof MAIN_TABS)[number]>("Monitoring");
  const [monitoringTab, setMonitoringTab] = useState<(typeof MONITORING_TABS)[number]>("Overview");
  const [scenarioTab, setScenarioTab] = useState<(typeof SCENARIO_TABS)[number]>("Run Orchestrator");
  const [evidenceTab, setEvidenceTab] = useState<(typeof EVIDENCE_TABS)[number]>("Latest Run Audit");
  const [flowTab, setFlowTab] = useState<(typeof FLOW_TABS)[number]>("Runtime Chain");
  const [modelTab, setModelTab] = useState<(typeof MODEL_TABS)[number]>("Data Chain");
  const [recentMinutes, setRecentMinutes] = useState(30);
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [runAudit, setRunAudit] = useState<RuntimeRunAuditResponse | null>(null);
  const [runTimings, setRunTimings] = useState<RuntimeRunTimingSummaryResponse | null>(null);
  const [runTimingsMessage, setRunTimingsMessage] = useState<string | null>(null);
  const [diagnostics, setDiagnostics] = useState<RuntimeDiagnosticDefinitionResponse[]>([]);
  const [diagnosticResult, setDiagnosticResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [selectedDiagnostic, setSelectedDiagnostic] = useState("runtime-table-counts");
  const [compareResult, setCompareResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [tableCounts, setTableCounts] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [scenarios, setScenarios] = useState<ScenarioResponse[]>([]);
  const [sensorNodes, setSensorNodes] = useState<SensorNodeResponse[]>([]);
  const [areaId, setAreaId] = useState("");
  const [geoJSON, setGeoJSON] = useState<any>(null);
  const [cells, setCells] = useState<AreaCellResponse[]>([]);
  const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);
  const [areaRiskDashboardLink, setAreaRiskDashboardLink] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [runResult, setRunResult] = useState<RuntimeRunStartResponse | null>(null);
  const [runMessage, setRunMessage] = useState<string | null>(null);
  const [submittingRun, setSubmittingRun] = useState(false);
  const [resetResult, setResetResult] = useState<RuntimeResetResponse | null>(null);
  const [confirm, setConfirm] = useState("");
  const canAccessScenarioLab = Boolean(user && (user.roles.includes("Sim") || user.roles.includes("Admin")));
  const visibleMainTabs = canAccessScenarioLab ? MAIN_TABS : MAIN_TABS.filter(tab => tab !== "Scenario Lab");
  const [runForm, setRunForm] = useState<RuntimeRunStartRequest>({
    areaCode,
    scenarioCode: "scenario_b",
    sensorCount: 6,
    numberOfCycles: 5,
    intervalSeconds: 5,
    seed: 12345,
    degradationProfile: "none",
    degradationProfiles: ["none"],
    collectEvidence: false,
    waitForCompletion: false,
    timeoutSeconds: 180,
    allowParallelRun: false,
    runLabel: "scenario-b-from-ui",
  });

  useEffect(() => {
    if (areaCodeParam && areaCodeParam !== areaCode) {
      setAreaCode(areaCodeParam);
    }
  }, [areaCodeParam, areaCode]);

  useEffect(() => {
    setRunForm(value => ({ ...value, areaCode }));
  }, [areaCode]);

  useEffect(() => {
    fetch("/area_dashboards_links.txt")
      .then(response => response.text())
      .then(text => setDashboardLinks(text.split("\n").map(line => line.trim()).filter(Boolean)))
      .catch(() => setDashboardLinks([]));

    fetch("/area_risk_link.txt")
      .then(response => response.text())
      .then(text => setAreaRiskDashboardLink(text.split("\n").map(line => line.trim()).find(Boolean) ?? null))
      .catch(() => setAreaRiskDashboardLink(null));
  }, []);


  const loadPublicWorkspace = useCallback(async () => {
    setLoading(true);
    try {
      const [areaList, sensors, geo, cellRows] = await Promise.all([
        api.getAreas(),
        api.getAreaSensorNodes(areaCode),
        api.getAreaGeoJSON(areaCode),
        api.getAreaCells(areaCode),
      ]);

      //console.log("geojson", geo.geometryGeoJson);
      //console.log("cells", cellRows);
      //console.log("sensors", sensors);

      setAreas(areaList);
      setSensorNodes(sensors);
      setAreaId(geo.id);
      setGeoJSON(parseJson(geo.geometryGeoJson));
      setCells(cellRows);
      setMessage(null);

    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  }, [areaCode]);


  const loadWorkspacePipeline = useCallback(async () => {
    setLoading(true);
    try {
      const [summaryResult, diagnosticCatalog, areaScenarios] = await Promise.all([
        api.getRuntimeSummary(areaCode, recentMinutes),
        api.getRuntimeDiagnostics(),
        api.getAreaScenarios(areaCode),
      ]);

      setSummary(summaryResult);
      setDiagnostics(diagnosticCatalog.diagnostics);
      setScenarios(areaScenarios);
      setLastUpdated(new Date());
      setMessage(null);
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  }, [areaCode, recentMinutes, diagnosticResult]);

  const loadWorkspaceSim = useCallback(async () => {
    setLoading(true);

    try {
      const [diagnosticCatalog] = await Promise.all([
        api.getRuntimeDiagnostics(),
      ]);

      setDiagnostics(diagnosticCatalog.diagnostics);
      setLastUpdated(new Date());
      setMessage(null);

      if (summary?.latestRun?.id) {
        setRunAudit(await api.getRuntimeRunAudit(summary.latestRun.id));
        try {
          setRunTimings(await api.getRuntimeRunTimings(summary.latestRun.id));
          setRunTimingsMessage(null);
        } catch (error) {
          setRunTimings(null);
          setRunTimingsMessage(`Run timings endpoint unavailable; using runtime summary fallback. ${formatError(error)}`);
        }
      } else {
        setRunAudit(null);
        setRunTimings(null);
        setRunTimingsMessage(null);
      }

      const compare = await api.executeRuntimeDiagnostic("compare-latest-b-vs-c", { areaCode, recentMinutes, scenarioCode: "scenario_b" });
      setCompareResult(compare);
      const counts = await api.executeRuntimeDiagnostic("runtime-table-counts", { areaCode, recentMinutes });
      setTableCounts(counts);
      if (!diagnosticResult) {
        setDiagnosticResult(counts);
      }
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  }, [areaCode, recentMinutes, diagnosticResult]);

  useEffect(() => {
    if (user) {
      if (user.roles.includes("Sim") || user.roles.includes("Admin")) {
        loadWorkspaceSim();
        loadWorkspacePipeline();
      }
      else if (user.roles.includes("Pipeline")) {
        loadWorkspacePipeline();
      }
    }
    loadPublicWorkspace();
  }, [loadWorkspacePipeline, loadWorkspaceSim, loadPublicWorkspace, user]);

  const displayRun = summary?.currentRun ?? summary?.latestRun ?? null;
  const activeSensorCount = sensorNodes.filter(sensor => sensor.isActive).length;
  const sensorCountTooHigh = runForm.sensorCount != null && activeSensorCount > 0 && runForm.sensorCount > activeSensorCount;

  const changeArea = (nextArea: string) => {
    setAreaCode(nextArea);
    navigate(`/workspace/${nextArea}`);
  };

  const executeDiagnostic = async (id = selectedDiagnostic) => {
    if (!id) return;
    setLoading(true);
    try {
      const result = await api.executeRuntimeDiagnostic(id, { areaCode, recentMinutes, scenarioCode: runForm.scenarioCode });
      setDiagnosticResult(result);
      setSelectedDiagnostic(id);
      setMessage(null);
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  };

  const startRun = async () => {
    if (sensorCountTooHigh) {
      setRunMessage(`sensorCount ${runForm.sensorCount} exceeds ${activeSensorCount} active sensor(s) for area '${areaCode}'.`);
      return;
    }

    setSubmittingRun(true);
    setRunMessage("Submitting run request...");
    try {
      const result = await api.startRuntimeRun({ ...runForm, areaCode });
      setRunResult(result);
      setRunMessage(result.message);
      await loadWorkspaceSim();
    } catch (error) {
      setRunMessage(formatError(error));
    } finally {
      setSubmittingRun(false);
    }
  };

  const resetRuntime = async (dryRun: boolean) => {
    setLoading(true);
    try {
      const result = await api.resetRuntimeState({ scope: "runtime-only", confirm, dryRun });
      setResetResult(result);
      setMessage(result.message);
      await loadWorkspaceSim();
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main style={{ minHeight: "calc(100vh - 58px)", background: colors.pageBg, color: colors.textPrimary, padding: "18px", fontFamily: "system-ui, -apple-system, sans-serif" }}>
      <WorkspaceTopBar
        colors={colors}
        isDark={isDark}
        setIsDark={setIsDark}
        areaCode={areaCode}
        areas={areas}
        latestRun={displayRun}
        recentMinutes={recentMinutes}
        setRecentMinutes={setRecentMinutes}
        lastUpdated={lastUpdated}
        loading={loading}
        onAreaChange={changeArea}
        onRefresh={
          () => {
            if (user?.roles.includes("Sim") || user?.roles.includes("Admin")) {
              loadWorkspaceSim();
              loadWorkspacePipeline();
            }
            else if (user?.roles.includes("Pipeline")) {
              loadWorkspacePipeline();
            }
            loadPublicWorkspace();
          }
        }
      />

      {message && <Banner colors={colors} tone="#b45309">{message}</Banner>}

      <Tabs values={visibleMainTabs} selected={mainTab} onSelect={setMainTab} colors={colors} />

      {mainTab === "Monitoring" && (
        <WorkspacePanel colors={colors}>
          <Tabs values={MONITORING_TABS} selected={monitoringTab} onSelect={setMonitoringTab} colors={colors} compact />
          {monitoringTab === "Overview" && <MonitoringOverview colors={colors} summary={summary} run={displayRun} audit={runAudit} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes} areaId={areaId} />}
          {monitoringTab === "Map & Cells" && <MapAndCells colors={colors} areaId={areaId} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes} />}
          {monitoringTab === "Sensor Dashboards" && <SensorDashboards colors={colors} areaId={areaId} dashboardLinks={dashboardLinks} />}
          {monitoringTab === "Area Risk" && <AreaRiskView colors={colors} areaId={areaId} summary={summary} dashboardLink={areaRiskDashboardLink} />}
          {monitoringTab === "Alerts" && <AlertsView colors={colors} alerts={summary?.activeAlerts ?? []} />}
        </WorkspacePanel>
      )}

      {mainTab === "Scenario Lab" && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={SCENARIO_TABS} selected={scenarioTab} onSelect={setScenarioTab} colors={colors} compact />
              {scenarioTab === "Run Orchestrator" && <RunOrchestrator colors={colors} scenarios={scenarios} sensorCountTooHigh={sensorCountTooHigh} activeSensorCount={activeSensorCount} runForm={runForm} setRunForm={setRunForm} startRun={startRun} submittingRun={submittingRun} runResult={runResult} runMessage={runMessage} areaCode={areaCode} setMainTab={setMainTab} setScenarioTab={setScenarioTab} />}
              {scenarioTab === "Scenario Definition" && <ScenarioDefinition colors={colors} scenarios={scenarios} />}
              {scenarioTab === "Latest Run" && <LatestRunView colors={colors} run={displayRun} />}
              {scenarioTab === "Runtime State Control" && <RuntimeStateControl colors={colors} confirm={confirm} setConfirm={setConfirm} resetRuntime={resetRuntime} loading={loading} resetResult={resetResult} />}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === "Evidence & Comparison" && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={EVIDENCE_TABS} selected={evidenceTab} onSelect={setEvidenceTab} colors={colors} compact />
              {evidenceTab === "Latest Run Audit" && <LatestRunAuditView colors={colors} audit={runAudit} />}
              {evidenceTab === "Compare B vs C" && <CompareBvsC colors={colors} compare={compareResult} />}
              {evidenceTab === "Run Timings" && <RunTimings colors={colors} run={displayRun} summary={summary} audit={runAudit} timings={runTimings} timingsMessage={runTimingsMessage} />}
              {evidenceTab === "Diagnostics" && <DiagnosticsView colors={colors} diagnostics={diagnostics} selectedDiagnostic={selectedDiagnostic} diagnosticResult={diagnosticResult} executeDiagnostic={executeDiagnostic} loading={loading} />}
              {evidenceTab === "Export Evidence" && <ExportEvidence colors={colors} audit={runAudit} compare={compareResult} summary={summary} />}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === "Flow Explorer" && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={FLOW_TABS} selected={flowTab} onSelect={setFlowTab} colors={colors} compact />
              {flowTab === "Runtime Chain" && <RuntimeChainView colors={colors} summary={summary} onNavigate={(target) => {
                if (target === "retry") setFlowTab("Retry & Quarantine");
                if (target === "state") setFlowTab("Persistence Views");
                if (target === "services") setFlowTab("Deployment & Services");
                if (target === "risk") { setMainTab("Monitoring"); setMonitoringTab("Area Risk"); }
                if (target === "alerts") { setMainTab("Monitoring"); setMonitoringTab("Alerts"); }
              }} />}
              {flowTab === "Processing Pipeline" && <ProcessingPipeline colors={colors} summary={summary} />}
              {flowTab === "Retry & Quarantine" && <RetryQuarantine colors={colors} summary={summary} />}
              {flowTab === "Persistence Views" && <PersistenceViews colors={colors} tableCounts={tableCounts} />}
              {flowTab === "Deployment & Services" && <DeploymentServices colors={colors} summary={summary} />}
              {flowTab === "Nominal Flow" && <NominalFlow colors={colors} summary={summary} audit={runAudit} runResult={runResult} />}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === "Model & Provenance" && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
                <Tabs values={MODEL_TABS} selected={modelTab} onSelect={setModelTab} colors={colors} compact />
                {modelTab === "Domain Model" && <DomainModel colors={colors} />}
                {modelTab === "Data Chain" && <DataChain colors={colors} />}
                {modelTab === "Data Provenance" && <DataProvenance colors={colors} summary={summary} />}
                {modelTab === "Territorial & Weather Context" && <TerritorialContext colors={colors} cells={cells} sensors={sensorNodes} summary={summary} />}
                {modelTab === "Code Mapping" && <CodeMapping colors={colors} />}
            </>
          )}
        </WorkspacePanel>
         </main>
  );
}

function WorkspaceTopBar(props: {
  colors: Colors;
  isDark: boolean;
  setIsDark: Dispatch<SetStateAction<boolean>>;
  areaCode: string;
  areas: AreaResponse[];
  latestRun: RuntimeRunSummaryResponse | null;
  recentMinutes: number;
  setRecentMinutes: (value: number) => void;
  lastUpdated: Date | null;
  loading: boolean;
  onAreaChange: (value: string) => void;
  onRefresh: () => void;
}) {
  const { colors, isDark, setIsDark, areaCode, areas, latestRun, recentMinutes, setRecentMinutes, lastUpdated, loading, onAreaChange, onRefresh } = props;
  return (
    <section style={{ ...panel(colors), marginBottom: "12px", display: "grid", gridTemplateColumns: "minmax(180px, 1fr) auto", gap: "12px", alignItems: "center", position: "sticky", top: 0, zIndex: 10 }}>
      <div style={{ display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" }}>
        <select style={{ ...input(colors), width: "190px" }} value={areaCode} onChange={event => onAreaChange(event.target.value)} aria-label="Area">
          {areas.length === 0 && <option value={areaCode}>{areaCode}</option>}
          {areas.map(area => <option key={area.code} value={area.code}>{area.code}</option>)}
        </select>
        <Pill colors={colors} label="Latest Run" value={latestRun?.scenarioCode ?? "No data"} />
        <Pill colors={colors} label="Scenario" value={latestRun?.scenarioName ?? "Not available"} />
        <Pill colors={colors} label="Status" value={latestRun?.status ?? "No run"} />
        <Pill colors={colors} label="Updated" value={lastUpdated ? lastUpdated.toLocaleTimeString() : "Pending"} />
      </div>
      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", justifyContent: "flex-end" }}>
        <SegmentedButtons values={WINDOW_OPTIONS} selected={recentMinutes} onSelect={setRecentMinutes} format={value => value === 1440 ? "24h" : `${value}m`} colors={colors} />
        <button style={button(colors)} onClick={onRefresh} disabled={loading}><RefreshCw size={16} /> Refresh</button>
      </div>
    </section>
  );
}

function MonitoringOverview({ colors, summary, run, audit, geoJSON, cells, sensorNodes, areaId }: { colors: Colors; summary: RuntimeSummaryResponse | null; run: RuntimeRunSummaryResponse | null; audit: RuntimeRunAuditResponse | null; geoJSON: any; cells: AreaCellResponse[]; sensorNodes: SensorNodeResponse[]; areaId: string }) {
  return (
    <ViewStack>
      <MetricGrid>
        <Metric colors={colors} title="Current Area Risk" value={formatScore(summary?.areaOperationalState?.aggregateRiskScore)} detail={summary?.areaOperationalState?.aggregateRiskLevel ?? "No projection"} icon={<Activity size={18} />} tone="#be123c" />
        <Metric colors={colors} title="Active Alert" value={summary?.activeAlerts.length ?? 0} detail={summary?.areaOperationalState?.alertState ?? "No active alert state"} icon={<AlertTriangle size={18} />} tone="#b45309" />
        <Metric colors={colors} title="Freshness" value={summary?.areaOperationalState?.freshnessStatus ?? "Not available"} detail={summary?.freshness ? `${summary.freshness.freshCount}/${summary.freshness.staleCount}/${summary.freshness.expiredCount} cells fresh/stale/expired` : "projection freshness"} icon={<Clock size={18} />} tone="#475569" />
        <Metric colors={colors} title="Coverage" value={summary?.areaOperationalState?.coverageStatus ?? "Not available"} detail={summary?.areaOperationalState?.operationalStatusReason ?? "projection coverage"} icon={<ShieldCheck size={18} />} tone="#2563eb" />
        <Metric colors={colors} title="Carry-forward" value={summary?.areaOperationalState?.carryForwardStatus ?? "Not available"} detail={formatDate(summary?.areaOperationalState?.lastAssessmentTimestamp)} icon={<RefreshCw size={18} />} tone="#7c3aed" />
        <Metric colors={colors} title="Latest Run" value={run?.status ?? "No run"} detail={run?.scenarioCode ?? "Not available"} icon={<Play size={18} />} tone="#2563eb" />
        <Metric colors={colors} title="Sensors" value={cells.reduce((sum, cell) => sum + cell.sensorNodeCount, 0)} detail={`${cells.length} cells exposed`} icon={<MapIcon size={18} />} tone="#059669" />
        <Metric colors={colors} title="Last Update" value={summary?.generatedAtUtc ? shortTime(summary.generatedAtUtc) : "No data"} detail="runtime summary generatedAtUtc" icon={<RefreshCw size={18} />} tone="#7c3aed" />
      </MetricGrid>

      <div style={twoCol()}>
        <Panel colors={colors}>
          <SectionHeader title="Runtime Chain" subtitle="Scenario Run -> Event Inbox -> Processing Attempts -> Risk -> State -> Alerts -> API/UI" />
          <RuntimeChainStrip colors={colors} summary={summary} />
        </Panel>
        <Panel colors={colors}>
          <SectionHeader title="Risk and Alert Summary" />
          <p style={paragraph(colors)}>{summary?.areaOperationalState?.summary ?? "No persisted area risk summary is exposed for this area."}</p>
          <p style={paragraph(colors)}>{summary?.activeAlerts.length ? `${summary.activeAlerts.length} active alert(s) are currently exposed by projection.alert_state.` : "No active alerts are currently exposed."}</p>
          <KeyValues colors={colors} rows={[
            ["Expected events", audit?.expectedEvents ?? "Not available"],
            ["Accepted readings", audit?.acceptedReadings ?? "Not available"],
            ["Missing events", audit?.missingEvents ?? "Not available"],
            ["Risk assessments", audit?.riskAssessments ?? "Not available"],
          ]} />
        </Panel>
      </div>

      <Panel colors={colors}>
        <SectionHeader title="Mini Map Preview" subtitle="Boundary and cells are read from existing area endpoints." />
        <div style={{ height: "340px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", overflow: "hidden" }}>
          {geoJSON ? <AreaMap areaId={areaId} mapType="standard" showGrid={false} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes} /> : <EmptyState colors={colors} text="Map data is not available." />}
        </div>
      </Panel>
    </ViewStack>
  );
}

function MapAndCells({ colors, areaId, geoJSON, cells, sensorNodes }: { colors: Colors; areaId: string; geoJSON: any; cells: AreaCellResponse[]; sensorNodes: SensorNodeResponse[] }) {
  return (
    <ViewStack>
      <Panel colors={colors}>
        <SectionHeader title="Map & Cells" subtitle="Existing Leaflet map, area boundary, grid cells and sensor markers." />
        <div style={{ height: "620px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", overflow: "hidden" }}>
          {geoJSON ? <AreaMap areaId={areaId} mapType="standard" showGrid={false} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes} /> : <EmptyState colors={colors} text="Map data is not available." />}
        </div>
      </Panel>
      <CollapsibleJson colors={colors} title="Cells exposed by API" value={cells} />
    </ViewStack>
  );
}

function SensorDashboards({ colors, areaId, dashboardLinks }: { colors: Colors; areaId: string; dashboardLinks: string[] }) {
  const sensorTabs = ["Temperature", "Humidity", "Wind"] as const;
  const [selected, setSelected] = useState<(typeof sensorTabs)[number]>("Temperature");
  const index = sensorTabs.indexOf(selected);
  const link = buildSafeGrafanaAreaUrl(dashboardLinks[index] ?? dashboardLinks[0] ?? null, areaId);
  return (
    <Panel colors={colors}>
      <SectionHeader title="Sensor Dashboards" subtitle="Only one Grafana embed is loaded by default to keep the view readable." />
      <Tabs values={sensorTabs} selected={selected} onSelect={setSelected} colors={colors} compact />
      {link ? (
        <div style={{ height: "560px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", overflow: "hidden" }}>
          <iframe src={link} width="100%" height="100%" style={{ border: 0, display: "block" }} title={`${selected} dashboard`} loading="lazy" />
        </div>
      ) : <EmptyState colors={colors} text="Grafana dashboard not configured." />}
    </Panel>
  );
}

function AreaRiskView({ colors, areaId, summary, dashboardLink }: { colors: Colors; areaId: string; summary: RuntimeSummaryResponse | null; dashboardLink: string | null }) {
  const grafanaUrl = buildSafeGrafanaAreaUrl(dashboardLink, areaId);
  return (
    <ViewStack>
      <MetricGrid>
        <Metric colors={colors} title="Current Area Score" value={formatScore(summary?.areaOperationalState?.aggregateRiskScore)} detail="aggregate projection / carry-forward" icon={<Activity size={18} />} tone="#be123c" />
        <Metric colors={colors} title="Latest NP Assessment" value={formatScore(summary?.scoreComponents?.npScore)} detail={`${summary?.scoreComponents?.npRiskClassLabel ?? summary?.scoreComponents?.npRiskClass ?? "class n/a"}; ${summary?.scoreComponents?.parameterSetVersion ?? "parameter set not exposed"}`} icon={<Activity size={18} />} tone="#be123c" />
        <Metric colors={colors} title="Risk Level" value={summary?.areaOperationalState?.aggregateRiskLevel ?? "No data"} detail={summary?.areaOperationalState?.severity ?? "Not available"} icon={<ShieldCheck size={18} />} tone="#b45309" />
        <Metric colors={colors} title="Assessment Count" value={summary?.areaOperationalState?.assessmentCount ?? "Not available"} detail="persisted projection count" icon={<BarChart3 size={18} />} tone="#2563eb" />
        <Metric colors={colors} title="Freshness" value={summary?.areaOperationalState?.freshnessStatus ?? "Not available"} detail={summary?.areaOperationalState?.carryForwardStatus ?? "carry-forward not exposed"} icon={<Clock size={18} />} tone="#475569" />
        <Metric colors={colors} title="Coverage" value={summary?.areaOperationalState?.coverageStatus ?? "Not available"} detail={summary?.areaOperationalState?.operationalStatusReason ?? "coverage status"} icon={<ShieldCheck size={18} />} tone="#059669" />
        <Metric colors={colors} title="FWI" value={formatMaybeScore(summary?.indexComparison?.fireWeatherIndex)} detail={`${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? "IPMA class n/a"}; near ${summary?.indexComparison?.fireWeatherNextIpmaClass ?? "n/a"} (${formatMaybeScore(summary?.indexComparison?.fireWeatherThresholdDistanceToNextClass)})`} icon={<BarChart3 size={18} />} tone="#7c3aed" />
        <Metric colors={colors} title="KBDI" value={formatMaybeScore(summary?.indexComparison?.keetchByramDroughtIndex)} detail={`${summary?.indexComparison?.kbdiDrynessClassLabel ?? "dryness class n/a"}; ${summary?.indexComparison?.kbdiAntecedentHistoryQuality ?? summary?.indexComparison?.kbdiCalculationStatus ?? "status n/a"}`} icon={<BarChart3 size={18} />} tone="#6d28d9" />
        <Metric colors={colors} title="Portuguese Context Proxy" value={summary?.indexComparison?.portugueseContextRiskProxyLabel ?? summary?.indexComparison?.portugueseContextRiskProxyClass ?? "Not available"} detail={`FWI ${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? "n/a"} x Territory ${summary?.indexComparison?.territorialHazardProxyClass ?? "n/a"}`} icon={<ShieldCheck size={18} />} tone="#0f766e" />
        <Metric colors={colors} title="Precipitation 24h" value={formatMaybeScore(summary?.indexComparison?.dailyPrecipitationMillimeters)} detail={summary?.indexComparison?.provenance ?? "daily reference not exposed"} icon={<CloudRain size={18} />} tone="#0369a1" />
        <Metric colors={colors} title="Recent Risk Rows" value={summary?.risk.recentCount ?? 0} detail={formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore)} icon={<Clock size={18} />} tone="#0891b2" />
      </MetricGrid>
      <Panel colors={colors}>
        <SectionHeader title="Score Components" subtitle="Read from persisted risk_assessment_log; frontend does not score." />
        <KeyValues colors={colors} rows={[
          ["BaseRisk / Adjusted", `${formatMaybeScore(summary?.scoreComponents?.baseRisk)} / ${formatMaybeScore(summary?.scoreComponents?.adjustedScore)}`],
          ["M / D / T", `${formatMaybeScore(summary?.scoreComponents?.meteorologyComponent)} / ${formatMaybeScore(summary?.scoreComponents?.droughtComponent)} / ${formatMaybeScore(summary?.scoreComponents?.territoryComponent)}`],
          ["H / F / G", `${formatMaybeScore(summary?.scoreComponents?.hazardComponent)} / ${formatMaybeScore(summary?.scoreComponents?.fuelComponent)} / ${formatMaybeScore(summary?.scoreComponents?.geomorphologyComponent)}`],
          ["C / I", `${formatMaybeScore(summary?.scoreComponents?.confidenceFactor)} / ${formatMaybeScore(summary?.scoreComponents?.integrityFactor)}`],
          ["Dominant driver", summary?.scoreComponents?.dominantDriver ?? "Not available"],
          ["Calculation", summary?.scoreComponents?.calculationStatus ?? "Not available"],
          ["Current Area vs Latest NP", "Area score is an aggregate projection. Latest NP assessment is the latest persisted risk_assessment_log row."],
          ["Precipitation 24h / provenance", `${formatMaybeScore(summary?.indexComparison?.dailyPrecipitationMillimeters)} / ${summary?.indexComparison?.provenance ?? "Not available"}`],
          ["FWI calculated / reference", `${formatMaybeScore(summary?.indexComparison?.calculatedFireWeatherIndex)} / ${formatMaybeScore(summary?.indexComparison?.referenceFireWeatherIndex)}`],
          ["KBDI calculated / reference", `${formatMaybeScore(summary?.indexComparison?.calculatedKeetchByramDroughtIndex)} / ${formatMaybeScore(summary?.indexComparison?.referenceKeetchByramDroughtIndex)}`],
          ["Local FWI percentile", `${summary?.indexComparison?.localFwiPercentileStatus ?? "Not available"}${summary?.indexComparison?.localFwiPercentileReason ? `: ${summary.indexComparison.localFwiPercentileReason}` : ""}`],
          ["Limitations", summary?.scoreComponents?.limitations ?? summary?.indexComparison?.limitations ?? "None exposed"],
        ]} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Recent Risk Scores" subtitle="Read from persisted risk_assessment_log values; frontend does not score." />
        <RiskLineChart colors={colors} summary={summary} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Grafana Area Risk" subtitle="External dashboard link is shown only when configured as a valid Grafana URL." />
        {grafanaUrl ? (
          <>
            <a style={button(colors)} href={grafanaUrl} target="_blank" rel="noreferrer">Open Grafana area risk dashboard</a>
            <div style={{ height: "560px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", overflow: "hidden" }}>
              <iframe src={grafanaUrl} width="100%" height="100%" style={{ border: 0, display: "block" }} title={`area dashboard`} loading="lazy" />
            </div>
          </>
        ) : (
          <EmptyState colors={colors} text="Grafana area risk dashboard not configured." />
        )}
      </Panel>
      <Banner colors={colors} tone="#b45309">Recent risk rows and persisted area operational state may differ because projections can include carry-forward.</Banner>
    </ViewStack>
  );
}

function AlertsView({ colors, alerts }: { colors: Colors; alerts: RuntimeAlertSummaryResponse[] }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title="Alerts" subtitle="Active alerts from projection.alert_state." />
      <AlertList colors={colors} alerts={alerts} detailed />
    </Panel>
  );
}

function RunOrchestrator(props: {
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
  const { colors, scenarios, activeSensorCount, sensorCountTooHigh, runForm, setRunForm, startRun, submittingRun, runResult, runMessage, areaCode, setMainTab, setScenarioTab } = props;
  const activeProfiles = normalizeProfiles(runForm.degradationProfiles, runForm.degradationProfile);
  const scenarioCWithoutDegradation = runForm.scenarioCode === "scenario_c" && activeProfiles.every(profile => profile === "none");
  const setDegradationProfile = (profile: string, checked: boolean) => {
    setRunForm(current => {
      const currentProfiles = normalizeProfiles(current.degradationProfiles, current.degradationProfile);
      let next = currentProfiles;
      if (profile === "none") {
        next = checked ? ["none"] : [];
      } else {
        next = checked
          ? [...currentProfiles.filter(value => value !== "none"), profile]
          : currentProfiles.filter(value => value !== profile);
      }
      if (next.length === 0) {
        next = ["none"];
      }
      return { ...current, degradationProfiles: next, degradationProfile: toLegacyProfile(next) };
    });
  };
  return (
    <Panel colors={colors}>
      <SectionHeader title="Run Orchestrator" subtitle="Starts Simulator.Host through the existing development control endpoint." />
      <FormGrid>
        <LabeledSelect colors={colors} label="scenario code" value={runForm.scenarioCode} options={scenarios.map(scenario => ({ value: scenario.code, label: `${scenario.code} - ${scenario.name}` }))} onChange={value => setRunForm(current => {
          const currentProfiles = normalizeProfiles(current.degradationProfiles, current.degradationProfile);
          const nextProfiles = value === "scenario_c" && currentProfiles.every(profile => profile === "none")
            ? ["missing-readings"]
            : currentProfiles;
          return { ...current, scenarioCode: value, degradationProfile: toLegacyProfile(nextProfiles), degradationProfiles: nextProfiles, runLabel: `${value}-from-ui` };
        })} />
        <LabeledNumber colors={colors} label="sensor count" value={runForm.sensorCount} max={activeSensorCount || undefined} onChange={value => setRunForm(current => ({ ...current, sensorCount: value }))} />
        <LabeledNumber colors={colors} label="number of cycles" value={runForm.numberOfCycles} onChange={value => setRunForm(current => ({ ...current, numberOfCycles: value }))} />
        <LabeledNumber colors={colors} label="interval seconds" value={runForm.intervalSeconds} onChange={value => setRunForm(current => ({ ...current, intervalSeconds: value }))} />
        <LabeledNumber colors={colors} label="seed" value={runForm.seed} onChange={value => setRunForm(current => ({ ...current, seed: value }))} />
        <LabeledNumber colors={colors} label="timeout seconds" value={runForm.timeoutSeconds} onChange={value => setRunForm(current => ({ ...current, timeoutSeconds: value ?? 180 }))} />
        <LabeledInput colors={colors} label="run label" value={runForm.runLabel ?? ""} onChange={value => setRunForm(current => ({ ...current, runLabel: value || null }))} />
      </FormGrid>
      <div style={{ marginTop: "10px" }}>
        <label style={labelStyle(colors)}>degradation profiles</label>
        <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
          {DEGRADATION_PROFILE_OPTIONS.map(profile => (
            <CheckRow key={profile} colors={colors} label={profile} checked={activeProfiles.includes(profile)} onChange={checked => setDegradationProfile(profile, checked)} />
          ))}
        </div>
      </div>
      <div style={{ color: colors.textSecond, fontSize: "13px", marginTop: "10px" }}>
        Active sensors available: {activeSensorCount || "Unknown"}; selected sensors requested: {runForm.sensorCount ?? "all"}; active profiles: {activeProfiles.join(", ")}
      </div>
      {sensorCountTooHigh && <Banner colors={colors} tone="#dc2626">sensorCount exceeds active sensors for this area.</Banner>}
      {scenarioCWithoutDegradation && <Banner colors={colors} tone="#b45309">scenario_c is intended for degraded/operational comparison. Select at least one degradation profile for a meaningful C run.</Banner>}
      <CheckRow colors={colors} label="collect evidence" checked={runForm.collectEvidence} onChange={value => setRunForm(current => ({ ...current, collectEvidence: value }))} />
      <CheckRow colors={colors} label="wait for completion" checked={runForm.waitForCompletion} onChange={value => setRunForm(current => ({ ...current, waitForCompletion: value }))} />
      <CheckRow colors={colors} label="allow parallel run" checked={runForm.allowParallelRun} onChange={value => setRunForm(current => ({ ...current, allowParallelRun: value }))} />
      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", marginTop: "12px" }}>
        <button style={button(colors)} onClick={startRun} disabled={submittingRun || sensorCountTooHigh}><Play size={16} /> {submittingRun ? "Submitting..." : "Start Run"}</button>
        <button style={button(colors)} onClick={() => { setMainTab("Scenario Lab"); setScenarioTab("Latest Run"); }}><ArrowRight size={16} /> Latest Run</button>
      </div>
      {(runMessage || runResult) && <RunRequestResult colors={colors} result={runResult} request={runForm} message={runMessage} areaCode={areaCode} />}
    </Panel>
  );
}

function ScenarioDefinition({ colors, scenarios }: { colors: Colors; scenarios: ScenarioResponse[] }) {
  const definitions = [
    ["scenario_a", "Baseline/normal", "Clean operational run", "none", "Stable readings and normal risk processing"],
    ["scenario_b", "High risk without degradation", "Compare against degraded scenario", "none", "High-risk inputs without missing readings"],
    ["scenario_c", "High risk degraded with missing readings", "Demonstrate degradation handling", "missing-readings", "Fewer accepted readings with explicit missing events"],
  ];
  return (
    <ViewStack>
      <div style={cardGrid()}>
        {definitions.map(([code, meaning, purpose, degradation, behavior]) => (
          <Panel key={code} colors={colors}>
            <SectionHeader title={code} subtitle={meaning} />
            <KeyValues colors={colors} rows={[["Purpose", purpose], ["Expected degradation", degradation], ["Expected behavior", behavior], ["Default parameters", "From scenario endpoint or run form defaults"]]} />
          </Panel>
        ))}
      </div>
      <CollapsibleJson colors={colors} title="Scenario definitions exposed by API" value={scenarios} />
    </ViewStack>
  );
}

function LatestRunView({ colors, run }: { colors: Colors; run: RuntimeRunSummaryResponse | null }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title="Latest Run" subtitle="Readable cards backed by control.simulation_runs metadata." />
      <RunDetails colors={colors} run={run} />
    </Panel>
  );
}

function RuntimeStateControl({ colors, confirm, setConfirm, resetRuntime, loading, resetResult }: { colors: Colors; confirm: string; setConfirm: (value: string) => void; resetRuntime: (dryRun: boolean) => void; loading: boolean; resetResult: RuntimeResetResponse | null }) {
  return (
    <Panel colors={colors} accent="#dc2626">
      <SectionHeader title="Runtime State Control" subtitle="Danger zone. Dry run first; real reset requires exact confirmation." />
      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
        <button style={button(colors)} onClick={() => resetRuntime(true)} disabled={loading}><Search size={16} /> Dry run reset</button>
        <input style={{ ...input(colors), width: "240px" }} value={confirm} onChange={event => setConfirm(event.target.value)} placeholder="RESET_RUNTIME_STATE" />
        <button style={{ ...button(colors), borderColor: "#dc2626", color: "#dc2626" }} onClick={() => resetRuntime(false)} disabled={loading || confirm !== "RESET_RUNTIME_STATE"}><RotateCcw size={16} /> Reset Runtime State</button>
      </div>
      {resetResult && <ResetCounts colors={colors} result={resetResult} />}
    </Panel>
  );
}

function LatestRunAuditView({ colors, audit }: { colors: Colors; audit: RuntimeRunAuditResponse | null }) {
  if (!audit) {
    return <Panel colors={colors}><EmptyState colors={colors} text="No latest run audit is available." /></Panel>;
  }

  return (
    <ViewStack>
      <MetricGrid>
        <Metric colors={colors} title="Expected events" value={audit.expectedEvents ?? "Not available"} detail="run overrides x cycles" icon={<Clipboard size={18} />} tone="#2563eb" />
        <Metric colors={colors} title="Accepted readings" value={audit.acceptedReadings} detail="accepted_reading_log" icon={<ShieldCheck size={18} />} tone="#059669" />
        <Metric colors={colors} title="Missing events" value={audit.missingEvents ?? "Not available"} detail="expected - accepted" icon={<AlertTriangle size={18} />} tone="#b45309" />
        <Metric colors={colors} title="Risk assessments" value={audit.riskAssessments} detail="risk_assessment_log" icon={<Activity size={18} />} tone="#0891b2" />
        <Metric colors={colors} title="Rejected" value={audit.rejected} detail="pipeline.rejected_events" icon={<AlertTriangle size={18} />} tone="#dc2626" />
        <Metric colors={colors} title="Quarantined" value={audit.quarantined} detail="pipeline.quarantined_events" icon={<AlertTriangle size={18} />} tone="#ea580c" />
      </MetricGrid>
      <div style={twoCol()}>
        <Panel colors={colors}><SectionHeader title="Quality Summary" /><StatusCounts colors={colors} rows={audit.qualityFlagsSummary} /></Panel>
        <Panel colors={colors}><SectionHeader title="Eligibility Summary" /><StatusCounts colors={colors} rows={audit.eligibilitySummary} /></Panel>
      </div>
      <Panel colors={colors}>
        <SectionHeader title="NP vs FWI vs KBDI" subtitle="Values are read from persisted backend projections and diagnostics; the UI does not calculate indexes." />
        <KeyValues colors={colors} rows={[
          ["NP score / base / adjusted", `${formatMaybeScore(audit.scoreComponents?.npScore)} / ${formatMaybeScore(audit.scoreComponents?.baseRisk)} / ${formatMaybeScore(audit.scoreComponents?.adjustedScore)}`],
          ["M / D / T", `${formatMaybeScore(audit.scoreComponents?.meteorologyComponent)} / ${formatMaybeScore(audit.scoreComponents?.droughtComponent)} / ${formatMaybeScore(audit.scoreComponents?.territoryComponent)}`],
          ["H / F / G", `${formatMaybeScore(audit.scoreComponents?.hazardComponent)} / ${formatMaybeScore(audit.scoreComponents?.fuelComponent)} / ${formatMaybeScore(audit.scoreComponents?.geomorphologyComponent)}`],
          ["C / I", `${formatMaybeScore(audit.scoreComponents?.confidenceFactor)} / ${formatMaybeScore(audit.scoreComponents?.integrityFactor)}`],
          ["FWI raw / normalized / status", `${formatMaybeScore(audit.indexComparison?.fireWeatherIndex)} / ${formatMaybeScore(audit.indexComparison?.normalizedFireWeatherIndex)} / ${audit.indexComparison?.fireWeatherCalculationStatus ?? "Not available"}`],
          ["FWI IPMA / EFFIS", `${audit.indexComparison?.fireWeatherIpmaClassLabel ?? audit.indexComparison?.fireWeatherIpmaClass ?? "Not available"} / ${audit.indexComparison?.fireWeatherEffisClass ?? "Not available"}`],
          ["KBDI raw / normalized / status", `${formatMaybeScore(audit.indexComparison?.keetchByramDroughtIndex)} / ${formatMaybeScore(audit.indexComparison?.normalizedKeetchByramDroughtIndex)} / ${audit.indexComparison?.kbdiCalculationStatus ?? "Not available"}`],
          ["KBDI dryness / antecedent", `${audit.indexComparison?.kbdiDrynessClassLabel ?? audit.indexComparison?.kbdiDrynessClass ?? "Not available"} / ${audit.indexComparison?.kbdiAntecedentHistoryQuality ?? "Not available"}`],
          ["Portuguese Context Proxy", `${audit.indexComparison?.portugueseContextRiskProxyLabel ?? audit.indexComparison?.portugueseContextRiskProxyClass ?? "Not available"}; territory ${audit.indexComparison?.territorialHazardProxyClass ?? "n/a"}`],
          ["Dominant driver", audit.scoreComponents?.dominantDriver ?? "Not available"],
          ["Parameter set", audit.scoreComponents?.parameterSetVersion ?? "Not available"],
          ["Limitations", audit.scoreComponents?.limitations ?? audit.indexComparison?.limitations ?? "None exposed"],
        ]} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Audit Notes" />
        <ul style={{ margin: 0, paddingLeft: "18px", color: colors.textSecond, lineHeight: 1.7 }}>
          {audit.areaSnapshot && <li>Area snapshot: {audit.areaSnapshot.aggregateRiskLevel} {audit.areaSnapshot.aggregateRiskScore} with {audit.areaSnapshot.assessmentCount} assessment(s).</li>}
          {audit.limitations.map(item => <li key={item.code}>{item.message}</li>)}
        </ul>
        <CollapsibleJson colors={colors} title="Raw audit JSON" value={audit} />
      </Panel>
    </ViewStack>
  );
}

function CompareBvsC({ colors, compare }: { colors: Colors; compare: RuntimeDiagnosticResultResponse | null }) {
  const rows = useMemo(() => buildCompareRows(compare), [compare]);
  return (
    <Panel colors={colors}>
      <SectionHeader title="Compare B vs C" subtitle="Promoted from diagnostics; uses persisted rows and does not recalculate risk." />
      <SimpleTable colors={colors} columns={["Metric", "Scenario B", "Scenario C", "Delta"]} rows={rows} />
      <NarrativeSummary colors={colors} rows={rows} />
      {compare?.limitations.length ? <ul style={{ color: colors.textSecond }}>{compare.limitations.map(item => <li key={item}>{item}</li>)}</ul> : null}
      <CollapsibleJson colors={colors} title="Raw comparison JSON" value={compare ?? "Not available"} />
    </Panel>
  );
}

function RunTimings({ colors, run, summary, audit, timings, timingsMessage }: { colors: Colors; run: RuntimeRunSummaryResponse | null; summary: RuntimeSummaryResponse | null; audit: RuntimeRunAuditResponse | null; timings: RuntimeRunTimingSummaryResponse | null; timingsMessage: string | null }) {
  const attemptTimings = buildAttemptTimingSummary(summary);
  const attempts = timings?.attempts;
  const stageRows = timings?.stages.map(item => [
    item.stage,
    item.outcome,
    item.errorCode ?? "None",
    item.count,
    formatDate(item.firstStartedAt),
    formatDate(item.lastFinishedAt),
    item.minDurationMs == null ? "Not exposed" : formatMs(item.minDurationMs),
    item.avgDurationMs == null ? "Not exposed" : formatMs(item.avgDurationMs),
    item.maxDurationMs == null ? "Not exposed" : formatMs(item.maxDurationMs),
  ]) ?? attemptTimings.rows.map(row => [
    row[0],
    row[1],
    "Not exposed",
    row[2],
    row[3],
    row[4],
    row[5],
    row[6],
    row[7],
  ]);
  const sourceDetail = timings ? "Read-only DB timing endpoint" : "Runtime summary fallback";

  return (
    <ViewStack>
      {timingsMessage && <Banner colors={colors} tone="#b45309">{timingsMessage}</Banner>}
      <MetricGrid>
        <Metric colors={colors} title="Run duration" value={timings?.runDurationMs == null ? run?.durationSeconds == null ? "Not available" : `${Math.round(run.durationSeconds)}s` : formatMs(timings.runDurationMs)} detail={`${formatDate(timings?.startedAt ?? run?.startedAt)} -> ${formatDate(timings?.endedAt ?? run?.endedAt)}`} icon={<Clock size={18} />} tone="#2563eb" />
        <Metric colors={colors} title="Time to first inbox" value={timings?.timeToFirstInboxMs == null ? "Not exposed" : formatMs(timings.timeToFirstInboxMs)} detail={formatDate(timings?.firstInboxReceivedAt)} icon={<Database size={18} />} tone="#059669" />
        <Metric colors={colors} title="Time to first risk" value={timings?.timeToFirstRiskAssessmentMs == null ? "Not exposed" : formatMs(timings.timeToFirstRiskAssessmentMs)} detail={formatDate(timings?.firstRiskAssessmentCreatedAt)} icon={<BarChart3 size={18} />} tone="#0891b2" />
        <Metric colors={colors} title="Time to first alert" value={timings?.timeToFirstAlertMs == null ? "Not exposed" : formatMs(timings.timeToFirstAlertMs)} detail={formatDate(timings?.firstAlertTriggeredAt)} icon={<AlertTriangle size={18} />} tone="#dc2626" />
        <Metric colors={colors} title="First processing attempt" value={timings?.firstProcessingAttemptStartedAt ? shortTime(timings.firstProcessingAttemptStartedAt) : attemptTimings.firstStarted ? shortTime(attemptTimings.firstStarted) : "Not exposed"} detail={sourceDetail} icon={<Activity size={18} />} tone="#7c3aed" />
        <Metric colors={colors} title="Last processing attempt" value={timings?.lastProcessingAttemptFinishedAt ? shortTime(timings.lastProcessingAttemptFinishedAt) : attemptTimings.lastFinished ? shortTime(attemptTimings.lastFinished) : "Not exposed"} detail="FinishedAt when exposed" icon={<Clock size={18} />} tone="#0891b2" />
        <Metric colors={colors} title="Attempt count" value={attempts?.attemptCount ?? summary?.pipeline.attemptsRecent ?? "No data"} detail={sourceDetail} icon={<Server size={18} />} tone="#475569" />
        <Metric colors={colors} title="Successful attempts" value={attempts?.successfulAttempts ?? attemptTimings.successfulAttempts ?? "Not exposed"} detail="Grouped by persisted outcome" icon={<ShieldCheck size={18} />} tone="#059669" />
        <Metric colors={colors} title="Failed attempts" value={attempts?.failedAttempts ?? attemptTimings.failedAttempts} detail="failed/retry outcomes" icon={<AlertTriangle size={18} />} tone="#dc2626" />
        <Metric colors={colors} title="Quarantined attempts" value={attempts?.quarantinedAttempts ?? attemptTimings.quarantinedAttempts} detail="quarantined outcome" icon={<AlertTriangle size={18} />} tone="#ea580c" />
        <Metric colors={colors} title="Avg attempt duration" value={(attempts?.avgDurationMs ?? attemptTimings.avgDurationMs) == null ? "Not exposed" : formatMs((attempts?.avgDurationMs ?? attemptTimings.avgDurationMs)!)} detail="Calculated only when StartedAt/FinishedAt exist" icon={<BarChart3 size={18} />} tone="#b45309" />
        <Metric colors={colors} title="Max attempt duration" value={(attempts?.maxDurationMs ?? attemptTimings.maxDurationMs) == null ? "Not exposed" : formatMs((attempts?.maxDurationMs ?? attemptTimings.maxDurationMs)!)} detail={timings ? "All attempts associated with run" : "Latest failed attempts subset"} icon={<Clock size={18} />} tone="#be123c" />
      </MetricGrid>
      <Panel colors={colors}>
        <SectionHeader title="Attempt timing summary" subtitle={timings ? "Uses pipeline.processing_attempts rows associated with this SimulationRunId." : "Uses processing attempt fields currently exposed by runtime summary fallback."} />
        {stageRows.length > 0 ? (
          <SimpleTable colors={colors} columns={["Stage", "Outcome", "Error", "Count", "First started", "Last finished", "Min duration", "Avg duration", "Max duration"]} rows={stageRows} />
        ) : (
          <EmptyState colors={colors} text="Attempt-level timings not exposed by current diagnostics." />
        )}
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Run evidence timing context" subtitle="Available runtime/audit timestamps and counts." />
        <KeyValues colors={colors} rows={[
          ["SimulationRunId", run?.id ?? "Not available"],
          ["Scenario", run?.scenarioCode ?? "Not available"],
          ["Created", formatDate(run?.createdAt)],
          ["Started", formatDate(run?.startedAt)],
          ["Finished", formatDate(run?.endedAt)],
          ["Expected events", audit?.expectedEvents ?? "Not available"],
          ["Accepted readings", audit?.acceptedReadings ?? "Not available"],
          ["Risk assessments", audit?.riskAssessments ?? "Not available"],
        ]} />
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title="Timing limitations" subtitle="The endpoint is read-only and does not parse local logs." />
        {timings?.limitations.length ? (
          <ul style={{ color: colors.textSecond, margin: 0 }}>
            {timings.limitations.map(item => <li key={item}>{item}</li>)}
          </ul>
        ) : (
          <Banner colors={colors} tone="#b45309">Logger stopwatch timings are emitted in logs but are not structurally associated with SimulationRunId yet. A future evidence summary should expose those elapsed timings without frontend log parsing.</Banner>
        )}
      </Panel>
    </ViewStack>
  );
}

function DiagnosticsView(props: { colors: Colors; diagnostics: RuntimeDiagnosticDefinitionResponse[]; selectedDiagnostic: string; diagnosticResult: RuntimeDiagnosticResultResponse | null; executeDiagnostic: (id?: string) => void; loading: boolean }) {
  const { colors, diagnostics, selectedDiagnostic, diagnosticResult, executeDiagnostic, loading } = props;
  const groups = groupDiagnostics(diagnostics);
  return (
    <div style={{ display: "grid", gridTemplateColumns: "280px 1fr", gap: "14px" }}>
      <Panel colors={colors}>
        <SectionHeader title="Diagnostics" subtitle="All quick queries remain available." />
        {Object.entries(groups).map(([group, items]) => (
          <details key={group} open={group === "Runs"} style={{ marginBottom: "10px" }}>
            <summary style={{ cursor: "pointer", fontWeight: 800 }}>{group}</summary>
            <div style={{ display: "grid", gap: "6px", marginTop: "8px" }}>
              {items.map(item => (
                <button key={item.id} style={button(colors, selectedDiagnostic === item.id)} onClick={() => executeDiagnostic(item.id)} disabled={loading}>
                  <Search size={14} /> {item.title}
                </button>
              ))}
            </div>
          </details>
        ))}
      </Panel>
      <Panel colors={colors}>
        <SectionHeader title={diagnosticResult?.title ?? "Diagnostic result"} subtitle={diagnosticResult?.description ?? "Choose a diagnostic to load data."} />
        <DiagnosticResult colors={colors} result={diagnosticResult} />
      </Panel>
    </div>
  );
}

function ExportEvidence({ colors, audit, compare, summary }: { colors: Colors; audit: RuntimeRunAuditResponse | null; compare: RuntimeDiagnosticResultResponse | null; summary: RuntimeSummaryResponse | null }) {
  const evidence = { summary: summary ?? "Not available", latestRunAudit: audit ?? "Not available", compareBvsC: compare ?? "Not available" };
  const markdown = buildEvidenceMarkdown(audit, compare, summary);
  return (
    <Panel colors={colors}>
      <SectionHeader title="Export Evidence" subtitle="Frontend-only export helpers; no backend changes." />
      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
        <button style={button(colors)} onClick={() => copyText(JSON.stringify(audit ?? "Not available", null, 2))}><Clipboard size={16} /> Copy audit JSON</button>
        <button style={button(colors)} onClick={() => downloadText("natureprotector-summary.json", JSON.stringify(evidence, null, 2))}><Download size={16} /> Export summary JSON</button>
        <button style={button(colors)} onClick={() => downloadText("natureprotector-summary.md", markdown)}><Download size={16} /> Export summary Markdown</button>
        <button style={button(colors)} onClick={() => downloadText("natureprotector-b-vs-c.json", JSON.stringify(compare ?? "Not available", null, 2))}><Download size={16} /> Export B/C comparison</button>
      </div>
      <CollapsibleJson colors={colors} title="Export preview JSON" value={evidence} />
    </Panel>
  );
}

function RuntimeChainView({ colors, summary, onNavigate }: { colors: Colors; summary: RuntimeSummaryResponse | null; onNavigate: (target: "retry" | "risk" | "state" | "alerts" | "services") => void }) {
  const chain = buildRuntimeChainDetails(summary);
  return (
    <ViewStack>
      <Panel colors={colors}>
        <SectionHeader title="Runtime Chain" subtitle="Scenario Run -> Event Inbox -> Processing Attempts -> Risk -> State -> Alerts -> API/UI" />
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "10px" }}>
          {chain.map(item => (
            <div key={item.label} style={{ ...panel(colors), borderLeft: `4px solid ${item.tone}`, minHeight: "154px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", gap: "8px" }}>
                <strong>{item.label}</strong>
                <Badge colors={colors}>{item.source}</Badge>
              </div>
              <div style={{ fontSize: "24px", fontWeight: 800, marginTop: "8px" }}>{item.count}</div>
              <div style={paragraph(colors)}>Status: {item.status}</div>
              <div style={paragraph(colors)}>Last update: {item.lastUpdate}</div>
              <div style={paragraph(colors)}>Latest error: {item.latestError}</div>
              {item.navigate && <button style={{ ...button(colors), marginTop: "8px" }} onClick={() => onNavigate(item.navigate!)}>Open related view</button>}
            </div>
          ))}
        </div>
      </Panel>
      <CollapsibleJson colors={colors} title="Runtime summary JSON" value={summary ?? "No data"} />
    </ViewStack>
  );
}

function ProcessingPipeline({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const stages = [
    ["Ingestion", `${summary?.pipeline.inboxRecent ?? 0} recent inbox rows`, "pipeline.event_inbox"],
    ["Validation", `${summary?.pipeline.rejectedRecent ?? 0} recent rejected`, "pipeline.rejected_events"],
    ["Normalization", "Not exposed", "normalized reading stage is internal"],
    ["Eligibility", "Audit summary only", "eligibility aggregate is not persisted"],
    ["Risk Scoring", `${summary?.risk.recentCount ?? 0} recent assessments`, "projection.risk_assessment_log"],
    ["Projection", `${summary?.cellOperationalStateCount ?? 0} cell states`, "projection.cell_operational_state"],
    ["Alert Policy", `${summary?.activeAlerts.length ?? 0} active alerts`, "projection.alert_state"],
  ];
  return <div style={cardGrid()}>{stages.map(([title, status, detail]) => <InfoCard key={title} colors={colors} title={title} status={status} detail={detail} />)}</div>;
}

function RetryQuarantine({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  return (
    <ViewStack>
      <div style={cardGrid()}>
        <ChartPanel colors={colors} title="Attempts by Outcome"><BarGraph data={(summary?.pipeline.attemptsByOutcomeAndError ?? []).map(item => ({ name: item.errorCode ? `${item.outcome}/${item.errorCode}` : item.outcome, value: item.count }))} color="#7c3aed" /></ChartPanel>
        <ChartPanel colors={colors} title="Failed Attempts by Error"><BarGraph data={(summary?.pipeline.attemptsByOutcomeAndError ?? []).filter(item => item.errorCode || !/success|completed|accepted/i.test(item.outcome)).map(item => ({ name: item.errorCode ?? item.outcome, value: item.count }))} color="#b45309" /></ChartPanel>
        <ChartPanel colors={colors} title="Rejected by Code"><BarGraph data={(summary?.pipeline.rejectedByCode ?? []).map(item => ({ name: item.code, value: item.count }))} color="#dc2626" /></ChartPanel>
        <ChartPanel colors={colors} title="Quarantined by Code"><BarGraph data={(summary?.pipeline.quarantinedByCode ?? []).map(item => ({ name: item.code, value: item.count }))} color="#ea580c" /></ChartPanel>
      </div>
      <div style={twoCol()}>
        <Panel colors={colors}><SectionHeader title="Latest Rejected" /><EventRows colors={colors} rows={(summary?.pipeline.latestRejected ?? []).map(item => [item.rejectionCode, item.rejectionReason, formatDate(item.rejectedAt)])} empty="No recent rejected events." /></Panel>
        <Panel colors={colors}><SectionHeader title="Latest Quarantined" /><EventRows colors={colors} rows={(summary?.pipeline.latestQuarantined ?? []).map(item => [item.quarantineCode, item.quarantineReason, formatDate(item.quarantinedAt)])} empty="No recent quarantined events." /></Panel>
        <Panel colors={colors}><SectionHeader title="Latest Failed Attempts" /><EventRows colors={colors} rows={(summary?.pipeline.latestFailedAttempts ?? []).map(item => [item.errorCode ?? item.outcome, `${item.stage} / attempt ${item.attemptNumber} / ${item.errorMessage ?? "No error message"}`, `${formatDate(item.startedAt)} -> ${formatDate(item.finishedAt)}`])} empty="No recent failed attempts." /></Panel>
      </div>
      <Banner colors={colors} tone="#64748b">Retry and quarantine are backend pipeline concerns: invalid events are rejected early; failed processing attempts may retry; terminal poison cases are quarantined. Counts here come from persisted pipeline summaries and diagnostics.</Banner>
    </ViewStack>
  );
}

function PersistenceViews({ colors, tableCounts }: { colors: Colors; tableCounts: RuntimeDiagnosticResultResponse | null }) {
  const tables = ["control.simulation_runs", "pipeline.event_inbox", "pipeline.processing_attempts", "pipeline.rejected_events", "pipeline.quarantined_events", "projection.risk_assessment_log", "projection.area_risk_snapshot_log", "projection.cell_operational_state", "projection.area_operational_state", "projection.alert_state"];
  const countFor = (tableName: string) => {
    const [schema, table] = tableName.split(".");
    const row = tableCounts?.rows.find(item => item.schema === schema && item.table === table);
    return row?.count ?? "Not exposed yet";
  };
  return <div style={cardGrid()}>{tables.map(table => <InfoCard key={table} colors={colors} title={table} status={countFor(table)} detail="Runtime/persistence view" />)}</div>;
}

function DeploymentServices({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const services = [
    ["Web UI", "Loaded", "Current browser app"],
    ["Backoffice API", summary ? "Reachable" : "Unknown", "runtime summary endpoint"],
    ["PostgreSQL", summary ? "Reachable through API" : "Unknown", "No direct health endpoint in UI"],
    ["RabbitMQ", "Not exposed", "No management adapter"],
    ["Prevention Host", "Not exposed", "No heartbeat endpoint"],
    ["Simulator Host", summary?.currentRun ? "Run active" : "Unknown", "Observed through simulation_runs"],
    ["InfluxDB", "Not exposed", "No health endpoint in UI"],
    ["Grafana", "Unknown", "Embeds are attempted when dashboards load"],
  ];
  return <div style={cardGrid()}>{services.map(([title, status, detail]) => <InfoCard key={title} colors={colors} title={title} status={status} detail={detail} />)}</div>;
}

function NominalFlow({ colors, summary, audit, runResult }: { colors: Colors; summary: RuntimeSummaryResponse | null; audit: RuntimeRunAuditResponse | null; runResult: RuntimeRunStartResponse | null }) {
  const steps = buildNominalFlowSteps(summary, audit, runResult);
  return (
    <Panel colors={colors}>
      <SectionHeader title="Nominal Flow" subtitle="Semi-live timeline inferred from persisted runtime summary, latest run audit and latest UI run response." />
      <div style={{ display: "grid", gap: "8px" }}>
        {steps.map((step, index) => (
          <div key={step.name} style={{ ...panel(colors), display: "grid", gridTemplateColumns: "42px 150px 1fr", gap: "10px", alignItems: "center", borderLeft: `4px solid ${statusTone(step.status)}` }}>
            <Badge colors={colors}>{String(index + 1).padStart(2, "0")}</Badge>
            <div>
              <strong>{step.name}</strong>
              <div style={{ color: statusTone(step.status), fontSize: "12px", fontWeight: 800 }}>{step.status}</div>
            </div>
            <div style={paragraph(colors)}>{step.evidence}</div>
          </div>
        ))}
      </div>
      <Banner colors={colors} tone="#64748b">Statuses marked as Done or Partial are frontend inferences from exposed counts and timestamps; they are not a separate backend workflow state machine.</Banner>
    </Panel>
  );
}

function DomainModel({ colors }: { colors: Colors }) {
  return (
    <ViewStack>
      <Banner colors={colors} tone="#2563eb">This page maps report concepts to implementation and UI evidence. It separates conceptual domain language from persisted runtime artifacts and visible UI widgets.</Banner>
      <div style={cardGrid()}>{MODEL_ARTIFACTS.map(item => <InfoCard key={item.concept} colors={colors} title={item.concept} status={item.status} detail={`${item.persistence}; ${item.uiEvidence}`} />)}</div>
    </ViewStack>
  );
}

function DataChain({ colors }: { colors: Colors }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title="Data Chain" subtitle="Conceptual-to-runtime chain with implementation, persistence and UI visibility state." />
      <SimpleTable
        colors={colors}
        columns={["Node", "Status", "Persistence", "UI evidence", "Code reference"]}
        rows={MODEL_ARTIFACTS.map(item => [item.concept, item.status, item.persistence, item.uiEvidence, item.code])}
      />
    </Panel>
  );
}

function DataProvenance({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const cards = [
    ["Simulated data", "Runtime evidence is generated by controlled scenarios. It is not presented as field validation of real wildfire prediction."],
    ["Scenario parameters", "Scenario code, cycles, seed, sensor count and degradation profile define the operational experiment."],
    ["Candidate parameter set", "Risk score V1 is a candidate operational parameter set, useful for comparison and traceability, not final scientific calibration."],
    ["FWI/KBDI provenance", "FWI/KBDI-like context is treated as provenance/candidate context unless a validated scientific calibration is exposed."],
    ["Missing readings", "Missing readings are represented by degradation settings plus expected-vs-accepted audit arithmetic."],
    ["Freshness/carry-forward", "Persisted projections can carry forward state; UI labels distinguish recent risk rows from current area state."],
    ["Limitations", "Unknown or unavailable facts remain explicitly marked as Not exposed, Not available, Not instrumented or No data."],
  ];
  return (
    <ViewStack>
      <div style={cardGrid()}>{cards.map(([title, detail]) => <InfoCard key={title} colors={colors} title={title} status="Provenance" detail={detail} />)}</div>
      <Panel colors={colors} accent="#0891b2">
        <SectionHeader title="NP vs FWI/KBDI" subtitle="Persisted comparison/provenance values; no frontend scoring or scientific validation claim." />
        <KeyValues colors={colors} rows={[
          ["Parameter set", summary?.scoreComponents?.parameterSetVersion ?? "Not available"],
          ["NP adjusted score", formatMaybeScore(summary?.scoreComponents?.adjustedScore)],
          ["FWI / normalized", `${formatMaybeScore(summary?.indexComparison?.fireWeatherIndex)} / ${formatMaybeScore(summary?.indexComparison?.normalizedFireWeatherIndex)}`],
          ["FWI IPMA class", `${summary?.indexComparison?.fireWeatherIpmaClassLabel ?? "Not available"}; next ${summary?.indexComparison?.fireWeatherNextIpmaClass ?? "n/a"}`],
          ["KBDI / normalized", `${formatMaybeScore(summary?.indexComparison?.keetchByramDroughtIndex)} / ${formatMaybeScore(summary?.indexComparison?.normalizedKeetchByramDroughtIndex)}`],
          ["KBDI dryness", `${summary?.indexComparison?.kbdiDrynessClassLabel ?? "Not available"}; ${summary?.indexComparison?.kbdiAntecedentHistoryQuality ?? "antecedent n/a"}`],
          ["Portuguese Context Proxy", summary?.indexComparison?.portugueseContextRiskProxyLabel ?? summary?.indexComparison?.portugueseContextRiskProxyClass ?? "Not available"],
          ["Local FWI percentile", summary?.indexComparison?.localFwiPercentileStatus ?? "Not available"],
          ["FWI/KBDI status", `${summary?.indexComparison?.fireWeatherCalculationStatus ?? "FWI n/a"}; ${summary?.indexComparison?.kbdiCalculationStatus ?? "KBDI n/a"}`],
          ["Limitations", summary?.scoreComponents?.limitations ?? summary?.indexComparison?.limitations ?? "None exposed"],
        ]} />
      </Panel>
      <Panel colors={colors} accent="#7c3aed">
        <SectionHeader title="RBAC readiness note" subtitle="Conceptual role-based visibility plan; not security enforcement." />
        <SimpleTable colors={colors} columns={["Role", "Future UI access"]} rows={[
          ["Viewer", "Monitoring; basic Model & Provenance"],
          ["Analyst", "Monitoring; Evidence; Compare B/C"],
          ["Operator", "Scenario Lab; Run Orchestrator"],
          ["Developer", "Flow Explorer; Diagnostics; Raw JSON"],
          ["Admin", "Runtime State Control; Reset; future user/role management"],
        ]} />
        <Banner colors={colors} tone="#b45309">Role-based visibility can be applied to tabs and actions, but backend authorization is required for enforcement. Frontend visibility is not security.</Banner>
      </Panel>
    </ViewStack>
  );
}

function TerritorialContext({ colors, cells, sensors, summary }: { colors: Colors; cells: AreaCellResponse[]; sensors: SensorNodeResponse[]; summary: RuntimeSummaryResponse | null }) {
  return (
    <div style={cardGrid()}>
      <InfoCard colors={colors} title="Area context" status={summary?.areaCode ?? "Not available"} detail="Selected workspace area" />
      <InfoCard colors={colors} title="Grid context" status={`${cells.length} cells`} detail="Read from grid-cells endpoint" />
      <InfoCard colors={colors} title="Sensor context" status={`${sensors.length} sensors`} detail={`${sensors.filter(item => item.isActive).length} active`} />
      <InfoCard colors={colors} title="Weather variables" status="Not exposed" detail="Use dashboards when available" />
      <InfoCard colors={colors} title="Daily state" status="Not exposed" detail="No dedicated daily state endpoint in this UI" />
      <InfoCard colors={colors} title="Territorial risk" status={summary?.areaOperationalState?.aggregateRiskLevel ?? "Not available"} detail="Projection-backed operational risk" />
    </div>
  );
}

function CodeMapping({ colors }: { colors: Colors }) {
  const rows = [
    ["Scenario orchestration", "Implemented", "control.simulation_runs", "Scenario Lab / Latest Run", "SimulationRunner"],
    ["SimulationRun", "Implemented", "Persisted", "Top bar; Latest Run; Run Timings", "SimulationRun"],
    ["TruthSnapshot", "Implemented", "Transient", "Model only", "TruthSnapshot"],
    ["LocalObservation", "Implemented", "Transient", "Model only", "LocalObservation"],
    ["EventEnvelope", "Implemented", "pipeline.event_inbox", "Runtime Chain / Persistence Views", "EventEnvelope<TPayload>"],
    ["PreventionWorker", "Implemented", "Runtime service", "Flow Explorer", "PreventionWorker"],
    ["ReadingRiskPipeline", "Implemented", "Processing attempts / projections", "Processing Pipeline", "ReadingRiskPipeline"],
    ["RiskEligibilityService", "Partial UI evidence", "Aggregate audit only", "Latest Run Audit", "RiskEligibilityService"],
    ["SimpleRiskScoringService", "Implemented", "projection.risk_assessment_log", "Area Risk / Evidence", "SimpleRiskScoringService"],
    ["DailyCellState", "Implemented", "Projection/carry-forward state", "Territorial Context", "DailyCellState"],
    ["RiskAssessment", "Implemented", "projection.risk_assessment_log", "Area Risk chart", "RiskAssessment"],
    ["AreaRiskSnapshot", "Implemented", "projection.area_risk_snapshot_log", "Latest Run Audit / Area Risk", "AreaRiskSnapshot"],
    ["V1AlertPolicy", "Implemented", "projection.alert_state", "Alerts", "V1AlertPolicy"],
    ["Projection store", "Implemented", "projection.*", "Monitoring / Persistence Views", "PostgresAreaOperationalProjectionStore"],
  ];
  return <Panel colors={colors}><SectionHeader title="Code Mapping" subtitle="Report concepts mapped to implementation state, persistence and visible UI evidence." /><SimpleTable colors={colors} columns={["Concept", "Status", "Persistence", "UI evidence", "Code"]} rows={rows} /></Panel>;
}

function RuntimeChainStrip({ colors, summary, large = false }: { colors: Colors; summary: RuntimeSummaryResponse | null; large?: boolean }) {
  const chain = [
    ["Scenario Run", summary?.currentRun ? "Active" : summary?.latestRun ? "Latest" : "No data", summary?.latestRun?.status ?? "Not observed", "#2563eb"],
    ["Event Inbox", summary?.pipeline.inboxTotal ?? "No data", `${summary?.pipeline.inboxRecent ?? 0} recent`, "#059669"],
    ["Processing Attempts", summary?.pipeline.attemptsRecent ?? "No data", "recent attempts", "#7c3aed"],
    ["Risk", summary?.risk.recentCount ?? "No data", formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore), "#0891b2"],
    ["State", summary?.cellOperationalStateCount ?? "No data", summary?.areaOperationalState ? "projection updated" : "No area state", "#be123c"],
    ["Alerts", summary?.activeAlerts.length ?? "No data", summary?.areaOperationalState?.alertState ?? "None", "#b45309"],
    ["API/UI", summary ? "Loaded" : "No data", "summary endpoint", "#475569"],
  ];
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(135px, 1fr))", gap: "10px" }}>
      {chain.map(([label, value, status, tone], index) => (
        <div key={label} style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          <div style={{ ...panel(colors), borderLeft: `4px solid ${tone}`, flex: 1, minHeight: large ? "112px" : "86px" }}>
            <div style={{ color: colors.textSecond, fontSize: "12px" }}>{label}</div>
            <div style={{ color: colors.textPrimary, fontWeight: 800, fontSize: large ? "24px" : "19px", marginTop: "5px" }}>{value}</div>
            <div style={{ color: colors.textSecond, fontSize: "12px", marginTop: "4px" }}>{status}</div>
          </div>
          {index < chain.length - 1 && <div style={{ color: colors.textMuted, fontWeight: 800 }}>-</div>}
        </div>
      ))}
    </div>
  );
}

function RiskLineChart({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const data = summary?.risk.recentScores.map(point => ({ time: shortTime(point.timestamp), score: point.riskScore, level: point.riskLevel })) ?? [];
  if (data.length === 0) {
    return <EmptyState colors={colors} text="No recent risk assessments in this window." />;
  }
  return (
    <div style={{ height: "260px" }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid stroke={colors.panelBorder} />
          <XAxis dataKey="time" stroke={colors.textSecond} tick={{ fontSize: 12 }} />
          <YAxis stroke={colors.textSecond} domain={[0, 1]} tick={{ fontSize: 12 }} />
          <Tooltip contentStyle={{ background: colors.panelBg, border: `1px solid ${colors.panelBorder}`, color: colors.textPrimary }} />
          <Line type="monotone" dataKey="score" stroke="#0891b2" strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

function BarGraph({ data, color }: { data: { name: string; value: number }[]; color: string }) {
  if (data.length === 0) {
    return <div style={{ height: "100%", display: "grid", placeItems: "center", color: "#64748b" }}>No data</div>;
  }
  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={data}>
        <CartesianGrid stroke="#e5e7eb" vertical={false} />
        <XAxis dataKey="name" tick={{ fontSize: 11 }} interval={0} angle={-15} textAnchor="end" height={55} />
        <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
        <Tooltip />
        <Bar dataKey="value" fill={color} radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}

function RunDetails({ colors, run }: { colors: Colors; run: RuntimeRunSummaryResponse | null }) {
  if (!run) {
    return <EmptyState colors={colors} text="No simulation run is persisted yet." />;
  }
  return (
    <ViewStack>
      <KeyValues colors={colors} rows={[
        ["SimulationRunId", run.id],
        ["ScenarioCode", run.scenarioCode],
        ["ScenarioName", run.scenarioName],
        ["Status", run.status],
        ["Started", formatDate(run.startedAt)],
        ["Ended", formatDate(run.endedAt)],
        ["Duration", run.durationSeconds == null ? "Not available" : `${Math.round(run.durationSeconds)}s`],
        ["Cycles", run.numberOfCycles],
        ["Interval", `${run.intervalSeconds}s`],
        ["Seed", run.executionSeed ?? "Not persisted"],
        ["CorrelationId", run.orchestratorCorrelationId ?? "Not available"],
        ["Requested overrides", formatOverrides(run.runOverrides?.requested)],
        ["Resolved overrides", formatOverrides(run.runOverrides?.resolved)],
        ["Selected sensors", run.runOverrides?.selectedSensorNames.join(", ") || "Not available"],
      ]} />
      <CollapsibleJson colors={colors} title="Raw metadata JSON" value={run.metadataJson ? parseJson(run.metadataJson) : "Not available"} />
    </ViewStack>
  );
}

function RunRequestResult({ colors, result, request, message, areaCode }: { colors: Colors; result: RuntimeRunStartResponse | null; request: RuntimeRunStartRequest; message: string | null; areaCode: string }) {
  const run = result?.run;
  const requested = result?.requested;
  return (
    <div style={{ ...panel(colors), marginTop: "14px" }}>
      <SectionHeader title="Run request result" subtitle={message ?? "Run request submitted."} />
      <KeyValues colors={colors} rows={[
        ["status", result?.status ?? "Submitted"],
        ["message", message ?? result?.message ?? "Not available"],
        ["correlationId", result?.orchestratorCorrelationId ?? "Not available"],
        ["runLabel", request.runLabel ?? "Not available"],
        ["areaCode", areaCode],
        ["scenarioCode", request.scenarioCode],
        ["sensorCount", requested?.sensorCount ?? request.sensorCount ?? "Not available"],
        ["numberOfCycles", requested?.numberOfCycles ?? request.numberOfCycles ?? "Not available"],
        ["intervalSeconds", requested?.intervalSeconds ?? request.intervalSeconds ?? "Not available"],
        ["seed", requested?.seed ?? request.seed ?? "Not available"],
        ["degradationProfile", requested?.degradationProfile ?? request.degradationProfile ?? "Not available"],
        ["degradationProfiles", (requested?.degradationProfiles ?? request.degradationProfiles ?? []).join(", ") || "Not available"],
        ["simulationRunId", run?.id ?? "waiting_for_persistence"],
        ["selectedSensors", run?.runOverrides?.selectedSensorNames.join(", ") || "Not available"],
        ["evidenceDirectory", result?.evidenceDirectory ?? result?.logDirectory ?? "Not available"],
      ]} />
      <CollapsibleJson colors={colors} title="Raw run response JSON" value={result ?? "Not available"} />
    </div>
  );
}

function ResetCounts({ result, colors }: { result: RuntimeResetResponse; colors: Colors }) {
  const rows = result.before.map(before => {
    const after = result.after.find(item => item.schema === before.schema && item.table === before.table);
    return [before.schema, before.table, String(before.count), String(after?.count ?? before.count)];
  });
  return (
    <div style={{ marginTop: "14px" }}>
      <SectionHeader title={`Reset result: ${result.status}`} subtitle={result.message} />
      <SimpleTable colors={colors} columns={["Schema", "Table", "Before", "After"]} rows={rows} />
      <CollapsibleJson colors={colors} title="Raw reset JSON" value={result} />
    </div>
  );
}

function DiagnosticResult({ colors, result }: { colors: Colors; result: RuntimeDiagnosticResultResponse | null }) {
  if (!result) {
    return <EmptyState colors={colors} text="Choose a diagnostic to load data." />;
  }
  return (
    <>
      <SimpleTable colors={colors} columns={result.columns} rows={result.rows.map(row => result.columns.map(column => row[column] ?? ""))} />
      {result.limitations.length > 0 && <ul style={{ color: colors.textSecond }}>{result.limitations.map(item => <li key={item}>{item}</li>)}</ul>}
      <CollapsibleJson colors={colors} title="Raw diagnostic JSON" value={result} />
    </>
  );
}

function StatusCounts({ colors, rows }: { colors: Colors; rows: { status: string; count: number }[] }) {
  if (rows.length === 0) {
    return <EmptyState colors={colors} text="No data." />;
  }
  return <KeyValues colors={colors} rows={rows.map(item => [item.status, item.count])} />;
}

function AlertList({ colors, alerts, detailed = false }: { colors: Colors; alerts: RuntimeAlertSummaryResponse[]; detailed?: boolean }) {
  if (alerts.length === 0) {
    return <EmptyState colors={colors} text="No active alerts." />;
  }
  return (
    <div style={{ display: "grid", gap: "8px" }}>
      {alerts.map(alert => (
        <div key={alert.id} style={{ ...panel(colors), borderLeft: `4px solid ${alert.severity?.toLowerCase() === "critical" ? "#dc2626" : "#b45309"}` }}>
          <strong>{alert.alertCode}</strong>
          <div style={paragraph(colors)}>{alert.alertState ?? alert.status} - {alert.severity}</div>
          {detailed && <div style={paragraph(colors)}>{alert.message}</div>}
          <small style={{ color: colors.textSecond }}>triggered {formatDate(alert.triggeredAt)}; resolved {formatDate(alert.resolvedAt)}; updated {formatDate(alert.updatedAt)}</small>
        </div>
      ))}
    </div>
  );
}

function KeyValues({ colors, rows }: { colors: Colors; rows: [string, ReactNode][] }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "8px" }}>
      {rows.map(([label, value]) => (
        <div key={label} style={{ background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "10px", minWidth: 0 }}>
          <div style={{ color: colors.textMuted, fontSize: "12px", marginBottom: "4px" }}>{label}</div>
          <div style={{ color: colors.textPrimary, fontWeight: 700, fontSize: "13px", overflowWrap: "anywhere" }}>{formatNode(value)}</div>
        </div>
      ))}
    </div>
  );
}

function SimpleTable({ colors, columns, rows }: { colors: Colors; columns: string[]; rows: ReactNode[][] }) {
  return (
    <div style={{ overflowX: "auto", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px" }}>
        <thead><tr>{columns.map(column => <th key={column} style={cell(colors, true)}>{column}</th>)}</tr></thead>
        <tbody>
          {rows.length === 0 ? <tr><td style={cell(colors)} colSpan={Math.max(1, columns.length)}>No data</td></tr> : rows.map((row, index) => (
            <tr key={index}>{columns.map((column, colIndex) => <td key={column} style={cell(colors)}>{formatNode(row[colIndex])}</td>)}</tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function FlowNodes({ colors, nodes }: { colors: Colors; nodes: string[][] }) {
  return (
    <div style={{ display: "grid", gap: "8px" }}>
      {nodes.map(([name, description, persistence, runtime], index) => (
        <div key={name} style={{ display: "grid", gridTemplateColumns: "36px 1fr", alignItems: "center", gap: "8px" }}>
          <div style={{ color: colors.textMuted, fontWeight: 800 }}>{index === 0 ? "" : "v"}</div>
          <div style={{ ...panel(colors), display: "flex", justifyContent: "space-between", gap: "12px", flexWrap: "wrap" }}>
            <div><strong>{name}</strong><div style={paragraph(colors)}>{description}</div></div>
            <div style={{ display: "flex", gap: "6px", alignItems: "center" }}><Badge colors={colors}>{persistence}</Badge><Badge colors={colors}>{runtime}</Badge></div>
          </div>
        </div>
      ))}
    </div>
  );
}

function Timeline({ colors, steps }: { colors: Colors; steps: string[] }) {
  return (
    <div style={{ display: "grid", gap: "8px" }}>
      {steps.map((step, index) => (
        <div key={step} style={{ ...panel(colors), display: "flex", gap: "10px", alignItems: "center" }}>
          <Badge colors={colors}>{String(index + 1).padStart(2, "0")}</Badge>
          <strong>{step}</strong>
        </div>
      ))}
    </div>
  );
}

function InfoCard({ colors, title, status, detail }: { colors: Colors; title: string; status: ReactNode; detail: ReactNode }) {
  return (
    <Panel colors={colors}>
      <div style={{ color: colors.textSecond, fontSize: "12px", marginBottom: "5px" }}>{title}</div>
      <div style={{ color: colors.textPrimary, fontWeight: 800, fontSize: "18px", overflowWrap: "anywhere" }}>{formatNode(status)}</div>
      <div style={{ color: colors.textSecond, fontSize: "13px", marginTop: "6px", lineHeight: 1.4 }}>{formatNode(detail)}</div>
    </Panel>
  );
}

function ChartPanel({ colors, title, children }: { colors: Colors; title: string; children: ReactNode }) {
  return <Panel colors={colors}><SectionHeader title={title} /><div style={{ height: "230px" }}>{children}</div></Panel>;
}

function EventRows({ colors, rows, empty }: { colors: Colors; rows: string[][]; empty: string }) {
  if (rows.length === 0) {
    return <EmptyState colors={colors} text={empty} />;
  }
  return <div style={{ display: "grid", gap: "8px" }}>{rows.map(([title, detail, date], index) => <div key={index} style={{ ...panel(colors) }}><strong>{title}</strong><div style={paragraph(colors)}>{detail}</div><small style={{ color: colors.textSecond }}>{date}</small></div>)}</div>;
}

function NarrativeSummary({ colors, rows }: { colors: Colors; rows: ReactNode[][] }) {
  const metric = (name: string) => rows.find(row => row[0] === name);
  const accepted = metric("observed accepted readings");
  const missing = metric("missing events");
  const lines: string[] = [];
  if (accepted) {
    const b = Number(accepted[1]);
    const c = Number(accepted[2]);
    if (Number.isFinite(b) && Number.isFinite(c) && c < b) {
      lines.push("Scenario C produced fewer accepted readings than Scenario B, consistent with missing-readings degradation.");
    }
  }
  if (missing) {
    const b = Number(missing[1]);
    const c = Number(missing[2]);
    if (Number.isFinite(b) && Number.isFinite(c) && c > b) {
      lines.push("Scenario C shows more missing events than Scenario B in the persisted comparison.");
    }
  }
  return <Banner colors={colors} tone="#2563eb">{lines.length ? lines.join(" ") : "No supported B/C narrative is available from the current comparison data."}</Banner>;
}

function Metric({ colors, title, value, detail, icon, tone }: { colors: Colors; title: string; value: ReactNode; detail: ReactNode; icon: ReactNode; tone: string }) {
  return (
    <div style={{ ...panel(colors), borderLeft: `4px solid ${tone}` }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: "8px", color: colors.textSecond, fontSize: "13px" }}>
        <span>{title}</span><span style={{ color: tone }}>{icon}</span>
      </div>
      <div style={{ fontSize: "24px", fontWeight: 800, marginTop: "8px", lineHeight: 1.1, overflowWrap: "anywhere" }}>{formatNode(value)}</div>
      <div style={{ color: colors.textSecond, fontSize: "12px", marginTop: "6px" }}>{formatNode(detail)}</div>
    </div>
  );
}

function Tabs<T extends string>({ values, selected, onSelect, colors, compact = false }: { values: readonly T[]; selected: T; onSelect: (value: T) => void; colors: Colors; compact?: boolean }) {
  return (
    <div style={{ display: "flex", gap: "6px", flexWrap: "wrap", marginBottom: compact ? "14px" : "12px" }}>
      {values.map(value => (
        <button key={value} onClick={() => onSelect(value)} style={button(colors, value === selected)}>
          {value}
        </button>
      ))}
    </div>
  );
}

function SegmentedButtons({ values, selected, onSelect, format, colors }: { values: number[]; selected: number; onSelect: (value: number) => void; format: (value: number) => string; colors: Colors }) {
  return (
    <div style={{ display: "flex", padding: "3px", border: `1px solid ${colors.panelBorder}`, background: colors.segBg, borderRadius: "8px" }}>
      {values.map(value => (
        <button key={value} onClick={() => onSelect(value)} style={{ border: "none", background: value === selected ? colors.segActive : "transparent", color: value === selected ? colors.textPrimary : colors.textSecond, borderRadius: "6px", padding: "7px 10px", cursor: "pointer", fontWeight: 700 }}>
          {format(value)}
        </button>
      ))}
    </div>
  );
}

function LabeledInput({ colors, label, value, onChange }: { colors: Colors; label: string; value: string; onChange: (value: string) => void }) {
  return <div><label style={labelStyle(colors)}>{label}</label><input style={input(colors)} value={value} onChange={event => onChange(event.target.value)} /></div>;
}

function LabeledNumber({ colors, label, value, onChange, max }: { colors: Colors; label: string; value: number | null; onChange: (value: number | null) => void; max?: number }) {
  return <div><label style={labelStyle(colors)}>{label}</label><input style={input(colors)} type="number" max={max} value={value ?? ""} onChange={event => onChange(event.target.value === "" ? null : Number(event.target.value))} /></div>;
}

function LabeledSelect({ colors, label, value, options, onChange }: { colors: Colors; label: string; value: string; options: { value: string; label: string }[]; onChange: (value: string) => void }) {
  return <div><label style={labelStyle(colors)}>{label}</label><select style={input(colors)} value={value} onChange={event => onChange(event.target.value)}>{options.length === 0 && <option value={value}>{value}</option>}{options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>;
}

function CheckRow({ colors, label, checked, onChange }: { colors: Colors; label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return <label style={{ display: "inline-flex", gap: "8px", alignItems: "center", marginTop: "10px", marginRight: "16px", color: colors.textSecond }}><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} /> {label}</label>;
}

function CollapsibleJson({ colors, title, value }: { colors: Colors; title: string; value: unknown }) {
  return (
    <details style={{ marginTop: "12px", color: colors.textSecond }}>
      <summary style={{ cursor: "pointer", color: colors.textPrimary, fontWeight: 800 }}>{title}</summary>
      <pre style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "12px", maxHeight: "260px", overflow: "auto" }}>{typeof value === "string" ? value : JSON.stringify(value, null, 2)}</pre>
    </details>
  );
}

function WorkspacePanel({ colors, children }: { colors: Colors; children: ReactNode }) {
  return <section style={{ ...panel(colors), minHeight: "620px" }}>{children}</section>;
}

function Panel({ colors, accent, children }: { colors: Colors; accent?: string; children: ReactNode }) {
  return <section style={{ ...panel(colors), borderTop: accent ? `3px solid ${accent}` : `1px solid ${colors.panelBorder}` }}>{children}</section>;
}

function Banner({ colors, tone, children }: { colors: Colors; tone: string; children: ReactNode }) {
  return <div style={{ ...panel(colors), borderLeft: `4px solid ${tone}`, color: colors.textSecond, margin: "10px 0", lineHeight: 1.5 }}>{children}</div>;
}

function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return <div style={{ marginBottom: "12px" }}><h2 style={{ margin: 0, fontSize: "18px", fontWeight: 800 }}>{title}</h2>{subtitle && <div style={{ color: "#64748b", fontSize: "13px", marginTop: "3px" }}>{subtitle}</div>}</div>;
}

function EmptyState({ colors, text }: { colors: Colors; text: string }) {
  return <div style={{ color: colors.textSecond, fontSize: "14px", padding: "12px 0" }}>{text}</div>;
}

function Pill({ colors, label, value }: { colors: Colors; label: string; value: ReactNode }) {
  return <span style={{ display: "inline-flex", gap: "5px", alignItems: "center", background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "999px", padding: "6px 10px", fontSize: "12px" }}><span style={{ color: colors.textMuted }}>{label}</span><strong>{formatNode(value)}</strong></span>;
}

function Badge({ colors, children }: { colors: Colors; children: ReactNode }) {
  return <span style={{ background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "999px", padding: "4px 8px", fontSize: "12px", fontWeight: 800 }}>{children}</span>;
}

function ViewStack({ children }: { children: ReactNode }) {
  return <div style={{ display: "grid", gap: "14px" }}>{children}</div>;
}

function MetricGrid({ children }: { children: ReactNode }) {
  return <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "12px" }}>{children}</div>;
}

function FormGrid({ children }: { children: ReactNode }) {
  return <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))", gap: "10px" }}>{children}</div>;
}

function panel(colors: Colors) {
  return { background: colors.panelBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "14px", boxShadow: "0 1px 8px rgba(15,23,42,0.06)" };
}

function button(colors: Colors, active = false) {
  return { display: "inline-flex", alignItems: "center", gap: "7px", border: `1px solid ${active ? colors.textPrimary : colors.panelBorder}`, background: active ? colors.segActive : colors.panelBg, color: colors.textPrimary, borderRadius: "8px", padding: "8px 11px", cursor: "pointer", fontWeight: 700, textDecoration: "none", minHeight: "36px" };
}

function input(colors: Colors) {
  return { width: "100%", border: `1px solid ${colors.panelBorder}`, background: colors.sectionBg, color: colors.textPrimary, borderRadius: "8px", padding: "8px 10px" };
}

function labelStyle(colors: Colors) {
  return { display: "block", color: colors.textSecond, fontSize: "12px", marginBottom: "4px", textTransform: "capitalize" as const };
}

function cell(colors: Colors, header = false) {
  return { borderBottom: `1px solid ${colors.panelBorder}`, padding: "8px", textAlign: "left" as const, background: header ? colors.sectionBg : "transparent", whiteSpace: "nowrap" as const, verticalAlign: "top" as const };
}

function cardGrid() {
  return { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "12px" };
}

function twoCol() {
  return { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: "12px" };
}

function paragraph(colors: Colors) {
  return { color: colors.textSecond, fontSize: "13px", lineHeight: 1.55, margin: "4px 0" };
}

function formatError(error: unknown) {
  return error instanceof Error ? error.message : "Unexpected UI/runtime error";
}

function parseJson(value: string | null | undefined) {
  if (!value) {
    return null;
  }
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString() : "Not available";
}

function shortTime(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function formatScore(value: number | null | undefined) {
  return value == null ? "No data" : value.toFixed(2);
}

function formatMaybeScore(value: number | null | undefined) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatRiskRange(min: number | null | undefined, max: number | null | undefined) {
  if (min == null || max == null) {
    return "No recent scores";
  }
  return `min ${min.toFixed(2)} / max ${max.toFixed(2)}`;
}

function normalizeProfiles(values: string[] | null | undefined, legacy: string | null | undefined) {
  const profiles = values && values.length > 0 ? values : legacy ? legacy.split(/[,+;|]/) : ["none"];
  const normalized = Array.from(new Set(profiles.map(value => value.trim()).filter(Boolean)));
  return normalized.length === 0 ? ["none"] : normalized.length > 1 ? normalized.filter(value => value !== "none") : normalized;
}

function toLegacyProfile(values: string[]) {
  return values.length === 1 ? values[0] : values.join("+");
}

function formatOverrides(values: RuntimeRunSummaryResponse["runOverrides"] extends infer T ? T extends { requested: infer R } ? R : never : never) {
  if (!values) {
    return "Not available";
  }
  const typed = values as { sensorCount?: number | null; numberOfCycles?: number | null; intervalSeconds?: number | null; seed?: number | null; degradationProfile?: string | null; degradationProfiles?: string[] | null };
  const parts = [
    typed.sensorCount == null ? null : `sensors ${typed.sensorCount}`,
    typed.numberOfCycles == null ? null : `cycles ${typed.numberOfCycles}`,
    typed.intervalSeconds == null ? null : `interval ${typed.intervalSeconds}s`,
    typed.seed == null ? null : `seed ${typed.seed}`,
    typed.degradationProfiles && typed.degradationProfiles.length > 0 ? typed.degradationProfiles.join("+") : typed.degradationProfile ?? null,
  ].filter(Boolean);
  return parts.length ? parts.join(" / ") : "Not available";
}

function formatNode(value: ReactNode) {
  if (value === null || value === undefined || value === "") {
    return "Not available";
  }
  if (typeof value === "object" && !Array.isArray(value)) {
    return value as ReactNode;
  }
  return value;
}

function buildSafeGrafanaAreaUrl(link: string | null, areaId: string) {
  if (!link || !areaId || link.includes("Enter value") || /\?t\?|\?h\?|\?w\?/.test(link)) {
    return null;
  }

  const replaced = link.replace(/\?\?\?/g, encodeURIComponent(areaId)).replace(/\?1\?/g, encodeURIComponent(areaId));
  if (/\?\?\?|\?1\?/.test(replaced)) {
    return null;
  }

  let url: URL;
  try {
    url = new URL(replaced, window.location.origin);
  } catch {
    return null;
  }

  const isInternalWebUi = url.origin === window.location.origin;
  const looksLikeGrafana = /grafana|:3000$/i.test(url.host) || url.pathname.startsWith("/d/");
  if (isInternalWebUi || !looksLikeGrafana) {
    return null;
  }

  if (!url.searchParams.has("kiosk")) {
    url.searchParams.set("kiosk", "");
  }

  console.log("Built Grafana URL:", url.toString());
  return url.toString();
}

function buildAttemptTimingSummary(summary: RuntimeSummaryResponse | null) {
  const attempts = summary?.pipeline.latestFailedAttempts ?? [];
  const durations = attempts
    .map(item => attemptDurationMs(item))
    .filter((value): value is number => value != null);
  const started = attempts.map(item => item.startedAt).filter(Boolean).sort();
  const finished = attempts.map(item => item.finishedAt).filter((value): value is string => Boolean(value)).sort();
  const grouped = new Map<string, RuntimeProcessingAttemptResponse[]>();

  for (const attempt of attempts) {
    const key = `${attempt.stage}::${attempt.outcome}`;
    grouped.set(key, [...(grouped.get(key) ?? []), attempt]);
  }

  const rows = Array.from(grouped.entries()).map(([key, items]) => {
    const [stage, outcome] = key.split("::");
    const itemDurations = items.map(item => attemptDurationMs(item)).filter((value): value is number => value != null);
    return [
      stage,
      outcome,
      items.length,
      minDate(items.map(item => item.startedAt)),
      maxDate(items.map(item => item.finishedAt).filter((value): value is string => Boolean(value))),
      itemDurations.length ? formatMs(Math.min(...itemDurations)) : "Not exposed",
      itemDurations.length ? formatMs(avg(itemDurations)) : "Not exposed",
      itemDurations.length ? formatMs(Math.max(...itemDurations)) : "Not exposed",
    ];
  });

  const failedAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter(item => item.errorCode || !/success|completed|accepted/i.test(item.outcome))
    .reduce((sum, item) => sum + item.count, 0);
  const quarantinedAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter(item => /quarantine/i.test(item.outcome) || /quarantine/i.test(item.errorCode ?? ""))
    .reduce((sum, item) => sum + item.count, 0);
  const successfulAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter(item => /success|completed|accepted/i.test(item.outcome))
    .reduce((sum, item) => sum + item.count, 0);

  return {
    firstStarted: started[0] ?? null,
    lastFinished: finished[finished.length - 1] ?? null,
    failedAttempts,
    quarantinedAttempts,
    successfulAttempts,
    avgDurationMs: durations.length ? avg(durations) : null,
    maxDurationMs: durations.length ? Math.max(...durations) : null,
    rows,
  };
}

function attemptDurationMs(attempt: RuntimeProcessingAttemptResponse) {
  if (!attempt.startedAt || !attempt.finishedAt) {
    return null;
  }
  const started = new Date(attempt.startedAt).getTime();
  const finished = new Date(attempt.finishedAt).getTime();
  return Number.isFinite(started) && Number.isFinite(finished) && finished >= started ? finished - started : null;
}

function buildRuntimeChainDetails(summary: RuntimeSummaryResponse | null) {
  const failed = summary?.pipeline.latestFailedAttempts[0];
  return [
    { label: "Scenario Run", count: summary?.currentRun ? "Active" : summary?.latestRun ? "Latest" : "No data", status: summary?.latestRun?.status ?? "Not observed", lastUpdate: formatDate(summary?.latestRun?.endedAt ?? summary?.latestRun?.startedAt), latestError: "Not exposed", source: "control.simulation_runs", tone: "#2563eb" },
    { label: "Event Inbox", count: summary?.pipeline.inboxTotal ?? "No data", status: `${summary?.pipeline.inboxRecent ?? 0} recent`, lastUpdate: formatDate(summary?.generatedAtUtc), latestError: "Not exposed", source: "pipeline.event_inbox", tone: "#059669", navigate: "retry" as const },
    { label: "Processing Attempts", count: summary?.pipeline.attemptsRecent ?? "No data", status: "Recent attempts", lastUpdate: formatDate(failed?.finishedAt ?? failed?.startedAt), latestError: failed?.errorCode ?? "No recent failed attempt", source: "pipeline.processing_attempts", tone: "#7c3aed", navigate: "retry" as const },
    { label: "Risk", count: summary?.risk.recentCount ?? "No data", status: formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore), lastUpdate: formatDate(summary?.risk.latestTimestamp), latestError: "Not exposed", source: "projection.risk_assessment_log", tone: "#0891b2", navigate: "risk" as const },
    { label: "State", count: summary?.cellOperationalStateCount ?? "No data", status: summary?.areaOperationalState ? "Projection updated" : "No area state", lastUpdate: formatDate(summary?.areaOperationalState?.updatedAt), latestError: "Not exposed", source: "projection.*_operational_state", tone: "#be123c", navigate: "state" as const },
    { label: "Alerts", count: summary?.activeAlerts.length ?? "No data", status: summary?.areaOperationalState?.alertState ?? "None", lastUpdate: formatDate(summary?.activeAlerts[0]?.updatedAt), latestError: "Not exposed", source: "projection.alert_state", tone: "#b45309", navigate: "alerts" as const },
    { label: "API/UI", count: summary ? "Loaded" : "No data", status: "Runtime summary endpoint", lastUpdate: formatDate(summary?.generatedAtUtc), latestError: summary?.warnings[0] ?? "No warning exposed", source: "/control/runtime/summary", tone: "#475569", navigate: "services" as const },
  ];
}

function buildNominalFlowSteps(summary: RuntimeSummaryResponse | null, audit: RuntimeRunAuditResponse | null, runResult: RuntimeRunStartResponse | null) {
  const run = summary?.currentRun ?? summary?.latestRun ?? null;
  const expected = audit?.expectedEvents ?? null;
  const inbox = summary?.pipeline.inboxTotal ?? 0;
  const attempts = summary?.pipeline.attemptsRecent ?? 0;
  const risk = audit?.riskAssessments ?? summary?.risk.recentCount ?? 0;
  const stateCount = summary?.cellOperationalStateCount ?? 0;
  const alertCount = summary?.activeAlerts.length ?? 0;
  return [
    { name: "Select scenario", status: run?.scenarioCode ? "Done" : "No data", evidence: run?.scenarioCode ? `scenarioCode=${run.scenarioCode}` : "No scenario selected or persisted." },
    { name: "Start run", status: run?.id ? "Done" : "No data", evidence: run?.id ? `simulationRunId=${run.id}; status=${run.status}` : "No simulation run persisted." },
    { name: "Generate readings", status: expected && expected > 0 ? "Done" : "Not exposed", evidence: expected && expected > 0 ? `expectedEvents=${expected}` : "Expected event count is not exposed for this run." },
    { name: "Publish events", status: inbox > 0 ? "Done" : "No data", evidence: `event inbox total=${inbox}` },
    { name: "Ingest inbox", status: inbox > 0 ? "Done" : "No data", evidence: `${summary?.pipeline.inboxRecent ?? 0} inbox rows in selected window; ${inbox} total in scope.` },
    { name: "Process risk", status: risk > 0 ? "Done" : attempts > 0 ? "Partial" : "No data", evidence: `attempts=${attempts}; riskAssessments=${risk}` },
    { name: "Update projections", status: stateCount > 0 || summary?.areaOperationalState ? "Done" : "No data", evidence: `cell states=${stateCount}; area state=${summary?.areaOperationalState?.aggregateRiskLevel ?? "Not available"}` },
    { name: "Emit alerts", status: alertCount > 0 ? "Done" : "No data", evidence: `active alerts=${alertCount}` },
    { name: "Show UI", status: summary ? "Done" : "No data", evidence: summary ? `/control/runtime/summary loaded at ${formatDate(summary.generatedAtUtc)}` : "API summary not loaded." },
    { name: "Collect evidence", status: runResult?.evidenceDirectory ? "Done" : "Not exposed", evidence: runResult?.evidenceDirectory ?? "No evidence directory exposed to this UI state." },
  ];
}

function statusTone(status: string) {
  if (status === "Done") return "#059669";
  if (status === "Partial") return "#b45309";
  if (status === "Failed") return "#dc2626";
  return "#64748b";
}

function minDate(values: string[]) {
  return values.length ? formatDate(values.sort()[0]) : "Not exposed";
}

function maxDate(values: string[]) {
  return values.length ? formatDate(values.sort()[values.length - 1]) : "Not exposed";
}

function avg(values: number[]) {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function formatMs(value: number) {
  if (value < 1000) {
    return `${Math.round(value)}ms`;
  }
  return `${(value / 1000).toFixed(2)}s`;
}

function buildCompareRows(compare: RuntimeDiagnosticResultResponse | null): ReactNode[][] {
  const metrics = ["expected events", "observed accepted readings", "missing events", "risk assessments", "rejected count for area", "quarantined count for area", "risk min/max/avg"];
  const byMetric = new Map<string, { b: string; c: string }>();
  for (const metric of metrics) {
    byMetric.set(metric, { b: "Not available", c: "Not available" });
  }
  for (const row of compare?.rows ?? []) {
    const metric = row.metric ?? "";
    if (!byMetric.has(metric) && metric.startsWith("metric ")) {
      byMetric.set(metric, { b: "Not available", c: "Not available" });
    }
    const item = byMetric.get(metric);
    if (!item) {
      continue;
    }
    if (row.scenario === "scenario_b") {
      item.b = row.value ?? "Not available";
    }
    if (row.scenario === "scenario_c") {
      item.c = row.value ?? "Not available";
    }
  }
  return Array.from(byMetric.entries()).map(([metric, values]) => [metric, values.b, values.c, compareDelta(values.b, values.c)]);
}

function compareDelta(b: string, c: string) {
  const nb = Number(b);
  const nc = Number(c);
  if (Number.isFinite(nb) && Number.isFinite(nc)) {
    const delta = nc - nb;
    return delta === 0 ? "0" : delta > 0 ? `+${delta}` : String(delta);
  }
  return "Not available";
}

function groupDiagnostics(diagnostics: RuntimeDiagnosticDefinitionResponse[]) {
  const groups: Record<string, RuntimeDiagnosticDefinitionResponse[]> = { Runs: [], Pipeline: [], Risk: [], Alerts: [], Scenario: [], "Model Evidence": [], "Raw Data": [] };
  for (const item of diagnostics) {
    const id = item.id.toLowerCase();
    if (id.includes("np-vs-fwi") || id.includes("component") || id.includes("cell-context") || id.includes("fwi") || id.includes("kbdi") || id.includes("quality") || id.includes("coverage")) groups["Model Evidence"].push(item);
    else if (id.includes("run")) groups.Runs.push(item);
    else if (id.includes("pipeline") || id.includes("attempt") || id.includes("inbox") || id.includes("rejected") || id.includes("quarantined")) groups.Pipeline.push(item);
    else if (id.includes("risk")) groups.Risk.push(item);
    else if (id.includes("alert")) groups.Alerts.push(item);
    else if (id.includes("scenario") || id.includes("compare")) groups.Scenario.push(item);
    else groups["Raw Data"].push(item);
  }
  return groups;
}

function buildEvidenceMarkdown(audit: RuntimeRunAuditResponse | null, compare: RuntimeDiagnosticResultResponse | null, summary: RuntimeSummaryResponse | null) {
  const score = summary?.scoreComponents ?? audit?.scoreComponents ?? null;
  const index = summary?.indexComparison ?? audit?.indexComparison ?? null;
  return [
    "# Nature Protector Evidence Summary",
    "",
    `Area: ${summary?.areaCode ?? "Not available"}`,
    `Latest run: ${summary?.latestRun?.scenarioCode ?? "Not available"} / ${summary?.latestRun?.status ?? "Not available"}`,
    `Expected events: ${audit?.expectedEvents ?? "Not available"}`,
    `Accepted readings: ${audit?.acceptedReadings ?? "Not available"}`,
    `Missing events: ${audit?.missingEvents ?? "Not available"}`,
    `Risk assessments: ${audit?.riskAssessments ?? "Not available"}`,
    `Parameter set: ${score?.parameterSetVersion ?? "Not available"}`,
    `NP score/base/adjusted: ${formatMaybeScore(score?.npScore)} / ${formatMaybeScore(score?.baseRisk)} / ${formatMaybeScore(score?.adjustedScore)}`,
    `M/D/T: ${formatMaybeScore(score?.meteorologyComponent)} / ${formatMaybeScore(score?.droughtComponent)} / ${formatMaybeScore(score?.territoryComponent)}`,
    `H/F/G: ${formatMaybeScore(score?.hazardComponent)} / ${formatMaybeScore(score?.fuelComponent)} / ${formatMaybeScore(score?.geomorphologyComponent)}`,
    `C/I: ${formatMaybeScore(score?.confidenceFactor)} / ${formatMaybeScore(score?.integrityFactor)}`,
    `FWI raw/normalized/status: ${formatMaybeScore(index?.fireWeatherIndex)} / ${formatMaybeScore(index?.normalizedFireWeatherIndex)} / ${index?.fireWeatherCalculationStatus ?? "Not available"}`,
    `KBDI raw/normalized/status: ${formatMaybeScore(index?.keetchByramDroughtIndex)} / ${formatMaybeScore(index?.normalizedKeetchByramDroughtIndex)} / ${index?.kbdiCalculationStatus ?? "Not available"}`,
    `Precipitation 24h/provenance: ${formatMaybeScore(index?.dailyPrecipitationMillimeters)} / ${index?.provenance ?? "Not available"}`,
    `Index limitations: ${index?.limitations ?? score?.limitations ?? "None exposed"}`,
    `Degradation profiles: ${summary?.latestRun?.runOverrides?.resolved?.degradationProfiles?.join(", ") ?? summary?.latestRun?.runOverrides?.resolved?.degradationProfile ?? "Not available"}`,
    `Coverage/freshness/carry-forward: ${summary?.areaOperationalState?.coverageStatus ?? "n/a"} / ${summary?.areaOperationalState?.freshnessStatus ?? "n/a"} / ${summary?.areaOperationalState?.carryForwardStatus ?? "n/a"}`,
    "",
    "## Compare B vs C",
    ...(buildCompareRows(compare).map(row => `- ${row[0]}: B=${row[1]}, C=${row[2]}, delta=${row[3]}`)),
  ].join("\n");
}

function copyText(text: string) {
  void navigator.clipboard?.writeText(text);
}

function downloadText(fileName: string, text: string) {
  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
