import {HttpError} from './httpError';
import {ErrorResponse} from '../types/index';

const API_BASE = '/api';

class HttpClient {
    async request<T>(path: string, options: RequestInit = {}): Promise<T> {
        const headers = {
            'Content-Type': 'application/json',
            ...(options.headers as Record<string, string>),
        };


        const response = await fetch(`${API_BASE}${path}`, {
            ...options,
            credentials: 'include',
            headers,
        });

        const text = await response.text();
        let data: any = {};

        try {
            if (text && text.trim().length > 0) {
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

        return data as T;
    }

    get<T>(path: string): Promise<T> {
        return this.request<T>(path);
    }

    post<T>(path: string, body?: any): Promise<T> {
        return this.request<T>(path, {
            method: 'POST',
            body: body ? JSON.stringify(body) : undefined,
        });
    }

    put<T>(path: string, body?: any): Promise<T> {
        return this.request<T>(path, {
            method: 'PUT',
            body: body ? JSON.stringify(body) : undefined,
        });
    }

    delete<T>(path: string): Promise<T> {
        return this.request<T>(path, { method: 'DELETE' });
    }
}

export const httpClient = new HttpClient();
