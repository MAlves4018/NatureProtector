import { httpClient } from './httpClient';
import {
  AreaCellResponse,
  AreaGeoJSONResponse,
  AreaResponse,
  ControlledValidationP3AvailabilityResponse,
  ControlledValidationP3RunRequest,
  ControlledValidationP3RunResponse,
  LoginRequest,
  LoginResponse,
  RoleResponse,
  User,
  RuntimeDiagnosticCatalogResponse,
  RuntimeDiagnosticRequest,
  RuntimeDiagnosticResultResponse,
  RuntimeEvidenceCatalogResponse,
  RuntimeOperationalHealthResponse,
  RabbitMqMetricsResponse,
  RuntimeResetRequest,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SimulationRunResponse,
  SensorNodeResponse,
  CapabilityProfileResponse,
  OperationDefinitionResponse,
  EngineeringOperationResponse,
  StartOperationRequest,
  OperationComparisonResponse,
  CloudEnvironmentResponse,
  AdminUserResponse,
  AdminRoleResponse,
  AlertStateResponse,
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

    if (!headers?.Authorization) {
      return Promise.reject(new Error('No auth token set'));
    }
    const options = api.getRequestOptions();
    return httpClient.get<User | null>(url, options);
  },

  getCurrentCapabilities: () => {
    return httpClient.get<CapabilityProfileResponse>('/users-roles/me/capabilities', api.getRequestOptions());
  },

  listUsers: () => {
    return httpClient.get<AdminUserResponse[]>('/users-roles/users', api.getRequestOptions());
  },

  listRoles: () => {
    return httpClient.get<AdminRoleResponse[]>('/users-roles/roles', api.getRequestOptions());
  },

  addRoleToUser: (userId: string, roleId: number) => {
    return httpClient.put(`/users-roles/users/${userId}/roles/${roleId}`, undefined, api.getRequestOptions());
  },

  removeRoleFromUser: (userId: string, roleId: number) => {
    return httpClient.delete(`/users-roles/users/${userId}/roles/${roleId}`, api.getRequestOptions());
  },

  listOperationCatalog: (category?: string) => {
    const query = category ? `?category=${encodeURIComponent(category)}` : '';
    return httpClient.get<OperationDefinitionResponse[]>(
      `/control/operations/catalog${query}`,
      api.getRequestOptions(),
    );
  },

  listOperations: (category?: string, take = 50) => {
    const params = new URLSearchParams({ take: String(take) });
    if (category) {
      params.set('category', category);
    }
    return httpClient.get<EngineeringOperationResponse[]>(`/control/operations?${params}`, api.getRequestOptions());
  },

  getOperation: (operationId: string) => {
    return httpClient.get<EngineeringOperationResponse>(`/control/operations/${operationId}`, api.getRequestOptions());
  },

  startOperation: (request: StartOperationRequest) => {
    return httpClient.post<EngineeringOperationResponse>('/control/operations', request, api.getRequestOptions());
  },

  cancelOperation: (operationId: string) => {
    return httpClient.post<EngineeringOperationResponse>(
      `/control/operations/${operationId}/cancel`,
      undefined,
      api.getRequestOptions(),
    );
  },

  decideOperation: (operationId: string, decision: 'approve' | 'reject', comment?: string) => {
    return httpClient.post<EngineeringOperationResponse>(
      `/control/approvals/${operationId}/decision`,
      { decision, comment: comment ?? null },
      api.getRequestOptions(),
    );
  },

  compareEvidenceOperations: (left: string, right: string) => {
    const params = new URLSearchParams({ left, right });
    return httpClient.get<OperationComparisonResponse>(`/control/evidence/compare?${params}`, api.getRequestOptions());
  },

  listCloudEnvironments: () => {
    return httpClient.get<CloudEnvironmentResponse[]>('/control/cloud/environments', api.getRequestOptions());
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

  listSimulationRuns: (areaCode?: string | null, scenarioCode?: string | null, take = 10) => {
    const params = new URLSearchParams();
    if (areaCode) {
      params.set('areaCode', areaCode);
    }
    if (scenarioCode) {
      params.set('scenarioCode', scenarioCode);
    }
    params.set('take', String(take));

    return httpClient.get<SimulationRunResponse[]>(
      `/control/simulation-runs?${params.toString()}`,
      api.getRequestOptions(),
    );
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
    return httpClient.get<RuntimeRunAuditResponse>(`/control/runtime/runs/${runId}/audit`, api.getRequestOptions());
  },

  getRuntimeRun: (runId: string) => {
    return httpClient.get<RuntimeRunSummaryResponse>(`/control/runtime/runs/${runId}`, api.getRequestOptions());
  },

  getRuntimeRunTimings: (runId: string) => {
    return httpClient.get<RuntimeRunTimingSummaryResponse>(
      `/control/runtime/runs/${runId}/timings`,
      api.getRequestOptions(),
    );
  },

  getRuntimeOperationalHealth: () => {
    return httpClient.get<RuntimeOperationalHealthResponse>(
      '/control/runtime/observability/health',
      api.getRequestOptions(),
    );
  },

  getRuntimeRabbitMqMetrics: () => {
    return httpClient.get<RabbitMqMetricsResponse>('/control/runtime/observability/rabbitmq', api.getRequestOptions());
  },

  listRuntimeEvidence: () => {
    return httpClient.get<RuntimeEvidenceCatalogResponse>(
      '/control/runtime/observability/evidence',
      api.getRequestOptions(),
    );
  },

  downloadRuntimeEvidence: (evidenceId: string) => {
    return httpClient.download(
      `/control/runtime/observability/evidence/${encodeURIComponent(evidenceId)}`,
      api.getRequestOptions(),
    );
  },

  getRuntimeDiagnostics: () => {
    return httpClient.get<RuntimeDiagnosticCatalogResponse>('/control/runtime/diagnostics', api.getRequestOptions());
  },

  executeRuntimeDiagnostic: (diagnosticId: string, request: RuntimeDiagnosticRequest) => {
    return httpClient.post<RuntimeDiagnosticResultResponse>(
      `/control/runtime/diagnostics/${diagnosticId}`,
      request,
      api.getRequestOptions(),
    );
  },

  startRuntimeRun: (request: RuntimeRunStartRequest) => {
    return httpClient.post<RuntimeRunStartResponse>('/control/runtime/runs', request, api.getRequestOptions());
  },

  resetRuntimeState: (request: RuntimeResetRequest) => {
    return httpClient.post<RuntimeResetResponse>('/control/runtime/reset', request, api.getRequestOptions());
  },

  getControlledValidationP3Availability: () => {
    return httpClient.get<ControlledValidationP3AvailabilityResponse>(
      '/dev/controlled-validation/p3',
      api.getRequestOptions(),
    );
  },

  startControlledValidationP3: (request: ControlledValidationP3RunRequest) => {
    return httpClient.post<ControlledValidationP3RunResponse>(
      '/dev/controlled-validation/p3/run',
      request,
      api.getRequestOptions(),
    );
  },

  getAlerts: (areaCode: string) => {
    return httpClient.get<AlertStateResponse[]>(`/control/areas/${encodeURIComponent(areaCode)}/alerts/active`, api.getRequestOptions());
  },

  withAuthToken(token: string) {
    this.options.headers = {
      ...(this.options.headers as Record<string, string> | undefined),
      Authorization: `Bearer ${token}`,
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
  },
};
