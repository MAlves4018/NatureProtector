import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createUiRuntimeSummaryFixture } from '../fixtures';
import type {
  RuntimeRunAuditResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  ScenarioResponse,
  SimulationRunResponse,
} from '../types';

const harness = vi.hoisted(() => ({
  area: { resolvedAreaCode: 'area-a' as string | null, areasLoading: false },
  getAreaScenarios: vi.fn(),
  listSimulationRuns: vi.fn(),
  getRuntimeRun: vi.fn(),
  getRuntimeRunAudit: vi.fn(),
  getRuntimeRunTimings: vi.fn(),
}));

vi.mock('./AreaContext', () => ({ useUiArea: () => harness.area }));
vi.mock('./CapabilityContext', () => ({
  useUiCapabilities: () => ({ canReadRisk: true, canReadRun: true, canReadScenario: true }),
}));
vi.mock('./LocaleContext', () => ({ useUiLocale: () => ({ locale: 'en' }) }));
vi.mock('./RiskContext', () => ({ useUiRisk: () => ({ summary: null }) }));
vi.mock('../services/api', () => ({
  api: {
    getAreaScenarios: harness.getAreaScenarios,
    listSimulationRuns: harness.listSimulationRuns,
    getRuntimeRun: harness.getRuntimeRun,
    getRuntimeRunAudit: harness.getRuntimeRunAudit,
    getRuntimeRunTimings: harness.getRuntimeRunTimings,
  },
}));

import { UiActivityProvider, useUiActivity } from './ActivityContext';

function run(id: string, areaCode: string): RuntimeRunSummaryResponse {
  return {
    ...createUiRuntimeSummaryFixture().latestRun!,
    id,
    areaCode,
    scenarioCode: `scenario-${areaCode}`,
    scenarioName: `Scenario ${areaCode}`,
  };
}

function scenario(areaCode: string): ScenarioResponse {
  return {
    id: `scenario-id-${areaCode}`,
    code: `scenario-${areaCode}`,
    name: `Scenario ${areaCode}`,
    scenarioKind: 'Nominal',
    configurationVersionNumber: 1,
    description: null,
    baseScenarioCode: null,
    datasetBindingCount: 0,
  };
}

function audit(value: RuntimeRunSummaryResponse): RuntimeRunAuditResponse {
  return {
    run: value,
    expectedEvents: 1,
    acceptedReadings: 1,
    missingEvents: 0,
    rejected: 0,
    quarantined: 0,
    retryAttempts: 0,
    riskAssessments: 1,
    qualityFlagsSummary: [],
    eligibilitySummary: [],
    areaSnapshot: null,
    limitations: [],
    scoreComponents: null,
    indexComparison: null,
    dataScope: {
      requestedRunId: value.id,
      resolvedRunId: value.id,
      dataRunId: value.id,
      observedAt: '2026-07-13T00:00:00Z',
      source: 'contract',
      scope: 'RunScoped',
      limitations: [],
    },
  };
}

