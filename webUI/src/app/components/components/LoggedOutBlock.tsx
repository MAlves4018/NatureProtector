import { getColors } from '../../utils/utils';
import { backdropStyle, cardStyle, wrapperStyle } from '../styles/styleUtils';
import { useToken } from '../../context/TokenContext';
import { LogInOutProps } from '../../types';
import { AlertTriangle } from 'lucide-react';

export function LoggedOutBlock({ isDark, message, mode = 'panel' }: LogInOutProps) {
  const c = getColors(isDark);
  const { token } = useToken();

  const signedIn = Boolean(token);
  const isPanel = mode === 'panel';

  return (
    <div style={wrapperStyle(c, isPanel)}>
      <div style={backdropStyle(isPanel ? c.panelBg : c.pageBg)} />
      <section style={cardStyle(c, isPanel)}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '10px' }}>
          <div style={{ color: signedIn ? '#16a34a' : '#d97706' }}>
            <AlertTriangle style={{ width: '100%', height: '100%' }} />
          </div>
          <div>
            <div style={{ color: c.textPrimary, fontSize: '20px', fontWeight: 800 }}>
              {signedIn ? 'Access granted' : 'Sign In Required'}
            </div>
          </div>
        </div>
        <p style={{ color: c.textSecond, textAlign: 'center', marginTop: '12px' }}>{message}</p>
      </section>
    </div>
  );
}
