import { useState, useCallback } from 'react';
import { Play, Terminal, Database } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';

type DbEngine = 'pgsql' | 'influx';

interface QueryResult {
  columns: string[];
  rows: Record<string, string>[];
  rowCount: number;
  durationMs: number;
}

function simulateQuery(engine: DbEngine, query: string): Promise<QueryResult> {
  const delay = 300 + Math.random() * 700;
  return new Promise((resolve, reject) => {
    setTimeout(() => {
      const upper = query.trim().toUpperCase();
      if (
        !upper.startsWith('SELECT') &&
        !upper.startsWith('SHOW') &&
        !upper.startsWith('DESCRIBE') &&
        !upper.startsWith('EXPLAIN')
      ) {
        reject(new Error('Apenas queries de leitura sao permitidas (SELECT, SHOW, DESCRIBE, EXPLAIN).'));
        return;
      }
      resolve({
        columns: ['result'],
        rows: [{ result: `[${engine === 'pgsql' ? 'PostgreSQL' : 'InfluxDB'}] Query executada: ${query.substring(0, 60)}...` }],
        rowCount: 1,
        durationMs: Math.round(delay),
      });
    }, delay);
  });
}

const ENGINE_LABELS: Record<DbEngine, string> = {
  pgsql: 'PostgreSQL',
  influx: 'InfluxDB',
};

export function DatabaseQueriesPage() {
  const [activeEngine, setActiveEngine] = useState<DbEngine>('pgsql');
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<QueryResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [executing, setExecuting] = useState(false);

  const handleExecute = useCallback(async () => {
    if (!query.trim()) return;
    setExecuting(true);
    setError(null);
    setResult(null);
    try {
      const res = await simulateQuery(activeEngine, query);
      setResult(res);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro desconhecido');
    } finally {
      setExecuting(false);
    }
  }, [query, activeEngine]);

  return (
    <section className="ui-page">
      <PageHeader
        title="Database Queries - Ainda incompleto, apenas mock de queries"
        subtitle="Acesso controlado a queries de leitura nas bases de dados do sistema."
        helpTopic="pipeline"
      />

      <div className="ui-notice" style={{ marginBottom: 16, padding: '8px 12px', borderRadius: 6, background: 'var(--ui-warning-bg)', color: 'var(--ui-warning)' }}>
        <strong>⚠ Aviso:</strong> Apenas queries de leitura permitidas. Operacoes de escrita (INSERT, UPDATE, DELETE, DROP) sao bloqueadas.
      </div>

      <div className="ui-tabs">
        {(Object.keys(ENGINE_LABELS) as DbEngine[]).map((engine) => (
          <button
            key={engine}
            type="button"
            className={`ui-tab${activeEngine === engine ? ' ui-tab-active' : ''}`}
            onClick={() => { setActiveEngine(engine); setResult(null); setError(null); }}
          >
            {engine === 'pgsql' ? <Database size={16} /> : <Terminal size={16} />}
            {ENGINE_LABELS[engine]}
          </button>
        ))}
      </div>

      <div className="ui-card" style={{ marginTop: 0 }}>
        <label className="ui-label" htmlFor="query-input">
          Query {ENGINE_LABELS[activeEngine]}
        </label>
        <textarea
          id="query-input"
          className="ui-query-editor"
          rows={6}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={`Digite sua query ${ENGINE_LABELS[activeEngine]}`}
          disabled={executing}
          spellCheck={false}
        />
        <div className="ui-button-row" style={{ marginTop: 10 }}>
          <button
            type="button"
            className="ui-button"
            disabled={executing || !query.trim()}
            onClick={() => void handleExecute()}
          >
            <Play size={16} />
            {executing ? 'A executar...' : 'Executar'}
          </button>
        </div>
      </div>

      {error && (
        <div className="ui-card" style={{ borderLeft: '4px solid var(--ui-error)' }}>
          <p style={{ color: 'var(--ui-error)', fontWeight: 700 }}>{error}</p>
        </div>
      )}

      {result && (
        <div className="ui-card">
          <div className="ui-section-heading">
            <h3>Resultados</h3>
            <span className="ui-badge">
              {result.rowCount} linha{result.rowCount !== 1 ? 's' : ''} em {(result.durationMs / 1000).toFixed(2)}s
            </span>
          </div>
          <div className="ui-query-results">
            <table className="ui-table">
              <thead>
                <tr>
                  {result.columns.map((col) => (
                    <th key={col}>{col}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {result.rows.length === 0 ? (
                  <tr>
                    <td colSpan={result.columns.length}>Sem resultados</td>
                  </tr>
                ) : (
                  result.rows.map((row, i) => (
                    <tr key={i}>
                      {result.columns.map((col) => (
                        <td key={col}>{row[col] ?? 'NULL'}</td>
                      ))}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}
