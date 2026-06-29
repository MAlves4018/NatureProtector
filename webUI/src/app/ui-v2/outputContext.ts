import type { RuntimeSummaryResponse } from '../types';
import { formatUiV2Date, formatUiV2Number, translate, UiV2Locale, UiV2MessageKey } from './i18n';

export type UiV2OutputState =
  | 'loading'
  | 'ready'
  | 'no-data'
  | 'partial'
  | 'stale'
  | 'blocked'
  | 'error'
  | 'unknown'
  | 'access-denied';

export interface UiV2ContextField {
  key: string;
  labelKey: UiV2MessageKey;
  value: string;
  state: UiV2OutputState;
  helpKey: UiV2MessageKey;
}

export interface UiV2RiskReadModel {
  state: UiV2OutputState;
  area: string;
  run: string;
  scoreDisplay: string | null;
  classDisplay: string | null;
  timestampDisplay: string;
  summary: string;
  contextFields: UiV2ContextField[];
  limitations: string[];
  warnings: string[];
  canShowScore: boolean;
}

export interface UiV2RiskReadInput {
  summary?: RuntimeSummaryResponse | null;
  loading?: boolean;
  error?: Error | null;
  accessDenied?: boolean;
}

const unknown = (locale: UiV2Locale) => translate(locale, 'value.unknown');
const notAvailable = (locale: UiV2Locale) => translate(locale, 'value.notAvailable');

export function buildUiV2RiskReadModel(input: UiV2RiskReadInput, locale: UiV2Locale): UiV2RiskReadModel {
  if (input.accessDenied) {
    return emptyModel('access-denied', locale);
  }

  if (input.loading) {
    return emptyModel('loading', locale);
  }

  if (input.error) {
    return {
      ...emptyModel('error', locale),
      warnings: [input.error.message],
    };
  }

  const summary = input.summary;
  if (!summary) {
    return emptyModel('no-data', locale);
  }

  const limitations = uniqueValues([
    ...summary.limitations.map((item) => item.message),
    ...splitMaybe(summary.scoreComponents?.limitations),
    ...splitMaybe(summary.indexComparison?.limitations),
  ]);
  const warnings = summary.warnings ?? [];
  const blocked = containsAny(
    [summary.scoreComponents?.calculationStatus, summary.areaOperationalState?.operationalStatusReason],
    ['blocked'],
  );
  const score = blocked
    ? null
    : (summary.scoreComponents?.npScore ?? summary.areaOperationalState?.aggregateRiskScore ?? null);
  const scoreDisplay = score === null ? null : formatUiV2Number(score, locale, 3);
  const classDisplay = blocked
    ? null
    : (summary.scoreComponents?.npRiskClassLabel ??
      summary.scoreComponents?.npRiskClass ??
      summary.areaOperationalState?.aggregateRiskLevel ??
      null);
  const timestamp =
    summary.scoreComponents?.latestAssessmentTimestamp ??
    summary.areaOperationalState?.lastAssessmentTimestamp ??
    summary.areaOperationalState?.snapshotTimestamp ??
    summary.generatedAtUtc;
  const stale = containsAny(
    [summary.areaOperationalState?.freshnessStatus, summary.freshness?.note],
    ['stale', 'expired'],
  );
  const partial =
    limitations.length > 0 ||
    warnings.length > 0 ||
    containsAny(
      [
        summary.scoreComponents?.calculationStatus,
        summary.indexComparison?.fireWeatherCalculationStatus,
        summary.indexComparison?.kbdiCalculationStatus,
      ],
      ['partial', 'limited', 'missing'],
    );

  const state: UiV2OutputState = blocked
    ? 'blocked'
    : score === null && !classDisplay
      ? 'no-data'
      : stale
        ? 'stale'
        : partial
          ? 'partial'
          : 'ready';

  return {
    state,
    area: summary.areaCode ?? summary.areaOperationalState?.areaCode ?? unknown(locale),
    run: summary.latestRun?.scenarioCode ?? summary.currentRun?.scenarioCode ?? notAvailable(locale),
    scoreDisplay,
    classDisplay,
    timestampDisplay: formatUiV2Date(timestamp, locale),
    summary: summary.areaOperationalState?.summary ?? translate(locale, 'risk.notAlert'),
    contextFields: buildContextFields(summary, locale, limitations),
    limitations: limitations.length > 0 ? limitations : [translate(locale, 'value.noneReported')],
    warnings,
    canShowScore: state !== 'blocked' && state !== 'no-data' && scoreDisplay !== null,
  };
}

