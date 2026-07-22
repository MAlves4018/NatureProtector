import { useEffect, useRef } from 'react';
import { AlertTriangle, X } from 'lucide-react';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'default';
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  variant = 'default',
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const confirmRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (open) {
      confirmRef.current?.focus();
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onCancel]);

  if (!open) return null;

  return (
    <div className="ui-overlay" role="dialog" aria-modal="true" aria-label={title}>
      <div className="ui-confirm-dialog">
        <div className="ui-section-heading">
          <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {variant === 'danger' && <AlertTriangle size={18} />}
            {title}
          </h3>
          <button type="button" className="ui-alert-dismiss" onClick={onCancel} aria-label="Fechar">
            <X size={16} />
          </button>
        </div>
        <p style={{ marginBottom: 16 }}>{message}</p>
        <div className="ui-button-row" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="ui-secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
            type="button"
            className={variant === 'danger' ? 'ui-button ui-button-danger' : 'ui-button'}
            ref={confirmRef}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
