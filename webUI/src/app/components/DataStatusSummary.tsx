import { ContextualHelp } from './ContextualHelp';
import { StatusBadge } from './StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useUiRisk } from '../state/RiskContext';
import type { UiContextField } from '../outputContext';
import { DataStatusDetails } from './DataStatusDetails';

export function DataStatusSummary({ showDetails = true }: { showDetails?: boolean }) {
  const { copy } = useUiLocale();
  const { riskModel } = useUiRisk();
  const freshness = fieldValue(riskModel.contextFields, 'freshness', copy('value.notConfirmed'));
  const coverage = fieldValue(riskModel.contextFields, 'coverage', copy('value.notConfirmed'));
  const eligibility = fieldValue(riskModel.contextFields, 'eligibility', copy('value.notConfirmed'));


  return (
    <section className="ui-card ui-data-status" aria-label="Estado dos dados">
      <div className="ui-section-heading">
        <h3>Estado dos dados</h3>
        <StatusBadge label={copy(stateLabelKey(riskModel.state))} state={riskModel.state} />
        <ContextualHelp topicId="freshness" />
      </div>
      <div className="ui-status-row">
        <StatusItem label="Estado geral" value={copy(stateLabelKey(riskModel.state))} />
        <StatusItem label="Atualidade" value={freshness} />
        <StatusItem label="Cobertura" value={coverage} />
        <StatusItem label="Utilizacao do resultado" value={eligibility} />
      </div>
      {showDetails && <DataStatusDetails />}
    </section>
  );
}

function StatusItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="ui-status-item">
      <span className="ui-label">{label}</span>
      <span className="ui-value">{value}</span>
    </div>
  );
}

function fieldValue(fields: UiContextField[], key: string, fallback: string) {
  return fields.find((field) => field.key === key)?.value ?? fallback;
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