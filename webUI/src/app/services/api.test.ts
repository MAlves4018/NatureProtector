import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

describe('api client', () => {
  beforeEach(() => {
    localStorage.clear();
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
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(null, { status: 204 })),
    );

    await api.logout();

    expect(fetch).toHaveBeenCalledWith(
      '/api/users-roles/logout',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer local-token' }),
      }),
    );
  });
});
