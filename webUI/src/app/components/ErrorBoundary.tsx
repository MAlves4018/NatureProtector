import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[ErrorBoundary]', error, info.componentStack);
  }

  private handleRetry = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;
      return (
        <div className="ui-card" style={{ textAlign: 'center', padding: 24 }}>
          <AlertTriangle size={32} style={{ marginBottom: 12 }} />
          <h3>Algo correu mal</h3>
          <p style={{ marginBottom: 12, fontSize: '0.85rem' }}>
            {this.state.error?.message || 'Erro inesperado ao renderizar esta pagina.'}
          </p>
          <button type="button" className="ui-button" onClick={this.handleRetry}>
            <RefreshCw size={16} />
            Tentar novamente
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
