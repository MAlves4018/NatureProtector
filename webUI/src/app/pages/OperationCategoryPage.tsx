import { useState } from 'react';
import { BookOpen, History } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';

export function OperationCategoryPage({
  category,
  title,
  subtitle,
}: {
  category: string;
  title: string;
  subtitle: string;
}) {
  const { catalog, operations, loading, error } = useOperations();
  const definitions = catalog.filter((definition) => definition.category === category);
  const executions = operations.filter((operation) => operation.category === category);
  const [tab, setTab] = useState<'catalog' | 'history'>('catalog');

  return (
    <section className="ui-page">
      <PageHeader title={title} subtitle={subtitle} helpTopic="qa" />
      {error && <p className="ui-notice ui-error">{error.message}</p>}
      {loading && <p className="ui-notice">A carregar catálogo e execuções…</p>}
      <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
        <button
          type="button"
          className={tab === 'catalog' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'catalog'}
          onClick={() => setTab('catalog')}
        >
          <BookOpen size={16} />
          Catálogo autorizado
        </button>
        <button
          type="button"
          className={tab === 'history' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'history'}
          onClick={() => setTab('history')}
        >
          <History size={16} />
          Histórico auditável
        </button>
      </div>
      {tab === 'catalog' && (
        <section className="ui-panel">
          <h2>Catálogo autorizado</h2>
          <div className="ui-grid ui-grid-wide">
            {definitions.map((definition) => (
              <OperationLauncher key={definition.operationId} definition={definition} />
            ))}
          </div>
        </section>
      )}
      {tab === 'history' && (
        <section className="ui-panel">
          <h2>Histórico auditável</h2>
          {executions.length === 0 ? (
            <p className="ui-notice">Sem execuções registadas para esta categoria.</p>
          ) : (
            <div className="ui-grid">
              {executions.map((operation) => (
                <OperationStatus key={operation.id} operation={operation} />
              ))}
            </div>
          )}
        </section>
      )}
    </section>
  );
}
