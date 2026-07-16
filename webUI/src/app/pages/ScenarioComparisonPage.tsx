import { useState, useCallback, useMemo } from 'react';
import { GitCompareArrows } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, LabelList } from 'recharts';
import { PageHeader } from '../components/PageHeader';
import { ExportActions } from '../components/ExportActions';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { api } from '../services/api';
import type { RuntimeDiagnosticResultResponse } from '../types';
import { diagnosticResultToCsv } from '../utils/operationalMetrics';

const B_COLOR = '#255f85';
const C_COLOR = '#176b4d';

interface MetricPair {
  metric: string;
  b: string | null;
  c: string | null;
}

interface ChartDatum {
  name: string;
  B: number;
  C: number;
}

function groupRows(rows: RuntimeDiagnosticResultResponse['rows']): MetricPair[] {
  const map = new Map<string, { b: string | null; c: string | null }>();
  for (const row of rows) {
    const scenario = row.scenario;
    const metric = row.metric;
    const value = row.value;
    if (!metric) continue;
    if (!map.has(metric)) {
      map.set(metric, { b: null, c: null });
    }
    const entry = map.get(metric)!;
    if (scenario === 'scenario_b') entry.b = value;
    else if (scenario === 'scenario_c') entry.c = value;
  }
  return Array.from(map.entries()).map(([metric, { b, c }]) => ({ metric, b, c }));
}

function isNumeric(val: string | null): val is string {
  return val !== null && val !== '' && !Number.isNaN(Number(val));
}

function parseCompound(val: string | null): number[] | null {
  if (!val) return null;
  const parts = val.split('/').map(Number);
  if (parts.some(Number.isNaN)) return null;
  return parts;
}

function getNumericChartData(pairs: MetricPair[]): ChartDatum[] {
  const numericMetrics = [
    'expected events',
    'observed accepted readings',
    'missing events',
    'risk assessments',
    'rejected count for area',
    'quarantined count for area',
  ];
  const result: ChartDatum[] = [];
  for (const m of numericMetrics) {
    const pair = pairs.find((p) => p.metric === m);
    if (pair && isNumeric(pair.b) && isNumeric(pair.c)) {
      result.push({ name: m, B: Number(pair.b), C: Number(pair.c) });
    }
  }
  return result;
}

function getCompoundChartData(pairs: MetricPair[]): { label: string; data: ChartDatum[] }[] {
  const riskPair = pairs.find((p) => p.metric === 'risk min/max/avg');
  const groups: { label: string; data: ChartDatum[] }[] = [];

  if (riskPair) {
    const bVals = parseCompound(riskPair.b);
    const cVals = parseCompound(riskPair.c);
    if (bVals && cVals && bVals.length >= 3 && cVals.length >= 3) {
      groups.push({
        label: 'risk min/max/avg',
        data: [
          { name: 'Min', B: bVals[0], C: cVals[0] },
          { name: 'Max', B: bVals[1], C: cVals[1] },
          { name: 'Avg', B: bVals[2], C: cVals[2] },
        ],
      });
    }
  }

  const metricTypePairs = pairs.filter(
    (p) => p.metric.startsWith('metric ') && p.metric.endsWith(' count/min/max/avg score'),
  );
  for (const pair of metricTypePairs) {
    const bVals = parseCompound(pair.b);
    const cVals = parseCompound(pair.c);
    if (bVals && cVals && bVals.length >= 4 && cVals.length >= 4) {
      const label = pair.metric.replace(' count/min/max/avg score', '');
      groups.push({
        label,
        data: [
          { name: 'Count', B: bVals[0], C: cVals[0] },
          { name: 'Min', B: bVals[1], C: cVals[1] },
          { name: 'Max', B: bVals[2], C: cVals[2] },
          { name: 'Avg', B: bVals[3], C: cVals[3] },
        ],
      });
    }
  }

  return groups;
}

