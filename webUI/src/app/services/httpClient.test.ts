import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HttpError } from './httpError';
import { httpClient } from './httpClient';

describe('httpClient', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('returns null for empty 204 responses while preserving credentials and JSON headers', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await httpClient.post('/control/runtime/reset');

    expect(result).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/runtime/reset',
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    );
  });

  it('throws structured HttpError with numeric Retry-After for problem details bodies', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 429, title: 'Rate limit', detail: 'retry later' }), {
          status: 429,
          headers: { 'Retry-After': '4.2', 'Content-Type': 'application/json' },
        }),
      ),
    );

    try {
      await httpClient.get('/control/runtime/runs');
    } catch (value) {
      const httpError = value as HttpError;
      expect(httpError.status).toBe(429);
      expect(httpError.title).toBe('Rate limit');
      expect(httpError.message).toBe('retry later');
      expect(httpError.retryAfterSeconds).toBe(5);
    }
  });

  it('falls back to status text for non-json error bodies and date Retry-After headers', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-21T10:00:00Z'));
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('<html>down</html>', {
          status: 503,
          statusText: 'Service Unavailable',
          headers: { 'Retry-After': 'Tue, 21 Jul 2026 10:00:03 GMT' },
        }),
      ),
    );

    try {
      await httpClient.get('/health');
    } catch (value) {
      const httpError = value as HttpError;
      expect(httpError.status).toBe(503);
      expect(httpError.message).toBe('Service Unavailable');
      expect(httpError.retryAfterSeconds).toBe(3);
    }
    expect(warn).toHaveBeenCalledWith('Failed to parse JSON response body.');
  });

  it('downloads blobs, parses UTF-8 filenames and uses stored bearer tokens when explicit auth is absent', async () => {
    localStorage.setItem('token', 'stored-token');
    const fetchMock = vi.fn().mockResolvedValue(
      new Response('csv,data', {
        status: 200,
        headers: {
          'Content-Disposition': "attachment; filename*=UTF-8''runtime%20evidence.csv",
          'Content-Type': 'text/csv',
        },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await httpClient.download('/control/runtime/observability/evidence/evidence-1');

    expect(result.filename).toBe('runtime evidence.csv');
    expect(result.contentType).toBe('text/csv');
    expect(result.blob.size).toBe(8);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/runtime/observability/evidence/evidence-1',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer stored-token' }),
      }),
    );
  });

  it('serializes PUT bodies and DELETE requests without inventing a body', async () => {
    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify({ ok: true }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );
    vi.stubGlobal('fetch', fetchMock);

    await httpClient.put('/users-roles/users/user-1/roles/2', { reason: 'test' });
    await httpClient.delete('/users-roles/users/user-1/roles/2');

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      '/api/users-roles/users/user-1/roles/2',
      expect.objectContaining({ method: 'PUT', body: JSON.stringify({ reason: 'test' }) }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/users-roles/users/user-1/roles/2',
      expect.objectContaining({ method: 'DELETE' }),
    );
    expect(fetchMock.mock.calls[1][1]).not.toHaveProperty('body');
  });

  it('keeps explicit authorization headers and parses plain download filenames', async () => {
    localStorage.setItem('token', 'stored-token');
    const fetchMock = vi.fn().mockResolvedValue(
      new Response('plain,data', {
        status: 200,
        headers: {
          'Content-Disposition': 'attachment; filename="plain.csv"',
          'Content-Type': 'text/csv',
        },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await httpClient.download('/exports/plain', { headers: { Authorization: 'Bearer explicit' } });

    expect(result.filename).toBe('plain.csv');
    expect(result.blob.size).toBe(10);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/exports/plain',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer explicit' }),
      }),
    );
  });

  it('throws structured download errors and ignores invalid Retry-After values', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 404, title: 'Missing', message: 'evidence not found' }), {
          status: 404,
          headers: { 'Retry-After': 'not-a-date' },
        }),
      ),
    );

    await expect(httpClient.download('/control/runtime/observability/evidence/missing')).rejects.toMatchObject({
      status: 404,
      title: 'Missing',
      message: 'evidence not found',
      retryAfterSeconds: null,
    });
  });

  it('falls back to download status text when the error body is empty or malformed', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('{not json', {
          status: 500,
          statusText: 'Broken download',
        }),
      ),
    );

    await expect(httpClient.download('/exports/broken')).rejects.toMatchObject({
      status: 500,
      title: 'Request Failed',
      message: 'Broken download',
    });
    expect(warn).toHaveBeenCalledWith('Failed to parse JSON response body.');
  });
});
