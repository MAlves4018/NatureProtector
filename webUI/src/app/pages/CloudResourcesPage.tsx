import { PageHeader } from '../components/PageHeader';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';

export function CloudResourcesPage() {
  const { environments, catalog, operations } = useOperations();
  const definitions = catalog.filter((definition) => definition.category === 'cloud');
  const cloudOperations = operations.filter((operation) => operation.category === 'cloud');

  return (
    <section className="ui-page">
      <PageHeader
        title="Cloud Resources"
        subtitle="Inventário declarado, operações limitadas e fronteira explícita entre configuração e observação live."
        helpTopic="pipeline"
      />
      <section className="ui-panel">
        <h2>Mapa de ambientes</h2>
        <div className="ui-grid">
          {environments.map((environment) => (
            <article className="ui-card" key={environment.environment}>
              <div className="ui-section-heading">
                <h3>{environment.environment}</h3>
                <span className="ui-operation-status">{environment.observedState}</span>
              </div>
              <div className="ui-fact-list">
                <span>
                  <strong>Project</strong>
                  {environment.projectId}
                </span>
                <span>
                  <strong>Region</strong>
                  {environment.region}
                </span>
                <span>
                  <strong>Deployable</strong>
                  {String(environment.deployable)}
                </span>
                <span>
                  <strong>Evidence</strong>
                  {environment.evidenceLevel}
                </span>
              </div>
              <table className="ui-table">
                <thead>
                  <tr>
                    <th>Tipo</th>
                    <th>Nome</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {environment.resources.map((resource) => (
                    <tr key={`${environment.environment}-${resource.resourceType}-${resource.name}`}>
                      <td>{resource.resourceType}</td>
                      <td>{resource.name}</td>
                      <td>{resource.state}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </article>
          ))}
        </div>
      </section>
      <section className="ui-panel">
        <h2>Operações cloud fechadas</h2>
        <div className="ui-grid ui-grid-wide">
          {definitions.map((definition) => (
            <OperationLauncher key={definition.operationId} definition={definition} />
          ))}
        </div>
      </section>
      <section className="ui-panel">
        <h2>Histórico</h2>
        <div className="ui-grid">
          {cloudOperations.map((operation) => (
            <OperationStatus key={operation.id} operation={operation} />
          ))}
        </div>
      </section>
    </section>
  );
}


