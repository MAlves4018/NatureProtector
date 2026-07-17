import type { RuntimeRunAuditResponse } from '../types';

export function RunScientificMetrics({ audit }: { audit: RuntimeRunAuditResponse | null }) {
  const score = audit?.scoreComponents;
  const indices = audit?.indexComparison;
  const eligible = audit?.eligibilitySummary
    .filter((item) => item.status.toLowerCase().includes('eligible'))
    .reduce((total, item) => total + item.count, 0);
  const blocked = audit?.eligibilitySummary
    .filter((item) => item.status.toLowerCase().includes('blocked'))
    .reduce((total, item) => total + item.count, 0);
  const coverage =
    audit?.expectedEvents && audit.expectedEvents > 0 ? (audit.acceptedReadings / audit.expectedEvents) * 100 : null;
  const timestamp = score?.latestAssessmentTimestamp ?? indices?.logicalDate ?? null;

  const rows: MetricRow[] = [
    row(
      'NP Score',
      score?.npScore,
      '0–1',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      'Score persistido; não recalculado no browser.',
    ),
    row(
      'FWI',
      indices?.fireWeatherIndex,
      'índice FWI',
      indices?.logicalDate,
      indices?.provenance,
      indices?.fireWeatherCalculationStatus,
    ),
    row(
      'KBDI',
      indices?.keetchByramDroughtIndex,
      '0–800',
      indices?.logicalDate,
      indices?.kbdiValueSource,
      indices?.kbdiCalculationStatus,
    ),
    row(
      'Portuguese Proxy',
      indices?.portugueseContextRiskProxyLabel ?? indices?.portugueseContextRiskProxyClass,
      'classe candidata',
      indices?.logicalDate,
      indices?.provenance,
      'Proxy de contexto português; não é um índice oficial.',
    ),
    row('Base Risk', score?.baseRisk, '0–1', timestamp, 'PostgreSQL · risk_assessment_log', 'Risco base persistido.'),
    row(
      'Adjusted Risk',
      score?.adjustedScore,
      '0–1',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      'Score ajustado persistido.',
    ),
    row(
      'Score100',
      score?.score100,
      '0–100',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      'Representação inteira persistida.',
    ),
    row(
      'Confidence',
      score?.confidenceFactor,
      'fator 0–1',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      'Fator de confiança do cálculo persistido.',
    ),
    row(
      'Integrity',
      score?.integrityFactor,
      'fator 0–1',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      'Fator de integridade persistido.',
    ),
    row(
      'Coverage',
      coverage,
      '% de eventos aceites',
      audit?.run.endedAt,
      'Run audit',
      'Accepted / expected para a execução selecionada.',
    ),
    row(
      'Eligible',
      eligible,
      'avaliações',
      audit?.run.endedAt,
      'Run audit · eligibility summary',
      'Avaliações persistidas com estado eligible.',
    ),
    row(
      'Blocked',
      blocked,
      'avaliações',
      audit?.run.endedAt,
      'Run audit · eligibility summary',
      'Avaliações persistidas com estado blocked.',
    ),
    row(
      'Risk level',
      score?.npRiskClassLabel ?? score?.npRiskClass,
      'classe',
      timestamp,
      'PostgreSQL · risk_assessment_log',
      score?.calculationStatus,
    ),
    row(
      'Alert state',
      null,
      'estado',
      null,
      'Run audit',
      'O contrato run-scoped atual não associa o estado final de alerta à run.',
    ),
    row(
      'Freshness',
      null,
      'estado',
      null,
      'Run audit',
      'O contrato run-scoped atual não persiste freshness agregada por run.',
    ),
  ];

  return (
    <section className="ui-card">
      <div className="ui-section-heading">
        <div>
          <span className="ui-eyebrow">Métricas persistidas da run</span>
          <h3>Índices científicos e qualidade</h3>
        </div>
        <span className="ui-section-note">SimulationRunId: {audit?.run.id ?? 'não selecionada'}</span>
      </div>
      <div className="ui-table-wrap">
        <table className="ui-table">
          <thead>
            <tr>
              <th>Métrica</th>
              <th>Valor</th>
              <th>Escala</th>
              <th>Timestamp</th>
              <th>Origem e interpretação</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((metric) => (
              <tr key={metric.label}>
                <td>
                  <strong>{metric.label}</strong>
                </td>
                <td>{formatValue(metric.value, metric.label)}</td>
                <td>{metric.unit}</td>
                <td>
                  {metric.timestamp ? new Date(metric.timestamp).toLocaleString('pt-PT') : 'Indisponível para esta run'}
                </td>
                <td>
                  {metric.source}
                  <small className="ui-table-note">{metric.note || 'Sem nota adicional.'}</small>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {(score?.limitations || indices?.limitations) && (
        <p className="ui-notice ui-warning">
          Limitações declaradas: {[score?.limitations, indices?.limitations].filter(Boolean).join('; ')}
        </p>
      )}
    </section>
  );
}

interface MetricRow {
  label: string;
  value: string | number | null | undefined;
  unit: string;
  timestamp: string | null | undefined;
  source: string | null | undefined;
  note: string | null | undefined;
}

function row(
  label: string,
  value: MetricRow['value'],
  unit: string,
  timestamp: MetricRow['timestamp'],
  source: MetricRow['source'],
  note: MetricRow['note'],
): MetricRow {
  return { label, value, unit, timestamp, source: source || 'Origem não registada', note };
}

function formatValue(value: MetricRow['value'], label: string) {
  if (value == null || value === '') return 'Indisponível para esta run';
  if (typeof value !== 'number') return value;
  if (label === 'Coverage') return `${value.toFixed(1)}%`;
  return Number.isInteger(value) ? String(value) : value.toFixed(3);
}
