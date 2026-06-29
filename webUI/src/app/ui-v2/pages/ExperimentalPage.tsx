import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { TechnicalDetail } from '../components/TechnicalDetail';
import { useUiV2 } from '../state/UiV2Context';

export function ExperimentalPage() {
  const { copy, p3Surface, p3Loading } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('p3.title')} subtitle={copy('p3.subtitle')} helpTopic="p3" />
      <section className="ui-v2-card">
        <div className="ui-v2-section-heading">
          <h3>{p3Surface.objective}</h3>
          <StatusBadge label={p3Loading ? copy('state.loading') : p3Surface.status} state="partial" />
        </div>
        <div className="ui-v2-fact-list">
          <span>
            <strong>Integracao</strong>
            {p3Surface.integrationStatus}
          </span>
          <span>
            <strong>Inputs esperados</strong>
            {p3Surface.expectedInputs}
          </span>
          <span>
            <strong>Outputs esperados</strong>
            {p3Surface.expectedOutputs}
          </span>
          <span>
            <strong>Evidence existente</strong>
            {p3Surface.existingEvidence}
          </span>
          <span>
            <strong>Readiness</strong>
            {p3Surface.readiness}
          </span>
          <span>
            <strong>Next gate</strong>
            {p3Surface.nextGate}
          </span>
        </div>
      </section>
      <TechnicalDetail title="Detalhe P3" fields={p3Surface.fields} />
      <section className="ui-v2-notice">
        <strong>Limites P3</strong>
        <ul>
          {p3Surface.limitations.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </section>
    </section>
  );
}