function emptyModel(state: UiV2OutputState, locale: UiV2Locale): UiV2RiskReadModel {
  return {
    state,
    area: unknown(locale),
    run: notAvailable(locale),
    scoreDisplay: null,
    classDisplay: null,
    timestampDisplay: notAvailable(locale),
    summary: state === 'error' ? translate(locale, 'state.error') : translate(locale, 'state.noData'),
    contextFields: [
      field('mode', 'status.mode', translate(locale, 'value.academicMode'), 'ready', 'help.calculated'),
      field('purpose', 'status.purpose', translate(locale, 'value.readPurpose'), 'ready', 'help.calculated'),
      field('origin', 'status.origin', unknown(locale), 'unknown', 'help.origin'),
    ],
    limitations: [translate(locale, 'value.noData')],
    warnings: [],
    canShowScore: false,
  };
}

function buildContextFields(
  summary: RuntimeSummaryResponse,
  locale: UiV2Locale,
  limitations: string[],
): UiV2ContextField[] {
  const expected = summary.currentRun?.numberOfCycles ?? null;
  const accepted = summary.risk.recentCount;
  const completeness = expected && expected > 0 ? `${accepted}/${expected}` : notAvailable(locale);
  const coverage =
    summary.areaOperationalState?.coverageStatus ??
    (summary.cellOperationalStateCount > 0 ? `${summary.cellOperationalStateCount}` : notAvailable(locale));

  return [
    field('mode', 'status.mode', translate(locale, 'value.academicMode'), 'ready', 'help.calculated'),
    field('purpose', 'status.purpose', translate(locale, 'value.readPurpose'), 'ready', 'help.calculated'),
    field(
      'origin',
      'status.origin',
      summary.latestRun ? translate(locale, 'value.prototypeProjection') : unknown(locale),
      summary.latestRun ? 'ready' : 'unknown',
      'help.origin',
    ),
    field('reality', 'status.reality', translate(locale, 'value.simulatedOrPersisted'), 'partial', 'help.origin'),
    field(
      'temporal',
      'status.temporal',
      `${formatUiV2Date(summary.generatedAtUtc, locale)} (${summary.recentWindowMinutes}m)`,
      'ready',
      'help.freshness',
    ),
    field(
      'freshness',
      'status.freshness',
      summary.areaOperationalState?.freshnessStatus ?? freshnessCounts(summary, locale),
      stateFromText(summary.areaOperationalState?.freshnessStatus),
      'help.freshness',
    ),
    field('completeness', 'status.completeness', completeness, expected ? 'ready' : 'unknown', 'help.coverage'),
    field(
      'coverage',
      'status.coverage',
      coverage,
      coverage === notAvailable(locale) ? 'unknown' : 'ready',
      'help.coverage',
    ),
    field(
      'eligibility',
      'status.eligibility',
      summary.scoreComponents?.calculationStatus ?? unknown(locale),
      stateFromText(summary.scoreComponents?.calculationStatus),
      'help.eligibility',
    ),
    field(
      'provenance',
      'status.provenance',
      summary.scoreComponents?.parameterSetVersion ?? summary.indexComparison?.provenance ?? unknown(locale),
      summary.scoreComponents?.parameterSetVersion ? 'partial' : 'unknown',
      'help.provenance',
    ),
    field(
      'continuity',
      'status.continuity',
      summary.areaOperationalState?.carryForwardStatus ?? notAvailable(locale),
      stateFromText(summary.areaOperationalState?.carryForwardStatus),
      'help.freshness',
    ),
    field(
      'limitations',
      'status.limitations',
      limitations.length === 0 ? translate(locale, 'value.noneReported') : String(limitations.length),
      limitations.length === 0 ? 'ready' : 'partial',
      'help.limitations',
    ),
  ];
}

function field(
  key: string,
  labelKey: UiV2MessageKey,
  value: string,
  state: UiV2OutputState,
  helpKey: UiV2MessageKey,
): UiV2ContextField {
  return { key, labelKey, value, state, helpKey };
}

function freshnessCounts(summary: RuntimeSummaryResponse, locale: UiV2Locale) {
  if (!summary.freshness) {
    return unknown(locale);
  }

  return `${summary.freshness.freshCount}/${summary.freshness.staleCount}/${summary.freshness.expiredCount}`;
}

function stateFromText(value: string | null | undefined): UiV2OutputState {
  const text = value?.toLowerCase() ?? '';
  if (!text) {
    return 'unknown';
  }
  if (text.includes('blocked')) {
    return 'blocked';
  }
  if (text.includes('stale') || text.includes('expired')) {
    return 'stale';
  }
  if (text.includes('partial') || text.includes('limited') || text.includes('missing')) {
    return 'partial';
  }
  return 'ready';
}

function splitMaybe(value: string | null | undefined) {
  return value
    ? value
        .split(/[;\n]/)
        .map((item) => item.trim())
        .filter(Boolean)
    : [];
}

function uniqueValues(values: string[]) {
  return Array.from(new Set(values));
}

function containsAny(values: Array<string | null | undefined>, needles: string[]) {
  return values.some((value) => {
    const text = value?.toLowerCase() ?? '';
    return needles.some((needle) => text.includes(needle));
  });
}
