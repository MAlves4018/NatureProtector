import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { X, CheckCircle2, AlertTriangle, Info, AlertCircle } from 'lucide-react';

export type ToastSeverity = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: string;
  severity: ToastSeverity;
  title: string;
  message?: string;
}

interface UiToastContextValue {
  toasts: Toast[];
  addToast: (toast: Omit<Toast, 'id'>) => void;
  removeToast: (id: string) => void;
}

const UiToastContext = createContext<UiToastContextValue | null>(null);

const TOAST_ICONS: Record<ToastSeverity, typeof Info> = {
  success: CheckCircle2,
  error: AlertCircle,
  warning: AlertTriangle,
  info: Info,
};

const TOAST_COLORS: Record<ToastSeverity, string> = {
  success: 'var(--ui-success)',
  error: 'var(--ui-error)',
  warning: 'var(--ui-warning)',
  info: 'var(--ui-accent)',
};

export function UiToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const addToast = useCallback((toast: Omit<Toast, 'id'>) => {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    setToasts((prev) => [...prev, { ...toast, id }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 5000);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const value = useMemo(() => ({ toasts, addToast, removeToast }), [toasts, addToast, removeToast]);

  return (
    <UiToastContext.Provider value={value}>
      {children}
      <section className="ui-toast-container" aria-label="Notificacoes">
        {toasts.map((toast) => {
          const Icon = TOAST_ICONS[toast.severity];
          return (
            <div
              key={toast.id}
              className="ui-toast"
              style={{ borderLeft: `4px solid ${TOAST_COLORS[toast.severity]}` }}
            >
              <Icon size={18} style={{ color: TOAST_COLORS[toast.severity], flexShrink: 0 }} />
              <div style={{ flex: 1 }}>
                <strong style={{ fontSize: '0.85rem' }}>{toast.title}</strong>
                {toast.message && (
                  <p style={{ fontSize: '0.8rem', margin: 0, color: 'var(--ui-muted)' }}>{toast.message}</p>
                )}
              </div>
              <button type="button" className="ui-alert-dismiss" onClick={() => removeToast(toast.id)}>
                <X size={14} />
              </button>
            </div>
          );
        })}
      </section>
    </UiToastContext.Provider>
  );
}

export function useUiToast() {
  const context = useContext(UiToastContext);
  if (!context) throw new Error('useUiToast must be used within UiToastProvider');
  return context;
}
