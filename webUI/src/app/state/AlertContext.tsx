import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useUiArea } from './AreaContext';

export interface Alert {
  id: string;
  severity: 'info' | 'warning' | 'critical';
  title: string;
  message: string;
  timestamp: string;
  dismissed: boolean;
}

interface UiAlertContextValue {
  alerts: Alert[];
  loading: boolean;
  error: string | null;
  dismissAlert: (id: string) => void;
  activeAlerts: Alert[];
}

const UiAlertContext = createContext<UiAlertContextValue | null>(null);

function generateMockAlerts(areaCode: string | null): Alert[] {
  if (!areaCode) return [];
  return [
    {
      id: 'alert-1',
      severity: 'info',
      title: 'Atualizacao de dados',
      message: `Novos dados de sensores disponiveis para a area ${areaCode}.`,
      timestamp: new Date().toISOString(),
      dismissed: false,
    },
    {
      id: 'alert-2',
      severity: 'warning',
      title: 'Risco elevado',
      message: `Condicoes meteorologicas na area ${areaCode} indicam risco elevado de incendio.`,
      timestamp: new Date().toISOString(),
      dismissed: false,
    },
    {
      id: 'alert-3',
      severity: 'critical',
      title: 'Alerta de incendio',
      message: `Potencial inicio de incendio detetado na area ${areaCode}. Verificar dados de sensores.`,
      timestamp: new Date().toISOString(),
      dismissed: false,
    },
  ];
}

export function UiAlertProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode } = useUiArea();
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!resolvedAreaCode) {
      setAlerts([]);
      return;
    }

    setLoading(true);
    setError(null);

    const fetchAlerts = () => {
      setAlerts(generateMockAlerts(resolvedAreaCode));
      setLoading(false);
    };

    fetchAlerts();
    const interval = setInterval(fetchAlerts, 30000);
    return () => clearInterval(interval);
  }, [resolvedAreaCode]);

  const dismissAlert = useCallback((id: string) => {
    setAlerts((prev) => prev.map((a) => (a.id === id ? { ...a, dismissed: true } : a)));
  }, []);

  const activeAlerts = useMemo(() => alerts.filter((a) => !a.dismissed), [alerts]);

  const value = useMemo(
    () => ({ alerts, loading, error, dismissAlert, activeAlerts }),
    [alerts, loading, error, dismissAlert, activeAlerts],
  );

  return <UiAlertContext.Provider value={value}>{children}</UiAlertContext.Provider>;
}

export function useUiAlerts() {
  const context = useContext(UiAlertContext);
  if (!context) {
    throw new Error('useUiAlerts must be used within UiAlertProvider');
  }
  return context;
}
