import { Shield, Trees } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { useUiV2 } from '../state/UiV2Context';

export function PublicOverviewPage() {
  const { copy, setActivePage } = useUiV2();

  return (
    <section className="ui-v2-page ui-v2-public-page">
      <div className="ui-v2-product-hero">
        <div>
          <p className="ui-v2-kicker">{copy('app.prototype')} / {copy('app.nonOperational')}</p>
          <h2>{copy('demo.title')}</h2>
          <p>{copy('demo.body')}</p>
          <div className="ui-v2-hero-actions">
            <button type="button" className="ui-v2-button" onClick={() => setActivePage('context')}>{copy('demo.primaryAction')}</button>
          </div>
        </div>
        <div className="ui-v2-product-signal" aria-hidden="true">
          <Trees size={48} />
          <Shield size={36} />
        </div>
      </div>
      <PageHeader title="Leitura publica" subtitle="A pagina publica apresenta o produto, limites e estado dos dados sem expor surfaces internas de pipeline, QA, simulacao ou P3." helpTopic="overview" />
      <div className="ui-v2-grid">
        <BoundaryCard title={copy('demo.boundaryOne')} />
        <BoundaryCard title={copy('demo.boundaryTwo')} />
        <BoundaryCard title={copy('demo.boundaryThree')} />
      </div>
      <AreaSelector />
      <DataStatusSummary />
    </section>
  );
}

function BoundaryCard({ title }: { title: string }) {
  return (
    <article className="ui-v2-card">
      <h3>{title}</h3>
      <p>{'Output contextual do prototipo, separado de alerta oficial e validacao cientifica final.'}</p>
    </article>
  );
}
