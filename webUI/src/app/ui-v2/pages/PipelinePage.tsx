import { BetaParityLinks } from '../components/BetaParityLinks';
import { PageHeader } from '../components/PageHeader';
import { TechnicalDetail } from '../components/TechnicalDetail';
import { useUiV2 } from '../state/UiV2Context';

export function PipelinePage() {
  const { copy, pipelineFields, pipelineLimitations } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('pipeline.title')} subtitle={copy('pipeline.subtitle')} helpTopic="pipeline" />
      <section className="ui-v2-card">
        <h3>Runtime current state</h3>
        <TechnicalDetail title="Campos de runtime, temporalidade e provenance" fields={pipelineFields} />
      </section>
      <section className="ui-v2-panel">
        <h3>Limitacoes tecnicas</h3>
        <ul>
          {pipelineLimitations.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </section>
      <BetaParityLinks ids={['runtime-monitor', 'flow-model']} />
    </section>
  );
}
