import { Database, Terminal } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';

const DATA_STORES = [
  {
    id: 'postgresql',
    name: 'PostgreSQL',
    icon: Database,
    scope: 'Control plane, inbox, histórico de risco, projeções e alertas.',
    guidance: 'Usa os diagnósticos e scripts allowlisted para obter resultados reproduzíveis.',
  },
  {
    id: 'influxdb',
    name: 'InfluxDB',
    icon: Terminal,
    scope: 'Séries temporais de leituras, avaliações e snapshots.',
    guidance: 'Usa dashboards ou consultas documentadas com área, run e janela temporal explícitas.',
  },
] as const;

export function DatabaseQueriesPage() {
  return (
    <section className="ui-page">
      <PageHeader
        title="Catálogo de consultas de diagnóstico"
        subtitle="Referência read-only dos data stores. Não existe executor de queries integrado no browser."
        helpTopic="pipeline"
      />
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Estado da integração</h3>
          <span className="ui-badge">Apenas leitura</span>
        </div>
        <p className="ui-notice">
          Esta superfície não envia SQL, InfluxQL ou outros comandos. Os resultados devem vir de scripts e diagnostics
          allowlisted, preservando ambiente, run, janela temporal e evidência.
        </p>
      </section>
      <section className="ui-card">
        <h3>Data stores documentados</h3>
        <div style={{ display: 'grid', gap: 12 }}>
          {DATA_STORES.map((store) => {
            const Icon = store.icon;
            return (
              <article key={store.id} className="ui-operation-card">
                <h4>
                  <Icon size={18} aria-hidden="true" /> {store.name}
                </h4>
                <p>{store.scope}</p>
                <p className="ui-notice">{store.guidance}</p>
              </article>
            );
          })}
        </div>
      </section>
    </section>
  );
}
