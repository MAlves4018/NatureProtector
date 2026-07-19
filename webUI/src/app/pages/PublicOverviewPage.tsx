import { Shield, Trees, Target, Flame } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { useUiLocale } from '../state/LocaleContext';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiArea } from '../state/AreaContext';

const TEAM_MEMBERS = [
  { name: 'Miguel Alves', email: '' },
  { name: 'Gabriel Mano', email: 'gmano_1@sapo.pt' },
];

export function PublicOverviewPage() {
  const { copy } = useUiLocale();
  const { setActivePage } = useUiCapabilities();
  const { resolvedAreaCode } = useUiArea();

  return (
    <section className="ui-page ui-public-page">
      <div className="ui-product-hero">
        <div>
          <p className="ui-kicker">
            {copy('app.prototype')} / {copy('app.nonOperational')}
          </p>
          <h2>{copy('demo.title')}</h2>
          <p>{copy('demo.body')}</p>
          <div className="ui-hero-actions">
            <button type="button" className="ui-button" onClick={() => setActivePage('context')}>
              {copy('demo.primaryAction')}
            </button>
          </div>
        </div>
        <div className="ui-product-signal" aria-hidden="true">
          <Trees size={48} />
          <Shield size={36} />
        </div>
      </div>
      <PageHeader
        title="Leitura pública"
        subtitle="A página pública apresenta o produto, os limites e o estado dos dados sem expor superfícies internas de pipeline, QA, simulação ou P3."
        helpTopic="overview"
      />
      <div className="ui-grid">
        <BoundaryCard title={copy('demo.boundaryOne')} />
        <BoundaryCard title={copy('demo.boundaryTwo')} />
        <BoundaryCard title={copy('demo.boundaryThree')} />
      </div>

      <section className="ui-card" style={{ marginTop: 24 }}>
        <div className="ui-section-heading">
          <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Target size={20} />
            Sobre o projeto
          </h3>
        </div>
        <div style={{ display: 'grid', gap: 14 }}>
          <p>
            O <strong>NatureProtector</strong> é um protótipo académico de monitorização e avaliação do risco de
            incêndios florestais. O sistema integra sensores, simulação de cenários e análise de risco para apoiar a
            tomada de decisão na prevenção e resposta a incêndios.
          </p>
          <p>
            A plataforma consome dados de estações meteorológicas, sensores de humidade e temperatura e modelos de
            propagação do fogo para produzir avaliações de risco em tempo real. Os resultados são contextualizados por
            origem, temporalidade, disponibilidade e limitações conhecidas, não substituindo autoridades oficiais como
            IPMA, ICNF ou Proteção Civil.
          </p>
          <div
            className="ui-grid"
            style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', marginTop: 8 }}
          >
            {TEAM_MEMBERS.map((member) => (
              <article key={member.name} className="ui-card" style={{ display: 'grid', gap: 4, padding: 12 }}>
                <strong style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Flame size={14} />
                  {member.name}
                </strong>
                {member.email && <span className="ui-label">{member.email}</span>}
              </article>
            ))}
          </div>
        </div>
      </section>

      <AreaSelector />
      {resolvedAreaCode ? <DataStatusSummary /> : <EmptyState title={copy('area.selectPrompt')} />}
    </section>
  );
}

function BoundaryCard({ title }: { title: string }) {
  return (
    <article className="ui-card">
      <h3>{title}</h3>
      <p>{'Saída contextual do protótipo, separada de alerta oficial e de validação científica final.'}</p>
    </article>
  );
}
