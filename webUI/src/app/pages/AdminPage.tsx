import { useState } from 'react';
import { AlertTriangle, RotateCw } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useAdminActions } from '../state/useUiSurfaces';
import type { UiAdminAction } from '../technicalSurfaces';
import { api } from '../services/api';
import { RuntimeResetRequest } from '../types';
import type { RuntimeResetResponse } from '../types';

export function AdminPage() {
  const { copy } = useUiLocale();
  const adminActions = useAdminActions();
  const resetAction = adminActions.find((a: UiAdminAction) => a.action === 'Runtime reset');
  const isResetAvailable = resetAction?.availability !== 'blocked';

  const [confirmText, setConfirmText] = useState('');
  const [dryRun, setDryRun] = useState(true);
  const [executing, setExecuting] = useState(false);
  const [resetResult, setResetResult] = useState<RuntimeResetResponse | null>(null);
  const [resetError, setResetError] = useState<string | null>(null);

  const handleReset = async () => {
    if (confirmText !== 'RESET_RUNTIME_STATE') return;
    setExecuting(true);
    setResetResult(null);
    setResetError(null);
    try {
      const result = await api.resetRuntimeState({
        scope: 'runtime-only',
        confirm: confirmText,
        dryRun: dryRun,
      } as RuntimeResetRequest);
      setResetResult(result);
      setConfirmText('');
    } catch (value) {
      setResetError(value instanceof Error ? value.message : 'O reset não foi aceite pelo backend.');
    } finally {
      setExecuting(false);
    }
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
        <section className="ui-card">
          <div className="ui-section-heading">
            <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <AlertTriangle size={20} />
              Runtime reset
            </h3>
          </div>
          <p style={{ marginBottom: 12 }}>
            Esta ação pede ao backend um reset do estado runtime. O backend valida atividade, inbox, filas e stores
            antes da fase destrutiva. Um reset bloqueado é uma proteção operacional, não uma falha do sistema.
          </p>
          <label className="ui-checkbox" style={{ marginBottom: 12 }}>
            <input type="checkbox" checked={dryRun} onChange={(e) => setDryRun(e.target.checked)} />
            <span>Dry-run (nenhum dado será alterado)</span>
          </label>
          <label className="ui-field" style={{ marginBottom: 12 }}>
            <span>
              Escreva <strong>RESET_RUNTIME_STATE</strong> para confirmar:
            </span>
            <input
              className="ui-input"
              value={confirmText}
              onChange={(e) => setConfirmText(e.target.value)}
              placeholder="RESET_RUNTIME_STATE"
              disabled={executing}
            />
          </label>
          <div className="ui-button-row">
            <button
              type="button"
              className="ui-button ui-button-danger"
              disabled={confirmText !== 'RESET_RUNTIME_STATE' || executing}
              onClick={() => void handleReset()}
            >
              {executing ? <RotateCw size={16} className="ui-spin" /> : <AlertTriangle size={16} />}
              {executing ? 'A executar...' : dryRun ? 'Executar dry-run' : 'Executar reset'}
            </button>
          </div>
          {resetError && <p className="ui-notice ui-error">{resetError}</p>}
          {resetResult && (
            <section className="ui-reset-result" aria-live="polite">
              <div className="ui-section-heading">
                <h4>{resetResult.dryRun ? 'Pré-visualização do reset' : 'Resultado do reset'}</h4>
                <StatusBadge
                  label={resetResult.status}
                  state={resetResult.status.toLowerCase().includes('blocked') ? 'partial' : 'ready'}
                />
              </div>
              <p>{resetResult.message}</p>
              <div className="ui-review-summary">
                <span>
                  <small>Antes</small>
                  <strong>{sumCounts(resetResult.before)}</strong>
                </span>
                <span>
                  <small>Depois</small>
                  <strong>{sumCounts(resetResult.after)}</strong>
                </span>
              </div>
            </section>
          )}
        </section>
      )}
    </section>
  );
}

function sumCounts(rows: RuntimeResetResponse['before']) {
  return String(rows.reduce((total, row) => total + row.count, 0));
}
