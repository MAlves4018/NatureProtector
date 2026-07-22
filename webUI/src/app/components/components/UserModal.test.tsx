import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UserModal } from './UserModal';

const logoutMock = vi.fn();
const navigateMock = vi.fn();

vi.mock('../../context/TokenContext', () => ({
  useToken: () => ({ logout: logoutMock }),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

describe('UserModal', () => {
  beforeEach(() => {
    logoutMock.mockClear();
    navigateMock.mockClear();
  });

  it('does not render while closed', () => {
    render(<UserModal isDark={false} user={null} isOpen={false} onClose={vi.fn()} />);

    expect(screen.queryByRole('dialog', { name: 'User information' })).not.toBeInTheDocument();
  });

  it('renders authenticated user details and logs out through the token authority', () => {
    const onClose = vi.fn();
    render(
      <UserModal
        isDark
        isOpen
        onClose={onClose}
        user={{
          id: 'user-1',
          username: 'miguel',
          fullName: 'Miguel Alves',
          email: 'miguel@example.test',
          roles: ['Admin', 'Operator'],
        }}
      />,
    );

    expect(screen.getByRole('dialog', { name: 'User information' })).toBeInTheDocument();
    expect(screen.getByText('Miguel Alves')).toBeInTheDocument();
    expect(screen.getByText('miguel@example.test')).toBeInTheDocument();
    expect(screen.getByText('Admin, Operator')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Logout' }));
    expect(logoutMock).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes via backdrop and Escape, and sends anonymous users to login', () => {
    const onClose = vi.fn();
    render(<UserModal isDark={false} user={null} isOpen onClose={onClose} />);

    expect(screen.getByText('You are not logged in.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Go to Login' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(navigateMock).toHaveBeenCalledWith('/login');

    fireEvent.click(screen.getByRole('button', { name: 'Close user dialog' }));
    expect(onClose).toHaveBeenCalledTimes(2);

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(3);
  });
});
