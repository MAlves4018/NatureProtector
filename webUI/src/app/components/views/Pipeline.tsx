import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { useParams } from "react-router-dom";
import {
  AlertTriangle,
  Activity,
  Clock,
  Database,
  Pause,
  Play,
  RefreshCw,
  Server,
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
import { api } from "../../services/api";
import {
  RuntimeAlertSummaryResponse,
  RuntimePipelineSummaryResponse,
  RuntimeProcessingAttemptResponse,
  RuntimeQuarantinedEventResponse,
  RuntimeRejectedEventResponse,
  RuntimeRunOverrideValuesResponse,
  RuntimeRunSummaryResponse,
  RuntimeSummaryResponse,
} from "../../types";
import { getColors } from "../../utils/utils";

const REFRESH_MS = 10000;
const WINDOW_OPTIONS = [10, 30, 1440];

export function Pipeline({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);
  const { areaCode: areaCodeParam } = useParams<{ areaCode: string }>();
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [recentMinutes, setRecentMinutes] = useState(30);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const loadSummary = useCallback(async (showLoading = false) => {
    if (showLoading) {
      setLoading(true);
    } else {
      setRefreshing(true);
    }

    try {
      const result = await api.getRuntimeSummary(areaCodeParam, recentMinutes);
      setSummary(result);
      setError(null);
      setLastUpdated(new Date());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load runtime summary");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [areaCodeParam, recentMinutes]);

  useEffect(() => {
    loadSummary(true);
  }, [loadSummary]);

  useEffect(() => {
    if (!autoRefresh) {
      return;
    }

    const timer = window.setInterval(() => {
      loadSummary(false);
    }, REFRESH_MS);

    return () => window.clearInterval(timer);
  }, [autoRefresh, loadSummary]);

  const displayRun = summary?.currentRun ?? summary?.latestRun ?? null;
  const chain = useMemo(() => buildRuntimeChain(summary), [summary]);

  return (
    <main style={{ minHeight: "100vh", background: c.pageBg, color: c.textPrimary, padding: "24px" }}>
      <section style={{ display: "flex", justifyContent: "space-between", gap: "16px", alignItems: "flex-start", marginBottom: "20px", flexWrap: "wrap" }}>
        <div>
          <h1 style={{ fontSize: "28px", lineHeight: 1.1, margin: 0, fontWeight: 800 }}>Runtime Monitor</h1>
          <div style={{ color: c.textSecond, marginTop: "6px", fontSize: "14px" }}>
            Area {areaCodeParam ?? summary?.areaCode ?? "all"} · recent window {summary?.recentWindowMinutes ?? recentMinutes} min
          </div>
        </div>

        <div style={{ display: "flex", gap: "8px", alignItems: "center", flexWrap: "wrap", justifyContent: "flex-end" }}>
          <span style={{ color: c.textSecond, fontSize: "13px", minWidth: "170px" }}>
            Last update: {lastUpdated ? lastUpdated.toLocaleTimeString() : "pending"}
          </span>
          <SegmentedButtons
            values={WINDOW_OPTIONS}
            selected={recentMinutes}
            onSelect={setRecentMinutes}
            format={value => value === 1440 ? "24h" : `${value}m`}
            colors={c}
          />
          <button style={iconButton(c)} onClick={() => setAutoRefresh(value => !value)} title={autoRefresh ? "Pause auto refresh" : "Resume auto refresh"}>
            {autoRefresh ? <Pause size={16} /> : <Play size={16} />}
            {autoRefresh ? "Auto" : "Paused"}
          </button>
          <button style={iconButton(c)} onClick={() => loadSummary(false)} disabled={refreshing} title="Refresh now">
            <RefreshCw size={16} />
            Refresh
          </button>
        </div>
      </section>

      {loading && (
        <Panel colors={c}>
          <div style={{ display: "flex", alignItems: "center", gap: "10px", color: c.textSecond }}>
            <RefreshCw size={18} /> Loading runtime summary
          </div>
        </Panel>
      )}

      {error && (
        <Panel colors={c} accent="#dc2626">
          <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#dc2626", fontWeight: 700 }}>
            <AlertTriangle size={18} /> {error}
          </div>
        </Panel>
      )}

      {!loading && !error && summary && (
        <div style={{ display: "flex", flexDirection: "column", gap: "18px" }}>
          <section style={gridStyle("repeat(auto-fit, minmax(180px, 1fr))")}>
            <MetricCard title="Run" value={displayRun?.status ?? "No run"} detail={displayRun?.scenarioCode ?? "No simulation run found"} colors={c} tone="#2563eb" icon={<Activity size={18} />} />
            <MetricCard title="Inbox" value={summary.pipeline.inboxTotal} detail={`${summary.pipeline.inboxRecent} recent`} colors={c} tone="#059669" icon={<Database size={18} />} />
            <MetricCard title="Attempts" value={summary.pipeline.attemptsRecent} detail="recent processing attempts" colors={c} tone="#7c3aed" icon={<Server size={18} />} />
            <MetricCard title="Rejected" value={summary.pipeline.rejectedRecent} detail={`${summary.pipeline.rejectedTotal} total`} colors={c} tone="#dc2626" icon={<AlertTriangle size={18} />} />
            <MetricCard title="Quarantined" value={summary.pipeline.quarantinedRecent} detail={`${summary.pipeline.quarantinedTotal} total`} colors={c} tone="#ea580c" icon={<AlertTriangle size={18} />} />
            <MetricCard title="Risk" value={summary.risk.recentCount} detail={formatRiskRange(summary.risk.minScore, summary.risk.maxScore)} colors={c} tone="#0891b2" icon={<Activity size={18} />} />
            <MetricCard title="Alerts" value={summary.activeAlerts.length} detail={summary.areaOperationalState?.alertState ?? "No active alert state"} colors={c} tone="#b45309" icon={<AlertTriangle size={18} />} />
            <MetricCard title="Area State Risk" value={formatScore(summary.areaOperationalState?.aggregateRiskScore)} detail={summary.areaOperationalState?.aggregateRiskLevel ?? "No projection"} colors={c} tone="#be123c" icon={<Clock size={18} />} />
            <MetricCard title="Freshness" value={summary.freshness ? `${summary.freshness.freshCount}/${summary.freshness.staleCount}/${summary.freshness.expiredCount}` : "n/a"} detail="fresh / stale / expired cell states" colors={c} tone="#475569" icon={<Clock size={18} />} />
          </section>

          <Panel colors={c}>
            <SectionHeader title="Current / Latest Run" subtitle="Read from persisted control.simulation_runs metadata" />
            <RunDetails run={displayRun} colors={c} />
          </Panel>

          <Panel colors={c}>
            <SectionHeader title="Runtime Chain" subtitle="Counts come from persisted projections and pipeline tables" />
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "10px" }}>
              {chain.map((item, index) => (
                <div key={item.label} style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                  <div style={{ ...chainBlock(c, item.tone), flex: 1 }}>
                    <div style={{ fontSize: "12px", color: c.textSecond }}>{item.label}</div>
                    <div style={{ fontSize: "22px", fontWeight: 800, marginTop: "4px" }}>{item.value}</div>
                    <div style={{ fontSize: "12px", color: item.warning ? "#dc2626" : c.textSecond, marginTop: "2px" }}>{item.status}</div>
                  </div>
                  {index < chain.length - 1 && <div style={{ color: c.textMuted, fontWeight: 800 }}>→</div>}
                </div>
              ))}
            </div>
          </Panel>

          <section style={gridStyle("repeat(auto-fit, minmax(280px, 1fr))")}>
            <ChartPanel title="Inbox by Status" colors={c}>
              <BarGraph data={summary.pipeline.inboxByStatus.map(item => ({ name: item.status, value: item.count }))} color="#059669" />
            </ChartPanel>
            <ChartPanel title="Attempts by Outcome" colors={c}>
              <BarGraph data={summary.pipeline.attemptsByOutcomeAndError.map(item => ({ name: item.errorCode ? `${item.outcome}/${item.errorCode}` : item.outcome, value: item.count }))} color="#7c3aed" />
            </ChartPanel>
            <ChartPanel title="Rejected by Code" colors={c}>
              <BarGraph data={summary.pipeline.rejectedByCode.map(item => ({ name: item.code, value: item.count }))} color="#dc2626" />
            </ChartPanel>
            <ChartPanel title="Quarantined by Code" colors={c}>
              <BarGraph data={summary.pipeline.quarantinedByCode.map(item => ({ name: item.code, value: item.count }))} color="#ea580c" />
            </ChartPanel>
          </section>

          <Panel colors={c}>
            <SectionHeader title="Risk Scores" subtitle="Recent persisted risk_assessment_log values, no frontend scoring" />
            {summary.risk.recentScores.length === 0 ? (
              <EmptyState text="No recent risk assessments in this window." colors={c} />
            ) : (
              <div style={{ height: "240px" }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={summary.risk.recentScores.map(point => ({ time: shortTime(point.timestamp), score: point.riskScore, level: point.riskLevel }))}>
                    <CartesianGrid stroke={c.panelBorder} />
                    <XAxis dataKey="time" stroke={c.textSecond} tick={{ fontSize: 12 }} />
                    <YAxis stroke={c.textSecond} domain={[0, 1]} tick={{ fontSize: 12 }} />
                    <Tooltip contentStyle={{ background: c.panelBg, border: `1px solid ${c.panelBorder}`, color: c.textPrimary }} />
                    <Line type="monotone" dataKey="score" stroke="#0891b2" strokeWidth={2} dot={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}
          </Panel>

          <section style={gridStyle("repeat(auto-fit, minmax(320px, 1fr))")}>
            <Panel colors={c}>
              <SectionHeader title="Active Alerts" subtitle="Read from projection.alert_state" />
              <AlertList alerts={summary.activeAlerts} colors={c} />
            </Panel>
            <Panel colors={c}>
              <SectionHeader title="Latest Rejected" subtitle="Recent rejected_events" />
              <RejectedList items={summary.pipeline.latestRejected} colors={c} />
            </Panel>
            <Panel colors={c}>
              <SectionHeader title="Latest Quarantined" subtitle="Recent quarantined_events" />
              <QuarantineList items={summary.pipeline.latestQuarantined} colors={c} />
            </Panel>
            <Panel colors={c}>
              <SectionHeader title="Failed Attempts" subtitle="Recent failed/retry/quarantined attempts" />
              <AttemptList items={summary.pipeline.latestFailedAttempts} colors={c} />
            </Panel>
          </section>

          <Panel colors={c} accent="#b45309">
            <SectionHeader title="Observability Limitations" subtitle="Known gaps are displayed instead of inferred" />
            <ul style={{ margin: 0, paddingLeft: "18px", color: c.textSecond, lineHeight: 1.7 }}>
              <li>Area Operational State uses persisted projections and may include carry-forward. It is not necessarily limited to the latest run.</li>
              {summary.freshness && <li>{summary.freshness.note}</li>}
              {summary.limitations.map(item => <li key={item.code}>{item.message}</li>)}
              {summary.warnings.map(item => <li key={item} style={{ color: "#b45309" }}>{item}</li>)}
            </ul>
          </Panel>
        </div>
      )}
    </main>
  );
}

function RunDetails({ run, colors }: { run: RuntimeRunSummaryResponse | null; colors: ReturnType<typeof getColors> }) {
  if (!run) {
    return <EmptyState text="No simulation run is persisted yet." colors={colors} />;
  }

  const requested = run.runOverrides?.requested;
  const resolved = run.runOverrides?.resolved;

  return (
    <div style={gridStyle("repeat(auto-fit, minmax(190px, 1fr))")}>
      <KeyValue label="SimulationRunId" value={run.id} colors={colors} />
      <KeyValue label="ScenarioCode" value={run.scenarioCode} colors={colors} />
      <KeyValue label="AreaCode" value={run.areaCode} colors={colors} />
      <KeyValue label="Status" value={run.status} colors={colors} />
      <KeyValue label="StartedAt" value={formatDate(run.startedAt)} colors={colors} />
      <KeyValue label="EndedAt" value={formatDate(run.endedAt)} colors={colors} />
      <KeyValue label="Duration" value={run.durationSeconds == null ? "running/unknown" : `${Math.round(run.durationSeconds)}s`} colors={colors} />
      <KeyValue label="Cycles" value={`${run.numberOfCycles}`} colors={colors} />
      <KeyValue label="Interval" value={`${run.intervalSeconds}s`} colors={colors} />
      <KeyValue label="Seed" value={run.executionSeed ?? "not persisted"} colors={colors} />
      <KeyValue label="Correlation" value={run.orchestratorCorrelationId ?? "not in metadata"} colors={colors} />
      <KeyValue label="Metadata" value={run.metadataJsonStatus} colors={colors} />
      <KeyValue label="Requested overrides" value={formatOverrides(requested)} colors={colors} />
      <KeyValue label="Resolved overrides" value={formatOverrides(resolved)} colors={colors} />
      <KeyValue label="Selected sensors" value={run.runOverrides?.selectedSensorNames.join(", ") || "not in metadata"} colors={colors} />
      <details style={{ gridColumn: "1 / -1", color: colors.textSecond }}>
        <summary style={{ cursor: "pointer", color: colors.textPrimary, fontWeight: 700 }}>MetadataJson raw</summary>
        <pre style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "12px", maxHeight: "220px", overflow: "auto" }}>
          {run.metadataJson ?? "null"}
        </pre>
      </details>
    </div>
  );
}

function MetricCard({ title, value, detail, colors, tone, icon }: { title: string; value: string | number; detail: string; colors: ReturnType<typeof getColors>; tone: string; icon: ReactNode }) {
  return (
    <div style={{ ...panelStyle(colors), borderLeft: `4px solid ${tone}` }}>
      <div style={{ display: "flex", justifyContent: "space-between", color: colors.textSecond, fontSize: "13px" }}>
        <span>{title}</span>
        <span style={{ color: tone }}>{icon}</span>
      </div>
      <div style={{ fontSize: "26px", fontWeight: 800, marginTop: "8px", lineHeight: 1.1 }}>{value}</div>
      <div style={{ color: colors.textSecond, fontSize: "12px", marginTop: "6px", minHeight: "18px" }}>{detail}</div>
    </div>
  );
}

function ChartPanel({ title, colors, children }: { title: string; colors: ReturnType<typeof getColors>; children: ReactNode }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title={title} />
      <div style={{ height: "220px" }}>{children}</div>
    </Panel>
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

function AlertList({ alerts, colors }: { alerts: RuntimeAlertSummaryResponse[]; colors: ReturnType<typeof getColors> }) {
  if (alerts.length === 0) {
    return <EmptyState text="No active alerts." colors={colors} />;
  }

  return (
    <ListBox>
      {alerts.map(alert => (
        <Row key={alert.id} colors={colors}>
          <strong>{alert.alertCode}</strong>
          <span>{alert.alertState ?? alert.status} · {alert.severity}</span>
          <small>{formatDate(alert.updatedAt)}</small>
        </Row>
      ))}
    </ListBox>
  );
}

function RejectedList({ items, colors }: { items: RuntimeRejectedEventResponse[]; colors: ReturnType<typeof getColors> }) {
  if (items.length === 0) {
    return <EmptyState text="No recent rejected events." colors={colors} />;
  }

  return (
    <ListBox>
      {items.map(item => (
        <Row key={item.id} colors={colors}>
          <strong>{item.rejectionCode}</strong>
          <span>{item.rejectionReason}</span>
          <small>{formatDate(item.rejectedAt)}</small>
        </Row>
      ))}
    </ListBox>
  );
}

function QuarantineList({ items, colors }: { items: RuntimeQuarantinedEventResponse[]; colors: ReturnType<typeof getColors> }) {
  if (items.length === 0) {
    return <EmptyState text="No recent quarantined events." colors={colors} />;
  }

  return (
    <ListBox>
      {items.map(item => (
        <Row key={item.id} colors={colors}>
          <strong>{item.quarantineCode}</strong>
          <span>{item.quarantineReason}</span>
          <small>{formatDate(item.quarantinedAt)} · attempt {item.finalAttemptNumber}</small>
        </Row>
      ))}
    </ListBox>
  );
}

function AttemptList({ items, colors }: { items: RuntimeProcessingAttemptResponse[]; colors: ReturnType<typeof getColors> }) {
  if (items.length === 0) {
    return <EmptyState text="No recent failed attempts." colors={colors} />;
  }

  return (
    <ListBox>
      {items.map(item => (
        <Row key={item.id} colors={colors}>
          <strong>{item.outcome} · {item.errorCode ?? "no error code"}</strong>
          <span>{item.stage}</span>
          <small>{formatDate(item.startedAt)}</small>
        </Row>
      ))}
    </ListBox>
  );
}

function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div style={{ marginBottom: "12px" }}>
      <h2 style={{ margin: 0, fontSize: "18px", fontWeight: 800 }}>{title}</h2>
      {subtitle && <div style={{ color: "#64748b", fontSize: "13px", marginTop: "3px" }}>{subtitle}</div>}
    </div>
  );
}

function Panel({ colors, accent, children }: { colors: ReturnType<typeof getColors>; accent?: string; children: ReactNode }) {
  return <section style={{ ...panelStyle(colors), borderTop: accent ? `3px solid ${accent}` : `1px solid ${colors.panelBorder}` }}>{children}</section>;
}

function KeyValue({ label, value, colors }: { label: string; value: ReactNode; colors: ReturnType<typeof getColors> }) {
  return (
    <div style={{ background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "10px", minWidth: 0 }}>
      <div style={{ color: colors.textSecond, fontSize: "12px", marginBottom: "4px" }}>{label}</div>
      <div style={{ fontWeight: 700, fontSize: "14px", overflowWrap: "anywhere" }}>{value}</div>
    </div>
  );
}

function EmptyState({ text, colors }: { text: string; colors: ReturnType<typeof getColors> }) {
  return <div style={{ color: colors.textSecond, fontSize: "14px", padding: "12px 0" }}>{text}</div>;
}

function ListBox({ children }: { children: ReactNode }) {
  return <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>{children}</div>;
}

function Row({ colors, children }: { colors: ReturnType<typeof getColors>; children: ReactNode }) {
  return (
    <div style={{ display: "grid", gap: "3px", border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "10px", background: colors.sectionBg, minWidth: 0 }}>
      {children}
    </div>
  );
}

function SegmentedButtons({ values, selected, onSelect, format, colors }: { values: number[]; selected: number; onSelect: (value: number) => void; format: (value: number) => string; colors: ReturnType<typeof getColors> }) {
  return (
    <div style={{ display: "flex", padding: "3px", border: `1px solid ${colors.panelBorder}`, background: colors.segBg, borderRadius: "8px" }}>
      {values.map(value => (
        <button
          key={value}
          onClick={() => onSelect(value)}
          style={{
            border: "none",
            background: value === selected ? colors.segActive : "transparent",
            color: value === selected ? colors.textPrimary : colors.textSecond,
            borderRadius: "6px",
            padding: "7px 10px",
            cursor: "pointer",
            fontWeight: 700,
          }}
        >
          {format(value)}
        </button>
      ))}
    </div>
  );
}

function buildRuntimeChain(summary: RuntimeSummaryResponse | null) {
  const pipeline: RuntimePipelineSummaryResponse | null = summary?.pipeline ?? null;

  return [
    { label: "Run", value: summary?.currentRun ? "active" : summary?.latestRun ? "latest" : "none", status: summary?.latestRun?.status ?? "not observed", warning: !summary?.latestRun, tone: "#2563eb" },
    { label: "Inbox", value: pipeline?.inboxTotal ?? 0, status: `${pipeline?.inboxRecent ?? 0} recent`, warning: false, tone: "#059669" },
    { label: "Attempts", value: pipeline?.attemptsRecent ?? 0, status: "recent", warning: false, tone: "#7c3aed" },
    { label: "Risk", value: summary?.risk.recentCount ?? 0, status: "assessments", warning: false, tone: "#0891b2" },
    { label: "Cell states", value: summary?.cellOperationalStateCount ?? 0, status: summary?.areaOperationalState ? "projection rows updated" : "no area state", warning: !summary?.areaOperationalState, tone: "#be123c" },
    { label: "Alerts", value: summary?.activeAlerts.length ?? 0, status: summary?.areaOperationalState?.alertState ?? "none", warning: false, tone: "#b45309" },
    { label: "API", value: summary ? "ok" : "n/a", status: summary ? "summary loaded" : "not loaded", warning: !summary, tone: "#475569" },
  ];
}

function panelStyle(colors: ReturnType<typeof getColors>) {
  return {
    background: colors.panelBg,
    border: `1px solid ${colors.panelBorder}`,
    borderRadius: "8px",
    padding: "16px",
    boxShadow: "0 1px 8px rgba(15, 23, 42, 0.08)",
  };
}

function chainBlock(colors: ReturnType<typeof getColors>, tone: string) {
  return {
    background: colors.sectionBg,
    border: `1px solid ${colors.panelBorder}`,
    borderLeft: `4px solid ${tone}`,
    borderRadius: "8px",
    padding: "12px",
    minHeight: "88px",
  };
}

function iconButton(colors: ReturnType<typeof getColors>) {
  return {
    display: "inline-flex",
    alignItems: "center",
    gap: "7px",
    border: `1px solid ${colors.panelBorder}`,
    background: colors.panelBg,
    color: colors.textPrimary,
    borderRadius: "8px",
    padding: "8px 11px",
    cursor: "pointer",
    fontWeight: 700,
  };
}

function gridStyle(columns: string) {
  return {
    display: "grid",
    gridTemplateColumns: columns,
    gap: "14px",
  };
}

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString() : "not persisted";
}

function shortTime(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function formatScore(value: number | null | undefined) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatRiskRange(min: number | null, max: number | null) {
  if (min == null || max == null) {
    return "no recent scores";
  }

  return `min ${min.toFixed(2)} · max ${max.toFixed(2)}`;
}

function formatOverrides(values: RuntimeRunOverrideValuesResponse | null | undefined) {
  if (!values) {
    return "not in metadata";
  }

  const parts = [
    values.sensorCount == null ? null : `sensors ${values.sensorCount}`,
    values.numberOfCycles == null ? null : `cycles ${values.numberOfCycles}`,
    values.intervalSeconds == null ? null : `interval ${values.intervalSeconds}s`,
    values.seed == null ? null : `seed ${values.seed}`,
    values.degradationProfiles && values.degradationProfiles.length > 0
      ? values.degradationProfiles.join("+")
      : values.degradationProfile == null ? null : values.degradationProfile,
  ].filter(Boolean);

  return parts.length === 0 ? "not in metadata" : parts.join(" · ");
}
