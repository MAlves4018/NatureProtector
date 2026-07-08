import { technicalLabel, technicalLabelDetail } from '../content/technicalLabels';
import { useUiLocale } from '../state/LocaleContext';
import { useUiRisk } from '../state/RiskContext';

export function DataStatusDetails() {
  const { copy, locale } = useUiLocale();
  const { riskModel } = useUiRisk();

  return (
    <details className="ui-details">
      <summary>{copy('provenance.details')}</summary>
      <div className="ui-detail-grid">
        {riskModel.contextFields.map((field) => (
          <div key={field.key} className="ui-detail-row">
            <span className="ui-label">{copy(field.labelKey)}</span>
            <span className="ui-value">{technicalLabel(field.value, locale)}</span>
            <small>{technicalLabelDetail(field.value, locale) || copy(field.helpKey)}</small>
          </div>
        ))}
      </div>
    </details>
  );
}
