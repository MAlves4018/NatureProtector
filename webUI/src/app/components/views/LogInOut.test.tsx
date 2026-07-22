import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LogInOut } from './LogInOut';

const auth = vi.hoisted(() => ({
  token: null as string | null,
  login: vi.fn(),
  logout: vi.fn(),
  apiLogout: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock('../../context/TokenContext', () => ({
  useToken: () => ({
    token: auth.token,
    login: auth.login,
    logout: auth.logout,
  }),
}));

vi.mock('../../services/api', () => ({
  api: {
    logout: auth.apiLogout,
  },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => auth.navigate,
}));

describe('LogInOut', () => {
  beforeEach(() => {
    auth.token = null;
    auth.login.mockReset();
    auth.logout.mockReset();
    auth.apiLogout.mockReset();
    auth.navigate.mockReset();
  });

  it('submits credentials, clears password and reports successful authentication', async () => {
    auth.login.mockResolvedValue(undefined);
    const onAuthChange = vi.fn();
    render(<LogInOut isDark={false} onAuthChange={onAuthChange} />);

    const signIn = screen.getByRole('button', { name: /sign in/i });
    expect(signIn).toBeDisabled();
    fireEvent.change(screen.getByLabelText(/username or email/i), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'admin123' } });
    fireEvent.click(signIn);

    await waitFor(() => expect(auth.login).toHaveBeenCalledWith('admin', 'admin123'));
    expect(onAuthChange).toHaveBeenCalledWith(true);
    expect(auth.navigate).toHaveBeenCalledWith('/');
    expect(screen.getByLabelText(/password/i)).toHaveValue('');
  });

  it('surfaces login failure without navigating', async () => {
    auth.login.mockRejectedValue(new Error('invalid credentials'));
    const onAuthChange = vi.fn();
    render(<LogInOut isDark onAuthChange={onAuthChange} />);

    fireEvent.change(screen.getByLabelText(/username or email/i), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText('invalid credentials')).toBeInTheDocument();
    expect(onAuthChange).toHaveBeenCalledWith(false);
    expect(auth.navigate).not.toHaveBeenCalled();
  });

  it('logs out through backend and clears local auth state', async () => {
    auth.token = 'token';
    auth.apiLogout.mockResolvedValue(undefined);
    const onAuthChange = vi.fn();
    render(<LogInOut isDark={false} mode="panel" onAuthChange={onAuthChange} />);

    fireEvent.click(screen.getByRole('button', { name: /sign out/i }));

    await waitFor(() => expect(auth.apiLogout).toHaveBeenCalledTimes(1));
    expect(auth.logout).toHaveBeenCalledTimes(1);
    expect(onAuthChange).toHaveBeenCalledWith(false);
    expect(auth.navigate).toHaveBeenCalledWith('/');
  });
});
