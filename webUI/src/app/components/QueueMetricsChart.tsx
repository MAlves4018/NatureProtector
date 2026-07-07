import { BarChart, Bar, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid } from 'recharts';
import type { RabbitMqMetricsResponse } from '../types';

interface Props {
  rabbitMq: RabbitMqMetricsResponse | null;
}

export function QueueMetricsChart({ rabbitMq }: Props) {
  if (!rabbitMq || rabbitMq.collectionStatus !== 'Measured' || rabbitMq.queues.length === 0) {
    return (
      <div className="ui-chart-empty">
        <p>Sem metricas de filas RabbitMQ.</p>
      </div>
    );
  }

  const data = rabbitMq.queues.map((q) => ({
    name: q.queueName.length > 30 ? q.queueName.substring(0, 28) + '...' : q.queueName,
    'Messages Ready': q.messagesReady ?? 0,
    'Unacknowledged': q.messagesUnacknowledged ?? 0,
    'Consumers': q.consumers ?? 0,
  }));

  return (
    <div className="ui-chart-card">
      <div className="ui-section-heading">
        <h3>Filas RabbitMQ</h3>
        <span className="ui-badge">{rabbitMq.source}</span>
      </div>
      <ResponsiveContainer width="100%" height={250}>
        <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--ui-border)" />
          <XAxis dataKey="name" tick={{ fontSize: 11 }} />
          <YAxis tick={{ fontSize: 11 }} />
          <Tooltip
            contentStyle={{
              background: 'var(--ui-surface)',
              border: '1px solid var(--ui-border)',
              borderRadius: 6,
              fontSize: 12,
            }}
          />
          <Legend fontSize={11} />
          <Bar dataKey="Messages Ready" fill="#255f85" radius={[3, 3, 0, 0]} />
          <Bar dataKey="Unacknowledged" fill="#a16207" radius={[3, 3, 0, 0]} />
          <Bar dataKey="Consumers" fill="#166534" radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
