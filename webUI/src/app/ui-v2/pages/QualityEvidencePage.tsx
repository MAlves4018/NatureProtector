import { BetaParityLinks } from '../components/BetaParityLinks';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function QualityEvidencePage() {
  const { copy, qaSuites, evidenceItems } = useUiV2();
  const latest = qaSuites[0] ?? null;
  const historical = evidenceItems.filter(item => item.environment !== 'Current UI/API session');
  const runtimeEvidence = evidenceItems.filter(item => item.environment === 'Current UI/API session');

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('qa.title')} subtitle={copy('qa.subtitle')} helpTopic="qa" />
      {latest && (
        <section className="ui-v2-card">
          <div className="ui-v2-section-heading">
            <h3>Latest test execution</h3>
            <StatusBadge label={latest.status} state="partial" />
          </div>
          <p>{latest.suiteName}</p>
          <div className="ui-v2-key-values">
            <span><strong>{copy('technical.testDefinition')}</strong>{latest.testDefinition}</span>
            <span><strong>{copy('technical.testExecution')}</strong>{latest.testExecution}</span>
            <span><strong>{copy('technical.environment')}</strong>{latest.environment}</span>
            <span><strong>{copy('technical.coverage')}</strong>{latest.coverage}</span>
          </div>
        </section>
      )}
      <EvidenceSection title="Runtime evidence" items={runtimeEvidence} />
      <EvidenceSection title="Historical evidence" items={historical} />
      <BetaParityLinks ids={['evidence-comparison']} />
    </section>
  );
}

function EvidenceSection({ title, items }: { title: string; items: ReturnType<typeof useUiV2>['evidenceItems'] }) {
  const { copy } = useUiV2();

  return (
    <section className="ui-v2-panel">
      <h3>{title}</h3>
      <div className="ui-v2-grid">
        {items.map(item => (
          <article className="ui-v2-card" key={item.evidenceId}>
            <div className="ui-v2-section-heading">
              <h4>{item.title}</h4>
              <StatusBadge label={item.availability} state={item.availability} />
            </div>
            <p>{item.scope}</p>
            <span className="ui-v2-label">{copy('technical.supports')}</span>
            <ul>{item.supportsClaims.map(claim => <li key={claim}>{claim}</li>)}</ul>
            <span className="ui-v2-label">{copy('technical.notSupport')}</span>
            <ul>{item.doesNotSupportClaims.map(claim => <li key={claim}>{claim}</li>)}</ul>
          </article>
        ))}
      </div>
    </section>
  );
}
