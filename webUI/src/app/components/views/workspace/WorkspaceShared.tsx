import type { Dispatch, ReactNode, SetStateAction } from 'react';
import { Clipboard } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type {
  RuntimeAlertSummaryResponse,
  RuntimeDiagnosticDefinitionResponse,
  RuntimeDiagnosticResultResponse,
  RuntimeProcessingAttemptResponse,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeSummaryResponse,
} from '../../../types';
import { getColors } from '../../../utils/utils';
import { DEGRADATION_PROFILE_DETAILS, P3_CANONICAL, P3_CASES, P3_EVIDENCE_REFERENCES } from './workspaceConstants';

export type Colors = ReturnType<typeof getColors>;

export function RuntimeChainStrip({
  colors,
  summary,
  large = false,
}: {
  colors: Colors;
  summary: RuntimeSummaryResponse | null;
  large?: boolean;
}) {
  const chain = [
    [
      'Scenario Run',
      summary?.currentRun ? 'Active' : summary?.latestRun ? 'Latest' : 'No data',
      summary?.latestRun?.status ?? 'Not observed',
      '#2563eb',
    ],
    [
      'Event Inbox',
      summary?.pipeline.inboxTotal ?? 'No data',
      `${summary?.pipeline.inboxRecent ?? 0} recent`,
      '#059669',
    ],
    ['Processing Attempts', summary?.pipeline.attemptsRecent ?? 'No data', 'recent attempts', '#7c3aed'],
    [
      'Risk',
      summary?.risk.recentCount ?? 'No data',
      formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore),
      '#0891b2',
    ],
    [
      'State',
      summary?.cellOperationalStateCount ?? 'No data',
      summary?.areaOperationalState ? 'projection updated' : 'No area state',
      '#be123c',
    ],
    [
      'Alerts',
      summary?.activeAlerts.length ?? 'No data',
      summary?.areaOperationalState?.alertState ?? 'None',
      '#b45309',
    ],
    ['API/UI', summary ? 'Loaded' : 'No data', 'summary endpoint', '#475569'],
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(135px, 1fr))', gap: '10px' }}>
      {chain.map(([label, value, status, tone], index) => (
        <div key={label} style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <div
            style={{ ...panel(colors), borderLeft: `4px solid ${tone}`, flex: 1, minHeight: large ? '112px' : '86px' }}
          >
            <div style={{ color: colors.textSecond, fontSize: '12px' }}>{label}</div>
            <div
              style={{
                color: colors.textPrimary,
                fontWeight: 800,
                fontSize: large ? '24px' : '19px',
                marginTop: '5px',
              }}
            >
              {value}
            </div>
            <div style={{ color: colors.textSecond, fontSize: '12px', marginTop: '4px' }}>{status}</div>
          </div>
          {index < chain.length - 1 && <div style={{ color: colors.textMuted, fontWeight: 800 }}>-</div>}
        </div>
      ))}
    </div>
  );
}

