import { technicalLabel, technicalLabelDetail } from '../content/technicalLabels';
import { useUiV2 } from '../state/UiV2Context';

export function DataStatusDetails() {
  const { copy, locale, riskModel } = useUiV2();

  return (
    <details className="ui-v2-details">
      <summary>{copy('provenance.details')}</summary>
      <div className="ui-v2-detail-grid">
        {riskModel.contextFields.map((field) => (
          <div key={field.key} className="ui-v2-detail-row">
            <span className="ui-v2-label">{copy(field.labelKey)}</span>
            <span className="ui-v2-value">{technicalLabel(field.value, locale)}</span>
            <small>{technicalLabelDetail(field.value, locale) || copy(field.helpKey)}</small>
          </div>
        ))}
      </div>
      <div className="ui-v2-limits">
        <span className="ui-v2-label">{copy('technical.limitation')}</span>
        <ul>
          {riskModel.limitations.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </div>
    </details>
  );
}
