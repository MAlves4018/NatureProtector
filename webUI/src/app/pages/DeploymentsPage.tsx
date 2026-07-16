import { Clock3, History, Rocket, ShieldAlert } from 'lucide-react';
import { useMemo, useState } from 'react';
import { PageHeader } from '../components/PageHeader';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';
import { formatDurationMs, operationDurationMs } from '../utils/operationalMetrics';

export function DeploymentsPage() {
  const { catalog, operations, loading, error } = useOperations();
  const definitions = catalog.filter((definition) => definition.category === 'deployment');
  const executions = operations.filter((operation) => operation.category === 'deployment');
  const environments = [...new Set(definitions.flatMap((definition) => definition.environments))];
  const [environment, setEnvironment] = useState(environments[0] ?? 'staging');
  const [selectedId, setSelectedId] = useState('');
  const selected = definitions.find((definition) => definition.operationId === selectedId) ?? null;
  const latest = executions[0] ?? null;
  const visibleDefinitions = useMemo(
    () => definitions.filter((definition) => definition.environments.includes(environment)),
    [definitions, environment],
  );

  return (
    <section className="ui-page">
      <PageHeader
        title="Deployments"
        subtitle="Catálogo, disponibilidade, pedido, provider e resultado terminal são apresentados como estados separados."
        helpTopic="requestedResolved"
      />
      <section className="ui-deployment-summary">
        <SummaryFact label="Provider" value={latest?.provider ?? 'Não confirmado'} />
        <SummaryFact
          label="Dispatcher"
          value={
            executions.some((operation) => operation.status === 'Queued') ? 'Pedidos em fila' : 'Sem fila observada'
          }
        />
        <SummaryFact label="Ambiente" value={environment} />
        <SummaryFact label="Operações disponíveis" value={String(visibleDefinitions.filter(isExecutable).length)} />
        <SummaryFact label="Último estado" value={latest?.status ?? 'Sem execução'} />
        <SummaryFact
          label="Última duração"
          value={latest ? formatDurationMs(operationDurationMs(latest)) : 'Não medido'}
        />
      </section>
      <div className="ui-notice ui-warning">
        <ShieldAlert size={16} />
        <span>
          <strong>Queued</strong> significa pedido registado ou aceite para dispatch. Não prova início, conclusão,
          deployment, rollback nem evidence verificada.
        </span>
      </div>
      {error && <p className="ui-notice ui-error">{error.message}</p>}
      {loading && <p className="ui-notice">A carregar catálogo e histórico…</p>}
      <section className="ui-card">
        <div className="ui-section-heading">
          <div>
            <span className="ui-eyebrow">Catálogo operacional</span>
            <h3>Operações de deployment</h3>
          </div>
          <label className="ui-field ui-inline-field">
            <span>Ambiente</span>
            <select value={environment} onChange={(event) => setEnvironment(event.target.value)}>
              {environments.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr>
                <th>Operação</th>
                <th>Risco</th>
                <th>Disponibilidade</th>
                <th>Última execução</th>
                <th>Duração</th>
                <th>Pedido por</th>
                <th>Evidence</th>
                <th>Ação</th>
              </tr>
            </thead>
            <tbody>
              {visibleDefinitions.map((definition) => {
                const last = executions.find((operation) => operation.operationId === definition.operationId);
                return (
                  <tr key={definition.operationId}>
                    <td>
                      <strong>{definition.displayName}</strong>
                      <small className="ui-table-note">{definition.description}</small>
                    </td>
                    <td>{definition.riskLevel}</td>
                    <td>
                      {definition.authorized ? definition.availability : 'Bloqueada por role'}
                      {!definition.authorized && (
                        <small className="ui-table-note">Requer {definition.requiredCapability}</small>
                      )}
                    </td>
                    <td>{last?.status ?? 'Nunca executada'}</td>
                    <td>{last ? formatDurationMs(operationDurationMs(last)) : 'Não medido'}</td>
                    <td>{last?.requestedBy ?? 'Não aplicável'}</td>
                    <td>{last?.evidenceLevel ?? definition.evidenceLevel}</td>
                    <td>
                      <button
                        type="button"
                        className="ui-secondary"
                        onClick={() => setSelectedId(definition.operationId)}
                      >
                        Abrir detalhe
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {visibleDefinitions.length === 0 && <p className="ui-notice">Sem operações para o ambiente selecionado.</p>}
      </section>
      {selected && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <div>
              <span className="ui-eyebrow">Detalhe progressivo</span>
              <h3>{selected.displayName}</h3>
            </div>
            <Rocket size={22} />
          </div>
          <OperationLauncher definition={selected} showTruthWarning={false} />
        </section>
      )}
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>
            <History size={18} /> Histórico
          </h3>
          <span className="ui-badge">{executions.length} registos</span>
        </div>
        {executions.length === 0 ? (
          <p className="ui-notice">Sem execuções registadas para deployments.</p>
        ) : (
          <div className="ui-grid">
            {executions.map((operation) => (
              <OperationStatus key={operation.id} operation={operation} compact />
            ))}
          </div>
        )}
      </section>
    </section>
  );
}

function SummaryFact({ label, value }: { label: string; value: string }) {
  return (
    <article>
      <Clock3 size={16} />
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function isExecutable(definition: { authorized: boolean; availability: string }) {
  return definition.authorized && definition.availability === 'implemented';
}
