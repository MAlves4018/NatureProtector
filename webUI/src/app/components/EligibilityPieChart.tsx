import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { useUiLocale } from '../state/LocaleContext';
import type { RuntimeStatusCountResponse } from '../types/runtime.ts';

interface EligibilityPieChartProps {
  data: RuntimeStatusCountResponse[] | null | undefined;
}

const GREEN = '#166534';
const AMBER = '#a16207';
const RED = '#b91c1c';
const EMPTY_COLOR = '#d7e1da';

function statusColor(status: string) {
  const text = status?.toLowerCase() ?? '';
  if (text.includes('complete') || text.includes('full') || text.includes('eligible')) return GREEN;
  if (text.includes('partial') || text.includes('limited')) return AMBER;
  if (text.includes('blocked') || text.includes('none')) return RED;
  return EMPTY_COLOR;
}

export function EligibilityPieChart({ data }: EligibilityPieChartProps) {
  const { copy } = useUiLocale();
  if (!data || data.length === 0) {
    return (
      <div className="ui-chart-card">
        <h4>Elegibilidade</h4>
        <p className="ui-chart-empty">{copy('state.noData')}</p>
      </div>
    );
  }

  const chartData = data.map((item) => ({
    name: item.status,
    value: item.count,
    color: statusColor(item.status),
  }));

  return (
    <div className="ui-chart-card">
      <h4>Elegibilidade</h4>
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
