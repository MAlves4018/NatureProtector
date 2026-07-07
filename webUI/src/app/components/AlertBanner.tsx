import { X, AlertTriangle, Info, AlertCircle, Bell } from 'lucide-react';
import { useUiAlerts } from '../state/AlertContext';

const SEVERITY_MAP = {
  info: { icon: Info, className: 'ui-alert-info' },
  warning: { icon: AlertTriangle, className: 'ui-alert-warning' },
  critical: { icon: AlertCircle, className: 'ui-alert-critical' },
} as const;

function getSeverityStyle(severity: string) {
  return SEVERITY_MAP[severity as keyof typeof SEVERITY_MAP] ?? SEVERITY_MAP.info;
}

export function AlertBanner() {
  const { activeAlerts, dismissAlert } = useUiAlerts();

  if (activeAlerts.length === 0) return null;

  return (
    <div className="ui-alert-banner" role="region" aria-label="Alertas ativos">
      <div className="ui-alert-header">
        <Bell size={14} />
        <span>{activeAlerts.length} alerta{activeAlerts.length !== 1 ? 's' : ''} ativo{activeAlerts.length !== 1 ? 's' : ''}</span>
      </div>
      <div className="ui-alert-list">
        {activeAlerts.map((alert) => {
          const { icon: Icon, className: severityClass } = getSeverityStyle(alert.severity);
          return (
            <div key={alert.id} className={`ui-alert-item ${severityClass}`}>
              <Icon size={18} className="ui-alert-icon" />
              <div className="ui-alert-content">
                <strong>{alert.alertCode}</strong>
                <p>{alert.message}</p>
              </div>
              <button
                type="button"
                className="ui-alert-dismiss"
                onClick={() => dismissAlert(alert.id)}
                aria-label={`Dismissir alerta: ${alert.alertCode}`}
              >
                <X size={14} />
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}
