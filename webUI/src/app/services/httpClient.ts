import {HttpError} from './httpError';
import {ErrorResponse} from '../types/index';

const API_BASE = '/api';

class HttpClient {
    async request<T>(path: string, options: RequestInit = {}): Promise<T> {
        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
            ...(options.headers as Record<string, string>),
        };

        if (!headers.Authorization) {
            const storedToken = localStorage.getItem('token');
            if (storedToken) {
                headers.Authorization = `Bearer ${storedToken}`;
            }
        }

        const response = await fetch(`${API_BASE}${path}`, {
            ...options,
            credentials: 'include',
            headers,
        });

        const text = await response.text();
        let data: any = {};

        const hasBody = text && text.trim().length > 0;
        try {
            if (hasBody) {
                data = JSON.parse(text);
            }
        } catch (e) {
            console.warn("Failed to parse JSON response:", text);
        }

        if (!response.ok) {
            const errorBody: ErrorResponse = data.message || data.detail ? {
                status: data.status || response.status,
                title: data.title || 'Request Failed',
                message: data.message || data.detail,
                detail: data.detail,
            } : {
                status: response.status,
                title: 'Request Failed',
                message: response.statusText || 'Unknown error'
            };
            throw HttpError.fromResponseBody(errorBody);
        }

        if (!hasBody || response.status === 204) {
            return null as T;
        }

        return data as T;
    }

    get<T>(path: string, options?: RequestInit): Promise<T> {
        return this.request<T>(path, options);
    }

    post<T>(path: string, body?: any, options?: RequestInit): Promise<T> {
        return this.request<T>(path, {
            method: 'POST',
            body: body ? JSON.stringify(body) : undefined,
            ...options
        });
    }

    put<T>(path: string, body?: any, options?: RequestInit): Promise<T> {
        return this.request<T>(path, {
            method: 'PUT',
            body: body ? JSON.stringify(body) : undefined,
            ...options
        });
    }

    delete<T>(path: string, options?: RequestInit): Promise<T> {
        return this.request<T>(path, { method: 'DELETE', ...options });
    }
}

export const httpClient = new HttpClient();
