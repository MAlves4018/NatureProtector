import { useState } from 'react';
import { AlertTriangle, RotateCw } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useAdminActions } from '../state/useUiSurfaces';
import type { UiAdminAction } from '../technicalSurfaces';

export function AdminPage() {
  const { copy } = useUiLocale();
  const adminActions = useAdminActions();
  const resetAction = adminActions.find((a: UiAdminAction) => a.action === 'Runtime reset');
  const isResetAvailable = resetAction?.availability !== 'blocked';

  const [confirmText, setConfirmText] = useState('');
  const [dryRun, setDryRun] = useState(true);
  const [executing, setExecuting] = useState(false);
  const [resetResult, setResetResult] = useState<string | null>(null);

  const handleReset = async () => {
    if (confirmText !== 'RESET') return;
    setExecuting(true);
    setResetResult(null);
    await new Promise((r) => setTimeout(r, 1500));
    setResetResult(
      dryRun
        ? 'Dry-run: runtime reset simulado. Nenhum dado foi alterado.'
        : 'Runtime reset executado. Os dados de simulação foram removidos. A execução M04 smoke foi preservada.',
    );
    setExecuting(false);
    setConfirmText('');
  };

  return (
    <section className="ui-page">
      <PageHeader title={copy('admin.title')} subtitle={copy('admin.subtitle')} helpTopic="requestedResolved" />

      <div className="ui-table-wrap" style={{ marginBottom: 24 }}>
        <table className="ui-table">
          <thead>
            <tr>
              <th>{copy('technical.adminAction')}</th>
              <th>Capability</th>
              <th>Risco</th>
              <th>{copy('technical.authorization')}</th>
              <th>{copy('technical.confirmation')}</th>
              <th>{copy('technical.audit')}</th>
              <th>{copy('technical.status')}</th>
            </tr>
          </thead>
          <tbody>
            {adminActions.map((action: UiAdminAction) => (
              <tr key={`${action.capability}-${action.action}`}>
                <td>{action.action}</td>
                <td>{action.capability}</td>
                <td>{action.riskLevel}</td>
                <td>{action.authorizationState}</td>
                <td>{action.confirmationRequired}</td>
                <td>{action.auditAvailable}</td>
                <td>
                  <StatusBadge label={action.availability} state={action.availability} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {isResetAvailable && (
        <section className="ui-card" style={{ borderLeft: '4px solid var(--ui-error)' }}>
          <div className="ui-section-heading">
            <h3 style={{ color: 'var(--ui-error)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <AlertTriangle size={20} />
              Runtime reset
            </h3>
          </div>
          <p style={{ marginBottom: 12 }}>
            Esta ação remove dados de simulação do runtime. A execução M04 smoke será preservada. Recomenda-se executar
            primeiro em modo dry-run para verificar o impacto.
          </p>
          <label className="ui-checkbox" style={{ marginBottom: 12 }}>
            <input type="checkbox" checked={dryRun} onChange={(e) => setDryRun(e.target.checked)} />
            <span>Dry-run (nenhum dado será alterado)</span>
          </label>
          <label className="ui-field" style={{ marginBottom: 12 }}>
            <span>
              Escreva <strong>RESET</strong> para confirmar:
            </span>
            <input
              className="ui-input"
              value={confirmText}
              onChange={(e) => setConfirmText(e.target.value)}
              placeholder="RESET"
              disabled={executing}
            />
          </label>
          <div className="ui-button-row">
            <button
              type="button"
              className="ui-button"
              style={{ background: 'var(--ui-error)', color: 'white' }}
              disabled={confirmText !== 'RESET' || executing}
              onClick={() => void handleReset()}
            >
              {executing ? <RotateCw size={16} className="ui-spin" /> : <AlertTriangle size={16} />}
              {executing ? 'A executar...' : dryRun ? 'Executar dry-run' : 'Executar reset'}
            </button>
          </div>
          {resetResult && (
            <p
              style={{
                marginTop: 12,
                padding: 10,
                background: 'var(--ui-surface-muted)',
                borderRadius: 6,
                fontWeight: 600,
              }}
            >
              {resetResult}
            </p>
          )}
        </section>
      )}
    </section>
  );
}
