import { httpClient } from './httpClient';
import {
    AreaCellResponse,
    AreaGeoJSONResponse,
    AreaResponse,
    LoginRequest,
    LoginResponse,
    RoleResponse,
    User,
    RuntimeDiagnosticCatalogResponse,
    RuntimeDiagnosticRequest,
    RuntimeDiagnosticResultResponse,
    RuntimeResetRequest,
    RuntimeResetResponse,
    RuntimeRunAuditResponse,
    RuntimeRunStartRequest,
    RuntimeRunStartResponse,
    RuntimeRunTimingSummaryResponse,
    RuntimeSummaryResponse,
    ScenarioResponse,
    SensorNodeResponse
} from '../types';

export const api = {
    options: {} as RequestInit,

    getRequestOptions() {
        return Object.keys(this.options).length > 0 ? this.options : undefined;
    },

    login: (request: LoginRequest) => {
        const url = '/users-roles/login';
        return httpClient.post<LoginResponse>(url, request, api.getRequestOptions());
    },

    logout: () => {
        const url = '/users-roles/logout';
        return httpClient.post(url, undefined, api.getRequestOptions());
    },

    getRoles: (userid: string) => {
        const url = `/users-roles/${userid}/roles`;
        return httpClient.get<RoleResponse[]>(url, api.getRequestOptions());
    },

    getCurrentUser: (): Promise<User | null> => {
        const url = '/users-roles/me';
        const headers = api.options.headers as Record<string, string> | undefined;

        if (!headers || !headers.Authorization) {
            return Promise.reject(new Error("No auth token set"));
        }
        const options = api.getRequestOptions();
        return httpClient.get<User | null>(url, api.options);
    },

    getAreas: () => {
        const url = '/control/areas';
        return httpClient.get<AreaResponse[]>(url, api.getRequestOptions());
    },

    getAreaGeoJSON: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/geojson`;
        return httpClient.get<AreaGeoJSONResponse>(url, api.getRequestOptions());
    },

    getAreaCells: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/grid-cells`;
        return httpClient.get<AreaCellResponse[]>(url, api.getRequestOptions());
    },

    getAreaScenarios: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/scenarios`;
        return httpClient.get<ScenarioResponse[]>(url, api.getRequestOptions());
    },

    getAreaSensorNodes: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/sensor-nodes`;
        return httpClient.get<SensorNodeResponse[]>(url, api.getRequestOptions());
    },

    getRuntimeSummary: (areaCode?: string, recentMinutes = 30) => {
        const params = new URLSearchParams();
        if (areaCode) {
            params.set('areaCode', areaCode);
        }
        params.set('recentMinutes', String(recentMinutes));

        const url = `/control/runtime/summary?${params.toString()}`;
        return httpClient.get<RuntimeSummaryResponse>(url, api.getRequestOptions());
    },

    getRuntimeRunAudit: (runId: string) => {
        return httpClient.get<RuntimeRunAuditResponse>(
            `/control/runtime/runs/${runId}/audit`,
            api.getRequestOptions()
        );
    },

    getRuntimeRunTimings: (runId: string) => {
        return httpClient.get<RuntimeRunTimingSummaryResponse>(
            `/control/runtime/runs/${runId}/timings`,
            api.getRequestOptions()
        );
    },

    getRuntimeDiagnostics: () => {
        return httpClient.get<RuntimeDiagnosticCatalogResponse>(
            '/control/runtime/diagnostics',
            api.getRequestOptions()
        );
    },

    executeRuntimeDiagnostic: (diagnosticId: string, request: RuntimeDiagnosticRequest) => {
        return httpClient.post<RuntimeDiagnosticResultResponse>(
            `/control/runtime/diagnostics/${diagnosticId}`,
            request,
            api.getRequestOptions()
        );
    },

    startRuntimeRun: (request: RuntimeRunStartRequest) => {
        return httpClient.post<RuntimeRunStartResponse>(
            '/control/runtime/runs',
            request,
            api.getRequestOptions()
        );
    },

    resetRuntimeState: (request: RuntimeResetRequest) => {
        return httpClient.post<RuntimeResetResponse>(
            '/control/runtime/reset',
            request,
            api.getRequestOptions()
        );
    },

    withAuthToken(token: string) {

        this.options.headers = {
            ...(this.options.headers as Record<string, string> | undefined),
            Authorization: `Bearer ${token}`
        };

        return this;
    },

    clearAuthToken() {
        if (!this.options.headers) {
            return this;
        }

        const { Authorization, authorization, ...rest } = this.options.headers as Record<string, string>;
        this.options.headers = rest;
        return this;
    }
};
