import { fireEvent, render, screen } from '@testing-library/react';
import { ChakraProvider, defaultSystem } from '@chakra-ui/react';
import type React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { NavBar } from './navBar';

const refreshTokenMock = vi.fn();
const userModalMock = vi.fn();
let tokenState: { token: string | null; user: { fullName?: string; username?: string } | null };

vi.mock('../../context/TokenContext', () => ({
  useToken: () => ({
    token: tokenState.token,
    user: tokenState.user,
    refreshToken: refreshTokenMock,
  }),
}));

vi.mock('../components/UserModal', () => ({
  UserModal: (props: { isOpen: boolean; isDark: boolean; user: unknown; onClose: () => void }) => {
    userModalMock(props);
    return props.isOpen ? (
      <button type="button" onClick={props.onClose}>
        mock user modal
      </button>
    ) : null;
  },
}));

describe('NavBar', () => {
  beforeEach(() => {
    refreshTokenMock.mockClear();
    userModalMock.mockClear();
    tokenState = { token: null, user: null };
  });

  it('shows anonymous state and toggles theme', () => {
    const setIsDark = vi.fn();

    renderNavBar({ isDark: false, setIsDark });

    expect(screen.getByText('Not signed in')).toBeInTheDocument();
    expect(screen.getByText('Dark')).toBeInTheDocument();
    fireEvent.click(screen.getByText('Dark'));

    expect(setIsDark).toHaveBeenCalledTimes(1);
    expect(refreshTokenMock).not.toHaveBeenCalled();
  });

  it('refreshes signed-in sessions without user details and opens the user modal', () => {
    tokenState = { token: 'token-1', user: null };

    renderNavBar({ isDark: false, setIsDark: vi.fn() });

    expect(refreshTokenMock).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Signed in')).toBeInTheDocument();
    fireEvent.click(screen.getByText('Signed in'));

    expect(screen.getByText('mock user modal')).toBeInTheDocument();
    expect(userModalMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        isOpen: true,
        isDark: false,
        user: null,
      }),
    );
  });

  it('uses authenticated initials and avoids redundant token refresh when user is loaded', () => {
    tokenState = { token: 'token-1', user: { fullName: 'Miguel Alves', username: 'miguel' } };

    renderNavBar({ isDark: true, setIsDark: vi.fn() });

    expect(screen.getByText('Signed in as Miguel Alves')).toBeInTheDocument();
    expect(screen.getByText('M')).toBeInTheDocument();
    expect(screen.getByText('Light')).toBeInTheDocument();
    expect(refreshTokenMock).not.toHaveBeenCalled();
  });
});

function renderNavBar(props: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }) {
  return render(
    <ChakraProvider value={defaultSystem}>
      <NavBar {...props} />
    </ChakraProvider>,
  );
}
