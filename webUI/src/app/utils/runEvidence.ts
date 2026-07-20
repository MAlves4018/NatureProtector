export type RunProfileScope = 'requested' | 'resolved';

export interface RunProfileSource {
  metadataJson?: string | null;
  runOverrides?: {
    requested?: {
      degradationProfile?: string | null;
      degradationProfiles?: string[] | null;
      sensorCount?: number | null;
    } | null;
    resolved?: {
      degradationProfile?: string | null;
      degradationProfiles?: string[] | null;
      sensorCount?: number | null;
    } | null;
  } | null;
}

export interface RuntimeEvidenceAssociation {
  evidenceId?: string | null;
  evidenceLocation?: string | null;
}

type UnknownRecord = Record<string, unknown>;

export function resolveRunProfiles(run: RunProfileSource, scope: RunProfileScope = 'resolved'): string[] {
  const direct = normalizeProfiles(
    run.runOverrides?.[scope]?.degradationProfiles,
    run.runOverrides?.[scope]?.degradationProfile,
  );
  if (direct.length > 0) return direct;

  const metadata = parseMetadata(run.metadataJson);
  const overrides = record(metadata?.run_overrides ?? metadata?.runOverrides);
  const scoped = record(overrides?.[scope]);
  const fromMetadata = normalizeProfiles(
    stringArray(scoped?.degradation_profiles ?? scoped?.degradationProfiles),
    stringValue(scoped?.degradation_profile ?? scoped?.degradationProfile),
  );
  return fromMetadata.length > 0 ? fromMetadata : ['none'];
}

export function formatRunProfiles(run: RunProfileSource, scope: RunProfileScope = 'resolved'): string {
  return resolveRunProfiles(run, scope).join(', ');
}

export function resolveRunSensorCount(run: RunProfileSource): number | null {
  const direct = numberValue(run.runOverrides?.resolved?.sensorCount);
  if (direct !== null) return direct;

  const metadata = parseMetadata(run.metadataJson);
  const overrides = record(metadata?.run_overrides ?? metadata?.runOverrides);
  const resolved = record(overrides?.resolved);

  return (
    numberValue(resolved?.sensor_count ?? resolved?.sensorCount) ??
    numberValue(metadata?.sensor_count ?? metadata?.sensorCount)
  );
}

export function directEvidenceAssociationLabel(operation: RuntimeEvidenceAssociation | null | undefined): string {
  return operation?.evidenceId ? 'Associada diretamente' : 'Não associada estruturalmente';
}

function normalizeProfiles(values: string[] | null | undefined, legacy: string | null | undefined): string[] {
  const candidates = values && values.length > 0 ? values : legacy ? legacy.split(/[,+;|]/) : [];

  const unique = new Set(candidates.map((value) => value.trim().toLowerCase()).filter(Boolean));
  return [...unique];
}

function parseMetadata(value: string | null | undefined): UnknownRecord | null {
  if (!value) return null;
  try {
    return record(JSON.parse(value));
  } catch {
    return null;
  }
}

function record(value: unknown): UnknownRecord | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as UnknownRecord) : null;
}

function stringArray(value: unknown): string[] | null {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : null;
}

function stringValue(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
