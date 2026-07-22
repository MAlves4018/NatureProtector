import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ComparisonBarChart } from './ComparisonBarChart';
import { ComponentHealthDashboard } from './ComponentHealthDashboard';
import { EligibilityPieChart } from './EligibilityPieChart';
import { PipelineTimeline } from './PipelineTimeline';
import { QueueMetricsChart } from './QueueMetricsChart';
import { RiskTimelineChart } from './RiskTimelineChart';
import { ThroughputDisplay } from './ThroughputDisplay';

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div data-testid="responsive">{children}</div>,
  BarChart: ({ children, data }: { children: React.ReactNode; data?: Array<Record<string, unknown>> }) => (
    <div data-testid="bar-chart" data-rows={data?.length ?? 0}>
      {children}
    </div>
  ),
  LineChart: ({ children, data }: { children: React.ReactNode; data?: Array<Record<string, unknown>> }) => (
    <div data-testid="line-chart" data-rows={data?.length ?? 0}>
      {children}
    </div>
  ),
  PieChart: ({ children }: { children: React.ReactNode }) => <div data-testid="pie-chart">{children}</div>,
  Pie: ({
    children,
    data,
  }: {
    children: React.ReactNode;
    data?: Array<Record<string, unknown>>;
  }) => (
    <div data-testid="pie" data-rows={data?.length ?? 0}>
      {children}
    </div>
  ),
  Cell: ({ fill }: { fill: string }) => <span data-testid="pie-cell" data-fill={fill} />,
  CartesianGrid: () => <span data-testid="grid" />,
  XAxis: ({ dataKey }: { dataKey: string }) => <span data-testid="x-axis">{dataKey}</span>,
  YAxis: () => <span data-testid="y-axis" />,
  Tooltip: () => <span data-testid="tooltip" />,
  Legend: () => <span data-testid="legend" />,
  Bar: ({ dataKey }: { dataKey: string }) => <span data-testid="bar">{dataKey}</span>,
  Line: ({
    dataKey,
    dot,
  }: {
    dataKey: string;
    dot?: (props: { cx: number; cy: number; payload: { riskLevel: string } }) => React.ReactNode;
  }) => (
    <svg data-testid="line" aria-label={dataKey}>
      {dot?.({ cx: 1, cy: 2, payload: { riskLevel: 'critical' } })}
      {dot?.({ cx: 3, cy: 4, payload: { riskLevel: 'medium' } })}
      {dot?.({ cx: 5, cy: 6, payload: { riskLevel: 'low' } })}
    </svg>
  ),
}));

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({ copy: (key: string) => `copy:${key}`, locale: 'pt-PT' }),
}));

