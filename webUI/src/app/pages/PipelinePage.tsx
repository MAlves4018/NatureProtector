import { ChevronDown, ChevronRight } from 'lucide-react';
import { useState } from 'react';
import { PageHeader } from '../components/PageHeader';
import { TechnicalDetail } from '../components/TechnicalDetail';
import { PipelineTimeline } from '../components/PipelineTimeline';
import { ComponentHealthDashboard } from '../components/ComponentHealthDashboard';
import { QueueMetricsChart } from '../components/QueueMetricsChart';
import { ThroughputDisplay } from '../components/ThroughputDisplay';
import { useUiLocale } from '../state/LocaleContext';
import { usePipelineSurface } from '../state/useUiSurfaces';
import { useUiActivity } from '../state/ActivityContext';
import { useUiObservability } from '../state/ObservabilityContext';

export function PipelinePage() {
  const { copy } = useUiLocale();
  const { fields: pipelineFields } = usePipelineSurface();
  const { runTimings, runAudit } = useUiActivity();
  const { operationalHealth, rabbitMqMetrics } = useUiObservability();
  const [showTechnical, setShowTechnical] = useState(false);

  return (
    <section className="ui-page">
      <PageHeader title={copy('pipeline.title')} subtitle={copy('pipeline.subtitle')} helpTopic="pipeline" />
      <ComponentHealthDashboard health={operationalHealth} />
      <QueueMetricsChart rabbitMq={rabbitMqMetrics} />
      <ThroughputDisplay audit={runAudit} timings={runTimings} />
      <PipelineTimeline timings={runTimings} />
      <section className="ui-card">
        <button
          type="button"
          className="ui-section-heading"
          style={{ cursor: 'pointer', background: 'none', border: 'none', width: '100%', textAlign: 'left' }}
          onClick={() => setShowTechnical(!showTechnical)}
        >
          <h3 style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {showTechnical ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            Runtime current state (detalhes tecnicos)
          </h3>
        </button>
        {showTechnical && <TechnicalDetail title="" fields={pipelineFields} />}
      </section>
    </section>
  );
}
