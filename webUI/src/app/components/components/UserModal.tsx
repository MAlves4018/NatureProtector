import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { useToken } from '../../context/TokenContext';
import { getColors } from '../../utils/utils';
import { User } from '../../types';

type UserModalProps = {
  isDark: boolean;
  user: User | null;
  isOpen: boolean;
  onClose: () => void;
};

export function UserModal({ isDark, user, isOpen, onClose }: UserModalProps) {
  const c = getColors(isDark);
  const { logout } = useToken();
  const nav = useNavigate();

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  const handleLogout = () => {
    logout();
    onClose();
  };

  if (!isOpen) {
    return null;
  }

  return createPortal(
    <div role="dialog" aria-modal="true" aria-label="User information" style={modalWrapperStyle()}>
      <button type="button" aria-label="Close user dialog" style={modalBackdropStyle(c)} onClick={onClose} />
      <div style={modalContentStyle(c)}>
        {user ? (
          <>
            <h2 style={titleStyle(c)}>User Information</h2>
            <p>
              <strong>Welcome,</strong> {user.fullName}
            </p>
            <p>
              <strong>Email:</strong> {user.email}
            </p>
            <p>
              <strong>Roles:</strong> {user.roles.join(', ')}
            </p>
            <button type="button" style={buttonStyle('logout')} onClick={handleLogout}>
              Logout
            </button>
          </>
        ) : (
          <>
            <p>You are not logged in.</p>
            <button
              type="button"
              style={buttonStyle('login')}
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

function modalWrapperStyle() {
  return {
    position: 'fixed' as const,
    inset: 0,
    display: 'grid',
    placeItems: 'center',
    zIndex: 1000,
  };
}

function modalBackdropStyle(_colors: ReturnType<typeof getColors>) {
  return {
    position: 'absolute' as const,
    inset: 0,
    background: `rgba(15, 23, 42, 0.6)`,
    backdropFilter: 'blur(4px)',
  };
}

function modalContentStyle(colors: ReturnType<typeof getColors>) {
  return {
    position: 'relative' as const,
    width: 'min(360px, 90vw)',
    backgroundColor: colors.panelBg,
    color: colors.textPrimary,
    borderRadius: '12px',
    border: `1px solid ${colors.panelBorder}`,
    padding: '20px',
    boxShadow: '0 24px 60px rgba(15, 23, 42, 0.4)',
    zIndex: 1,
  };
}

function titleStyle(colors: ReturnType<typeof getColors>) {
  return {
    margin: 0,
    marginBottom: '12px',
    fontSize: '18px',
    fontWeight: 700,
    color: colors.textPrimary,
  };
}

function buttonStyle(kind: 'login' | 'logout') {
  return {
    marginTop: '12px',
    padding: '8px 12px',
    borderRadius: '8px',
    border: 'none',
    backgroundColor: kind === 'logout' ? '#ef4444' : '#22c55e',
    color: '#ffffff',
    cursor: 'pointer',
  };
}
