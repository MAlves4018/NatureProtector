import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { useToken } from '../../context/TokenContext';
import { X } from 'lucide-react';
import { User } from '../../types';

type UserModalProps = {
  isDark: boolean;
  user: User | null;
  isOpen: boolean;
  onClose: () => void;
};

export function UserModal({ isDark, user, isOpen, onClose }: UserModalProps) {
  const { logout } = useToken();
  const nav = useNavigate();

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  const handleLogout = () => {
    logout();
    onClose();
  };

  if (!isOpen) return null;

  return createPortal(
    <div className="ui-overlay" role="dialog" aria-modal="true" aria-label="User information" data-theme={isDark ? 'dark' : 'light'} style={{ zIndex: 1000 }}>
      <div className="ui-confirm-dialog" style={{ maxWidth: 'min(360px, 90vw)', width: '100%', borderTop: '4px solid var(--ui-primary)' }}>
        <div className="ui-section-heading">
          <h3>{user ? 'User Information' : 'Not signed in'}</h3>
          <button type="button" className="ui-alert-dismiss" onClick={onClose} aria-label="Close user dialog">
            <X size={16} />
          </button>
        </div>
        {user ? (
          <>
            <p><strong>Welcome,</strong> {user.fullName}</p>
            <p><strong>Email:</strong> {user.email}</p>
            <p><strong>Roles:</strong> {user.roles.join(', ')}</p>
            <button type="button" className="ui-button ui-button-danger" onClick={handleLogout}>
              Logout
            </button>
          </>
        ) : (
          <>
            <p>You are not logged in.</p>
            <button
              type="button"
              className="ui-button"
              onClick={() => {
                onClose();
                nav('/login');
              }}
            >
              Go to Login
            </button>
          </>
        )}
      </div>
    </div>,
    document.body,
  );
}
