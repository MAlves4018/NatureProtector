export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface LoginResponse {
  userId: string;
  username: string;
  fullName: string | null;
  email: string | null;
  roles: string[];
  token: string;
}

export interface RoleResponse {
  id: string;
  name: string;
}

export interface ErrorResponse {
  title: string;
  status: number;
  message: string;
  detail?: string;
}

export interface User {
  id: string;
  username: string;
  fullName: string | null;
  email: string | null;
  roles: string[];
}

export type LogInOutProps = {
  isDark: boolean;
  message?: string;
  onAuthChange?: (signedIn: boolean) => void;
  mode?: 'page' | 'panel';
};
