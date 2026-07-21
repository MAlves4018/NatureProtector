import { useState } from 'react';
import { Cloud, Terminal, History } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';
import { EnvironmetCardMap } from '../components/EnvironmetCardMap';

export function CloudResourcesPage() {
  const { environments, catalog, operations } = useOperations();
  const definitions = catalog.filter((definition) => definition.category === 'cloud');
  const cloudOperations = operations.filter((operation) => operation.category === 'cloud');
  const [tab, setTab] = useState<'environments' | 'operations' | 'history'>('environments');

  return (
    <section className="ui-page">
      <PageHeader
        title="Cloud Resources"
        subtitle="Inventário declarado, operações limitadas e fronteira explícita entre configuração e observação live."
        helpTopic="pipeline"
      />
      <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
        <button
          type="button"
          className={tab === 'environments' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'environments'}
          onClick={() => setTab('environments')}
        >
          <Cloud size={16} />
          Ambientes
        </button>
        <button
          type="button"
          className={tab === 'operations' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'operations'}
          onClick={() => setTab('operations')}
        >
          <Terminal size={16} />
          Operações
        </button>
        <button
          type="button"
          className={tab === 'history' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'history'}
          onClick={() => setTab('history')}
        >
          <History size={16} />
          Histórico
        </button>
      </div>
      {tab === 'environments' && (
        <EnvironmetCardMap environments={environments}/>
      )}
      {tab === 'operations' && (
        <section className="ui-panel">
          <h2>Operações cloud fechadas</h2>
          <div className="ui-grid ui-grid-wide">
            {definitions.map((definition) => (
              <OperationLauncher key={definition.operationId} definition={definition} />
            ))}
          </div>
        </section>
      )}
      {tab === 'history' && (
        <section className="ui-panel">
          <h2>Histórico</h2>
          <div className="ui-grid">
            {cloudOperations.map((operation) => (
              <OperationStatus key={operation.id} operation={operation} />
            ))}
          </div>
        </section>
      )}
    </section>
  );
}
