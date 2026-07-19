import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { OperationComparisonResponse } from '../types/operations';

interface ComparisonBarChartProps {
  comparison: OperationComparisonResponse | null;
}

const LEFT_COLOR = '#3b82f6';
const RIGHT_COLOR = '#22c55e';
const SHARED_COLOR = '#6b7280';

export function ComparisonBarChart({ comparison }: ComparisonBarChartProps) {
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
      <p style={{ margin: '0 0 8px', fontSize: 13 }}>
        {comparison.leftStatus} &rarr; {comparison.rightStatus} &middot; Evidence: {comparison.evidenceLevel}
      </p>
      <ResponsiveContainer width="100%" height={200}>
        <BarChart data={chartData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#d7e1da" />
          <XAxis dataKey="name" tick={{ fontSize: 11 }} />
          <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
          <Tooltip
            contentStyle={{
              background: '#fff',
              border: '1px solid #d7e1da',
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
