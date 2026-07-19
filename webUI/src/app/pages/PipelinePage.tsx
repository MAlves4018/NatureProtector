import { useState } from 'react';
import { ActivitySquare, BarChart3, Gauge, Clock, Code } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { TechnicalDetail } from '../components/TechnicalDetail';
import { PipelineTimeline } from '../components/PipelineTimeline';
import { ComponentHealthDashboard } from '../components/ComponentHealthDashboard';
import { QueueMetricsChart } from '../components/QueueMetricsChart';
import { ThroughputDisplay } from '../components/ThroughputDisplay';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { usePipelineSurface } from '../state/useUiSurfaces';
import { useUiActivity } from '../state/ActivityContext';
import { useUiObservability } from '../state/ObservabilityContext';

export function PipelinePage() {
  const { copy } = useUiLocale();
  const { resolvedAreaCode } = useUiArea();
  const { fields: pipelineFields } = usePipelineSurface();
  const { runTimings, runAudit } = useUiActivity();
  const { operationalHealth, rabbitMqMetrics } = useUiObservability();
  const [tab, setTab] = useState<'health' | 'queue' | 'throughput' | 'timeline' | 'technical'>('health');

  return (
    <section className="ui-page">
      <PageHeader title={copy('pipeline.title')} subtitle={copy('pipeline.subtitle')} helpTopic="pipeline" />
      <AreaSelector compact />
      {resolvedAreaCode ? (
        <>
          <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
            <button
              type="button"
              className={tab === 'health' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'health'}
              onClick={() => setTab('health')}
            >
              <ActivitySquare size={16} />
              Saúde
            </button>
            <button
              type="button"
              className={tab === 'queue' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'queue'}
              onClick={() => setTab('queue')}
            >
              <BarChart3 size={16} />
              Filas
            </button>
            <button
              type="button"
              className={tab === 'throughput' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'throughput'}
              onClick={() => setTab('throughput')}
            >
              <Gauge size={16} />
              Throughput
            </button>
            <button
              type="button"
              className={tab === 'timeline' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'timeline'}
              onClick={() => setTab('timeline')}
            >
              <Clock size={16} />
              Timeline
            </button>
            <button
              type="button"
              className={tab === 'technical' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'technical'}
              onClick={() => setTab('technical')}
            >
              <Code size={16} />
              Técnico
            </button>
          </div>
          {tab === 'health' && <ComponentHealthDashboard health={operationalHealth} />}
          {tab === 'queue' && <QueueMetricsChart rabbitMq={rabbitMqMetrics} />}
          {tab === 'throughput' && <ThroughputDisplay audit={runAudit} timings={runTimings} />}
          {tab === 'timeline' && <PipelineTimeline timings={runTimings} />}
          {tab === 'technical' && (
            <section className="ui-card">
              <div className="ui-section-heading">
                <h3>Runtime current state</h3>
              </div>
              <TechnicalDetail title="" fields={pipelineFields} />
            </section>
          )}
        </>
      ) : (
        <EmptyState title={copy('area.selectPrompt')} />
      )}
    </section>
  );
}