function timings(value: RuntimeRunSummaryResponse): RuntimeRunTimingSummaryResponse {
  return {
    simulationRunId: value.id,
    runDurationMs: 1000,
    startedAt: value.startedAt,
    endedAt: value.endedAt,
    firstInboxReceivedAt: value.startedAt,
    firstProcessingAttemptStartedAt: value.startedAt,
    lastProcessingAttemptFinishedAt: value.endedAt,
    firstRiskAssessmentCreatedAt: value.startedAt,
    firstAlertTriggeredAt: null,
    timeToFirstInboxMs: 1,
    timeToFirstProcessingAttemptMs: 2,
    timeToFirstRiskAssessmentMs: 3,
    timeToFirstAlertMs: null,
    attempts: {
      attemptCount: 0,
      successfulAttempts: 0,
      failedAttempts: 0,
      quarantinedAttempts: 0,
      minDurationMs: null,
      avgDurationMs: null,
      maxDurationMs: null,
    },
    stages: [],
    limitations: [],
    dataScope: {
      requestedRunId: value.id,
      resolvedRunId: value.id,
      dataRunId: value.id,
      observedAt: '2026-07-13T00:00:00Z',
      source: 'contract',
      scope: 'RunScoped',
      limitations: [],
    },
  };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

function Probe() {
  const value = useUiActivity();
  return (
    <>
      <output data-testid="scenario">{value.selectedScenarioCode}</output>
      <output data-testid="run-id">{value.selectedRunId}</output>
      <output data-testid="selected-run">{value.selectedRun?.id ?? ''}</output>
      <output data-testid="loading">{String(value.runDetailsLoading)}</output>
      <button type="button" onClick={() => value.setSelectedRunId('run-A')}>
        run A
      </button>
      <button type="button" onClick={() => value.setSelectedRunId('run-B')}>
        run B
      </button>
    </>
  );
}

function renderProvider() {
  return render(
    <UiActivityProvider>
      <Probe />
    </UiActivityProvider>,
  );
}

beforeEach(() => {
  sessionStorage.clear();
  harness.area.resolvedAreaCode = 'area-a';
  vi.clearAllMocks();
  harness.getAreaScenarios.mockImplementation(async (area: string) => [scenario(area)]);
  harness.listSimulationRuns.mockImplementation(
    async (area: string) => [run(`run-${area.at(-1)?.toUpperCase()}`, area)] as SimulationRunResponse[],
  );
  harness.getRuntimeRun.mockImplementation(async (id: string) => run(id, id === 'run-A' ? 'area-a' : 'area-b'));
  harness.getRuntimeRunAudit.mockImplementation(async (id: string) =>
    audit(run(id, id === 'run-A' ? 'area-a' : 'area-b')),
  );
  harness.getRuntimeRunTimings.mockImplementation(async (id: string) =>
    timings(run(id, id === 'run-A' ? 'area-a' : 'area-b')),
  );
});

describe('R1M-002 ActivityContext scope contract', () => {
  it('clears a run and its details when the selected area changes', async () => {
    const view = renderProvider();
    fireEvent.click(screen.getByRole('button', { name: 'run A' }));
    await waitFor(() => expect(screen.getByTestId('selected-run')).toHaveTextContent('run-A'));

    harness.area.resolvedAreaCode = 'area-b';
    view.rerender(
      <UiActivityProvider>
        <Probe />
      </UiActivityProvider>,
    );

    await waitFor(() => expect(screen.getByTestId('run-id')).toHaveTextContent(/^$/));
    expect(screen.getByTestId('selected-run')).toHaveTextContent(/^$/);
  });

  it('revalidates the selected scenario when the area changes', async () => {
    sessionStorage.setItem('np.Ui.scenarioCode', 'scenario-area-a');
    const view = renderProvider();
    await waitFor(() => expect(screen.getByTestId('scenario')).toHaveTextContent('scenario-area-a'));

    harness.area.resolvedAreaCode = 'area-b';
    view.rerender(
      <UiActivityProvider>
        <Probe />
      </UiActivityProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('scenario')).toHaveTextContent('scenario-area-b'));
  });

  it('clears previous run details immediately while the next run is loading', async () => {
    const next = deferred<RuntimeRunSummaryResponse>();
    renderProvider();
    fireEvent.click(screen.getByRole('button', { name: 'run A' }));
    await waitFor(() => expect(screen.getByTestId('selected-run')).toHaveTextContent('run-A'));
    harness.getRuntimeRun.mockImplementation((id: string) =>
      id === 'run-B' ? next.promise : Promise.resolve(run(id, 'area-a')),
    );

    fireEvent.click(screen.getByRole('button', { name: 'run B' }));
    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('true'));
    expect(screen.getByTestId('selected-run')).not.toHaveTextContent('run-A');
  });

  it('ignores a late response for a run that is no longer selected', async () => {
    const lateA = deferred<RuntimeRunSummaryResponse>();
    harness.listSimulationRuns.mockResolvedValue([
      run('run-A', 'area-a'),
      run('run-B', 'area-a'),
    ] as SimulationRunResponse[]);
    harness.getRuntimeRun.mockImplementation((id: string) =>
      id === 'run-A' ? lateA.promise : Promise.resolve(run('run-B', 'area-a')),
    );
    renderProvider();
    fireEvent.click(screen.getByRole('button', { name: 'run A' }));
    fireEvent.click(screen.getByRole('button', { name: 'run B' }));
    await waitFor(() => expect(screen.getByTestId('selected-run')).toHaveTextContent('run-B'));

    await act(async () => {
      lateA.resolve(run('run-A', 'area-a'));
      await lateA.promise;
    });
    expect(screen.getByTestId('selected-run')).toHaveTextContent('run-B');
  });
});
