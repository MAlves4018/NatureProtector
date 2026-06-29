import type { UiV2TechnicalField } from '../technicalSurfaces';
import { useUiV2 } from '../state/UiV2Context';
import { StatusBadge } from './StatusBadge';

export function TechnicalDetail({ title, fields }: { title: string; fields: readonly UiV2TechnicalField[] }) {
  const { copy } = useUiV2();

  if (fields.length === 0) {
    return null;
  }

  return (
    <details className="ui-v2-details">
      <summary>{title}</summary>
      <div className="ui-v2-table-wrap">
        <table className="ui-v2-table">
          <thead>
            <tr>
              <th>{copy('technical.category')}</th>
              <th>{copy('technical.status')}</th>
              <th>Valor</th>
              <th>{copy('technical.source')}</th>
              <th>{copy('technical.scope')}</th>
              <th>{copy('technical.timestamp')}</th>
              <th>{copy('technical.limitation')}</th>
            </tr>
          </thead>
          <tbody>
            {fields.map((field) => (
              <tr key={`${field.label}-${field.source}`}>
                <td>{field.label}</td>
                <td>
                  <StatusBadge label={field.state} state={field.state} />
                </td>
                <td>{field.value}</td>
                <td>{field.source}</td>
                <td>{field.scope}</td>
                <td>{field.timestamp}</td>
                <td>{field.limitation || copy('value.noneReported')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}
