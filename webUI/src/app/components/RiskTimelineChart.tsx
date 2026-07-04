import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { useUiLocale } from '../state/LocaleContext';
import { formatUiDate } from '../i18n';
import type { RuntimeRiskPointResponse } from '../types';

interface RiskTimelineChartProps {
  data: RuntimeRiskPointResponse[];
}

const PALE_GREEN = '#166534';
const PALE_AMBER = '#a16207';
const PALE_RED = '#b91c1c';

function scoreColor(level: string) {
  const text = level?.toLowerCase() ?? '';
  if (text.includes('critical') || text.includes('high') || text.includes('extreme')) return PALE_RED;
  if (text.includes('medium') || text.includes('moderate')) return PALE_AMBER;
  return PALE_GREEN;
}

export function RiskTimelineChart({ data }: RiskTimelineChartProps) {
  const { copy, locale } = useUiLocale();
  if (!data || data.length === 0) {
    return (
      <div className="ui-chart-card">
        <h4>Evolucao do risco</h4>
        <p className="ui-chart-empty">{copy('state.noData')}</p>
      </div>
    );
  }

  const chartData = data.map((point) => ({
    ...point,
    label: formatUiDate(point.timestamp, locale),
  }));

  return (
    <div className="ui-chart-card">
      <h4>Evolucao do risco ({data.length} pontos)</h4>
      <ResponsiveContainer width="100%" height={260}>
        <LineChart data={chartData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--ui-border, #d7e1da)" />
          <XAxis
            dataKey="label"
            tick={{ fontSize: 11 }}
            interval="preserveStartEnd"
          />
          <YAxis domain={[0, 1]} tick={{ fontSize: 11 }} />
          <Tooltip
            contentStyle={{
              background: 'var(--ui-surface, #fff)',
              border: '1px solid var(--ui-border, #d7e1da)',
              borderRadius: 6,
              fontSize: 13,
            }}
          />
          <Line
            type="monotone"
            dataKey="riskScore"
            stroke={PALE_GREEN}
            strokeWidth={2}
            dot={(props) => {
              const { cx, cy, payload } = props;
              return (
                <circle
                  cx={cx ?? 0}
                  cy={cy ?? 0}
                  r={4}
                  fill={scoreColor(payload?.riskLevel)}
                  stroke="var(--ui-surface, #fff)"
                  strokeWidth={1.5}
                />
              );
            }}
            activeDot={{ r: 6, fill: PALE_GREEN }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
