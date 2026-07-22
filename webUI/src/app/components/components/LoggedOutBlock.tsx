import { useToken } from '../../context/TokenContext';
import { LogInOutProps } from '../../types';
import { AlertTriangle } from 'lucide-react';

export function LoggedOutBlock({ isDark, message, mode = 'panel' }: LogInOutProps) {
  const { token } = useToken();
  const signedIn = Boolean(token);
  const isPanel = mode === 'panel';

  return (
    <div className={`ui-auth-wrapper${isPanel ? ' ui-auth-wrapper-panel' : ''}`} data-theme={isDark ? 'dark' : 'light'}>
      <div className="ui-auth-backdrop" />
      <section className={`ui-auth-card${isPanel ? ' ui-auth-card-panel' : ''}`}>
        <div className="ui-button-row" style={{ justifyContent: 'center', gap: '10px' }}>
          <div>
            <AlertTriangle style={{ width: '100%', height: '100%' }} />
          </div>
          <div>
            <div style={{ fontSize: '20px', fontWeight: 800 }}>{signedIn ? 'Access granted' : 'Sign In Required'}</div>
          </div>
        </div>
        <p style={{ textAlign: 'center', marginTop: '12px' }}>{message}</p>
      </section>
    </div>
  );
}
