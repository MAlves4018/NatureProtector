import type {
  AreaResponse,
  RuntimeRunAuditResponse,
  RuntimeRunOverridesResponse,
  RuntimeRunOverrideValuesResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SimulationRunResponse,
} from './types';
import { formatUiDate, translate, UiLocale } from './i18n';

export type UiAreaSelectionStatus =
  | 'not-selected'
  | 'resolving'
  | 'resolved'
  | 'unavailable'
  | 'invalid'
  | 'not-found'
  | 'error';

export type UiRunLifecycleState =
  | 'not-selected'
  | 'not-found'
  | 'pending'
  | 'running'
  | 'completed'
  | 'failed'
  | 'blocked'
  | 'cancelled'
  | 'unknown'
  | 'error';

export type UiScenarioAvailability = 'not-selected' | 'available' | 'not-found' | 'unavailable' | 'error';

export interface UiAreaResolutionModel {
  requestedArea: string | null;
  resolvedArea: AreaResponse | null;
  availableAreas: AreaResponse[];
  selectionStatus: UiAreaSelectionStatus;
  resolutionStatus: UiAreaSelectionStatus;
  resolutionReason: string;
}

export interface UiRunContextModel {
  requestedRunId: string | null;
  resolvedRunId: string | null;
  run: RuntimeRunSummaryResponse | SimulationRunResponse | null;
  state: UiRunLifecycleState;
  fields: Array<{ label: string; value: string }>;
  limitations: string[];
}

export interface UiScenarioContextModel {
  requestedScenarioId: string | null;
  resolvedScenarioId: string | null;
  scenario: ScenarioResponse | null;
  availability: UiScenarioAvailability;
  fields: Array<{ label: string; value: string }>;
  limitations: string[];
}

export interface UiSimulationReviewModel {
  requested: RuntimeRunStartRequest;
  resolved: RuntimeRunOverrideValuesResponse | null;
  resultStatus: string;
  resultMessage: string;
  fields: Array<{
    label: string;
    requested: string;
    resolved: string;
    state: 'requested' | 'resolved' | 'defaulted' | 'unknown';
  }>;
  warnings: string[];
}

export function resolveUiArea(
  requestedArea: string | null | undefined,
  availableAreas: AreaResponse[],
  locale: UiLocale,
  loading = false,
  error: Error | null = null,
): UiAreaResolutionModel {
  const normalized = requestedArea?.trim() || null;

  if (error) {
    return areaModel(normalized, null, availableAreas, 'error', 'error', error.message);
  }

  if (loading) {
    return areaModel(normalized, null, availableAreas, 'resolving', 'resolving', translate(locale, 'area.resolving'));
  }

  if (!normalized) {
    return areaModel(null, null, availableAreas, 'not-selected', 'not-selected', translate(locale, 'area.notSelected'));
  }

  if (!/^[a-z0-9][a-z0-9-]{0,80}$/i.test(normalized)) {
    return areaModel(normalized, null, availableAreas, 'invalid', 'invalid', translate(locale, 'area.invalid'));
  }

  if (availableAreas.length === 0) {
    return areaModel(
      normalized,
      null,
      availableAreas,
      'unavailable',
      'unavailable',
      translate(locale, 'area.unavailable'),
    );
  }

  const match = availableAreas.find((area) => area.code.toLowerCase() === normalized.toLowerCase()) ?? null;
  if (!match) {
    return areaModel(normalized, null, availableAreas, 'not-found', 'not-found', translate(locale, 'area.notFound'));
  }

  return areaModel(normalized, match, availableAreas, 'resolved', 'resolved', translate(locale, 'area.resolvedStatus'));
}

