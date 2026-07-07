import { X, AlertTriangle, Info, AlertCircle, Bell } from 'lucide-react';
import { useUiAlerts } from '../state/AlertContext';

const SEVERITY_ICONS = {
  info: Info,
  warning: AlertTriangle,
  critical: AlertCircle,
} as const;

const SEVERITY_CLASSES = {
  info: 'ui-alert-info',
  warning: 'ui-alert-warning',
  critical: 'ui-alert-critical',
} as const;

export function AlertBanner() {
  const { activeAlerts, dismissAlert } = useUiAlerts();

  if (activeAlerts.length === 0) return null;

  return (
    <section className="ui-alert-banner" aria-label="Alertas ativos">
      <div className="ui-alert-header">
        <Bell size={14} />
        <span>
          {activeAlerts.length} alerta{activeAlerts.length !== 1 ? 's' : ''} ativo{activeAlerts.length !== 1 ? 's' : ''}
        </span>
      </div>
      <div className="ui-alert-list">
        {activeAlerts.map((alert) => {
          const Icon = SEVERITY_ICONS[alert.severity];
          return (
            <div key={alert.id} className={`ui-alert-item ${SEVERITY_CLASSES[alert.severity]}`}>
              <Icon size={18} className="ui-alert-icon" />
              <div className="ui-alert-content">
                <strong>{alert.title}</strong>
                <p>{alert.message}</p>
              </div>
              <button
                type="button"
                className="ui-alert-dismiss"
                onClick={() => dismissAlert(alert.id)}
                aria-label={`Dismissir alerta: ${alert.title}`}
              >
                <X size={14} />
              </button>
            </div>
          );
        })}
      </div>
    </section>
  );
}
