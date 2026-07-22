import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

describe('api client', () => {
  beforeEach(() => {
    localStorage.clear();
    api.options = {};
    api.clearAuthToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('requests areas through the API proxy prefix', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([{ id: 'area-1', code: 'proenca-a-nova' }]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const areas = await api.getAreas();

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/areas',
      expect.objectContaining({
        credentials: 'include',
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    );
    expect(areas).toEqual([{ id: 'area-1', code: 'proenca-a-nova' }]);
  });

  it('requests filtered simulation runs with query parameters', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await api.listSimulationRuns('proenca-a-nova', 'scenario_b', 5);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/simulation-runs?areaCode=proenca-a-nova&scenarioCode=scenario_b&take=5',
      expect.objectContaining({ credentials: 'include' }),
    );
  });

  it('requests user roles through the backend users route', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([{ id: 1, name: 'Admin' }]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const roles = await api.getRoles('00000000-0000-0000-0000-000000000001');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/users-roles/users/00000000-0000-0000-0000-000000000001/roles',
      expect.objectContaining({ credentials: 'include' }),
    );
    expect(roles).toEqual([{ id: 1, name: 'Admin' }]);
  });

  it('requests runtime run details by id', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'run-1' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await api.getRuntimeRun('run-1');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/runtime/runs/run-1',
      expect.objectContaining({ credentials: 'include' }),
    );
  });

  it('adds a bearer token from local storage when no explicit auth header exists', async () => {
    localStorage.setItem('token', 'local-token');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    await api.logout();

    expect(fetch).toHaveBeenCalledWith(
      '/api/users-roles/logout',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer local-token' }),
      }),
    );
  });

  it('downloads runtime evidence through the authenticated API client', async () => {
    api.withAuthToken('explicit-token');
    const fetchMock = vi.fn().mockResolvedValue(
      new Response('runtime evidence', {
        status: 200,
        headers: {
          'Content-Disposition': 'attachment; filename="runtime-smoke.txt"',
          'Content-Type': 'text/plain',
        },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await api.downloadRuntimeEvidence('runtime-smoke');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/runtime/observability/evidence/runtime-smoke',
      expect.objectContaining({
        credentials: 'include',
        headers: expect.objectContaining({ Authorization: 'Bearer explicit-token' }),
      }),
    );
    expect(result.filename).toBe('runtime-smoke.txt');
    expect(result.contentType).toBe('text/plain');
  });

  it('keeps request options undefined until an auth token or caller option is set', () => {
    expect(api.getRequestOptions()).toBeUndefined();

    expect(api.withAuthToken('explicit-token').getRequestOptions()).toEqual({
      headers: { Authorization: 'Bearer explicit-token' },
    });

    expect(api.clearAuthToken().getRequestOptions()).toEqual({ headers: {} });
  });

  it('clears both canonical and lowercase authorization headers without dropping other headers', () => {
    api.options.headers = {
      Authorization: 'Bearer canonical',
      authorization: 'Bearer lowercase',
      'X-Trace': 'trace-1',
    };

    api.clearAuthToken();

    expect(api.options.headers).toEqual({ 'X-Trace': 'trace-1' });
  });

  it.each([
    [
      'getCurrentCapabilities',
      () => api.getCurrentCapabilities(),
      '/api/users-roles/me/capabilities',
      'GET',
      undefined,
    ],
    ['listUsers', () => api.listUsers(), '/api/users-roles/users', 'GET', undefined],
    ['listRoles', () => api.listRoles(), '/api/users-roles/roles', 'GET', undefined],
    [
      'addRoleToUser',
      () => api.addRoleToUser('user/with space', 7),
      '/api/users-roles/users/user/with space/roles/7',
      'PUT',
      undefined,
    ],
    [
      'removeRoleFromUser',
      () => api.removeRoleFromUser('user/with space', 7),
      '/api/users-roles/users/user/with space/roles/7',
      'DELETE',
      undefined,
    ],
    [
      'listOperationCatalog',
      () => api.listOperationCatalog('runtime evidence'),
      '/api/control/operations/catalog?category=runtime%20evidence',
      'GET',
      undefined,
    ],
    [
      'listOperations',
      () => api.listOperations('cloud ops', 12),
      '/api/control/operations?take=12&category=cloud+ops',
      'GET',
      undefined,
    ],
    ['getOperation', () => api.getOperation('op-1'), '/api/control/operations/op-1', 'GET', undefined],
    [
      'startOperation',
      () =>
        api.startOperation({
          operationId: 'quality-smoke',
          environment: 'ci',
          ref: 'HEAD',
          inputs: {},
          collectEvidence: true,
          confirmation: null,
        }),
      '/api/control/operations',
      'POST',
      {
        operationId: 'quality-smoke',
        environment: 'ci',
        ref: 'HEAD',
        inputs: {},
        collectEvidence: true,
        confirmation: null,
      },
    ],
    ['cancelOperation', () => api.cancelOperation('op-1'), '/api/control/operations/op-1/cancel', 'POST', undefined],
    [
      'decideOperation',
      () => api.decideOperation('op-1', 'reject'),
      '/api/control/approvals/op-1/decision',
      'POST',
      { decision: 'reject', comment: null },
    ],
    [
      'compareEvidenceOperations',
      () => api.compareEvidenceOperations('left-1', 'right-1'),
      '/api/control/evidence/compare?left=left-1&right=right-1',
      'GET',
      undefined,
    ],
    ['listCloudEnvironments', () => api.listCloudEnvironments(), '/api/control/cloud/environments', 'GET', undefined],
    ['getAreaGeoJSON', () => api.getAreaGeoJSON('area 1'), '/api/control/areas/area 1/geojson', 'GET', undefined],
    ['getAreaCells', () => api.getAreaCells('area 1'), '/api/control/areas/area 1/grid-cells', 'GET', undefined],
    ['getAreaScenarios', () => api.getAreaScenarios('area 1'), '/api/control/areas/area 1/scenarios', 'GET', undefined],
    [
      'getAreaSensorNodes',
      () => api.getAreaSensorNodes('area 1'),
      '/api/control/areas/area 1/sensor-nodes',
      'GET',
      undefined,
    ],
    [
      'listSimulationRuns without filters',
      () => api.listSimulationRuns(null, null, 25),
      '/api/control/simulation-runs?take=25',
      'GET',
      undefined,
    ],
    [
      'getRuntimeSummary',
      () => api.getRuntimeSummary('area 1', 45),
      '/api/control/runtime/summary?areaCode=area+1&recentMinutes=45',
      'GET',
      undefined,
    ],
    [
      'getRuntimeSummary without area',
      () => api.getRuntimeSummary(undefined, 15),
      '/api/control/runtime/summary?recentMinutes=15',
      'GET',
      undefined,
    ],
    [
      'getRuntimeRunAudit',
      () => api.getRuntimeRunAudit('run-1'),
      '/api/control/runtime/runs/run-1/audit',
      'GET',
      undefined,
    ],
    [
      'getRuntimeOperationByRun',
      () => api.getRuntimeOperationByRun('run/1'),
      '/api/control/runtime/runs/run%2F1/operation',
      'GET',
      undefined,
    ],
    [
      'getRuntimeRunTimings',
      () => api.getRuntimeRunTimings('run-1'),
      '/api/control/runtime/runs/run-1/timings',
      'GET',
      undefined,
    ],
    [
      'getRuntimeOperationalHealth',
      () => api.getRuntimeOperationalHealth(),
      '/api/control/runtime/observability/health',
      'GET',
      undefined,
    ],
    [
      'getRuntimeRabbitMqMetrics',
      () => api.getRuntimeRabbitMqMetrics(),
      '/api/control/runtime/observability/rabbitmq',
      'GET',
      undefined,
    ],
    [
      'listRuntimeEvidence',
      () => api.listRuntimeEvidence(),
      '/api/control/runtime/observability/evidence',
      'GET',
      undefined,
    ],
    ['getRuntimeDiagnostics', () => api.getRuntimeDiagnostics(), '/api/control/runtime/diagnostics', 'GET', undefined],
    [
      'executeRuntimeDiagnostic',
      () =>
        api.executeRuntimeDiagnostic('diag 1', { areaCode: 'area-1', recentMinutes: 10, scenarioCode: 'scenario_b' }),
      '/api/control/runtime/diagnostics/diag 1',
      'POST',
      { areaCode: 'area-1', recentMinutes: 10, scenarioCode: 'scenario_b' },
    ],
    [
      'startRuntimeRun',
      () =>
        api.startRuntimeRun({
          scenarioCode: 'scenario_b',
          areaCode: 'area-1',
          sensorCount: 2,
          numberOfCycles: 3,
          intervalSeconds: 60,
          seed: 42,
          degradationProfile: 'none',
          collectEvidence: false,
          waitForCompletion: false,
          timeoutSeconds: 300,
          allowParallelRun: false,
          runLabel: null,
          degradationProfiles: ['none'],
        }),
      '/api/control/runtime/runs',
      'POST',
      {
        scenarioCode: 'scenario_b',
        areaCode: 'area-1',
        sensorCount: 2,
        numberOfCycles: 3,
        intervalSeconds: 60,
        seed: 42,
        degradationProfile: 'none',
        collectEvidence: false,
        waitForCompletion: false,
        timeoutSeconds: 300,
        allowParallelRun: false,
        runLabel: null,
        degradationProfiles: ['none'],
      },
    ],
    [
      'getRuntimeOperation',
      () => api.getRuntimeOperation('op/1'),
      '/api/control/runtime/operations/op%2F1',
      'GET',
      undefined,
    ],
    [
      'getRuntimeOperationByRequest',
      () => api.getRuntimeOperationByRequest('request/1'),
      '/api/control/runtime/operations/by-request/request%2F1',
      'GET',
      undefined,
    ],
    [
      'resetRuntimeState',
      () => api.resetRuntimeState({ scope: 'runtime', confirm: 'CONFIRM', dryRun: true }),
      '/api/control/runtime/reset',
      'POST',
      { scope: 'runtime', confirm: 'CONFIRM', dryRun: true },
    ],
    [
      'getControlledValidationP3Availability',
      () => api.getControlledValidationP3Availability(),
      '/api/dev/controlled-validation/p3',
      'GET',
      undefined,
    ],
    [
      'startControlledValidationP3',
      () =>
        api.startControlledValidationP3({
          runLabel: 'p3-smoke',
          waitForCompletion: false,
          collectEvidence: false,
          runAuditAfterCompletion: false,
          timeoutSeconds: 300,
        }),
      '/api/dev/controlled-validation/p3/run',
      'POST',
      {
        runLabel: 'p3-smoke',
        waitForCompletion: false,
        collectEvidence: false,
        runAuditAfterCompletion: false,
        timeoutSeconds: 300,
      },
    ],
    ['getAlerts', () => api.getAlerts('area/1'), '/api/control/areas/area%2F1/alerts/active', 'GET', undefined],
  ])('requests %s through the documented API route', async (_, call, expectedUrl, expectedMethod, expectedBody) => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await call();

    const [, options] = fetchMock.mock.calls[0];
    expect(fetchMock).toHaveBeenCalledWith(expectedUrl, expect.objectContaining({ credentials: 'include' }));
    expect((options as RequestInit).method ?? 'GET').toBe(expectedMethod);
    expect((options as RequestInit).body).toBe(expectedBody === undefined ? undefined : JSON.stringify(expectedBody));
  });

  it('requires an auth header before requesting the current user profile', async () => {
    await expect(api.getCurrentUser()).rejects.toThrow('No auth token set');
  });

  it('requests the current user when an auth header is configured', async () => {
    api.withAuthToken('explicit-token');
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'user-1', userName: 'sim' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const user = await api.getCurrentUser();

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/users-roles/me',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer explicit-token' }),
      }),
    );
    expect(user).toEqual({ id: 'user-1', userName: 'sim' });
  });
});
