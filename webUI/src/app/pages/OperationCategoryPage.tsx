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

  return (
    <section className="ui-page">
      <PageHeader title={title} subtitle={subtitle} helpTopic="qa" />
      {error && <p className="ui-notice ui-error">{error.message}</p>}
      {loading && <p className="ui-notice">A carregar catálogo e execuções…</p>}
      <section className="ui-panel">
        <h2>Catálogo autorizado</h2>
        <div className="ui-grid ui-grid-wide">
          {definitions.map((definition) => (
            <OperationLauncher key={definition.operationId} definition={definition} />
          ))}
        </div>
      </section>
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
    </section>
  );
}


