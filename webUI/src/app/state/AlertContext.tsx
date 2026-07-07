import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useUiArea } from './AreaContext';
import { api } from '../services/api';
import { AlertStateResponse } from '../types';

interface UiAlertContextValue {
  alerts: AlertStateResponse[];
  loading: boolean;
  error: string | null;
  dismissAlert: (id: string) => void;
  activeAlerts: AlertStateResponse[];
}

const UiAlertContext = createContext<UiAlertContextValue | null>(null);

export function UiAlertProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode } = useUiArea();
  const [alerts, setAlerts] = useState<AlertStateResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dismissedIdsRef = useRef<Set<string>>(new Set());

  useEffect(() => {
    if (!resolvedAreaCode) {
      setAlerts([]);
      return;
    }

    let cancelled = false;

    const fetchAlerts = () => {
      api.getAlerts(resolvedAreaCode)
        .then((data) => {
          if (!cancelled) setAlerts(data);
        })
        .catch(() => {
          if (!cancelled) setError('Failed to fetch alerts');
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    };

    fetchAlerts();
    const interval = setInterval(fetchAlerts, 30000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [resolvedAreaCode]);

  const dismissAlert = useCallback((id: string) => {
    dismissedIdsRef.current.add(id);
    setAlerts((prev) => prev.filter((a) => a.id !== id));
  }, []);

  const activeAlerts = useMemo(
    () =>
      alerts
        .filter((a) => !dismissedIdsRef.current.has(a.id))
        .sort((a, b) => new Date(b.triggeredAt).getTime() - new Date(a.triggeredAt).getTime())
        .slice(0, 3),
    [alerts],
  );

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
