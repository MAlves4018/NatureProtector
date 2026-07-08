import type { UiTechnicalField } from '../technicalSurfaces';
import { useUiLocale } from '../state/LocaleContext';
import { StatusBadge } from './StatusBadge';

export function TechnicalDetail({ title, fields }: { title: string; fields: readonly UiTechnicalField[] }) {
  const { copy } = useUiLocale();

  if (fields.length === 0) {
    return null;
  }

  return (
    <details className="ui-details">
      <summary>{title}</summary>
      <div className="ui-table-wrap">
        <table className="ui-table">
          <thead>
            <tr>
              <th>{copy('technical.category')}</th>
              <th>{copy('technical.status')}</th>
              <th>Valor</th>
              <th>{copy('technical.source')}</th>
              <th>{copy('technical.scope')}</th>
              <th>{copy('technical.timestamp')}</th>
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
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}
