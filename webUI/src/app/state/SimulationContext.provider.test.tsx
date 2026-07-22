import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { UiSimulationProvider, useUiSimulation } from './SimulationContext';

const mocks = vi.hoisted(() => ({
  area: {
    resolvedAreaCode: 'PT-11' as string | null,
    selectedAreaCode: 'PT-11',
    reloadAreaContext: vi.fn(),
  },
  capabilities: {
    canExecuteSimulation: true,
  },
  activity: {
    selectedScenarioCode: 'scenario_b',
    setSelectedRunId: vi.fn(),
  },
}));

vi.mock('./AreaContext', () => ({
  useUiArea: () => mocks.area,
}));

vi.mock('./CapabilityContext', () => ({
  useUiCapabilities: () => mocks.capabilities,
}));

vi.mock('./ActivityContext', () => ({
  useUiActivity: () => mocks.activity,
}));

vi.mock('./LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'simulation.forbidden': 'simulation forbidden',
        'simulation.blockedNoArea': 'simulation blocked without area',
        'simulation.blockedNoScenario': 'simulation blocked without scenario',
      })[key] ?? key,
  }),
}));

vi.mock('../services/api', () => ({
  api: {
    startRuntimeRun: vi.fn(),
    getRuntimeOperation: vi.fn(),
  },
}));

function SimulationProbe() {
  const {
    simulationError,
    simulationRequest,
    simulationResult,
    runtimeOperation,
    simulationSubmitting,
    canExecuteSimulation,
    submitSimulation,
  } = useUiSimulation();

  return (
    <div>
      <p>canExecute:{String(canExecuteSimulation)}</p>
      <p>area:{simulationRequest.areaCode}</p>
      <p>scenario:{simulationRequest.scenarioCode}</p>
      <p>result:{simulationResult?.requestId ?? 'none'}</p>
      <p>operation:{runtimeOperation?.terminalOutcome ?? 'none'}</p>
      <p>submitting:{String(simulationSubmitting)}</p>
      <p>error:{simulationError?.message ?? 'none'}</p>
      <button type="button" onClick={() => void submitSimulation()}>
        submit
      </button>
    </div>
  );
}

function renderProvider() {
  return render(
    <UiSimulationProvider>
      <SimulationProbe />
    </UiSimulationProvider>,
  );
}

function startResponse(overrides: Record<string, unknown> = {}) {
  return {
    requestId: 'request-1',
    orchestratorCorrelationId: 'corr-1',
    status: 'Accepted',
    message: 'accepted',
    requestedAtUtc: '2026-07-21T10:00:00.000Z',
    requested: {
      sensorCount: 2,
      numberOfCycles: 3,
      intervalSeconds: 60,
      seed: 42,
      degradationProfile: 'none',
      degradationProfiles: ['none'],
      orchestratorCorrelationId: 'corr-1',
    },
    run: {
      id: 'run-direct',
      areaCode: 'PT-11',
      scenarioCode: 'scenario_b',
      scenarioName: 'Scenario B',
      status: 'Running',
      configurationVersionNumber: 1,
      createdAt: '2026-07-21T10:00:00.000Z',
      startedAt: '2026-07-21T10:00:01.000Z',
      endedAt: null,
      durationSeconds: null,
      logicalStartTimestamp: '2026-07-21T10:00:00.000Z',
      intervalSeconds: 60,
      numberOfCycles: 3,
      executionSeed: 42,
      metadataJson: null,
      metadataJsonStatus: 'Missing',
      orchestratorCorrelationId: 'corr-1',
      runOverrides: null,
    },
    warnings: [],
    logDirectory: null,
    evidenceDirectory: null,
    operationId: 'operation-1',
    ...overrides,
  };
}

