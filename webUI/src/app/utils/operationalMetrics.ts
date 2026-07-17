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
  acceptedPercent: number | null;
  lostPercent: number | null;
  pending: number | null;
  processing: number | null;
  retryPending: number | null;
  quarantined: number | null;
  settled: boolean | null;
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
  const acceptedPercent = ratioPercent(accepted, expected);
  const completedPercent = ratioPercent(assessed, expected);

  return {
    expected,
    accepted,
    assessed,
    completedPercent,
    acceptedPercent,
    lostPercent: acceptedPercent == null ? null : Math.max(0, 100 - acceptedPercent),
    pending,
    processing: operation?.accounting.processingInbox ?? null,
    retryPending: operation?.accounting.retryPendingInbox ?? null,
    quarantined: operation?.accounting.quarantinedInbox ?? audit?.quarantined ?? null,
    settled: operation?.accounting.settled ?? (audit?.run.status === 'Completed' ? true : null),
  };
}

export function formatDurationMs(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value) || value < 0) return 'Indisponível';
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

export function operationDurationMs(operation: {
  acceptedAt?: string;
  requestedAt?: string;
  updatedAt: string;
}): number | null {
  return elapsedMs(operation.acceptedAt ?? operation.requestedAt, operation.updatedAt);
}

export function timingFacts(
  timings: RuntimeRunTimingSummaryResponse | null,
  operation?: RuntimeOperationResponse | null,
) {
  const settledAt = operation?.accounting.settled ? (operation.finishedAt ?? operation.systemCompletedAt) : null;
  return [
    {
      label: 'Pedido aceite → Simulator iniciado',
      value: formatDurationMs(elapsedMs(operation?.acceptedAt, operation?.startedAt)),
    },
    { label: 'Duração total', value: formatDurationMs(timings?.runDurationMs) },
    { label: 'Até à primeira observação', value: formatDurationMs(timings?.timeToFirstInboxMs) },
    { label: 'Até ao processamento', value: formatDurationMs(timings?.timeToFirstProcessingAttemptMs) },
    { label: 'Até à primeira avaliação', value: formatDurationMs(timings?.timeToFirstRiskAssessmentMs) },
    { label: 'Até ao primeiro alerta', value: formatDurationMs(timings?.timeToFirstAlertMs) },
    { label: 'Latência média de tentativa', value: formatDurationMs(timings?.attempts?.avgDurationMs) },
    { label: 'Latência p50', value: sampleDuration(timings?.attempts?.p50DurationMs, timings?.attempts?.attemptCount) },
    { label: 'Latência p95', value: sampleDuration(timings?.attempts?.p95DurationMs, timings?.attempts?.attemptCount) },
    { label: 'Latência p99', value: sampleDuration(timings?.attempts?.p99DurationMs, timings?.attempts?.attemptCount) },
    { label: 'Latência máxima de tentativa', value: formatDurationMs(timings?.attempts?.maxDurationMs) },
    {
      label: 'Até SystemCompleted',
      value: formatDurationMs(elapsedMs(operation?.acceptedAt, operation?.systemCompletedAt)),
    },
    { label: 'SystemCompleted → settled', value: formatDurationMs(elapsedMs(operation?.systemCompletedAt, settledAt)) },
    { label: 'Duração operacional total', value: formatDurationMs(elapsedMs(operation?.acceptedAt, settledAt)) },
  ];
}

export function throughputPerSecond(count: number | null | undefined, durationMs: number | null | undefined) {
  if (count == null || durationMs == null || durationMs <= 0) return null;
  return count / (durationMs / 1000);
}

export function evidenceIdentityMatchesRun(
  evidenceId: string,
  scope: string,
  runId: string,
  operationEvidenceId?: string | null,
) {
  const normalizedEvidenceId = evidenceId.trim().toLowerCase();
  const normalizedRunId = runId.trim().toLowerCase();
  if (!normalizedRunId) return false;
  if (operationEvidenceId && normalizedEvidenceId === operationEvidenceId.trim().toLowerCase()) return true;
  const scopeTokens = scope
    .toLowerCase()
    .split(/[^a-z0-9-]+/)
    .filter(Boolean);
  return normalizedEvidenceId === normalizedRunId || scopeTokens.includes(normalizedRunId);
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

function ratioPercent(value: number | null, total: number | null) {
  return value == null || total == null || total <= 0 ? null : Math.min(100, Math.max(0, (value / total) * 100));
}

function sampleDuration(value: number | null | undefined, count: number | null | undefined) {
  return count != null && count < 2 ? 'Amostra insuficiente' : formatDurationMs(value);
}

function csvCell(value: unknown) {
  const text = value == null ? '' : String(value);
  return `"${text.replaceAll('"', '""')}"`;
}