describe('chart components', () => {
  it('renders empty states for missing runtime chart data', () => {
    render(
      <>
        <RiskTimelineChart data={[]} />
        <PipelineTimeline timings={null} />
        <QueueMetricsChart rabbitMq={null} />
        <ComparisonBarChart comparison={null} />
        <ComponentHealthDashboard health={null} />
      </>,
    );

    expect(screen.getAllByText('copy:state.noData')).toHaveLength(2);
    expect(screen.getByText('Sem metricas de filas RabbitMQ.')).toBeInTheDocument();
    expect(screen.getByText('Seleciona duas operacoes para comparar.')).toBeInTheDocument();
    expect(screen.getByText('Sem dados de saude dos componentes.')).toBeInTheDocument();
  });

  it('renders health components ordered by severity and optional fields', () => {
    render(
      <ComponentHealthDashboard
        health={{
          observedAt: '2026-07-21T12:00:00Z',
          components: [
            {
              component: 'Unknown source',
              status: 'Unknown',
              source: 'probe',
              ageSeconds: null,
              lastSuccessAt: null,
              lastFailureAt: null,
              reason: '',
            },
            {
              component: 'Backoffice API',
              status: 'Healthy',
              source: 'http',
              ageSeconds: 120,
              lastSuccessAt: '2026-07-21T11:59:00Z',
              lastFailureAt: null,
              reason: 'ready',
            },
            {
              component: 'RabbitMQ',
              status: 'Unhealthy',
              source: 'management',
              ageSeconds: 300,
              lastSuccessAt: null,
              lastFailureAt: '2026-07-21T11:58:00Z',
              reason: 'connection refused',
            },
          ],
        }}
      />,
    );

    expect(screen.getByText('Saude dos Componentes')).toBeInTheDocument();
    expect(screen.getByText('Backoffice API')).toBeInTheDocument();
    expect(screen.getByText('RabbitMQ')).toBeInTheDocument();
    expect(screen.getByText('Unknown source')).toBeInTheDocument();
    expect(screen.getByText('2min')).toBeInTheDocument();
    expect(screen.getByText('5min')).toBeInTheDocument();
    expect(screen.getByText('connection refused')).toBeInTheDocument();
  });

  it('renders queue and comparison charts with measured operational data', () => {
    render(
      <>
        <QueueMetricsChart
          rabbitMq={{
            collectionStatus: 'Measured',
            observedAt: '2026-07-21T12:00:00Z',
            source: 'rabbit-management',
            queues: [
              {
                queueName: 'natureprotector.pipeline.primary.queue.with.long.name',
                queueRole: 'PrimaryWorkQueue',
                messagesReady: 4,
                messagesUnacknowledged: 2,
                consumers: 3,
              },
            ],
          }}
        />
        <ComparisonBarChart
          comparison={{
            leftOperationId: 'op-a',
            rightOperationId: 'op-b',
            leftStatus: 'Completed',
            rightStatus: 'Failed',
            evidenceLevel: 'PROVED_LOCAL',
            sharedArtifacts: ['manifest.csv'],
            onlyOnLeft: ['stdout.log', 'stderr.log'],
            onlyOnRight: [],
          }}
        />
      </>,
    );

    expect(screen.getByText('Filas RabbitMQ')).toBeInTheDocument();
    expect(screen.getByText('rabbit-management')).toBeInTheDocument();
    expect(screen.getByText('Messages Ready')).toBeInTheDocument();
    expect(screen.getByText('Unacknowledged')).toBeInTheDocument();
    expect(screen.getByText('Consumers')).toBeInTheDocument();
    expect(screen.getByText(/Completed → Failed · Evidence: PROVED_LOCAL/i)).toBeInTheDocument();
    expect(screen.getAllByTestId('bar-chart')).toHaveLength(2);
  });

  it('renders risk points and mixed pipeline timestamp availability', () => {
    render(
      <>
        <RiskTimelineChart
          data={[
            { timestamp: '2026-07-21T12:00:00Z', riskScore: 0.2, riskLevel: 'low' },
            { timestamp: '2026-07-21T12:01:00Z', riskScore: 0.6, riskLevel: 'medium' },
            { timestamp: '2026-07-21T12:02:00Z', riskScore: 0.9, riskLevel: 'critical' },
          ]}
        />
        <PipelineTimeline
          timings={{
            runDurationMs: 4000,
            timeToFirstInboxMs: 100,
            firstInboxReceivedAt: '2026-07-21T12:00:01Z',
            firstProcessingAttemptStartedAt: '2026-07-21T12:00:02Z',
            lastProcessingAttemptFinishedAt: null,
            firstRiskAssessmentCreatedAt: '2026-07-21T12:00:03Z',
            firstAlertTriggeredAt: null,
            attempts: {},
            stages: [],
            timeline: [],
          }}
        />
      </>,
    );

    expect(screen.getByText('Evolucao do risco (3 pontos)')).toBeInTheDocument();
    expect(screen.getByTestId('line-chart')).toHaveAttribute('data-rows', '3');
    expect(screen.getByText('Consumido (inbox)')).toBeInTheDocument();
    expect(screen.getByText('Risk assessment')).toBeInTheDocument();
    expect(screen.getAllByText('Nao instrumentado / indisponivel')).toHaveLength(2);
  });

  it('renders throughput metrics without substituting unavailable values', () => {
    render(
      <ThroughputDisplay
        audit={{
          expectedEvents: 40,
          acceptedReadings: 36,
          rejected: 2,
          quarantined: 1,
          retryAttempts: 3,
          riskAssessments: 35,
        }}
        timings={{
          timeline: [
            { stage: 'published', status: 'completed' },
            { stage: 'assessed', status: 'completed' },
            { stage: 'projected', status: 'pending' },
          ],
          attempts: {
            attemptCount: 40,
            successfulAttempts: 36,
            avgDurationMs: 1234,
          },
        }}
      />,
    );

    expect(screen.getByText('Throughput / Latencia')).toBeInTheDocument();
    expect(screen.getByText('Taxa de aceitacao')).toBeInTheDocument();
    expect(screen.getAllByText('90.0%')).toHaveLength(2);
    expect(screen.getByText('Rejeitados')).toBeInTheDocument();
    expect(screen.getByText('Quarentena')).toBeInTheDocument();
    expect(screen.getByText('Retries')).toBeInTheDocument();
    expect(screen.getByText('Pipeline stages')).toBeInTheDocument();
    expect(screen.getByText('2/3')).toBeInTheDocument();
    expect(screen.getByText('Sucesso attempts')).toBeInTheDocument();
    expect(screen.getByText('1.23s')).toBeInTheDocument();
    expect(screen.getByText('Eventos esperados')).toBeInTheDocument();
  });

  it('renders throughput fallback and zero-attempt behavior explicitly', () => {
    const { rerender } = render(<ThroughputDisplay audit={null} timings={null} />);

    expect(screen.getByText('Sem metricas de throughput.')).toBeInTheDocument();

    rerender(
      <ThroughputDisplay
        audit={{
          expectedEvents: null,
          acceptedReadings: 7,
          rejected: 0,
          quarantined: 0,
          retryAttempts: 0,
          riskAssessments: 7,
        }}
        timings={{
          attempts: {
            attemptCount: 0,
            successfulAttempts: 0,
            avgDurationMs: null,
          },
        }}
      />,
    );

    expect(screen.getAllByText('7')).toHaveLength(2);
    expect(screen.getByText('N/A')).toBeInTheDocument();
    expect(screen.queryByText('Eventos esperados')).not.toBeInTheDocument();
  });

  it('renders eligibility status colors and empty copy', () => {
    const { rerender } = render(<EligibilityPieChart data={null} />);

    expect(screen.getByText('Elegibilidade')).toBeInTheDocument();
    expect(screen.getByText('copy:state.noData')).toBeInTheDocument();

    rerender(
      <EligibilityPieChart
        data={[
          { status: 'eligible', count: 20 },
          { status: 'partial', count: 4 },
          { status: 'blocked', count: 2 },
          { status: 'unknown', count: 1 },
        ]}
      />,
    );

    expect(screen.getByTestId('pie')).toHaveAttribute('data-rows', '4');
    expect(screen.getByText('eligible: 20')).toBeInTheDocument();
    expect(screen.getByText('partial: 4')).toBeInTheDocument();
    expect(screen.getByText('blocked: 2')).toBeInTheDocument();
    expect(screen.getByText('unknown: 1')).toBeInTheDocument();
    expect(screen.getAllByTestId('pie-cell').map((cell) => cell.getAttribute('data-fill'))).toEqual([
      '#166534',
      '#a16207',
      '#b91c1c',
      '#d7e1da',
    ]);
  });
});
