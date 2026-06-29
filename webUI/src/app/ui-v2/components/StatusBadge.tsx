import type { UiV2OutputState } from '../outputContext';
import type { UiV2TechnicalState } from '../technicalSurfaces';

export function StatusBadge({
  label,
  state = 'ready',
}: {
  label: string;
  state?: UiV2OutputState | UiV2TechnicalState | 'ready';
}) {
  const tone =
    state === 'blocked' || state === 'error' || state === 'access-denied' || state === 'no-evidence'
      ? 'ui-v2-badge-error'
      : state === 'partial' || state === 'stale' || state === 'not-instrumented' || state === 'not-confirmed'
        ? 'ui-v2-badge-warning'
        : 'ui-v2-badge-ready';

  return <span className={`ui-v2-badge ${tone}`}>{label}</span>;
}
