import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useUiLocale } from '../state/LocaleContext';
import type { OperationComparisonResponse } from '../types/operations';

interface ComparisonBarChartProps {
  comparison: OperationComparisonResponse | null;
}

const LEFT_COLOR = '#255f85';
const RIGHT_COLOR = '#176b4d';
const SHARED_COLOR = '#53625b';

export function ComparisonBarChart({ comparison }: ComparisonBarChartProps) {
  const { copy } = useUiLocale();
  if (!comparison) {
    return (
      <div className="ui-chart-card ui-chart-full">
        <h4>Comparacao de artefactos</h4>
        <p className="ui-chart-empty">Seleciona duas operacoes para comparar.</p>
      </div>
    );
  }

  const chartData = [
    {
      name: 'Partilhados',
      valor: comparison.sharedArtifacts.length,
      fill: SHARED_COLOR,
    },
    {
      name: 'So esquerda',
      valor: comparison.onlyOnLeft.length,
      fill: LEFT_COLOR,
    },
    {
      name: 'So direita',
      valor: comparison.onlyOnRight.length,
      fill: RIGHT_COLOR,
    },
  ];

  return (
    <div className="ui-chart-card ui-chart-full">
      <h4>Comparacao de artefactos</h4>
      <p style={{ margin: '0 0 8px', color: 'var(--ui-muted)', fontSize: 13 }}>
        {comparison.leftStatus} &rarr; {comparison.rightStatus} &middot; Evidence: {comparison.evidenceLevel}
      </p>
      <ResponsiveContainer width="100%" height={200}>
        <BarChart data={chartData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--ui-border, #d7e1da)" />
          <XAxis dataKey="name" tick={{ fontSize: 11 }} />
          <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
          <Tooltip
            contentStyle={{
              background: 'var(--ui-surface, #fff)',
              border: '1px solid var(--ui-border, #d7e1da)',
              borderRadius: 6,
              fontSize: 13,
            }}
          />
          <Bar dataKey="valor" radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
