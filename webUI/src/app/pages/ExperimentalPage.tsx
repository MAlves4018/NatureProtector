import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { TechnicalDetail } from '../components/TechnicalDetail';
import { useUiLocale } from '../state/LocaleContext';
import { useP3Surface, useP3Data } from '../state/useUiSurfaces';

export function ExperimentalPage() {
  const { copy } = useUiLocale();
  const p3Surface = useP3Surface();
  const { p3Loading } = useP3Data();

  return (
    <section className="ui-page">
      <PageHeader title={copy('p3.title')} subtitle={copy('p3.subtitle')} helpTopic="p3" />
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>{p3Surface.objective}</h3>
          <StatusBadge label={p3Loading ? copy('state.loading') : p3Surface.status} state="partial" />
        </div>
        <div className="ui-fact-list">
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
    </section>
  );
}