function operationResponse(overrides: Record<string, unknown> = {}) {
  return {
    operationId: 'operation-1',
    requestId: 'request-1',
    correlationId: 'corr-1',
    simulationRunId: 'run-polled',
    requestedState: 'Accepted',
    providerState: 'Completed',
    runState: 'SystemCompleted',
    processingState: 'Settled',
    state: 'SystemCompleted',
    terminalOutcome: 'SystemCompleted',
    acceptedAt: '2026-07-21T10:00:00.000Z',
    updatedAt: '2026-07-21T10:00:10.000Z',
    startedAt: '2026-07-21T10:00:01.000Z',
    producerCompletedAt: '2026-07-21T10:00:05.000Z',
    systemCompletedAt: '2026-07-21T10:00:10.000Z',
    finishedAt: '2026-07-21T10:00:10.000Z',
    failureCode: null,
    failureDetail: null,
    evidenceId: 'evidence-1',
    evidenceLocation: 'local',
    accounting: {
      expectedObservations: 6,
      acceptedObservations: 6,
      pendingInbox: 0,
      processingInbox: 0,
      retryPendingInbox: 0,
      processedInbox: 6,
      quarantinedInbox: 0,
      settled: true,
    },
    ...overrides,
  };
}

describe('UiSimulationProvider', () => {
  beforeEach(() => {
    sessionStorage.clear();
    mocks.area.resolvedAreaCode = 'PT-11';
    mocks.area.selectedAreaCode = 'PT-11';
    mocks.area.reloadAreaContext.mockReset();
    mocks.capabilities.canExecuteSimulation = true;
    mocks.activity.selectedScenarioCode = 'scenario_b';
    mocks.activity.setSelectedRunId.mockReset();
    vi.mocked(api.startRuntimeRun).mockReset();
    vi.mocked(api.getRuntimeOperation).mockReset();
  });

  it('blocks submission fail-closed before calling the runtime API when capability is missing', async () => {
    mocks.capabilities.canExecuteSimulation = false;

    renderProvider();

    fireEvent.click(screen.getByRole('button', { name: 'submit' }));

    expect(await screen.findByText('error:simulation forbidden')).toBeInTheDocument();
    expect(api.startRuntimeRun).not.toHaveBeenCalled();
  });

  it('submits the resolved request, selects direct and polled RunIds, then reloads area context on terminal operation', async () => {
    vi.mocked(api.startRuntimeRun).mockResolvedValue(startResponse() as any);
    vi.mocked(api.getRuntimeOperation).mockResolvedValue(operationResponse() as any);

    renderProvider();

    expect(screen.getByText('area:PT-11')).toBeInTheDocument();
    expect(screen.getByText('scenario:scenario_b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'submit' }));

    await waitFor(() =>
      expect(api.startRuntimeRun).toHaveBeenCalledWith(
        expect.objectContaining({
          areaCode: 'PT-11',
          scenarioCode: 'scenario_b',
          degradationProfile: 'none',
          degradationProfiles: ['none'],
        }),
      ),
    );
    expect(await screen.findByText('result:request-1')).toBeInTheDocument();
    expect(await screen.findByText('operation:SystemCompleted')).toBeInTheDocument();
    expect(mocks.activity.setSelectedRunId).toHaveBeenCalledWith('run-direct');
    expect(mocks.activity.setSelectedRunId).toHaveBeenCalledWith('run-polled');
    expect(mocks.area.reloadAreaContext).toHaveBeenCalledTimes(2);
  });

  it('surfaces runtime start and polling failures without inventing a successful operation', async () => {
    vi.mocked(api.startRuntimeRun).mockRejectedValueOnce('network reset');
    const { unmount } = renderProvider();

    fireEvent.click(screen.getByRole('button', { name: 'submit' }));

    expect(await screen.findByText('error:Failed to start simulation')).toBeInTheDocument();
    expect(screen.getByText('result:none')).toBeInTheDocument();
    unmount();

    vi.mocked(api.startRuntimeRun).mockResolvedValue(startResponse({ run: null }) as any);
    vi.mocked(api.getRuntimeOperation).mockRejectedValueOnce('operation API unavailable');
    renderProvider();
    fireEvent.click(screen.getByRole('button', { name: 'submit' }));

    expect(await screen.findByText('result:request-1')).toBeInTheDocument();
    expect(await screen.findByText('error:Failed to read persisted runtime operation')).toBeInTheDocument();
    expect(screen.getByText('operation:none')).toBeInTheDocument();
  });
});
