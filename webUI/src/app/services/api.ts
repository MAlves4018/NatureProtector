import {httpClient} from './httpClient';
import {
    AreaCellResponse,
    AreaGeoJSONResponse,
    AreaResponse,
    RuntimeDiagnosticCatalogResponse,
    RuntimeDiagnosticRequest,
    RuntimeDiagnosticResultResponse,
    RuntimeResetRequest,
    RuntimeResetResponse,
    RuntimeRunStartRequest,
    RuntimeRunStartResponse,
    RuntimeSummaryResponse,
    ScenarioResponse,
    SensorNodeResponse
} from '../types';

export const api = {
    getAreas: () => {
        const url = '/control/areas';
        return httpClient.get<AreaResponse[]>(url);
    },

    getAreaGeoJSON: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/geojson`;
        return httpClient.get<AreaGeoJSONResponse>(url); 
    },

    getAreaCells: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/grid-cells`;
        return httpClient.get<AreaCellResponse[]>(url);
    },

    getAreaScenarios: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/scenarios`;
        return httpClient.get<ScenarioResponse[]>(url);
    },

    getAreaSensorNodes: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/sensor-nodes`;
        return httpClient.get<SensorNodeResponse[]>(url);
    },

    getRuntimeSummary: (areaCode?: string, recentMinutes = 30) => {
        const params = new URLSearchParams();
        if (areaCode) {
            params.set('areaCode', areaCode);
        }
        params.set('recentMinutes', String(recentMinutes));

        const url = `/control/runtime/summary?${params.toString()}`;
        return httpClient.get<RuntimeSummaryResponse>(url);
    },

    getRuntimeDiagnostics: () => {
        return httpClient.get<RuntimeDiagnosticCatalogResponse>('/control/runtime/diagnostics');
    },

    executeRuntimeDiagnostic: (diagnosticId: string, request: RuntimeDiagnosticRequest) => {
        return httpClient.post<RuntimeDiagnosticResultResponse>(`/control/runtime/diagnostics/${diagnosticId}`, request);
    },

    startRuntimeRun: (request: RuntimeRunStartRequest) => {
        return httpClient.post<RuntimeRunStartResponse>('/control/runtime/runs', request);
    },

    resetRuntimeState: (request: RuntimeResetRequest) => {
        return httpClient.post<RuntimeResetResponse>('/control/runtime/reset', request);
    },
};