export function RiskLineChart({ colors, summary }: { colors: Colors; summary: RuntimeSummaryResponse | null }) {
  const data =
    summary?.risk.recentScores.map((point) => ({
      time: shortTime(point.timestamp),
      score: point.riskScore,
      level: point.riskLevel,
    })) ?? [];
  if (data.length === 0) {
    return <EmptyState colors={colors} text="No recent risk assessments in this window." />;
  }
  return (
    <div style={{ height: '260px' }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid stroke={colors.panelBorder} />
          <XAxis dataKey="time" stroke={colors.textSecond} tick={{ fontSize: 12 }} />
          <YAxis stroke={colors.textSecond} domain={[0, 1]} tick={{ fontSize: 12 }} />
          <Tooltip
            contentStyle={{
              background: colors.panelBg,
              border: `1px solid ${colors.panelBorder}`,
              color: colors.textPrimary,
            }}
          />
          <Line type="monotone" dataKey="score" stroke="#0891b2" strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export function BarGraph({ data, color }: { data: { name: string; value: number }[]; color: string }) {
  if (data.length === 0) {
    return <div style={{ height: '100%', display: 'grid', placeItems: 'center', color: '#64748b' }}>No data</div>;
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

export function RunDetails({ colors, run }: { colors: Colors; run: RuntimeRunSummaryResponse | null }) {
  if (!run) {
    return <EmptyState colors={colors} text="No simulation run is persisted yet." />;
  }
  return (
    <ViewStack>
      <KeyValues
        colors={colors}
        rows={[
          ['SimulationRunId', run.id],
          ['ScenarioCode', run.scenarioCode],
          ['ScenarioName', run.scenarioName],
          ['Status', run.status],
          ['Started', formatDate(run.startedAt)],
          ['Ended', formatDate(run.endedAt)],
          ['Duration', run.durationSeconds == null ? 'Not available' : `${Math.round(run.durationSeconds)}s`],
          ['Cycles', run.numberOfCycles],
          ['Interval', `${run.intervalSeconds}s`],
          ['Seed', run.executionSeed ?? 'Not persisted'],
          ['CorrelationId', run.orchestratorCorrelationId ?? 'Not available'],
          ['Requested overrides', formatOverrides(run.runOverrides?.requested ?? null)],
          ['Resolved overrides', formatOverrides(run.runOverrides?.resolved ?? null)],
          ['Selected sensors', run.runOverrides?.selectedSensorNames.join(', ') || 'Not available'],
        ]}
      />
      <CollapsibleJson
        colors={colors}
        title="Raw metadata JSON"
        value={run.metadataJson ? parseJson(run.metadataJson) : 'Not available'}
      />
    </ViewStack>
  );
}

export function RunRequestResult({
  colors,
  result,
  request,
  message,
  areaCode,
}: {
  colors: Colors;
  result: RuntimeRunStartResponse | null;
  request: RuntimeRunStartRequest;
  message: string | null;
  areaCode: string;
}) {
  const run = result?.run;
  const requested = result?.requested;
  return (
    <div style={{ ...panel(colors), marginTop: '14px' }}>
      <SectionHeader title="Run request result" subtitle={message ?? 'Run request submitted.'} />
      <KeyValues
        colors={colors}
        rows={[
          ['status', result?.status ?? 'Submitted'],
          ['message', message ?? result?.message ?? 'Not available'],
          ['correlationId', result?.orchestratorCorrelationId ?? 'Not available'],
          ['runLabel', request.runLabel ?? 'Not available'],
          ['areaCode', areaCode],
          ['scenarioCode', request.scenarioCode],
          ['sensorCount', requested?.sensorCount ?? request.sensorCount ?? 'Not available'],
          ['numberOfCycles', requested?.numberOfCycles ?? request.numberOfCycles ?? 'Not available'],
          ['intervalSeconds', requested?.intervalSeconds ?? request.intervalSeconds ?? 'Not available'],
          ['seed', requested?.seed ?? request.seed ?? 'Not available'],
          ['degradationProfile', requested?.degradationProfile ?? request.degradationProfile ?? 'Not available'],
          [
            'degradationProfiles',
            (requested?.degradationProfiles ?? request.degradationProfiles ?? []).join(', ') || 'Not available',
          ],
          ['simulationRunId', run?.id ?? 'waiting_for_persistence'],
          ['selectedSensors', run?.runOverrides?.selectedSensorNames.join(', ') || 'Not available'],
          ['evidenceDirectory', result?.evidenceDirectory ?? result?.logDirectory ?? 'Not available'],
        ]}
      />
      <CollapsibleJson colors={colors} title="Raw run response JSON" value={result ?? 'Not available'} />
    </div>
  );
}

export function ResetCounts({ result, colors }: { result: RuntimeResetResponse; colors: Colors }) {
  const rows = result.before.map((before) => {
    const after = result.after.find((item) => item.schema === before.schema && item.table === before.table);
    return [before.schema, before.table, String(before.count), String(after?.count ?? before.count)];
  });
  return (
    <div style={{ marginTop: '14px' }}>
      <SectionHeader title={`Reset result: ${result.status}`} subtitle={result.message} />
      <SimpleTable colors={colors} columns={['Schema', 'Table', 'Before', 'After']} rows={rows} />
      <CollapsibleJson colors={colors} title="Raw reset JSON" value={result} />
    </div>
  );
}

export function DiagnosticResult({
  colors,
  result,
}: {
  colors: Colors;
  result: RuntimeDiagnosticResultResponse | null;
}) {
  if (!result) {
    return <EmptyState colors={colors} text="Choose a diagnostic to load data." />;
  }
  return (
    <>
      <SimpleTable
        colors={colors}
        columns={result.columns}
        rows={result.rows.map((row) => result.columns.map((column) => row[column] ?? ''))}
      />
      {result.limitations.length > 0 && (
        <ul style={{ color: colors.textSecond }}>
          {result.limitations.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
      <CollapsibleJson colors={colors} title="Raw diagnostic JSON" value={result} />
    </>
  );
}

export function StatusCounts({ colors, rows }: { colors: Colors; rows: { status: string; count: number }[] }) {
  if (rows.length === 0) {
    return <EmptyState colors={colors} text="No data." />;
  }
  return <KeyValues colors={colors} rows={rows.map((item) => [item.status, item.count])} />;
}

export function AlertList({
  colors,
  alerts,
  detailed = false,
}: {
  colors: Colors;
  alerts: RuntimeAlertSummaryResponse[];
  detailed?: boolean;
}) {
  if (alerts.length === 0) {
    return <EmptyState colors={colors} text="No active alerts." />;
  }
  return (
    <div style={{ display: 'grid', gap: '8px' }}>
      {alerts.map((alert) => (
        <div
          key={alert.id}
          style={{
            ...panel(colors),
            borderLeft: `4px solid ${alert.severity?.toLowerCase() === 'critical' ? '#dc2626' : '#b45309'}`,
          }}
        >
          <strong>{alert.alertCode}</strong>
          <div style={paragraph(colors)}>
            {alert.alertState ?? alert.status} - {alert.severity}
          </div>
          {detailed && <div style={paragraph(colors)}>{alert.message}</div>}
          <small style={{ color: colors.textSecond }}>
            triggered {formatDate(alert.triggeredAt)}; resolved {formatDate(alert.resolvedAt)}; updated{' '}
            {formatDate(alert.updatedAt)}
          </small>
        </div>
      ))}
    </div>
  );
}

export function KeyValues({ colors, rows }: { colors: Colors; rows: [string, ReactNode][] }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '8px' }}>
      {rows.map(([label, value]) => (
        <div
          key={label}
          style={{
            background: colors.sectionBg,
            border: `1px solid ${colors.panelBorder}`,
            borderRadius: '8px',
            padding: '10px',
            minWidth: 0,
          }}
        >
          <div style={{ color: colors.textMuted, fontSize: '12px', marginBottom: '4px' }}>{label}</div>
          <div style={{ color: colors.textPrimary, fontWeight: 700, fontSize: '13px', overflowWrap: 'anywhere' }}>
            {formatNode(value)}
          </div>
        </div>
      ))}
    </div>
  );
}

export function SimpleTable({ colors, columns, rows }: { colors: Colors; columns: string[]; rows: ReactNode[][] }) {
  return (
    <div style={{ overflowX: 'auto', border: `1px solid ${colors.panelBorder}`, borderRadius: '8px' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column} style={cell(colors, true)}>
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td style={cell(colors)} colSpan={Math.max(1, columns.length)}>
                No data
              </td>
            </tr>
          ) : (
            rows.map((row, index) => (
              <tr key={row.map((value) => String(value)).join('|')}>
                {columns.map((column, colIndex) => (
                  <td key={column} style={cell(colors)}>
                    {formatNode(row[colIndex])}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

export function FlowNodes({ colors, nodes }: { colors: Colors; nodes: string[][] }) {
  return (
    <div style={{ display: 'grid', gap: '8px' }}>
      {nodes.map(([name, description, persistence, runtime], index) => (
        <div key={name} style={{ display: 'grid', gridTemplateColumns: '36px 1fr', alignItems: 'center', gap: '8px' }}>
          <div style={{ color: colors.textMuted, fontWeight: 800 }}>{index === 0 ? '' : 'v'}</div>
          <div
            style={{
              ...panel(colors),
              display: 'flex',
              justifyContent: 'space-between',
              gap: '12px',
              flexWrap: 'wrap',
            }}
          >
            <div>
              <strong>{name}</strong>
              <div style={paragraph(colors)}>{description}</div>
            </div>
            <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
              <Badge colors={colors}>{persistence}</Badge>
              <Badge colors={colors}>{runtime}</Badge>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

export function Timeline({ colors, steps }: { colors: Colors; steps: string[] }) {
  return (
    <div style={{ display: 'grid', gap: '8px' }}>
      {steps.map((step, index) => (
        <div key={step} style={{ ...panel(colors), display: 'flex', gap: '10px', alignItems: 'center' }}>
          <Badge colors={colors}>{String(index + 1).padStart(2, '0')}</Badge>
          <strong>{step}</strong>
        </div>
      ))}
    </div>
  );
}

export function InfoCard({
  colors,
  title,
  status,
  detail,
}: {
  colors: Colors;
  title: string;
  status: ReactNode;
  detail: ReactNode;
}) {
  return (
    <Panel colors={colors}>
      <div style={{ color: colors.textSecond, fontSize: '12px', marginBottom: '5px' }}>{title}</div>
      <div style={{ color: colors.textPrimary, fontWeight: 800, fontSize: '18px', overflowWrap: 'anywhere' }}>
        {formatNode(status)}
      </div>
      <div style={{ color: colors.textSecond, fontSize: '13px', marginTop: '6px', lineHeight: 1.4 }}>
        {formatNode(detail)}
      </div>
    </Panel>
  );
}

export function ChartPanel({ colors, title, children }: { colors: Colors; title: string; children: ReactNode }) {
  return (
    <Panel colors={colors}>
      <SectionHeader title={title} />
      <div style={{ height: '230px' }}>{children}</div>
    </Panel>
  );
}

export function EventRows({ colors, rows, empty }: { colors: Colors; rows: string[][]; empty: string }) {
  if (rows.length === 0) {
    return <EmptyState colors={colors} text={empty} />;
  }
  return (
    <div style={{ display: 'grid', gap: '8px' }}>
      {rows.map(([title, detail, date], index) => (
        <div key={`${title}|${date}|${detail}`} style={{ ...panel(colors) }}>
          <strong>{title}</strong>
          <div style={paragraph(colors)}>{detail}</div>
          <small style={{ color: colors.textSecond }}>{date}</small>
        </div>
      ))}
    </div>
  );
}

export function NarrativeSummary({ colors, rows }: { colors: Colors; rows: ReactNode[][] }) {
  const metric = (name: string) => rows.find((row) => row[0] === name);
  const accepted = metric('observed accepted readings');
  const missing = metric('missing events');
  const lines: string[] = [];
  if (accepted) {
    const b = Number(accepted[1]);
    const c = Number(accepted[2]);
    if (Number.isFinite(b) && Number.isFinite(c) && c < b) {
      lines.push(
        'Scenario C produced fewer accepted readings than Scenario B, consistent with missing-readings degradation.',
      );
    }
  }
  if (missing) {
    const b = Number(missing[1]);
    const c = Number(missing[2]);
    if (Number.isFinite(b) && Number.isFinite(c) && c > b) {
      lines.push('Scenario C shows more missing events than Scenario B in the persisted comparison.');
    }
  }
  return (
    <Banner colors={colors} tone="#2563eb">
      {lines.length ? lines.join(' ') : 'No supported B/C narrative is available from the current comparison data.'}
    </Banner>
  );
}

export function Metric({
  colors,
  title,
  value,
  detail,
  icon,
  tone,
}: {
  colors: Colors;
  title: string;
  value: ReactNode;
  detail: ReactNode;
  icon: ReactNode;
  tone: string;
}) {
  return (
    <div style={{ ...panel(colors), borderLeft: `4px solid ${tone}` }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          gap: '8px',
          color: colors.textSecond,
          fontSize: '13px',
        }}
      >
        <span>{title}</span>
        <span style={{ color: tone }}>{icon}</span>
      </div>
      <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '8px', lineHeight: 1.1, overflowWrap: 'anywhere' }}>
        {formatNode(value)}
      </div>
      <div style={{ color: colors.textSecond, fontSize: '12px', marginTop: '6px' }}>{formatNode(detail)}</div>
    </div>
  );
}

export function Tabs<T extends string>({
  values,
  selected,
  onSelect,
  colors,
  compact = false,
}: {
  values: readonly T[];
  selected: T;
  onSelect: Dispatch<SetStateAction<T>>;
  colors: Colors;
  compact?: boolean;
}) {
  return (
    <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', marginBottom: compact ? '14px' : '12px' }}>
      {values.map((value) => (
        <button type="button" key={value} onClick={() => onSelect(value)} style={button(colors, value === selected)}>
          {value}
        </button>
      ))}
    </div>
  );
}

export function SegmentedButtons({
  values,
  selected,
  onSelect,
  format,
  colors,
}: {
  values: number[];
  selected: number;
  onSelect: (value: number) => void;
  format: (value: number) => string;
  colors: Colors;
}) {
  return (
    <div
      style={{
        display: 'flex',
        padding: '3px',
        border: `1px solid ${colors.panelBorder}`,
        background: colors.segBg,
        borderRadius: '8px',
      }}
    >
      {values.map((value) => (
        <button
          type="button"
          key={value}
          onClick={() => onSelect(value)}
          style={{
            border: 'none',
            background: value === selected ? colors.segActive : 'transparent',
            color: value === selected ? colors.textPrimary : colors.textSecond,
            borderRadius: '6px',
            padding: '7px 10px',
            cursor: 'pointer',
            fontWeight: 700,
          }}
        >
          {format(value)}
        </button>
      ))}
    </div>
  );
}

export function LabeledInput({
  colors,
  label,
  value,
  onChange,
}: {
  colors: Colors;
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label style={{ display: 'grid', gap: '4px' }}>
      <span style={labelStyle(colors)}>{label}</span>
      <input
        aria-label={label}
        style={input(colors)}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

export function LabeledNumber({
  colors,
  label,
  value,
  onChange,
  max,
}: {
  colors: Colors;
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
  max?: number;
}) {
  return (
    <label style={{ display: 'grid', gap: '4px' }}>
      <span style={labelStyle(colors)}>{label}</span>
      <input
        aria-label={label}
        style={input(colors)}
        type="number"
        max={max}
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? null : Number(event.target.value))}
      />
    </label>
  );
}

export function LabeledSelect({
  colors,
  label,
  value,
  options,
  onChange,
}: {
  colors: Colors;
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChange: (value: string) => void;
}) {
  return (
    <label style={{ display: 'grid', gap: '4px' }}>
      <span style={labelStyle(colors)}>{label}</span>
      <select aria-label={label} style={input(colors)} value={value} onChange={(event) => onChange(event.target.value)}>
        {options.length === 0 && <option value={value}>{value}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export function CheckRow({
  colors,
  label,
  checked,
  onChange,
}: {
  colors: Colors;
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label
      style={{
        display: 'inline-flex',
        gap: '8px',
        alignItems: 'center',
        marginTop: '10px',
        marginRight: '16px',
        color: colors.textSecond,
      }}
    >
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} /> {label}
    </label>
  );
}

export function CollapsibleJson({ colors, title, value }: { colors: Colors; title: string; value: unknown }) {
  return (
    <details style={{ marginTop: '12px', color: colors.textSecond }}>
      <summary style={{ cursor: 'pointer', color: colors.textPrimary, fontWeight: 800 }}>{title}</summary>
      <pre
        style={{
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
          background: colors.sectionBg,
          border: `1px solid ${colors.panelBorder}`,
          borderRadius: '8px',
          padding: '12px',
          maxHeight: '260px',
          overflow: 'auto',
        }}
      >
        {typeof value === 'string' ? value : JSON.stringify(value, null, 2)}
      </pre>
    </details>
  );
}

export function WorkspacePanel({ colors, children }: { colors: Colors; children: ReactNode }) {
  return <section style={{ ...panel(colors), minHeight: '620px' }}>{children}</section>;
}

export function Panel({ colors, accent, children }: { colors: Colors; accent?: string; children: ReactNode }) {
  return (
    <section
      style={{ ...panel(colors), borderTop: accent ? `3px solid ${accent}` : `1px solid ${colors.panelBorder}` }}
    >
      {children}
    </section>
  );
}

export function Banner({ colors, tone, children }: { colors: Colors; tone: string; children: ReactNode }) {
  return (
    <div
      style={{
        ...panel(colors),
        borderLeft: `4px solid ${tone}`,
        color: colors.textSecond,
        margin: '10px 0',
        lineHeight: 1.5,
      }}
    >
      {children}
    </div>
  );
}

export function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div style={{ marginBottom: '12px' }}>
      <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 800 }}>{title}</h2>
      {subtitle && <div style={{ color: '#64748b', fontSize: '13px', marginTop: '3px' }}>{subtitle}</div>}
    </div>
  );
}

export function EmptyState({ colors, text }: { colors: Colors; text: string }) {
  return <div style={{ color: colors.textSecond, fontSize: '14px', padding: '12px 0' }}>{text}</div>;
}

export function Pill({ colors, label, value }: { colors: Colors; label: string; value: ReactNode }) {
  return (
    <span
      style={{
        display: 'inline-flex',
        gap: '5px',
        alignItems: 'center',
        background: colors.sectionBg,
        border: `1px solid ${colors.panelBorder}`,
        borderRadius: '999px',
        padding: '6px 10px',
        fontSize: '12px',
      }}
    >
      <span style={{ color: colors.textMuted }}>{label}</span>
      <strong>{formatNode(value)}</strong>
    </span>
  );
}

export function Badge({ colors, children }: { colors: Colors; children: ReactNode }) {
  return (
    <span
      style={{
        background: colors.sectionBg,
        border: `1px solid ${colors.panelBorder}`,
        borderRadius: '999px',
        padding: '4px 8px',
        fontSize: '12px',
        fontWeight: 800,
      }}
    >
      {children}
    </span>
  );
}

export function ViewStack({ children }: { children: ReactNode }) {
  return <div style={{ display: 'grid', gap: '14px' }}>{children}</div>;
}

export function MetricGrid({ children }: { children: ReactNode }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px' }}>
      {children}
    </div>
  );
}

export function FormGrid({ children }: { children: ReactNode }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '10px' }}>
      {children}
    </div>
  );
}

export function panel(colors: Colors) {
  return {
    background: colors.panelBg,
    border: `1px solid ${colors.panelBorder}`,
    borderRadius: '8px',
    padding: '14px',
    boxShadow: '0 1px 8px rgba(15,23,42,0.06)',
  };
}

export function button(colors: Colors, active = false) {
  return {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '7px',
    border: `1px solid ${active ? colors.textPrimary : colors.panelBorder}`,
    background: active ? colors.segActive : colors.panelBg,
    color: colors.textPrimary,
    borderRadius: '8px',
    padding: '8px 11px',
    cursor: 'pointer',
    fontWeight: 700,
    textDecoration: 'none',
    minHeight: '36px',
  };
}

export function input(colors: Colors) {
  return {
    width: '100%',
    border: `1px solid ${colors.panelBorder}`,
    background: colors.sectionBg,
    color: colors.textPrimary,
    borderRadius: '8px',
    padding: '8px 10px',
  };
}

export function labelStyle(colors: Colors) {
  return {
    display: 'block',
    color: colors.textSecond,
    fontSize: '12px',
    marginBottom: '4px',
    textTransform: 'capitalize' as const,
  };
}

export function cell(colors: Colors, header = false) {
  return {
    borderBottom: `1px solid ${colors.panelBorder}`,
    padding: '8px',
    textAlign: 'left' as const,
    background: header ? colors.sectionBg : 'transparent',
    whiteSpace: 'nowrap' as const,
    verticalAlign: 'top' as const,
  };
}

export function cardGrid() {
  return { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px' };
}

export function twoCol() {
  return { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '12px' };
}

export function paragraph(colors: Colors) {
  return { color: colors.textSecond, fontSize: '13px', lineHeight: 1.55, margin: '4px 0' };
}

export function formatError(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected UI/runtime error';
}

export function buildP3RunLabel() {
  return `controlled-validation-p3-negative-pipeline-${new Date()
    .toISOString()
    .replace(/[-:TZ.]/g, '')
    .slice(0, 14)}-ui`;
}

export function parseJson(value: string | null | undefined) {
  if (!value) {
    return null;
  }
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

export function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString() : 'Not available';
}

export function shortTime(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

export function formatScore(value: number | null | undefined) {
  return value == null ? 'No data' : value.toFixed(2);
}

export function formatMaybeScore(value: number | null | undefined) {
  return value == null ? 'n/a' : value.toFixed(2);
}

export function formatRiskRange(min: number | null | undefined, max: number | null | undefined) {
  if (min == null || max == null) {
    return 'No recent scores';
  }
  return `min ${min.toFixed(2)} / max ${max.toFixed(2)}`;
}

export function isBlockedDegradationProfile(profile: string) {
  return DEGRADATION_PROFILE_DETAILS[profile]?.blocked === true;
}

export function isControlledValidationP3Run(run: RuntimeRunSummaryResponse | null | undefined) {
  if (!run) {
    return false;
  }
  const text = [
    run.orchestratorCorrelationId,
    run.scenarioCode,
    run.scenarioName,
    run.metadataJson,
    run.runOverrides?.requested?.degradationProfile,
    run.runOverrides?.resolved?.degradationProfile,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return (
    text.includes(P3_CANONICAL.runLabel.toLowerCase()) ||
    text.includes('p3_negative_pipeline') ||
    text.includes('p3-negative-pipeline')
  );
}

export function p3CaseRows(): ReactNode[][] {
  return P3_CASES.map((item) => [item.id, item.category, item.expected, item.status, item.effect]);
}

export function p3EvidenceRows(colors: Colors): ReactNode[][] {
  return P3_EVIDENCE_REFERENCES.map(([label, path, purpose]) => [
    label,
    path,
    purpose,
    <button type="button" key={path} style={button(colors)} onClick={() => copyText(path)}>
      <Clipboard size={16} /> Copy path
    </button>,
  ]);
}

export function normalizeProfiles(values: string[] | null | undefined, legacy: string | null | undefined) {
  const profiles = values && values.length > 0 ? values : legacy ? legacy.split(/[,+;|]/) : ['none'];
  const normalized = Array.from(new Set(profiles.map((value) => value.trim()).filter(Boolean)));
  return normalized.length === 0
    ? ['none']
    : normalized.length > 1
      ? normalized.filter((value) => value !== 'none')
      : normalized;
}

export function toLegacyProfile(values: string[]) {
  return values.length === 1 ? values[0] : values.join('+');
}

export function formatOverrides(
  values: RuntimeRunSummaryResponse['runOverrides'] extends infer T
    ? T extends { requested: infer R }
      ? R
      : never
    : never,
) {
  if (!values) {
    return 'Not available';
  }
  const typed = values as {
    sensorCount?: number | null;
    numberOfCycles?: number | null;
    intervalSeconds?: number | null;
    seed?: number | null;
    degradationProfile?: string | null;
    degradationProfiles?: string[] | null;
  };
  const parts = [
    typed.sensorCount == null ? null : `sensors ${typed.sensorCount}`,
    typed.numberOfCycles == null ? null : `cycles ${typed.numberOfCycles}`,
    typed.intervalSeconds == null ? null : `interval ${typed.intervalSeconds}s`,
    typed.seed == null ? null : `seed ${typed.seed}`,
    typed.degradationProfiles && typed.degradationProfiles.length > 0
      ? typed.degradationProfiles.join('+')
      : (typed.degradationProfile ?? null),
  ].filter(Boolean);
  return parts.length ? parts.join(' / ') : 'Not available';
}

export function formatNode(value: ReactNode) {
  if (value === null || value === undefined || value === '') {
    return 'Not available';
  }
  if (typeof value === 'object' && !Array.isArray(value)) {
    return value as ReactNode;
  }
  return value;
}

export function buildSafeGrafanaAreaUrl(link: string | null, areaId: string) {
  if (!link || !areaId || link.includes('Enter value') || /\?t\?|\?h\?|\?w\?/.test(link)) {
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
  const looksLikeGrafana = /grafana|:3000$/i.test(url.host) || url.pathname.startsWith('/d/');
  if (isInternalWebUi || !looksLikeGrafana) {
    return null;
  }

  if (!url.searchParams.has('kiosk')) {
    url.searchParams.set('kiosk', '');
  }

  console.log('Built Grafana URL:', url.toString());
  return url.toString();
}

export function buildAttemptTimingSummary(summary: RuntimeSummaryResponse | null) {
  const attempts = summary?.pipeline.latestFailedAttempts ?? [];
  const durations = attempts.map((item) => attemptDurationMs(item)).filter((value): value is number => value != null);
  const started = attempts
    .map((item) => item.startedAt)
    .filter(Boolean)
    .sort();
  const finished = attempts
    .map((item) => item.finishedAt)
    .filter((value): value is string => Boolean(value))
    .sort();
  const grouped = new Map<string, RuntimeProcessingAttemptResponse[]>();

  for (const attempt of attempts) {
    const key = `${attempt.stage}::${attempt.outcome}`;
    grouped.set(key, [...(grouped.get(key) ?? []), attempt]);
  }

  const rows = Array.from(grouped.entries()).map(([key, items]) => {
    const [stage, outcome] = key.split('::');
    const itemDurations = items
      .map((item) => attemptDurationMs(item))
      .filter((value): value is number => value != null);
    return [
      stage,
      outcome,
      items.length,
      minDate(items.map((item) => item.startedAt)),
      maxDate(items.map((item) => item.finishedAt).filter((value): value is string => Boolean(value))),
      itemDurations.length ? formatMs(Math.min(...itemDurations)) : 'Not exposed',
      itemDurations.length ? formatMs(avg(itemDurations)) : 'Not exposed',
      itemDurations.length ? formatMs(Math.max(...itemDurations)) : 'Not exposed',
    ];
  });

  const failedAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter((item) => item.errorCode || !/success|completed|accepted/i.test(item.outcome))
    .reduce((sum, item) => sum + item.count, 0);
  const quarantinedAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter((item) => /quarantine/i.test(item.outcome) || /quarantine/i.test(item.errorCode ?? ''))
    .reduce((sum, item) => sum + item.count, 0);
  const successfulAttempts = (summary?.pipeline.attemptsByOutcomeAndError ?? [])
    .filter((item) => /success|completed|accepted/i.test(item.outcome))
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

export function attemptDurationMs(attempt: RuntimeProcessingAttemptResponse) {
  if (!attempt.startedAt || !attempt.finishedAt) {
    return null;
  }
  const started = new Date(attempt.startedAt).getTime();
  const finished = new Date(attempt.finishedAt).getTime();
  return Number.isFinite(started) && Number.isFinite(finished) && finished >= started ? finished - started : null;
}

export function buildRuntimeChainDetails(summary: RuntimeSummaryResponse | null) {
  const failed = summary?.pipeline.latestFailedAttempts[0];
  return [
    {
      label: 'Scenario Run',
      count: summary?.currentRun ? 'Active' : summary?.latestRun ? 'Latest' : 'No data',
      status: summary?.latestRun?.status ?? 'Not observed',
      lastUpdate: formatDate(summary?.latestRun?.endedAt ?? summary?.latestRun?.startedAt),
      latestError: 'Not exposed',
      source: 'control.simulation_runs',
      tone: '#2563eb',
    },
    {
      label: 'Event Inbox',
      count: summary?.pipeline.inboxTotal ?? 'No data',
      status: `${summary?.pipeline.inboxRecent ?? 0} recent`,
      lastUpdate: formatDate(summary?.generatedAtUtc),
      latestError: 'Not exposed',
      source: 'pipeline.event_inbox',
      tone: '#059669',
      navigate: 'retry' as const,
    },
    {
      label: 'Processing Attempts',
      count: summary?.pipeline.attemptsRecent ?? 'No data',
      status: 'Recent attempts',
      lastUpdate: formatDate(failed?.finishedAt ?? failed?.startedAt),
      latestError: failed?.errorCode ?? 'No recent failed attempt',
      source: 'pipeline.processing_attempts',
      tone: '#7c3aed',
      navigate: 'retry' as const,
    },
    {
      label: 'Risk',
      count: summary?.risk.recentCount ?? 'No data',
      status: formatRiskRange(summary?.risk.minScore, summary?.risk.maxScore),
      lastUpdate: formatDate(summary?.risk.latestTimestamp),
      latestError: 'Not exposed',
      source: 'projection.risk_assessment_log',
      tone: '#0891b2',
      navigate: 'risk' as const,
    },
    {
      label: 'State',
      count: summary?.cellOperationalStateCount ?? 'No data',
      status: summary?.areaOperationalState ? 'Projection updated' : 'No area state',
      lastUpdate: formatDate(summary?.areaOperationalState?.updatedAt),
      latestError: 'Not exposed',
      source: 'projection.*_operational_state',
      tone: '#be123c',
      navigate: 'state' as const,
    },
    {
      label: 'Alerts',
      count: summary?.activeAlerts.length ?? 'No data',
      status: summary?.areaOperationalState?.alertState ?? 'None',
      lastUpdate: formatDate(summary?.activeAlerts[0]?.updatedAt),
      latestError: 'Not exposed',
      source: 'projection.alert_state',
      tone: '#b45309',
      navigate: 'alerts' as const,
    },
    {
      label: 'API/UI',
      count: summary ? 'Loaded' : 'No data',
      status: 'Runtime summary endpoint',
      lastUpdate: formatDate(summary?.generatedAtUtc),
      latestError: summary?.warnings[0] ?? 'No warning exposed',
      source: '/control/runtime/summary',
      tone: '#475569',
      navigate: 'services' as const,
    },
  ];
}

export function buildNominalFlowSteps(
  summary: RuntimeSummaryResponse | null,
  audit: RuntimeRunAuditResponse | null,
  runResult: RuntimeRunStartResponse | null,
) {
  const run = summary?.currentRun ?? summary?.latestRun ?? null;
  const expected = audit?.expectedEvents ?? null;
  const inbox = summary?.pipeline.inboxTotal ?? 0;
  const attempts = summary?.pipeline.attemptsRecent ?? 0;
  const risk = audit?.riskAssessments ?? summary?.risk.recentCount ?? 0;
  const stateCount = summary?.cellOperationalStateCount ?? 0;
  const alertCount = summary?.activeAlerts.length ?? 0;
  return [
    {
      name: 'Select scenario',
      status: run?.scenarioCode ? 'Done' : 'No data',
      evidence: run?.scenarioCode ? `scenarioCode=${run.scenarioCode}` : 'No scenario selected or persisted.',
    },
    {
      name: 'Start run',
      status: run?.id ? 'Done' : 'No data',
      evidence: run?.id ? `simulationRunId=${run.id}; status=${run.status}` : 'No simulation run persisted.',
    },
    {
      name: 'Generate readings',
      status: expected && expected > 0 ? 'Done' : 'Not exposed',
      evidence:
        expected && expected > 0 ? `expectedEvents=${expected}` : 'Expected event count is not exposed for this run.',
    },
    { name: 'Publish events', status: inbox > 0 ? 'Done' : 'No data', evidence: `event inbox total=${inbox}` },
    {
      name: 'Ingest inbox',
      status: inbox > 0 ? 'Done' : 'No data',
      evidence: `${summary?.pipeline.inboxRecent ?? 0} inbox rows in selected window; ${inbox} total in scope.`,
    },
    {
      name: 'Process risk',
      status: risk > 0 ? 'Done' : attempts > 0 ? 'Partial' : 'No data',
      evidence: `attempts=${attempts}; riskAssessments=${risk}`,
    },
    {
      name: 'Update projections',
      status: stateCount > 0 || summary?.areaOperationalState ? 'Done' : 'No data',
      evidence: `cell states=${stateCount}; area state=${summary?.areaOperationalState?.aggregateRiskLevel ?? 'Not available'}`,
    },
    { name: 'Emit alerts', status: alertCount > 0 ? 'Done' : 'No data', evidence: `active alerts=${alertCount}` },
    {
      name: 'Show UI',
      status: summary ? 'Done' : 'No data',
      evidence: summary
        ? `/control/runtime/summary loaded at ${formatDate(summary.generatedAtUtc)}`
        : 'API summary not loaded.',
    },
    {
      name: 'Collect evidence',
      status: runResult?.evidenceDirectory ? 'Done' : 'Not exposed',
      evidence: runResult?.evidenceDirectory ?? 'No evidence directory exposed to this UI state.',
    },
  ];
}

export function statusTone(status: string) {
  if (status === 'Done') return '#059669';
  if (status === 'Partial') return '#b45309';
  if (status === 'Failed') return '#dc2626';
  return '#64748b';
}

export function minDate(values: string[]) {
  return values.length ? formatDate(values.sort()[0]) : 'Not exposed';
}

export function maxDate(values: string[]) {
  return values.length ? formatDate(values.sort()[values.length - 1]) : 'Not exposed';
}

export function avg(values: number[]) {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

export function formatMs(value: number) {
  if (value < 1000) {
    return `${Math.round(value)}ms`;
  }
  return `${(value / 1000).toFixed(2)}s`;
}

export function buildCompareRows(compare: RuntimeDiagnosticResultResponse | null): ReactNode[][] {
  const metrics = [
    'expected events',
    'observed accepted readings',
    'missing events',
    'risk assessments',
    'rejected count for area',
    'quarantined count for area',
    'risk min/max/avg',
  ];
  const byMetric = new Map<string, { b: string; c: string }>();
  for (const metric of metrics) {
    byMetric.set(metric, { b: 'Not available', c: 'Not available' });
  }
  for (const row of compare?.rows ?? []) {
    const metric = row.metric ?? '';
    if (!byMetric.has(metric) && metric.startsWith('metric ')) {
      byMetric.set(metric, { b: 'Not available', c: 'Not available' });
    }
    const item = byMetric.get(metric);
    if (!item) {
      continue;
    }
    if (row.scenario === 'scenario_b') {
      item.b = row.value ?? 'Not available';
    }
    if (row.scenario === 'scenario_c') {
      item.c = row.value ?? 'Not available';
    }
  }
  return Array.from(byMetric.entries()).map(([metric, values]) => [
    metric,
    values.b,
    values.c,
    compareDelta(values.b, values.c),
  ]);
}

export function compareDelta(b: string, c: string) {
  const nb = Number(b);
  const nc = Number(c);
  if (Number.isFinite(nb) && Number.isFinite(nc)) {
    const delta = nc - nb;
    return delta === 0 ? '0' : delta > 0 ? `+${delta}` : String(delta);
  }
  return 'Not available';
}

export function groupDiagnostics(diagnostics: RuntimeDiagnosticDefinitionResponse[]) {
  const groups: Record<string, RuntimeDiagnosticDefinitionResponse[]> = {
    Runs: [],
    Pipeline: [],
    Risk: [],
    Alerts: [],
    Scenario: [],
    'Model Evidence': [],
    'Raw Data': [],
  };
  for (const item of diagnostics) {
    const id = item.id.toLowerCase();
    if (
      id.includes('np-vs-fwi') ||
      id.includes('component') ||
      id.includes('cell-context') ||
      id.includes('fwi') ||
      id.includes('kbdi') ||
      id.includes('quality') ||
      id.includes('coverage')
    )
      groups['Model Evidence'].push(item);
    else if (id.includes('run')) groups.Runs.push(item);
    else if (
      id.includes('pipeline') ||
      id.includes('attempt') ||
      id.includes('inbox') ||
      id.includes('rejected') ||
      id.includes('quarantined')
    )
      groups.Pipeline.push(item);
    else if (id.includes('risk')) groups.Risk.push(item);
    else if (id.includes('alert')) groups.Alerts.push(item);
    else if (id.includes('scenario') || id.includes('compare')) groups.Scenario.push(item);
    else groups['Raw Data'].push(item);
  }
  return groups;
}

export function buildEvidenceMarkdown(
  audit: RuntimeRunAuditResponse | null,
  compare: RuntimeDiagnosticResultResponse | null,
  summary: RuntimeSummaryResponse | null,
) {
  const score = summary?.scoreComponents ?? audit?.scoreComponents ?? null;
  const index = summary?.indexComparison ?? audit?.indexComparison ?? null;
  return [
    '# Nature Protector Evidence Summary',
    '',
    `Area: ${summary?.areaCode ?? 'Not available'}`,
    `Latest run: ${summary?.latestRun?.scenarioCode ?? 'Not available'} / ${summary?.latestRun?.status ?? 'Not available'}`,
    `Expected events: ${audit?.expectedEvents ?? 'Not available'}`,
    `Accepted readings: ${audit?.acceptedReadings ?? 'Not available'}`,
    `Missing events: ${audit?.missingEvents ?? 'Not available'}`,
    `Risk assessments: ${audit?.riskAssessments ?? 'Not available'}`,
    `Parameter set: ${score?.parameterSetVersion ?? 'Not available'}`,
    `NP score/base/adjusted: ${formatMaybeScore(score?.npScore)} / ${formatMaybeScore(score?.baseRisk)} / ${formatMaybeScore(score?.adjustedScore)}`,
    `M/D/T: ${formatMaybeScore(score?.meteorologyComponent)} / ${formatMaybeScore(score?.droughtComponent)} / ${formatMaybeScore(score?.territoryComponent)}`,
    `H/F/G: ${formatMaybeScore(score?.hazardComponent)} / ${formatMaybeScore(score?.fuelComponent)} / ${formatMaybeScore(score?.geomorphologyComponent)}`,
    `C/I: ${formatMaybeScore(score?.confidenceFactor)} / ${formatMaybeScore(score?.integrityFactor)}`,
    `FWI raw/normalized/status: ${formatMaybeScore(index?.fireWeatherIndex)} / ${formatMaybeScore(index?.normalizedFireWeatherIndex)} / ${index?.fireWeatherCalculationStatus ?? 'Not available'}`,
    `KBDI raw/normalized/status: ${formatMaybeScore(index?.keetchByramDroughtIndex)} / ${formatMaybeScore(index?.normalizedKeetchByramDroughtIndex)} / ${index?.kbdiCalculationStatus ?? 'Not available'}`,
    `Precipitation 24h/provenance: ${formatMaybeScore(index?.dailyPrecipitationMillimeters)} / ${index?.provenance ?? 'Not available'}`,
    `Index limitations: ${index?.limitations ?? score?.limitations ?? 'None exposed'}`,
    `Degradation profiles: ${summary?.latestRun?.runOverrides?.resolved?.degradationProfiles?.join(', ') ?? summary?.latestRun?.runOverrides?.resolved?.degradationProfile ?? 'Not available'}`,
    `Coverage/freshness/carry-forward: ${summary?.areaOperationalState?.coverageStatus ?? 'n/a'} / ${summary?.areaOperationalState?.freshnessStatus ?? 'n/a'} / ${summary?.areaOperationalState?.carryForwardStatus ?? 'n/a'}`,
    '',
    '## Compare B vs C',
    ...buildCompareRows(compare).map((row) => `- ${row[0]}: B=${row[1]}, C=${row[2]}, delta=${row[3]}`),
  ].join('\n');
}

export function copyText(text: string) {
  void navigator.clipboard?.writeText(text);
}

export function downloadText(fileName: string, text: string) {
  const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
