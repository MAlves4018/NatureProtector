import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { useUiLocale } from '../state/LocaleContext';
import type { RuntimeFreshnessSummaryResponse } from '../types';

interface FreshnessPieChartProps {
  data: RuntimeFreshnessSummaryResponse | null | undefined;
}

const FRESH_COLOR = 'var(--ui-success)';
const STALE_COLOR = 'var(--ui-warning)';
const EXPIRED_COLOR = 'var(--ui-error)';

export function FreshnessPieChart({ data }: FreshnessPieChartProps) {
  const { copy } = useUiLocale();
  if (!data) {
    return (
      <div className="ui-chart-card">
        <h4>Freshness</h4>
        <p className="ui-chart-empty">{copy('state.noData')}</p>
      </div>
    );
  }

  const chartData = [
    { name: 'Fresh', value: data.freshCount, color: FRESH_COLOR },
    { name: 'Stale', value: data.staleCount, color: STALE_COLOR },
    { name: 'Expired', value: data.expiredCount, color: EXPIRED_COLOR },
  ].filter((item) => item.value > 0);

  if (chartData.length === 0) {
    return (
      <div className="ui-chart-card">
        <h4>Freshness</h4>
        <p className="ui-chart-empty">{copy('state.noData')}</p>
      </div>
    );
  }

  return (
    <div className="ui-chart-card">
      <h4>Freshness dos dados</h4>
      <ResponsiveContainer width="100%" height={200}>
        <PieChart>
          <Pie
            data={chartData}
            dataKey="value"
            nameKey="name"
            cx="50%"
            cy="50%"
            outerRadius={70}
            innerRadius={30}
            paddingAngle={2}
          >
            {chartData.map((entry) => (
              <Cell key={entry.name} fill={entry.color} />
            ))}
          </Pie>
          <Tooltip
            contentStyle={{
              background: 'var(--ui-surface, #fff)',
              border: '1px solid var(--ui-border, #d7e1da)',
              borderRadius: 6,
              fontSize: 13,
            }}
          />
        </PieChart>
      </ResponsiveContainer>
      <div className="ui-status-row" style={{ marginTop: 4 }}>
        {chartData.map((entry) => (
          <span key={entry.name} className="ui-badge" style={{ borderColor: entry.color }}>
            {entry.name}: {entry.value}
          </span>
        ))}
      </div>
    </div>
  );
}
