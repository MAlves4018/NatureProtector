import type {
  RuntimeDiagnosticResultResponse,
  RuntimeEvidenceCatalogItemResponse,
  RuntimeOperationResponse,
  RuntimeRunAuditResponse,
  RuntimeRunTimingSummaryResponse,
} from '../types';

export interface RunProgressMetric {
  expected: number | null;
  accepted: number | null;
  assessed: number | null;
  completedPercent: number | null;
  pending: number | null;
}

export interface EvidenceCatalogView {
  id: string;
  title: string;
  evidenceClass: string;
  status: string;
  generatedAt: string | null;
  environment: string;
  scope: string;
  version: string | null;
  size: number;
  downloadable: boolean;
  limitation: string | null;
}

export function buildRunProgress(
  audit: RuntimeRunAuditResponse | null,
  operation: RuntimeOperationResponse | null = null,
): RunProgressMetric {
  const expected = operation?.accounting.expectedObservations ?? audit?.expectedEvents ?? null;
  const accepted = operation?.accounting.acceptedObservations ?? audit?.acceptedReadings ?? null;
  const assessed = operation?.accounting.processedInbox ?? audit?.riskAssessments ?? null;
  const pending =
    operation == null
      ? null
      : operation.accounting.pendingInbox +
        operation.accounting.processingInbox +
        operation.accounting.retryPendingInbox;

  return {
    expected,
    accepted,
    assessed,
    completedPercent:
      expected != null && expected > 0 && assessed != null
        ? Math.min(100, Math.max(0, (assessed / expected) * 100))
        : null,
    pending,
  };
}

export function formatDurationMs(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value) || value < 0) return 'Não medido';
  if (value < 1000) return `${Math.round(value)} ms`;
  const seconds = value / 1000;
  if (seconds < 60) return `${seconds.toFixed(seconds < 10 ? 1 : 0)} s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.round(seconds % 60);
  if (minutes < 60) return `${minutes} min ${remainingSeconds.toString().padStart(2, '0')} s`;
  const hours = Math.floor(minutes / 60);
  return `${hours} h ${(minutes % 60).toString().padStart(2, '0')} min`;
}

export function elapsedMs(start: string | null | undefined, end: string | null | undefined): number | null {
  if (!start || !end) return null;
  const startMs = Date.parse(start);
  const endMs = Date.parse(end);
  if (Number.isNaN(startMs) || Number.isNaN(endMs) || endMs < startMs) return null;
  return endMs - startMs;
}

export function operationDurationMs(operation: { requestedAt: string; updatedAt: string }): number | null {
  return elapsedMs(operation.requestedAt, operation.updatedAt);
}

export function timingFacts(timings: RuntimeRunTimingSummaryResponse | null) {
  return [
    { label: 'Duração total', value: formatDurationMs(timings?.runDurationMs) },
    { label: 'Até à primeira observação', value: formatDurationMs(timings?.timeToFirstInboxMs) },
    { label: 'Até ao processamento', value: formatDurationMs(timings?.timeToFirstProcessingAttemptMs) },
    { label: 'Até à primeira avaliação', value: formatDurationMs(timings?.timeToFirstRiskAssessmentMs) },
    { label: 'Até ao primeiro alerta', value: formatDurationMs(timings?.timeToFirstAlertMs) },
    { label: 'Latência média de tentativa', value: formatDurationMs(timings?.attempts?.avgDurationMs) },
    { label: 'Latência máxima de tentativa', value: formatDurationMs(timings?.attempts?.maxDurationMs) },
    { label: 'p50 / p95', value: 'Não medido pelo contrato atual' },
  ];
}

export function normalizeEvidenceCatalog(items: readonly RuntimeEvidenceCatalogItemResponse[]): EvidenceCatalogView[] {
  return items.map((item) => ({
    id: item.evidenceId,
    title: item.title,
    evidenceClass: classifyEvidence(item.type, item.status),
    status: item.status,
    generatedAt: item.generatedAt,
    environment: item.environment,
    scope: item.scope,
    version: item.version,
    size: item.size,
    downloadable: item.contentAvailable && item.downloadAvailable,
    limitation: item.limitation,
  }));
}

export function diagnosticResultToCsv(result: RuntimeDiagnosticResultResponse): string {
  const rows = [result.columns, ...result.rows.map((row) => result.columns.map((column) => row[column] ?? ''))];
  return rows.map((row) => row.map(csvCell).join(',')).join('\r\n');
}

export function rowsToCsv(rows: readonly Record<string, unknown>[]): string {
  const columns = [...new Set(rows.flatMap((row) => Object.keys(row)))];
  return [columns, ...rows.map((row) => columns.map((column) => row[column] ?? ''))]
    .map((row) => row.map(csvCell).join(','))
    .join('\r\n');
}

function classifyEvidence(type: string, status: string) {
  const normalized = `${type} ${status}`.toLowerCase();
  if (normalized.includes('runtime') || normalized.includes('live')) return 'Execução runtime';
  if (normalized.includes('test') || normalized.includes('quality')) return 'Teste';
  if (normalized.includes('build') || normalized.includes('compile')) return 'Compilação';
  if (normalized.includes('config')) return 'Configuração';
  return 'Artefacto indexado';
}

function csvCell(value: unknown) {
  const text = value == null ? '' : String(value);
  return `"${text.replaceAll('"', '""')}"`;
}