export function buildUiRunContext(
  input: {
    requestedRunId?: string | null;
    selectedRun?: RuntimeRunSummaryResponse | SimulationRunResponse | null;
    summary?: RuntimeSummaryResponse | null;
    audit?: RuntimeRunAuditResponse | null;
    timings?: RuntimeRunTimingSummaryResponse | null;
    error?: Error | null;
  },
  locale: UiLocale,
): UiRunContextModel {
  if (input.error) {
    return {
      requestedRunId: input.requestedRunId ?? null,
      resolvedRunId: null,
      run: null,
      state: 'error',
      fields: [{ label: 'Error', value: input.error.message }],
      limitations: [input.error.message],
    };
  }

  const run = input.audit?.run ?? input.selectedRun ?? input.summary?.currentRun ?? input.summary?.latestRun ?? null;
  if (!run) {
    return {
      requestedRunId: input.requestedRunId ?? null,
      resolvedRunId: null,
      run: null,
      state: input.requestedRunId ? 'not-found' : 'not-selected',
      fields: [],
      limitations: [translate(locale, 'value.noData')],
    };
  }

  const overrides = getRunOverrides(run);
  return {
    requestedRunId: input.requestedRunId ?? null,
    resolvedRunId: run.id,
    run,
    state: mapRunState(run.status),
    fields: [
      { label: coreLabel(locale, 'runId'), value: run.id },
      { label: coreLabel(locale, 'status'), value: run.status },
      { label: coreLabel(locale, 'area'), value: run.areaCode },
      { label: coreLabel(locale, 'scenario'), value: `${run.scenarioCode} / ${run.scenarioName}` },
      { label: coreLabel(locale, 'created'), value: formatUiDate(run.createdAt, locale) },
      { label: coreLabel(locale, 'started'), value: formatUiDate(run.startedAt, locale) },
      { label: coreLabel(locale, 'completed'), value: formatUiDate(run.endedAt, locale) },
      { label: coreLabel(locale, 'cycles'), value: String(run.numberOfCycles) },
      { label: coreLabel(locale, 'interval'), value: `${run.intervalSeconds}s` },
      {
        label: coreLabel(locale, 'seed'),
        value: run.executionSeed == null ? translate(locale, 'value.notAvailable') : String(run.executionSeed),
      },
      { label: coreLabel(locale, 'requestedConfig'), value: formatOverrideValues(overrides?.requested, locale) },
      { label: coreLabel(locale, 'resolvedConfig'), value: formatOverrideValues(overrides?.resolved, locale) },
      {
        label: coreLabel(locale, 'acceptedMissing'),
        value: input.audit
          ? `${input.audit.acceptedReadings}/${input.audit.missingEvents ?? translate(locale, 'value.unknown')}`
          : translate(locale, 'value.notAvailable'),
      },
      {
        label: coreLabel(locale, 'timing'),
        value:
          input.timings?.runDurationMs == null
            ? translate(locale, 'value.notAvailable')
            : `${Math.round(input.timings.runDurationMs)}ms`,
      },
    ],
    limitations: input.audit?.limitations.map((item) => item.message) ?? [],
  };
}

export function buildUiScenarioContext(
  scenarioCode: string | null | undefined,
  scenarios: ScenarioResponse[],
  locale: UiLocale,
  error: Error | null = null,
): UiScenarioContextModel {
  const requestedScenarioId = scenarioCode?.trim() || null;
  if (error) {
    return scenarioModel(requestedScenarioId, null, 'error', locale, [error.message]);
  }

  if (!requestedScenarioId) {
    return scenarioModel(null, null, 'not-selected', locale);
  }

  if (scenarios.length === 0) {
    return scenarioModel(requestedScenarioId, null, 'unavailable', locale);
  }

  const scenario = scenarios.find((item) => item.code === requestedScenarioId) ?? null;
  return scenario
    ? scenarioModel(requestedScenarioId, scenario, 'available', locale)
    : scenarioModel(requestedScenarioId, null, 'not-found', locale);
}

export function buildUiSimulationReview(
  request: RuntimeRunStartRequest,
  result: RuntimeRunStartResponse | null,
  locale: UiLocale,
): UiSimulationReviewModel {
  const resolved = result?.run?.runOverrides?.resolved ?? null;
  return {
    requested: request,
    resolved,
    resultStatus: result?.status ?? translate(locale, 'simulation.idle'),
    resultMessage: result?.message ?? translate(locale, 'simulation.notSubmitted'),
    warnings: result?.warnings ?? [],
    fields: [
      reviewField('areaCode', request.areaCode, result?.run?.areaCode, locale),
      reviewField('scenarioCode', request.scenarioCode, result?.run?.scenarioCode, locale),
      reviewField('sensorCount', request.sensorCount, resolved?.sensorCount, locale),
      reviewField('numberOfCycles', request.numberOfCycles, resolved?.numberOfCycles, locale),
      reviewField('intervalSeconds', request.intervalSeconds, resolved?.intervalSeconds, locale),
      reviewField('seed', request.seed, resolved?.seed, locale),
      reviewField('degradationProfile', request.degradationProfile, resolved?.degradationProfile, locale),
      reviewField(
        'orchestratorCorrelationId',
        result?.requested.orchestratorCorrelationId,
        resolved?.orchestratorCorrelationId,
        locale,
      ),
    ],
  };
}

function areaModel(
  requestedArea: string | null,
  resolvedArea: AreaResponse | null,
  availableAreas: AreaResponse[],
  selectionStatus: UiAreaSelectionStatus,
  resolutionStatus: UiAreaSelectionStatus,
  resolutionReason: string,
): UiAreaResolutionModel {
  return { requestedArea, resolvedArea, availableAreas, selectionStatus, resolutionStatus, resolutionReason };
}

