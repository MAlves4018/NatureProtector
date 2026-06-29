import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function AdminPage() {
  const { copy, adminActions } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('admin.title')} subtitle={copy('admin.subtitle')} helpTopic="requestedResolved" />
      <div className="ui-v2-table-wrap">
        <table className="ui-v2-table">
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
            {adminActions.map((action) => (
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
    </section>
  );
}
