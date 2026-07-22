import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { TokenProvider, useToken } from './TokenContext';

vi.mock('../services/api', () => ({
  api: {
    clearAuthToken: vi.fn(),
    withAuthToken: vi.fn(),
    getCurrentUser: vi.fn(),
    login: vi.fn(),
  },
}));

function TokenProbe() {
  const { user, token, login, logout, refreshToken, setToken } = useToken();
  return (
    <div>
      <p>user:{user?.username ?? 'none'}</p>
      <p>token:{token ?? 'none'}</p>
      <button type="button" onClick={() => void login('miguel', 'secret')}>
        login
      </button>
      <button type="button" onClick={() => logout()}>
        logout
      </button>
      <button type="button" onClick={() => void refreshToken()}>
        refresh
      </button>
      <button type="button" onClick={() => setToken('manual')}>
        set-token
      </button>
    </div>
  );
}

describe('TokenContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(api.clearAuthToken).mockClear();
    vi.mocked(api.withAuthToken).mockClear();
    vi.mocked(api.getCurrentUser).mockReset();
    vi.mocked(api.login).mockReset();
  });

  it('initializes from a stored token and applies the current user authority', async () => {
    localStorage.setItem('token', 'stored-token');
    vi.mocked(api.getCurrentUser).mockResolvedValue({
      id: 'user-1',
      username: 'miguel',
      fullName: 'Miguel Alves',
      email: 'miguel@example.test',
      roles: ['Admin'],
    });

    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );

    expect(await screen.findByText('user:miguel')).toBeInTheDocument();
    expect(screen.getByText('token:stored-token')).toBeInTheDocument();
    expect(api.withAuthToken).toHaveBeenCalledWith('stored-token');
  });

  it('logs in, updates local storage and logs out cleanly', async () => {
    vi.mocked(api.login).mockResolvedValue({
      token: 'new-token',
      userId: 'user-2',
      username: 'operator',
      fullName: 'Ops User',
      email: 'ops@example.test',
      roles: ['Operator'],
    });

    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );

    expect(await screen.findByText('user:none')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'login' }));
    expect(await screen.findByText('user:operator')).toBeInTheDocument();
    expect(screen.getByText('token:new-token')).toBeInTheDocument();
    expect(localStorage.getItem('token')).toBe('new-token');

    fireEvent.click(screen.getByRole('button', { name: 'logout' }));
    await waitFor(() => expect(screen.getByText('user:none')).toBeInTheDocument());
    expect(screen.getByText('token:none')).toBeInTheDocument();
    expect(api.clearAuthToken).toHaveBeenCalled();
    expect(localStorage.getItem('token')).toBeNull();
  });

  it('logs out when refresh resolves to an invalid current user', async () => {
    localStorage.setItem('token', 'stored-token');
    vi.mocked(api.getCurrentUser).mockResolvedValue(null);

    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );

    expect(await screen.findByText('user:none')).toBeInTheDocument();
    expect(api.clearAuthToken).toHaveBeenCalled();
  });

  it('renders without backend calls when there is no stored token', async () => {
    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );

    expect(await screen.findByText('user:none')).toBeInTheDocument();
    expect(screen.getByText('token:none')).toBeInTheDocument();
    expect(api.getCurrentUser).not.toHaveBeenCalled();
    expect(api.withAuthToken).not.toHaveBeenCalled();
  });

  it('refreshes a stored token into the current backend user authority', async () => {
    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );
    expect(await screen.findByText('user:none')).toBeInTheDocument();

    localStorage.setItem('token', 'refresh-token');
    vi.mocked(api.getCurrentUser).mockResolvedValue({
      id: 'user-3',
      username: 'reviewer',
      fullName: 'Review User',
      email: 'review@example.test',
      roles: ['ReleaseApprover'],
    });

    fireEvent.click(screen.getByRole('button', { name: 'refresh' }));

    expect(await screen.findByText('user:reviewer')).toBeInTheDocument();
    expect(screen.getByText('token:refresh-token')).toBeInTheDocument();
    expect(api.withAuthToken).toHaveBeenCalledWith('refresh-token');
  });

  it('logs out on refresh failures except the explicit missing-token authority error', async () => {
    render(
      <TokenProvider>
        <TokenProbe />
      </TokenProvider>,
    );
    expect(await screen.findByText('user:none')).toBeInTheDocument();

    localStorage.setItem('token', 'stale-token');
    vi.mocked(api.getCurrentUser).mockRejectedValueOnce(new Error('profile endpoint unavailable'));
    fireEvent.click(screen.getByRole('button', { name: 'refresh' }));

    await waitFor(() => expect(api.clearAuthToken).toHaveBeenCalledTimes(1));
    expect(localStorage.getItem('token')).toBeNull();

    localStorage.setItem('token', 'missing-token');
    vi.mocked(api.getCurrentUser).mockRejectedValueOnce(new Error('No auth token set'));
    fireEvent.click(screen.getByRole('button', { name: 'refresh' }));

    await waitFor(() => expect(screen.getByText('token:missing-token')).toBeInTheDocument());
    expect(api.clearAuthToken).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem('token')).toBe('missing-token');
  });
});
