import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { RuntimeOperationResponse, RuntimeRunAuditResponse, RuntimeRunTimingSummaryResponse } from '../types';
import { createUiRuntimeSummaryFixture } from '../fixtures';
import { RunProgressCockpit } from './RunProgressCockpit';

describe('RunProgressCockpit', () => {
  it('renders selected-run fallback, unavailable metrics and refresh action', () => {
    const onRefresh = vi.fn();

    render(
      <RunProgressCockpit
        operation={null}
        audit={null}
        timings={null}
        selectedRunId="run-selected"
        onRefresh={onRefresh}
      />,
    );

    expect(screen.getByRole('heading', { name: 'run-selected' })).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'Progresso observado' })).toHaveAttribute('aria-valuenow', '0');
    expect(screen.getAllByText('Indisponível').length).toBeGreaterThan(1);

    fireEvent.click(screen.getByRole('button', { name: /Atualizar/i }));
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('prioritizes operation accounting, copies the SimulationRunId and shows settlement latency', () => {
    const writeText = vi.fn();
    Object.assign(navigator, { clipboard: { writeText } });

    render(
      <RunProgressCockpit
        operation={operationFixture({
          accounting: { ...baseAccounting(), settled: true },
          finishedAt: '2026-06-13T21:04:05Z',
        })}
        audit={auditFixture()}
        timings={timingFixture()}
        selectedRunId="ignored-run"
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Copiar SimulationRunId' }));

    expect(writeText).toHaveBeenCalledWith('operation-run');
    expect(screen.getByRole('heading', { name: 'operation-run' })).toBeInTheDocument();
    expect(screen.getByText('30 / 29')).toBeInTheDocument();
    expect(screen.getByText('96.7%')).toBeInTheDocument();
    expect(screen.getByText('0.0%')).toBeInTheDocument();
    expect(screen.getByText('Sim')).toBeInTheDocument();
    expect(screen.getByText('SystemCompleted → settled')).toBeInTheDocument();
    expect(screen.getAllByText('5.0 s').length).toBeGreaterThan(0);
  });

  it('shows consolidation and failure details for unfinished operations', () => {
    render(
      <RunProgressCockpit
        operation={operationFixture({
          accounting: {
            ...baseAccounting(),
            settled: false,
            pendingInbox: 2,
            processingInbox: 1,
            retryPendingInbox: 1,
          },
          failureDetail: 'provider exited with code 1',
          systemCompletedAt: '2026-06-13T21:04:00Z',
        })}
        audit={null}
        timings={null}
        selectedRunId={null}
      />,
    );

    expect(screen.getByText('A consolidar')).toBeInTheDocument();
    expect(screen.getByText('provider exited with code 1')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
  });
});

function auditFixture(): RuntimeRunAuditResponse {
  const summary = createUiRuntimeSummaryFixture();
  return {
    run: { ...summary.latestRun!, id: 'audit-run' },
    expectedEvents: 30,
    acceptedReadings: 28,
    missingEvents: 2,
    rejected: 0,
    quarantined: 0,
    retryAttempts: 1,
    riskAssessments: 28,
    qualityFlagsSummary: [],
    eligibilitySummary: [],
    areaSnapshot: null,
    limitations: [],
    scoreComponents: summary.scoreComponents,
    indexComparison: summary.indexComparison,
  };
}

function baseAccounting() {
  return {
    expectedObservations: 30,
    acceptedObservations: 30,
    pendingInbox: 0,
    processingInbox: 0,
    retryPendingInbox: 0,
    processedInbox: 29,
    quarantinedInbox: 1,
    settled: false,
  };
}

function operationFixture(overrides: Partial<RuntimeOperationResponse> = {}): RuntimeOperationResponse {
  return {
    operationId: 'op-001',
    requestId: 'request-001',
    correlationId: 'corr-001',
    simulationRunId: 'operation-run',
    requestedState: 'Accepted',
    providerState: 'Completed',
    runState: 'Completed',
    processingState: 'Completed',
    state: 'Completed',
    terminalOutcome: 'Succeeded',
    acceptedAt: '2026-06-13T21:00:00Z',
    updatedAt: '2026-06-13T21:04:05Z',
    startedAt: '2026-06-13T21:00:05Z',
    producerCompletedAt: '2026-06-13T21:03:00Z',
    systemCompletedAt: '2026-06-13T21:04:00Z',
    finishedAt: null,
    failureCode: null,
    failureDetail: null,
    evidenceId: 'evidence-001',
    evidenceLocation: null,
    accounting: baseAccounting(),
    ...overrides,
  };
}

function timingFixture(): RuntimeRunTimingSummaryResponse {
  return {
    simulationRunId: 'operation-run',
    runDurationMs: 245000,
    startedAt: '2026-06-13T21:00:00Z',
    endedAt: '2026-06-13T21:04:05Z',
    firstInboxReceivedAt: '2026-06-13T21:00:10Z',
    firstProcessingAttemptStartedAt: '2026-06-13T21:00:11Z',
    lastProcessingAttemptFinishedAt: '2026-06-13T21:04:00Z',
    firstRiskAssessmentCreatedAt: '2026-06-13T21:00:12Z',
    firstAlertTriggeredAt: '2026-06-13T21:00:20Z',
    timeToFirstInboxMs: 10000,
    timeToFirstProcessingAttemptMs: 11000,
    timeToFirstRiskAssessmentMs: 12000,
    timeToFirstAlertMs: 20000,
    attempts: {
      attemptCount: 30,
      successfulAttempts: 29,
      failedAttempts: 1,
      quarantinedAttempts: 0,
      minDurationMs: 80,
      avgDurationMs: 125.2,
      maxDurationMs: 250,
      p50DurationMs: 120,
      p95DurationMs: 210,
      p99DurationMs: 240,
    },
    stages: [],
    limitations: [],
  };
}
