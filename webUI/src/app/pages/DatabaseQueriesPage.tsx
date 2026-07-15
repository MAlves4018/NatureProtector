import { useState, useCallback } from 'react';
import { Play } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { api } from '../services/api';
import { ROQueryResponse, ROQueryRequest } from '../types';


export function DatabaseQueriesPage() {
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<ROQueryResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [executing, setExecuting] = useState(false);

  const handleExecute = useCallback(async () => {
    if (!query.trim()) return;
    setExecuting(true);
    setError(null);
    setResult(null);
    try {
      const upper = query.trim().toUpperCase();
      console.log('Executing query:', upper);
      if (
        !upper.startsWith('SELECT') &&
        !upper.startsWith('SHOW') &&
        !upper.startsWith('DESCRIBE') &&
        !upper.startsWith('EXPLAIN') || (
        upper.includes('UPDATE') ||
        upper.includes('INSERT') ||
        upper.includes('DELETE') ||
        upper.includes('DROP'))
      ) {
        throw new Error('Apenas queries de leitura sao permitidas (SELECT, SHOW, DESCRIBE, EXPLAIN).');
      }
      const queryRequest : ROQueryRequest = {
        type: query.trim().split(' ')[0].toUpperCase(),
        table: query.trim().split('FROM')[1]?.trim() ?? '',
        query: query.trim(),
      }
      const res = await api.postgresQuery(queryRequest);
      setResult(res);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro desconhecido');
    } finally {
      setExecuting(false);
    }
  }, [query]);

  return (
    <section className="ui-page">
      <PageHeader
        title="Database Queries - Ainda incompleto, apenas mock de queries"
        subtitle="Acesso controlado a queries de leitura nas bases de dados do sistema."
        helpTopic="pipeline"
      />

      <div
        className="ui-notice"
        style={{
          marginBottom: 16,
          padding: '8px 12px',
          borderRadius: 6,
          background: 'var(--ui-warning-bg)',
          color: 'var(--ui-warning)',
        }}
      >
        <strong>⚠ Aviso:</strong> Apenas queries de leitura permitidas. Operacoes de escrita (INSERT, UPDATE, DELETE,
        DROP) sao bloqueadas.
      </div>

      <div className="ui-card" style={{ marginTop: 0 }}>
        <textarea
          id="query-input"
          className="ui-query-editor"
          rows={6}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Digite sua query"
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
                  result.rows.map((row) => (
                    <tr key={result.columns.map((col) => row[col] ?? 'NULL').join('|')}>
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
