import { StatusBadge } from '../components/StatusBadge';
import { PageHeader } from '../components/PageHeader';
import { useUiQaTests } from '../state/QaTestContext';

function suiteState(status: string) {
  if (status === 'Passed') return 'ready' as const;
  if (status.toLowerCase().includes('finding')) return 'partial' as const;
  return 'unknown' as const;
}

export function QaTestSuitePage() {
  const { qaSuites } = useUiQaTests();

  return (
    <section className="ui-page">
      <PageHeader
        title="Catálogo de suites QA"
        subtitle="Consulta definições e evidência histórica. A execução de testes não está integrada nesta interface."
        helpTopic="qa"
      />
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Estado da integração</h3>
          <StatusBadge label="Apenas leitura" state="partial" />
        </div>
        <p className="ui-notice">
          Esta página não executa comandos nem gera resultados. Os estados correspondem apenas à evidência histórica
          indicada em cada suite e devem ser confirmados nos respetivos artefactos.
        </p>
      </section>
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Suites documentadas ({qaSuites.length})</h3>
        </div>
        <div style={{ display: 'grid', gap: 14 }}>
          {qaSuites.map((suite) => (
            <article key={suite.suiteId} className="ui-operation-card">
              <div className="ui-section-heading">
                <div>
                  <h4>{suite.suiteName}</h4>
                  <p className="ui-label">{suite.category}</p>
                </div>
                <StatusBadge label={suite.status} state={suiteState(suite.status)} />
              </div>
              <dl className="ui-fact-list">
                <div>
                  <dt>Definição</dt>
                  <dd>
                    <code>{suite.testDefinition}</code>
                  </dd>
                </div>
                <div>
                  <dt>Último registo</dt>
                  <dd>{suite.executedAt ?? 'Não disponível'}</dd>
                </div>
                <div>
                  <dt>Ambiente registado</dt>
                  <dd>{suite.environment}</dd>
                </div>
                <div>
                  <dt>Referência de evidência</dt>
                  <dd>{suite.evidenceReference}</dd>
                </div>
              </dl>
              {suite.limitations.length > 0 && (
                <div className="ui-notice ui-warning">
                  <strong>Limitações</strong>
                  <ul>
                    {suite.limitations.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </div>
              )}
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}
