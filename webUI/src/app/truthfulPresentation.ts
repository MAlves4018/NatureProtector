import type { UiTechnicalState } from './technicalSurfaces';

export type GlobalOperationalStatus = 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unknown';

const TERMINAL_SUCCESS = new Set(['completed', 'succeeded', 'validated', 'rolledback']);
const IN_PROGRESS = new Set(['accepted', 'launchaccepted', 'started', 'queued', 'running', 'pending']);
const NEGATIVE = new Set([
  'blocked',
  'failed',
  'timedout',
  'timed-out',
  'cancelled',
  'rejected',
  'processexitedwithoutrun',
]);

export function executionStatusState(status: string, idleLabel = 'idle'): UiTechnicalState {
  const normalized = normalizeStatus(status);
  if (!normalized || normalized === normalizeStatus(idleLabel)) return 'partial';
  if (TERMINAL_SUCCESS.has(normalized)) return 'ready';
  if (IN_PROGRESS.has(normalized)) return 'partial';
  if (NEGATIVE.has(normalized)) return 'blocked';
  return 'unknown';
}

export function globalOperationalStatus(components: readonly { status: string }[]): GlobalOperationalStatus {
  if (components.some((component) => normalizeStatus(component.status) === 'unhealthy')) return 'Unhealthy';
  if (components.some((component) => normalizeStatus(component.status) === 'degraded')) return 'Degraded';
  if (components.length > 0 && components.every((component) => normalizeStatus(component.status) === 'healthy')) {
    return 'Healthy';
  }
  return 'Unknown';
}

export function operationStatusMeaning(input: {
  status: string;
  provider: string | null;
  evidenceLevel: string;
}): string {
  const status = normalizeStatus(input.status);
  const provider = normalizeStatus(input.provider ?? '');
  const evidence = input.evidenceLevel.toUpperCase();

  if (provider === 'simulation' || evidence === 'DEMONSTRATION_ONLY') {
    return 'Simulated record only; no external workflow or cloud mutation is proved.';
  }
  if (status === 'queued') return 'Request recorded or accepted for dispatch; provider completion is not proved.';
  if (status === 'running') return 'Provider reported work in progress; no terminal result is proved.';
  if (status === 'succeeded' || status === 'rolledback') {
    return evidence.startsWith('PROVED')
      ? 'Terminal provider result with recorded proved evidence.'
      : 'Terminal status recorded; evidence still does not establish a proved result.';
  }
  if (NEGATIVE.has(status)) return 'The requested operation did not complete successfully.';
  return 'Recorded control-plane state; inspect limitations and provider evidence before making operational claims.';
}

function normalizeStatus(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[\s_]+/g, '');
}
