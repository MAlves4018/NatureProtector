import { describe, expect, it } from 'vitest';
import {
  buildRunProgress,
  diagnosticResultToCsv,
  elapsedMs,
  evidenceIdentityMatchesRun,
  formatDurationMs,
  normalizeEvidenceCatalog,
} from './operationalMetrics';

describe('operational metrics', () => {
  it('formats only defensible durations and never turns missing data into zero', () => {
    expect(formatDurationMs(null)).toBe('Indisponível');
    expect(formatDurationMs(850)).toBe('850 ms');
    expect(formatDurationMs(65_000)).toBe('1 min 05 s');
    expect(elapsedMs('2026-07-16T10:00:00Z', '2026-07-16T10:01:05Z')).toBe(65_000);
    expect(elapsedMs('2026-07-16T10:01:05Z', '2026-07-16T10:00:00Z')).toBeNull();
  });

  it('uses persisted operation accounting for live progress', () => {
    const progress = buildRunProgress(null, {
      accounting: {
        expectedObservations: 20,
        acceptedObservations: 15,
        pendingInbox: 2,
        processingInbox: 1,
        retryPendingInbox: 1,
        processedInbox: 12,
        quarantinedInbox: 0,
        settled: false,
      },
    } as never);

    expect(progress).toEqual({
      expected: 20,
      accepted: 15,
      assessed: 12,
      completedPercent: 60,
      acceptedPercent: 75,
      lostPercent: 25,
      pending: 4,
      processing: 1,
      retryPending: 1,
      quarantined: 0,
      settled: false,
    });
  });

  it('normalizes imported evidence without promoting its status', () => {
    expect(
      normalizeEvidenceCatalog([
        {
          evidenceId: 'runtime-1',
          title: 'Runtime smoke',
          type: 'runtime-log',
          generatedAt: '2026-07-16T10:00:00Z',
          environment: 'local',
          scope: 'run-1',
          version: 'abc123',
          contentAvailable: true,
          downloadAvailable: true,
          size: 42,
          status: 'IMPLEMENTED_NOT_EXECUTED',
          limitation: 'Historical only',
        },
      ]),
    ).toEqual([
      expect.objectContaining({
        evidenceClass: 'Execução runtime',
        status: 'IMPLEMENTED_NOT_EXECUTED',
        downloadable: true,
        limitation: 'Historical only',
      }),
    ]);
  });

  it('scopes evidence by exact operation identity or a delimited run token', () => {
    expect(evidenceIdentityMatchesRun('evidence-1', 'run:run-123; local', 'run-123')).toBe(true);
    expect(evidenceIdentityMatchesRun('evidence-1', 'run:run-1234; local', 'run-123')).toBe(false);
    expect(evidenceIdentityMatchesRun('evidence-1', 'global', 'run-123', 'evidence-1')).toBe(true);
  });

  it('exports prepared-query results with stable columns and escaped values', () => {
    expect(
      diagnosticResultToCsv({
        id: 'query',
        title: 'Query',
        description: '',
        columns: ['metric', 'value'],
        rows: [{ metric: 'accepted, total', value: '10' }],
        limitations: [],
      }),
    ).toBe('"metric","value"\r\n"accepted, total","10"');
  });
});
