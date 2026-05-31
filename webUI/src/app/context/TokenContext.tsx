import { useEffect } from "react";
import { api } from "../services/api";
import { LoginRequest, User } from "../types";
import React from "react";

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
    const checkToken = async () => {
        const storedToken = localStorage.getItem('token');

        if (storedToken) {
            setToken(storedToken);
            api.withAuthToken(storedToken);
        }
    };

    useEffect(() => {
        const initialize = async () => {
            try {
                await checkToken();
            } finally {
                setIsInitializing(false);
            }
        };

        initialize();
    }, []);

    const login = async (username: string, password: string) => {
        try {
            const req = { usernameOrEmail: username, password: password } as LoginRequest;
            const resp = await api.login(req);
            api.withAuthToken(resp.token);
            setToken(resp.token);
            const user: User = {
                id: resp.userId,
                username: resp.username,
                fullName: resp.fullName,
                email: resp.email,
                roles: resp.roles
            };

            setUser(user);
            localStorage.setItem('token', resp.token);
        } catch (error) {
            console.error('Login failed:', error);
            throw error;
        }
    };

    const logout = () => {
        api.clearAuthToken();
        setToken(null);
        setUser(null);
        localStorage.removeItem('token');
    };

    const refreshToken = async () => {
        await checkToken();
        const storedToken = localStorage.getItem('token');
        if (!storedToken) {
            logout();
            return;
        }
        await api.withAuthToken(storedToken).getCurrentUser().then(user => {
            if (!user) {
                console.warn('refreshToken: /users-roles/me returned null');
                logout();
                return;
            }
            if (!user.id) {
                console.warn('refreshToken: /users-roles/me missing id', user);
                logout();
                return;
            }
            const currentUser: User = {
                id: user.id,
                username: user.username,
                fullName: user.fullName,
                email: user.email,
                roles: user.roles
            };
            setUser(currentUser)
            console.log('refreshToken: user loaded', currentUser);
        }).catch((e) => {
            console.error('refreshToken: /users-roles/me threw', e);
            if (e instanceof Error && e.message !== "No auth token set") {
                logout();
            }
        });
    };

    return (
        <TokenContext.Provider value={{ user, token, login, setToken, logout, refreshToken }}>
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
