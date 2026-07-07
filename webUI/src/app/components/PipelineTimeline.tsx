import { useUiLocale } from '../state/LocaleContext';
import { formatUiDate } from '../i18n';
import type { RuntimeRunTimingSummaryResponse } from '../types';

interface PipelineTimelineProps {
  timings: RuntimeRunTimingSummaryResponse | null;
}

const STAGE_LABELS: Record<string, string> = {
  firstInboxReceivedAt: 'Consumido (inbox)',
  firstProcessingAttemptStartedAt: 'Processamento (inicio)',
  lastProcessingAttemptFinishedAt: 'Processamento (fim)',
  firstRiskAssessmentCreatedAt: 'Risk assessment',
  firstAlertTriggeredAt: 'Alerta',
};

const STAGE_ORDER = [
  'firstInboxReceivedAt',
  'firstProcessingAttemptStartedAt',
  'lastProcessingAttemptFinishedAt',
  'firstRiskAssessmentCreatedAt',
  'firstAlertTriggeredAt',
] as const;

type StageKey = (typeof STAGE_ORDER)[number];

interface Stage {
  key: StageKey;
  label: string;
  value: string | null;
  isAvailable: boolean;
}

export function PipelineTimeline({ timings }: PipelineTimelineProps) {
  const { copy, locale } = useUiLocale();
  if (!timings) {
    return (
      <div className="ui-chart-card ui-chart-full">
        <h4>Timeline do pipeline</h4>
        <p className="ui-chart-empty">{copy('state.noData')}</p>
      </div>
    );
  }

  const stages: Stage[] = STAGE_ORDER.map((key) => {
    const value = timings[key as keyof RuntimeRunTimingSummaryResponse] as string | null;
    return {
      key,
      label: STAGE_LABELS[key] ?? key,
      value: value ? formatUiDate(value, locale) : null,
      isAvailable: !!value,
    };
  });

  const availableStages = stages.filter((s) => s.isAvailable);

  if (availableStages.length === 0) {
    return (
      <div className="ui-chart-card ui-chart-full">
        <h4>Timeline do pipeline</h4>
        <p className="ui-chart-empty">Sem timestamps de stage disponiveis.</p>
      </div>
    );
  }

  return (
    <div className="ui-chart-card ui-chart-full">
      <h4>Timeline do pipeline</h4>
      <ul className="ui-timeline">
        {stages.map((stage) => (
          <li key={stage.key}>
            <strong>{stage.label}</strong>
            <br />
            {stage.isAvailable ? (
              <span style={{ color: 'var(--ui-text)', fontWeight: 700 }}>{stage.value}</span>
            ) : (
              <span style={{ color: 'var(--ui-muted)', fontStyle: 'italic' }}>Nao instrumentado / indisponivel</span>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
