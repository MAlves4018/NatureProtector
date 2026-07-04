import { api } from '../services/api';
import { LoginRequest, User } from '../types';
import React from 'react';

interface TokenContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  setToken: (token: string | null) => void;
  logout: () => void;
  refreshToken: () => Promise<void>;
}

const TokenContext = React.createContext<TokenContextType | undefined>(undefined);

export function TokenProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = React.useState<User | null>(null);
  const [token, setToken] = React.useState<string | null>(null);
  const [isInitializing, setIsInitializing] = React.useState(true);

  const logout = React.useCallback(() => {
    api.clearAuthToken();
    setToken(null);
    setUser(null);
    localStorage.removeItem('token');
  }, []);

  const applyCurrentUser = React.useCallback(
    (currentUser: User | null) => {
      if (!currentUser?.id) {
        logout();
        return;
      }

      setUser({
        id: currentUser.id,
        username: currentUser.username,
        fullName: currentUser.fullName,
        email: currentUser.email,
        roles: currentUser.roles,
      });
    },
    [logout],
  );

  const checkToken = React.useCallback(async () => {
    const storedToken = localStorage.getItem('token');

    if (!storedToken) {
      logout();
      return;
    }

    setToken(storedToken);
    api.withAuthToken(storedToken);
    const currentUser = await api.getCurrentUser();
    applyCurrentUser(currentUser);
  }, [applyCurrentUser, logout]);

  React.useEffect(() => {
    const initialize = async () => {
      try {
        const storedToken = localStorage.getItem('token');
        if (storedToken) {
          await checkToken();
        }
      } catch {
        logout();
      } finally {
        setIsInitializing(false);
      }
    };

    initialize();
  }, [logout, checkToken]);

  const login = React.useCallback(async (username: string, password: string) => {
    const req = { usernameOrEmail: username, password } as LoginRequest;
    const resp = await api.login(req);
    api.withAuthToken(resp.token);
    setToken(resp.token);
    setUser({
      id: resp.userId,
      username: resp.username,
      fullName: resp.fullName,
      email: resp.email,
      roles: resp.roles,
    });
    localStorage.setItem('token', resp.token);
  }, []);

  const refreshToken = React.useCallback(async () => {
    await checkToken();
  }, [checkToken]);

  const refreshTokenWithLogout = React.useCallback(async () => {
    await refreshToken().catch((error) => {
      if (error instanceof Error && error.message !== 'No auth token set') {
        logout();
      }
    });
  }, [logout, refreshToken]);

  return (
    <TokenContext.Provider value={{ user, token, login, setToken, logout, refreshToken: refreshTokenWithLogout }}>
      {isInitializing ? null : children}
    </TokenContext.Provider>
  );
}

export function useToken() {
  const context = React.useContext(TokenContext);
  if (context === undefined) {
    throw new Error('useToken must be used within a TokenProvider');
  }
  return context;
}