function scenarioModel(
  requestedScenarioId: string | null,
  scenario: ScenarioResponse | null,
  availability: UiScenarioAvailability,
  locale: UiLocale,
  limitations: string[] = [],
): UiScenarioContextModel {
  return {
    requestedScenarioId,
    resolvedScenarioId: scenario?.code ?? null,
    scenario,
    availability,
    fields: scenario
      ? [
          { label: coreLabel(locale, 'scenarioId'), value: scenario.code },
          { label: coreLabel(locale, 'name'), value: scenario.name },
          { label: coreLabel(locale, 'kind'), value: scenario.scenarioKind },
          {
            label: coreLabel(locale, 'description'),
            value: scenario.description ?? translate(locale, 'value.notAvailable'),
          },
          {
            label: coreLabel(locale, 'baseScenario'),
            value: scenario.baseScenarioCode ?? translate(locale, 'value.notAvailable'),
          },
          { label: coreLabel(locale, 'datasetBindings'), value: String(scenario.datasetBindingCount) },
        ]
      : [],
    limitations,
  };
}

function mapRunState(status: string): UiRunLifecycleState {
  const value = status.toLowerCase();
  if (value.includes('running') || value.includes('started')) {
    return 'running';
  }
  if (value.includes('completed') || value.includes('succeeded')) {
    return 'completed';
  }
  if (value.includes('failed')) {
    return 'failed';
  }
  if (value.includes('blocked') || value.includes('rejected')) {
    return 'blocked';
  }
  if (value.includes('cancel')) {
    return 'cancelled';
  }
  if (value.includes('pending') || value.includes('validated') || value.includes('started')) {
    return 'pending';
  }
  return 'unknown';
}

function formatOverrideValues(values: RuntimeRunOverrideValuesResponse | null | undefined, locale: UiLocale) {
  if (!values) {
    return translate(locale, 'value.notAvailable');
  }

  const parts = [
    ['sensorCount', values.sensorCount],
    ['numberOfCycles', values.numberOfCycles],
    ['intervalSeconds', values.intervalSeconds],
    ['seed', values.seed],
    ['degradationProfile', values.degradationProfile],
  ]
    .filter(([, value]) => value !== null && value !== undefined && value !== '')
    .map(([key, value]) => `${key}=${value}`);

  return parts.length > 0 ? parts.join('; ') : translate(locale, 'value.notAvailable');
}

function getRunOverrides(run: RuntimeRunSummaryResponse | SimulationRunResponse): RuntimeRunOverridesResponse | null {
  return 'runOverrides' in run ? run.runOverrides : null;
}

function reviewField(
  label: string,
  requested: unknown,
  resolved: unknown,
  locale: UiLocale,
): UiSimulationReviewModel['fields'][number] {
  const requestedValue = formatMaybe(requested, locale);
  const resolvedValue = formatMaybe(resolved, locale);
  const state =
    resolved === null || resolved === undefined
      ? 'requested'
      : requestedValue === resolvedValue
        ? 'resolved'
        : 'defaulted';

  return { label, requested: requestedValue, resolved: resolvedValue, state };
}

function formatMaybe(value: unknown, locale: UiLocale) {
  if (Array.isArray(value)) {
    return value.length > 0 ? value.join(', ') : translate(locale, 'value.notAvailable');
  }
  if (value === null || value === undefined || value === '') {
    return translate(locale, 'value.notAvailable');
  }
  return String(value);
}

type CoreLabelKey =
  | 'acceptedMissing'
  | 'area'
  | 'baseScenario'
  | 'completed'
  | 'created'
  | 'cycles'
  | 'datasetBindings'
  | 'description'
  | 'interval'
  | 'kind'
  | 'name'
  | 'requestedConfig'
  | 'resolvedConfig'
  | 'runId'
  | 'scenario'
  | 'scenarioId'
  | 'seed'
  | 'started'
  | 'status'
  | 'timing';

function coreLabel(locale: UiLocale, key: CoreLabelKey) {
  const labels: Record<UiLocale, Record<CoreLabelKey, string>> = {
    'pt-PT': {
      acceptedMissing: 'Aceites/em falta',
      area: 'Area',
      baseScenario: 'Cenario base',
      completed: 'Concluida',
      created: 'Criada',
      cycles: 'Ciclos',
      datasetBindings: 'Bindings de dataset',
      description: 'Descricao',
      interval: 'Intervalo',
      kind: 'Tipo',
      name: 'Nome',
      requestedConfig: 'Configuracao pedida',
      resolvedConfig: 'Configuracao resolvida',
      runId: 'Run ID',
      scenario: 'Cenario',
      scenarioId: 'Scenario ID',
      seed: 'Seed',
      started: 'Iniciada',
      status: 'Estado',
      timing: 'Timing',
    },
    en: {
      acceptedMissing: 'Accepted/missing',
      area: 'Area',
      baseScenario: 'Base scenario',
      completed: 'Completed',
      created: 'Created',
      cycles: 'Cycles',
      datasetBindings: 'Dataset bindings',
      description: 'Description',
      interval: 'Interval',
      kind: 'Kind',
      name: 'Name',
      requestedConfig: 'Requested config',
      resolvedConfig: 'Resolved config',
      runId: 'Run ID',
      scenario: 'Scenario',
      scenarioId: 'Scenario ID',
      seed: 'Seed',
      started: 'Started',
      status: 'Status',
      timing: 'Timing',
    },
  };

  return labels[locale][key];
}