export function ScenarioComparisonPage() {
  const { copy } = useUiLocale();
  const { resolvedAreaCode, areas, selectedAreaCode, setSelectedAreaCode, areasLoading } = useUiArea();
  const [result, setResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pairs = useMemo(() => (result ? groupRows(result.rows) : []), [result]);

  const numericChartData = useMemo(() => getNumericChartData(pairs), [pairs]);
  const compoundChartGroups = useMemo(() => getCompoundChartData(pairs), [pairs]);

  const handleCompare = useCallback(async () => {
    if (!resolvedAreaCode) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const data = await api.executeRuntimeDiagnostic('compare-latest-b-vs-c', {
        areaCode: resolvedAreaCode,
        recentMinutes: 30,
      });
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to execute comparison');
    } finally {
      setLoading(false);
    }
  }, [resolvedAreaCode]);

  const LABEL_B = 'Scenario B';
  const LABEL_C = 'Scenario C';

  return (
    <section className="ui-page">
      <PageHeader
        title={copy('scenario-compare.title')}
        subtitle={copy('scenario-compare.subtitle')}
        helpTopic="runState"
      />

      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>{copy('area.title')}</h3>
        </div>
        <label className="ui-field">
          <span>{copy('area.selectLabel')}</span>
          <select
            className="ui-select"
            value={selectedAreaCode}
            onChange={(event) => setSelectedAreaCode(event.target.value)}
            disabled={areasLoading}
          >
            <option value="">{areasLoading ? copy('state.loading') : copy('area.placeholder')}</option>
            {areas.map((item) => (
              <option key={item.code} value={item.code}>
                {item.name} ({item.code})
              </option>
            ))}
          </select>
        </label>
        <div className="ui-button-row">
          <button type="button" className="ui-button" onClick={handleCompare} disabled={!resolvedAreaCode || loading}>
            <GitCompareArrows size={16} />
            {loading ? copy('state.loading') : copy('scenario-compare.execute')}
          </button>
        </div>
      </section>

      {error && (
        <section className="ui-card">
          <p className="ui-state-error">{error}</p>
        </section>
      )}

      {result && (
        <>
          <section className="ui-card">
            <div className="ui-section-heading">
              <h3>{result.title}</h3>
              <ExportActions
                filename={`comparacao-${resolvedAreaCode ?? 'area'}.csv`}
                content={diagnosticResultToCsv(result)}
              />
            </div>
            <p>{result.description}</p>
          </section>

          <section className="ui-card">
            <div className="ui-table-wrap">
              <table className="ui-table">
                <thead>
                  <tr>
                    <th>{copy('scenario-compare.metric')}</th>
                    <th>{LABEL_B}</th>
                    <th>{LABEL_C}</th>
                  </tr>
                </thead>
                <tbody>
                  {pairs.length === 0 ? (
                    <tr>
                      <td colSpan={3}>{copy('scenario-compare.noData')}</td>
                    </tr>
                  ) : (
                    pairs.map((pair) => (
                      <tr key={pair.metric}>
                        <td>{pair.metric}</td>
                        <td>{pair.b ?? '\u2014'}</td>
                        <td>{pair.c ?? '\u2014'}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>

          {numericChartData.length > 0 && (
            <section className="ui-card">
              <div className="ui-section-heading">
                <h3>Comparação numérica</h3>
              </div>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={numericChartData} margin={{ top: 20, right: 30, left: 0, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--ui-border, #d7e1da)" />
                  <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip
                    contentStyle={{
                      background: 'var(--ui-surface, #fff)',
                      border: '1px solid var(--ui-border, #d7e1da)',
                      borderRadius: 6,
                      fontSize: 13,
                    }}
                  />
                  <Bar dataKey="B" fill={B_COLOR} radius={[4, 4, 0, 0]} name={LABEL_B}>
                    <LabelList dataKey="B" position="top" fontSize={10} />
                  </Bar>
                  <Bar dataKey="C" fill={C_COLOR} radius={[4, 4, 0, 0]} name={LABEL_C}>
                    <LabelList dataKey="C" position="top" fontSize={10} />
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </section>
          )}

          {compoundChartGroups.map((group) => (
            <section key={group.label} className="ui-card">
              <div className="ui-section-heading">
                <h3>{group.label}</h3>
              </div>
              <ResponsiveContainer width="100%" height={250}>
                <BarChart data={group.data} margin={{ top: 20, right: 30, left: 0, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--ui-border, #d7e1da)" />
                  <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip
                    contentStyle={{
                      background: 'var(--ui-surface, #fff)',
                      border: '1px solid var(--ui-border, #d7e1da)',
                      borderRadius: 6,
                      fontSize: 13,
                    }}
                  />
                  <Bar dataKey="B" fill={B_COLOR} radius={[4, 4, 0, 0]} name={LABEL_B}>
                    <LabelList dataKey="B" position="top" fontSize={10} />
                  </Bar>
                  <Bar dataKey="C" fill={C_COLOR} radius={[4, 4, 0, 0]} name={LABEL_C}>
                    <LabelList dataKey="C" position="top" fontSize={10} />
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </section>
          ))}
        </>
      )}

      {!result && !loading && !error && (
        <section className="ui-card">
          <p>{resolvedAreaCode ? copy('scenario-compare.execute') + '...' : copy('scenario-compare.noArea')}</p>
        </section>
      )}
    </section>
  );
}
