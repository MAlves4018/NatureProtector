import { ContextualHelp } from './ContextualHelp';
import { StatusBadge } from './StatusBadge';
import { useUiV2 } from '../state/UiV2Context';
import type { UiV2ContextField } from '../outputContext';
import { DataStatusDetails } from './DataStatusDetails';

export function DataStatusSummary({ showDetails = true }: { showDetails?: boolean }) {
  const { copy, riskModel } = useUiV2();
  const freshness = fieldValue(riskModel.contextFields, 'freshness', copy('value.notConfirmed'));
  const coverage = fieldValue(riskModel.contextFields, 'coverage', copy('value.notConfirmed'));
  const eligibility = fieldValue(riskModel.contextFields, 'eligibility', copy('value.notConfirmed'));
  const limitation = riskModel.limitations[0] ?? copy('value.noneReported');

  return (
    <section className="ui-v2-card ui-v2-data-status" aria-label="Data Status">
      <div className="ui-v2-section-heading">
        <h3>Data Status</h3>
        <StatusBadge label={copy(stateLabelKey(riskModel.state))} state={riskModel.state} />
        <ContextualHelp topicId="freshness" />
      </div>
      <div className="ui-v2-status-row" aria-label="Data Status summary">
        <StatusItem label="Estado geral" value={copy(stateLabelKey(riskModel.state))} />
        <StatusItem label="Atualidade" value={freshness} />
        <StatusItem label="Cobertura" value={coverage} />
        <StatusItem label="Utilizacao do resultado" value={eligibility} />
        <StatusItem label="Limitacao principal" value={limitation} />
      </div>
      {showDetails && <DataStatusDetails />}
    </section>
  );
}

function StatusItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="ui-v2-status-item">
      <span className="ui-v2-label">{label}</span>
      <span className="ui-v2-value">{value}</span>
    </div>
  );
}

function fieldValue(fields: UiV2ContextField[], key: string, fallback: string) {
  return fields.find(field => field.key === key)?.value ?? fallback;
}

function stateLabelKey(state: string) {
  switch (state) {
    case 'loading':
      return 'state.loading';
    case 'partial':
      return 'state.partial';
    case 'stale':
      return 'state.stale';
    case 'blocked':
      return 'state.blocked';
    case 'error':
      return 'state.error';
    case 'access-denied':
      return 'state.accessDenied';
    case 'no-data':
      return 'state.noData';
    default:
      return 'state.unknown';
  }
}
