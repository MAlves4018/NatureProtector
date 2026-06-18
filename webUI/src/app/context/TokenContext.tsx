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

    const logout = () => {
        api.clearAuthToken();
        setToken(null);
        setUser(null);
        localStorage.removeItem('token');
    };

    const applyCurrentUser = (currentUser: User | null) => {
        if (!currentUser?.id) {
            logout();
            return;
        }

        setUser({
            id: currentUser.id,
            username: currentUser.username,
            fullName: currentUser.fullName,
            email: currentUser.email,
            roles: currentUser.roles
        });
    };

    const checkToken = async () => {
        const storedToken = localStorage.getItem('token');

        if (!storedToken) {
            logout();
            return;
        }

        setToken(storedToken);
        api.withAuthToken(storedToken);
        const currentUser = await api.getCurrentUser();
        applyCurrentUser(currentUser);
    };

    useEffect(() => {
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
            throw error;
        }
    };

    const refreshToken = async () => {
        await checkToken();
    };

    const refreshTokenWithLogout = async () => {
        await refreshToken().catch((e) => {
            if (e instanceof Error && e.message !== "No auth token set") {
                logout();
            }
        });
    };

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
