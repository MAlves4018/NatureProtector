import { useState } from 'react';
import { LogIn, LogOut } from 'lucide-react';
import { api } from '../../services/api';
import { useToken } from '../../context/TokenContext';
import { useNavigate } from 'react-router-dom';
import { LogInOutProps } from '../../types';

export function LogInOut({ isDark, message, onAuthChange, mode = 'page' }: LogInOutProps) {
  const [usernameOrEmail, setUsernameOrEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { token, login, logout } = useToken();
  const nav = useNavigate();
  const isPanel = mode === 'panel';

  const handleLogin = async () => {
    setLoading(true);
    setError(null);
    try {
      const _resp = await login(usernameOrEmail, password);
      setPassword('');
      onAuthChange?.(true);
      nav('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sign in.');
      onAuthChange?.(false);
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    setLoading(true);
    setError(null);
    try {
      await api.logout();
      logout();
      onAuthChange?.(false);
      nav('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sign out.');
    } finally {
      setLoading(false);
    }
  };

  const signedIn = Boolean(token);

  return (
    <div className={`ui-auth-wrapper${isPanel ? ' ui-auth-wrapper-panel' : ''}`} data-theme={isDark ? 'dark' : 'light'}>
      <div className="ui-auth-backdrop" />
      <section className={`ui-auth-card${isPanel ? ' ui-auth-card-panel' : ''}`}>
        <div className="ui-button-row" style={{ gap: '10px' }}>
          <div className="ui-icon-ring">
            <img src="./images/NPIconNoBg.png" width={18} height={18} alt="" />
          </div>
          <div>
            <div style={{ fontSize: '20px', fontWeight: 800 }}>{signedIn ? 'Access granted' : 'Sign In Required'}</div>
          </div>
        </div>

        {signedIn ? (
          <div style={{ marginTop: '18px', display: 'grid', gap: '12px' }}>
            <button
              type="button"
              onClick={handleLogout}
              disabled={loading}
              className="ui-button ui-button-danger"
              style={{ width: '100%' }}
            >
              <LogOut size={16} /> Sign out
            </button>
          </div>
        ) : (
          <div style={{ marginTop: '18px', display: 'grid', gap: '12px' }}>
            <label className="ui-label" htmlFor="usernameOrEmail">
              Username or email
            </label>
            <input
              id="usernameOrEmail"
              className="ui-input"
              value={usernameOrEmail}
              onChange={(event) => setUsernameOrEmail(event.target.value)}
              placeholder="user@domain.pt"
              autoComplete="username"
            />
            <label className="ui-label" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              className="ui-input"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="password"
              autoComplete="current-password"
            />
            <button
              type="button"
              onClick={handleLogin}
              disabled={loading || !usernameOrEmail || !password}
              className="ui-button"
              style={{ width: '100%' }}
            >
              <LogIn size={16} /> {loading ? 'Signing in...' : 'Sign in'}
            </button>
          </div>
        )}

        {error && (
          <div className="ui-notice ui-error" style={{ marginTop: '12px' }}>
            {error}
          </div>
        )}
      </section>
    </div>
  );
}
