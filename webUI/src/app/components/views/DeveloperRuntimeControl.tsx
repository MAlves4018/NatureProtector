import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, Play, RefreshCw, RotateCcw, Search } from "lucide-react";
import { api } from "../../services/api";
import {
  RuntimeDiagnosticDefinitionResponse,
  RuntimeDiagnosticResultResponse,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SensorNodeResponse,
} from "../../types";
import { getColors } from "../../utils/utils";

const DEFAULT_AREA = "proenca-a-nova";
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

export function DeveloperRuntimeControl({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);
  const [areaCode, setAreaCode] = useState(DEFAULT_AREA);
  const [recentMinutes, setRecentMinutes] = useState(30);
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [runAudit, setRunAudit] = useState<RuntimeRunAuditResponse | null>(null);
  const [diagnostics, setDiagnostics] = useState<RuntimeDiagnosticDefinitionResponse[]>([]);
  const [selectedDiagnostic, setSelectedDiagnostic] = useState<string>("runtime-table-counts");
  const [diagnosticResult, setDiagnosticResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [scenarios, setScenarios] = useState<ScenarioResponse[]>([]);
  const [sensorNodes, setSensorNodes] = useState<SensorNodeResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [submittingRun, setSubmittingRun] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [runMessage, setRunMessage] = useState<string | null>(null);
  const [runResult, setRunResult] = useState<RuntimeRunStartResponse | null>(null);
  const [resetResult, setResetResult] = useState<RuntimeResetResponse | null>(null);
  const [confirm, setConfirm] = useState("");
  const [runForm, setRunForm] = useState<RuntimeRunStartRequest>({
    areaCode: DEFAULT_AREA,
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

  const activeDiagnostic = useMemo(
    () => diagnostics.find(item => item.id === selectedDiagnostic) ?? diagnostics[0],
    [diagnostics, selectedDiagnostic]);
  const activeSensorCount = useMemo(
    () => sensorNodes.filter(sensor => sensor.isActive).length,
    [sensorNodes]);
  const activeDegradationProfiles = normalizeProfiles(runForm.degradationProfiles, runForm.degradationProfile);
  const scenarioCWithoutDegradation = runForm.scenarioCode === "scenario_c" && activeDegradationProfiles.every(profile => profile === "none");
  const sensorCountTooHigh = runForm.sensorCount != null && activeSensorCount > 0 && runForm.sensorCount > activeSensorCount;

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

  const loadBase = async () => {
    setLoading(true);
    try {
      const [summaryResult, catalog, areaScenarios, sensors] = await Promise.all([
        api.getRuntimeSummary(areaCode, recentMinutes),
        api.getRuntimeDiagnostics(),
        api.getAreaScenarios(areaCode),
        api.getAreaSensorNodes(areaCode),
      ]);
      setSummary(summaryResult);
      if (summaryResult.latestRun?.id) {
        setRunAudit(await api.getRuntimeRunAudit(summaryResult.latestRun.id));
      } else {
        setRunAudit(null);
      }
      setDiagnostics(catalog.diagnostics);
      setScenarios(areaScenarios);
      setSensorNodes(sensors);
      setMessage(null);
    } catch (err) {
      setMessage(formatError(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadBase();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const executeDiagnostic = async (id = selectedDiagnostic) => {
    setLoading(true);
    try {
      const result = await api.executeRuntimeDiagnostic(id, { areaCode, recentMinutes, scenarioCode: runForm.scenarioCode });
      setDiagnosticResult(result);
      setSelectedDiagnostic(id);
      setMessage(null);
    } catch (err) {
      setMessage(formatError(err));
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
      await loadBase();
    } catch (err) {
      setRunMessage(formatError(err));
    } finally {
      setSubmittingRun(false);
    }
  };

  const resetRuntime = async (dryRun: boolean) => {
    setLoading(true);
    try {
      const result = await api.resetRuntimeState({
        scope: "runtime-only",
        confirm,
        dryRun,
      });
      setResetResult(result);
      setMessage(result.message);
      await loadBase();
    } catch (err) {
      setMessage(formatError(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main style={{ minHeight: "100vh", background: c.pageBg, color: c.textPrimary, padding: "24px" }}>
      <section style={topBar()}>
        <div>
          <h1 style={{ margin: 0, fontSize: "28px", fontWeight: 800 }}>Developer Runtime Control</h1>
          <p style={{ margin: "6px 0 0", color: c.textSecond }}>
            Fixed diagnostics, run orchestration and controlled runtime reset for local development.
          </p>
        </div>
        <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", justifyContent: "flex-end" }}>
          <input style={input(c)} value={areaCode} onChange={event => setAreaCode(event.target.value)} aria-label="Area code" />
          <input style={{ ...input(c), width: "90px" }} type="number" value={recentMinutes} onChange={event => setRecentMinutes(Number(event.target.value))} aria-label="Recent minutes" />
          <button style={button(c)} onClick={loadBase} disabled={loading}><RefreshCw size={16} /> Refresh</button>
          <a style={button(c)} href={`/dashboards/${areaCode}/pipeline`}>Runtime Monitor</a>
        </div>
      </section>

      {message && <Panel colors={c} accent="#b45309"><strong>{message}</strong></Panel>}

      <section style={grid("repeat(auto-fit, minmax(180px, 1fr))")}>
        <Metric colors={c} title="API" value={summary ? "ok" : "unknown"} detail="summary endpoint" />
        <Metric colors={c} title="PostgreSQL" value={summary ? "ok" : "unknown"} detail="API query succeeded" />
        <Metric colors={c} title="RabbitMQ" value="not exposed" detail="no management adapter" />
        <Metric colors={c} title="Prevention.Host" value="not exposed" detail="no heartbeat yet" />
        <Metric colors={c} title="Latest run" value={summary?.latestRun?.status ?? "none"} detail={summary?.latestRun?.scenarioCode ?? "no run"} />
        <Metric colors={c} title="Missing events" value={runAudit?.missingEvents ?? "n/a"} detail="latest run audit" />
        <Metric colors={c} title="Risk assessments" value={runAudit?.riskAssessments ?? "n/a"} detail="latest run audit" />
        <Metric colors={c} title="Freshness" value={summary?.freshness ? `${summary.freshness.freshCount}/${summary.freshness.staleCount}/${summary.freshness.expiredCount}` : "n/a"} detail="fresh/stale/expired" />
      </section>

      {runAudit && (
        <Panel colors={c} accent="#2563eb">
          <SectionTitle title="Latest Run Audit" subtitle={`${runAudit.run.scenarioCode} · ${runAudit.run.runOverrides?.resolved?.degradationProfile ?? "degradation unknown"}`} />
          <ResultTable
            colors={c}
            result={{
              id: "latest-run-audit",
              title: "Latest run audit",
              description: "Persisted run-scoped audit summary",
              columns: ["metric", "value"],
              rows: [
                { metric: "simulationRunId", value: runAudit.run.id },
                { metric: "expectedEvents", value: String(runAudit.expectedEvents ?? "") },
                { metric: "acceptedReadings", value: String(runAudit.acceptedReadings) },
                { metric: "missingEvents", value: String(runAudit.missingEvents ?? "") },
                { metric: "rejected", value: String(runAudit.rejected) },
                { metric: "quarantined", value: String(runAudit.quarantined) },
                { metric: "riskAssessments", value: String(runAudit.riskAssessments) },
                { metric: "qualityFlags", value: runAudit.qualityFlagsSummary.map(item => `${item.status}:${item.count}`).join(", ") },
                { metric: "eligibility", value: runAudit.eligibilitySummary.map(item => `${item.status}:${item.count}`).join(", ") },
                { metric: "areaSnapshot", value: runAudit.areaSnapshot ? `${runAudit.areaSnapshot.aggregateRiskLevel} ${runAudit.areaSnapshot.aggregateRiskScore}` : "" },
              ],
              limitations: runAudit.limitations.map(item => item.message),
            }}
          />
        </Panel>
      )}

      <Panel colors={c}>
        <SectionTitle title="Diagnostics" subtitle="Buttons execute fixed backend diagnostics. No free-form SQL is accepted." />
        <div style={{ display: "grid", gridTemplateColumns: "260px 1fr", gap: "16px" }}>
          <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
            {diagnostics.map(item => (
              <button key={item.id} style={button(c, selectedDiagnostic === item.id)} onClick={() => executeDiagnostic(item.id)} disabled={loading}>
                <Search size={15} /> {item.title}
              </button>
            ))}
          </div>
          <div>
            <SectionTitle title={diagnosticResult?.title ?? activeDiagnostic?.title ?? "Diagnostic result"} subtitle={diagnosticResult?.description ?? activeDiagnostic?.description} />
            <ResultTable result={diagnosticResult} colors={c} />
          </div>
        </div>
      </Panel>

      <section style={grid("repeat(auto-fit, minmax(360px, 1fr))")}>
        <Panel colors={c}>
          <SectionTitle title="Run Orchestrator" subtitle="Starts Simulator.Host in Development with RunOverrides. Parallel runs are blocked by default." />
          <FormGrid>
            <LabeledSelect
              colors={c}
              label="scenarioCode"
              value={runForm.scenarioCode}
              options={scenarios.map(scenario => ({ value: scenario.code, label: `${scenario.code} - ${scenario.name} (${scenario.scenarioKind})` }))}
              onChange={value => setRunForm({
                ...runForm,
                scenarioCode: value,
                degradationProfile: value === "scenario_c" && runForm.degradationProfile === "none" ? "missing-readings" : runForm.degradationProfile,
                degradationProfiles: value === "scenario_c" && activeDegradationProfiles.every(profile => profile === "none") ? ["missing-readings"] : activeDegradationProfiles,
                runLabel: value === "scenario_c" ? "scenario-c-from-ui" : runForm.runLabel,
              })}
            />
            <LabeledNumber colors={c} label="sensorCount" value={runForm.sensorCount} max={activeSensorCount || undefined} onChange={value => setRunForm({ ...runForm, sensorCount: value })} />
            <LabeledNumber colors={c} label="numberOfCycles" value={runForm.numberOfCycles} onChange={value => setRunForm({ ...runForm, numberOfCycles: value })} />
            <LabeledNumber colors={c} label="intervalSeconds" value={runForm.intervalSeconds} onChange={value => setRunForm({ ...runForm, intervalSeconds: value })} />
            <LabeledNumber colors={c} label="seed" value={runForm.seed} onChange={value => setRunForm({ ...runForm, seed: value })} />
            <LabeledNumber colors={c} label="timeoutSeconds" value={runForm.timeoutSeconds} onChange={value => setRunForm({ ...runForm, timeoutSeconds: value ?? 180 })} />
            <LabeledInput colors={c} label="runLabel" value={runForm.runLabel ?? ""} onChange={value => setRunForm({ ...runForm, runLabel: value || null })} />
          </FormGrid>
          <div style={{ color: c.textSecond, fontSize: "13px", marginTop: "10px" }}>
            Active sensors available: {activeSensorCount || "unknown"} · Selected sensors requested: {runForm.sensorCount ?? "all"}
          </div>
          <div style={{ marginTop: "10px" }}>
            <label style={label(c)}>degradation profiles</label>
            <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
              {DEGRADATION_PROFILE_OPTIONS.map(profile => (
                <CheckRow
                  key={profile}
                  colors={c}
                  label={profile}
                  checked={activeDegradationProfiles.includes(profile)}
                  onChange={checked => setDegradationProfile(profile, checked)}
                />
              ))}
            </div>
            <div style={{ color: c.textSecond, fontSize: "13px", marginTop: "6px" }}>
              Active profiles: {activeDegradationProfiles.join(", ")}
            </div>
          </div>
          {sensorCountTooHigh && <InlineWarning colors={c}>sensorCount {runForm.sensorCount} exceeds {activeSensorCount} active sensor(s). Lower the value before submitting.</InlineWarning>}
          {scenarioCWithoutDegradation && <InlineWarning colors={c}>scenario_c is intended for degraded/operational comparison. With degradationProfile=none it may behave like a clean scenario.</InlineWarning>}
          <CheckRow colors={c} label="collectEvidence" checked={runForm.collectEvidence} onChange={value => setRunForm({ ...runForm, collectEvidence: value })} />
          <CheckRow colors={c} label="waitForCompletion" checked={runForm.waitForCompletion} onChange={value => setRunForm({ ...runForm, waitForCompletion: value })} />
          <CheckRow colors={c} label="allowParallelRun" checked={runForm.allowParallelRun} onChange={value => setRunForm({ ...runForm, allowParallelRun: value })} />
          <button style={{ ...button(c), marginTop: "12px", opacity: submittingRun || sensorCountTooHigh ? 0.65 : 1 }} onClick={startRun} disabled={submittingRun || sensorCountTooHigh}>
            <Play size={16} /> {submittingRun ? "Submitting..." : "Start Run"}
          </button>
          {(runMessage || runResult) && <RunRequestResult colors={c} result={runResult} request={runForm} message={runMessage} areaCode={areaCode} />}
        </Panel>

        <Panel colors={c} accent="#dc2626">
          <SectionTitle title="Runtime State Control" subtitle="Dry run first. Real reset requires exact confirmation and blocks active runs." />
          <button style={button(c)} onClick={() => resetRuntime(true)} disabled={loading}><Search size={16} /> Dry run reset</button>
          <div style={{ marginTop: "12px" }}>
            <label style={label(c)}>Confirmation</label>
            <input style={input(c)} value={confirm} onChange={event => setConfirm(event.target.value)} placeholder="RESET_RUNTIME_STATE" />
          </div>
          <button style={{ ...button(c), borderColor: "#dc2626", color: "#dc2626", marginTop: "12px" }} onClick={() => resetRuntime(false)} disabled={loading || confirm !== "RESET_RUNTIME_STATE"}>
            <RotateCcw size={16} /> Reset Runtime State
          </button>
          {resetResult && <ResetCounts result={resetResult} colors={c} />}
        </Panel>
      </section>

      <Panel colors={c} accent="#64748b">
        <SectionTitle title="Known Limitations" subtitle="Displayed as runtime facts, not inferred data." />
        <ul style={{ color: c.textSecond, lineHeight: 1.7, margin: 0, paddingLeft: "20px" }}>
          {summary?.limitations.map(item => <li key={item.code}>{item.message}</li>)}
          {summary?.freshness && <li>{summary.freshness.note}</li>}
          <li>Area Operational State uses persisted projections and may include carry-forward. It is not necessarily limited to the latest run.</li>
        </ul>
      </Panel>
    </main>
  );
}

function ResultTable({ result, colors }: { result: RuntimeDiagnosticResultResponse | null; colors: ReturnType<typeof getColors> }) {
  if (!result) {
    return <div style={{ color: colors.textSecond }}>Choose a diagnostic to load data.</div>;
  }

  return (
    <>
      <div style={{ overflowX: "auto", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px" }}>
          <thead>
            <tr>{result.columns.map(column => <th key={column} style={cell(colors, true)}>{column}</th>)}</tr>
          </thead>
          <tbody>
            {result.rows.length === 0 ? (
              <tr><td style={cell(colors)} colSpan={Math.max(1, result.columns.length)}>No rows</td></tr>
            ) : result.rows.map((row, index) => (
              <tr key={index}>{result.columns.map(column => <td key={column} style={cell(colors)}>{row[column] ?? ""}</td>)}</tr>
            ))}
          </tbody>
        </table>
      </div>
      {result.limitations.length > 0 && <ul style={{ color: colors.textSecond }}>{result.limitations.map(item => <li key={item}>{item}</li>)}</ul>}
      <JsonBlock colors={colors} value={result} />
    </>
  );
}

function ResetCounts({ result, colors }: { result: RuntimeResetResponse; colors: ReturnType<typeof getColors> }) {
  return (
    <div style={{ marginTop: "12px" }}>
      <strong>{result.status}</strong>
      <ResultTable
        colors={colors}
        result={{
          id: "reset-counts",
          title: "Reset counts",
          description: result.message,
          columns: ["schema", "table", "before", "after"],
          limitations: [],
          rows: result.before.map(before => {
            const after = result.after.find(item => item.schema === before.schema && item.table === before.table);
            return {
              schema: before.schema,
              table: before.table,
              before: String(before.count),
              after: String(after?.count ?? before.count),
            };
          }),
        }}
      />
    </div>
  );
}

function Metric({ title, value, detail, colors }: { title: string; value: string | number; detail: string; colors: ReturnType<typeof getColors> }) {
  return (
    <Panel colors={colors}>
      <div style={{ color: colors.textSecond, fontSize: "13px" }}>{title}</div>
      <div style={{ fontSize: "24px", fontWeight: 800, marginTop: "5px" }}>{value}</div>
      <div style={{ color: colors.textSecond, fontSize: "12px" }}>{detail}</div>
    </Panel>
  );
}

function Panel({ colors, accent, children }: { colors: ReturnType<typeof getColors>; accent?: string; children: ReactNode }) {
  return <section style={{ ...panel(colors), borderTop: accent ? `3px solid ${accent}` : `1px solid ${colors.panelBorder}`, marginBottom: "16px" }}>{children}</section>;
}

function SectionTitle({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div style={{ marginBottom: "12px" }}>
      <h2 style={{ margin: 0, fontSize: "18px", fontWeight: 800 }}>{title}</h2>
      {subtitle && <div style={{ color: "#64748b", fontSize: "13px", marginTop: "3px" }}>{subtitle}</div>}
    </div>
  );
}

function FormGrid({ children }: { children: ReactNode }) {
  return <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "10px" }}>{children}</div>;
}

function LabeledInput({ label: text, value, onChange, colors }: { label: string; value: string; onChange: (value: string) => void; colors: ReturnType<typeof getColors> }) {
  return <div><label style={label(colors)}>{text}</label><input style={input(colors)} value={value} onChange={event => onChange(event.target.value)} /></div>;
}

function LabeledSelect({ label: text, value, options, onChange, colors }: { label: string; value: string; options: { value: string; label: string }[]; onChange: (value: string) => void; colors: ReturnType<typeof getColors> }) {
  return (
    <div>
      <label style={label(colors)}>{text}</label>
      <select style={input(colors)} value={value} onChange={event => onChange(event.target.value)}>
        {options.length === 0 && <option value={value}>{value}</option>}
        {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </div>
  );
}

function LabeledNumber({ label: text, value, onChange, colors, max }: { label: string; value: number | null; onChange: (value: number | null) => void; colors: ReturnType<typeof getColors>; max?: number }) {
  return <div><label style={label(colors)}>{text}</label><input style={input(colors)} type="number" max={max} value={value ?? ""} onChange={event => onChange(event.target.value === "" ? null : Number(event.target.value))} /></div>;
}

function CheckRow({ label: text, checked, onChange, colors }: { label: string; checked: boolean; onChange: (value: boolean) => void; colors: ReturnType<typeof getColors> }) {
  return <label style={{ display: "flex", gap: "8px", alignItems: "center", marginTop: "10px", color: colors.textSecond }}><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} /> {text}</label>;
}

function JsonBlock({ value, colors }: { value: unknown; colors: ReturnType<typeof getColors> }) {
  return (
    <details style={{ marginTop: "12px", color: colors.textSecond }}>
      <summary style={{ cursor: "pointer", color: colors.textPrimary, fontWeight: 700 }}>Raw JSON</summary>
      <pre style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "12px", maxHeight: "260px", overflow: "auto" }}>
        {JSON.stringify(value, null, 2)}
      </pre>
    </details>
  );
}

function InlineWarning({ children, colors }: { children: ReactNode; colors: ReturnType<typeof getColors> }) {
  return (
    <div style={{ display: "flex", gap: "8px", alignItems: "flex-start", marginTop: "10px", color: "#b45309", fontSize: "13px" }}>
      <AlertTriangle size={16} /> <span>{children}</span>
    </div>
  );
}

function RunRequestResult({ result, request, message, areaCode, colors }: { result: RuntimeRunStartResponse | null; request: RuntimeRunStartRequest; message: string | null; areaCode: string; colors: ReturnType<typeof getColors> }) {
  const run = result?.run;
  const requested = result?.requested ?? {
    sensorCount: request.sensorCount,
    numberOfCycles: request.numberOfCycles,
    intervalSeconds: request.intervalSeconds,
    seed: request.seed,
    degradationProfile: request.degradationProfile,
    degradationProfiles: request.degradationProfiles,
    orchestratorCorrelationId: result?.orchestratorCorrelationId ?? null,
  };

  const rows = [
    ["request", result ? "accepted/submitted" : "submitting"],
    ["status", result?.status ?? "submitted"],
    ["message", message ?? result?.message ?? ""],
    ["correlationId", result?.orchestratorCorrelationId ?? ""],
    ["runLabel", request.runLabel ?? ""],
    ["areaCode", areaCode],
    ["scenarioCode", request.scenarioCode],
    ["sensorCount", requested.sensorCount ?? ""],
    ["numberOfCycles", requested.numberOfCycles ?? ""],
    ["intervalSeconds", requested.intervalSeconds ?? ""],
    ["seed", requested.seed ?? ""],
    ["degradationProfile", requested.degradationProfile ?? ""],
    ["degradationProfiles", (requested.degradationProfiles ?? request.degradationProfiles ?? []).join(", ")],
    ["collectEvidence", String(request.collectEvidence)],
    ["waitForCompletion", String(request.waitForCompletion)],
    ["simulationRunId", run?.id ?? "waiting_for_persistence"],
    ["startedAt", run?.startedAt ?? ""],
    ["endedAt", run?.endedAt ?? ""],
    ["durationSeconds", run?.durationSeconds ?? ""],
    ["selectedSensors", run?.runOverrides?.selectedSensorNames?.join(", ") ?? ""],
    ["evidenceDirectory", result?.evidenceDirectory ?? result?.logDirectory ?? ""],
  ];

  return (
    <div style={{ marginTop: "14px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "12px", background: colors.sectionBg }}>
      <SectionTitle title="Run request result" subtitle={request.waitForCompletion ? "Waiting for terminal status when requested." : "Run submitted. Follow live processing in Runtime Monitor."} />
      <ResultTable
        colors={colors}
        result={{
          id: "run-request-result",
          title: "Run request result",
          description: "Run submission response",
          columns: ["field", "value"],
          rows: rows.map(([field, value]) => ({ field, value: String(value ?? "") })),
          limitations: result?.warnings ?? [],
        }}
      />
      <a style={{ ...button(colors), marginTop: "10px" }} href={`/dashboards/${areaCode}/pipeline`}>Open Runtime Monitor</a>
      {result && <JsonBlock colors={colors} value={result} />}
    </div>
  );
}

function normalizeProfiles(values: string[] | null | undefined, legacy: string | null | undefined) {
  const profiles = values && values.length > 0 ? values : legacy ? legacy.split(/[,+;|]/) : ["none"];
  const normalized = Array.from(new Set(profiles.map(value => value.trim()).filter(Boolean)));
  return normalized.length === 0 ? ["none"] : normalized.length > 1 ? normalized.filter(value => value !== "none") : normalized;
}

function toLegacyProfile(values: string[]) {
  return values.length === 1 ? values[0] : values.join("+");
}

function formatError(err: unknown) {
  return err instanceof Error ? err.message : "Unexpected runtime control error";
}

function topBar() {
  return { display: "flex", justifyContent: "space-between", gap: "16px", alignItems: "flex-start", marginBottom: "20px", flexWrap: "wrap" as const };
}

function grid(columns: string) {
  return { display: "grid", gridTemplateColumns: columns, gap: "14px", marginBottom: "16px" };
}

function panel(colors: ReturnType<typeof getColors>) {
  return { background: colors.panelBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "16px", boxShadow: "0 1px 8px rgba(15, 23, 42, 0.08)" };
}

function button(colors: ReturnType<typeof getColors>, active = false) {
  return { display: "inline-flex", alignItems: "center", gap: "7px", border: `1px solid ${active ? colors.textPrimary : colors.panelBorder}`, background: active ? colors.segActive : colors.panelBg, color: colors.textPrimary, borderRadius: "8px", padding: "8px 11px", cursor: "pointer", fontWeight: 700, textDecoration: "none" };
}

function input(colors: ReturnType<typeof getColors>) {
  return { width: "100%", border: `1px solid ${colors.panelBorder}`, background: colors.sectionBg, color: colors.textPrimary, borderRadius: "8px", padding: "8px 10px" };
}

function label(colors: ReturnType<typeof getColors>) {
  return { display: "block", color: colors.textSecond, fontSize: "12px", marginBottom: "4px" };
}

function cell(colors: ReturnType<typeof getColors>, header = false) {
  return { borderBottom: `1px solid ${colors.panelBorder}`, padding: "8px", textAlign: "left" as const, background: header ? colors.sectionBg : "transparent", whiteSpace: "nowrap" as const, verticalAlign: "top" as const };
}
