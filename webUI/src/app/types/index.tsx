export interface ErrorResponse {
    title: string,
    status: number,
    message: string,
    detail?: string,
}

export interface AreaResponse {
    id: string,
    code: string,
    name: string,
    countryCode: string,
    configurationVersionNumber: number,
    gridCellCount: number,
    sensorNodeCount: number,
    scenarioCount: number
}

export interface AreaGeoJSONResponse {
    id: string;
    geometryGeoJson: string | null;
}


export type MapType = 'standard' | 'terrain';

export interface MapProps {
    areaId: string;
    showGrid: boolean;
    showColorByDanger?: boolean;
    mapType: MapType;
    isDark?: boolean;
    geoJSON?: any;
    cells?: AreaCellResponse[];
}

export interface SensorInfo { 
    id: string; 
    type: string;
}
export interface AreaCellResponse {
    cellCode: string;
    sensorNodeIds: SensorInfo[];
    configurationVersionNumber: number;
    centroidLatitude: number;
    centroidLongitude: number;
    altitudeMeters: number | null;
    slopeDegrees: number | null;
    aspectDegrees: number | null;
    landCoverClass: string | null;
    dominantForestType: string | null;
    dominantFuelModel: string | null;
    treeCoverDensity: number | null;
    structuralHazard: string | null;
    conjuncturalHazard: string | null;
    sensorNodeCount: number;
}

export interface SensorNodeResponse {
    id: string;
    name: string;
    type: string;
    configurationVersionNumber: number;
    cellCode: string;
    profileName: string;
    sensorFamily: string | null;
    networkName: string | null;
    latitude: number;
    longitude: number;
    altitudeMeters: number | null;
    isActive: boolean;
    installationProfile: string | null;
}

export interface RuntimeSummaryResponse {
    generatedAtUtc: string;
    recentWindowMinutes: number;
    areaCode: string | null;
    currentRun: RuntimeRunSummaryResponse | null;
    latestRun: RuntimeRunSummaryResponse | null;
    pipeline: RuntimePipelineSummaryResponse;
    risk: RuntimeRiskSummaryResponse;
    areaOperationalState: RuntimeAreaOperationalSummaryResponse | null;
    cellOperationalStateCount: number;
    activeAlerts: RuntimeAlertSummaryResponse[];
    freshness: RuntimeFreshnessSummaryResponse | null;
    limitations: RuntimeLimitationResponse[];
    warnings: string[];
}

export interface RuntimeRunSummaryResponse {
    id: string;
    areaCode: string;
    scenarioCode: string;
    scenarioName: string;
    status: string;
    configurationVersionNumber: number;
    createdAt: string;
    startedAt: string | null;
    endedAt: string | null;
    durationSeconds: number | null;
    logicalStartTimestamp: string;
    intervalSeconds: number;
    numberOfCycles: number;
    executionSeed: number | null;
    metadataJson: string | null;
    metadataJsonStatus: string;
    orchestratorCorrelationId: string | null;
    runOverrides: RuntimeRunOverridesResponse | null;
}

export interface RuntimeRunOverridesResponse {
    requested: RuntimeRunOverrideValuesResponse | null;
    resolved: RuntimeRunOverrideValuesResponse | null;
    selectedSensorNames: string[];
}

export interface RuntimeRunOverrideValuesResponse {
    sensorCount: number | null;
    numberOfCycles: number | null;
    intervalSeconds: number | null;
    seed: number | null;
    degradationProfile: string | null;
    orchestratorCorrelationId: string | null;
}

export interface RuntimePipelineSummaryResponse {
    inboxTotal: number;
    inboxRecent: number;
    inboxByStatus: RuntimeStatusCountResponse[];
    attemptsRecent: number;
    attemptsByOutcomeAndError: RuntimeAttemptCountResponse[];
    rejectedRecent: number;
    rejectedTotal: number;
    rejectedByCode: RuntimeCodeCountResponse[];
    quarantinedRecent: number;
    quarantinedTotal: number;
    quarantinedByCode: RuntimeCodeCountResponse[];
    latestRejected: RuntimeRejectedEventResponse[];
    latestQuarantined: RuntimeQuarantinedEventResponse[];
    latestFailedAttempts: RuntimeProcessingAttemptResponse[];
}

export interface RuntimeStatusCountResponse {
    status: string;
    count: number;
}

export interface RuntimeAttemptCountResponse {
    outcome: string;
    errorCode: string | null;
    count: number;
}

export interface RuntimeCodeCountResponse {
    code: string;
    count: number;
}

export interface RuntimeRejectedEventResponse {
    id: string;
    eventId: string | null;
    rejectionCode: string;
    rejectionReason: string;
    rejectedAt: string;
    metadataJson: string | null;
}

export interface RuntimeQuarantinedEventResponse {
    id: string;
    eventId: string;
    finalAttemptNumber: number;
    quarantineCode: string;
    quarantineReason: string;
    quarantinedAt: string;
    metadataJson: string | null;
}

export interface RuntimeProcessingAttemptResponse {
    id: string;
    inboxEventId: string;
    attemptNumber: number;
    stage: string;
    startedAt: string;
    finishedAt: string | null;
    outcome: string;
    errorCode: string | null;
    errorMessage: string | null;
}

export interface RuntimeRiskSummaryResponse {
    recentCount: number;
    minScore: number | null;
    maxScore: number | null;
    latestTimestamp: string | null;
    recentScores: RuntimeRiskPointResponse[];
}

export interface RuntimeRiskPointResponse {
    timestamp: string;
    riskScore: number;
    riskLevel: string;
}

export interface RuntimeAreaOperationalSummaryResponse {
    areaCode: string;
    configurationVersionNumber: number;
    snapshotTimestamp: string;
    aggregateRiskScore: number;
    aggregateRiskLevel: string;
    severity: string;
    summary: string | null;
    assessmentCount: number;
    updatedAt: string;
    alertState: string | null;
}

export interface RuntimeAlertSummaryResponse {
    id: string;
    areaCode: string;
    configurationVersionNumber: number;
    alertCode: string;
    severity: string;
    status: string;
    message: string;
    triggeredAt: string;
    updatedAt: string;
    resolvedAt: string | null;
    alertState: string | null;
}

export interface RuntimeLimitationResponse {
    code: string;
    message: string;
}

export interface RuntimeFreshnessSummaryResponse {
    freshCount: number;
    staleCount: number;
    expiredCount: number;
    oldestIncludedAssessment: string | null;
    latestIncludedAssessment: string | null;
    freshSeconds: number;
    staleSeconds: number;
    note: string;
}

export interface RuntimeDiagnosticCatalogResponse {
    diagnostics: RuntimeDiagnosticDefinitionResponse[];
}

export interface RuntimeDiagnosticDefinitionResponse {
    id: string;
    title: string;
    description: string;
}

export interface RuntimeDiagnosticRequest {
    areaCode?: string | null;
    recentMinutes?: number;
    scenarioCode?: string | null;
}

export interface RuntimeDiagnosticResultResponse {
    id: string;
    title: string;
    description: string;
    columns: string[];
    rows: Record<string, string | null>[];
    limitations: string[];
}

export interface RuntimeRunStartRequest {
    areaCode: string;
    scenarioCode: string;
    sensorCount: number | null;
    numberOfCycles: number | null;
    intervalSeconds: number | null;
    seed: number | null;
    degradationProfile: string | null;
    collectEvidence: boolean;
    waitForCompletion: boolean;
    timeoutSeconds: number;
    allowParallelRun: boolean;
    runLabel: string | null;
}

export interface RuntimeRunStartResponse {
    requestId: string;
    orchestratorCorrelationId: string;
    status: string;
    message: string;
    requestedAtUtc: string;
    requested: RuntimeRunOverrideValuesResponse;
    run: RuntimeRunSummaryResponse | null;
    warnings: string[];
    logDirectory: string | null;
    evidenceDirectory: string | null;
}

export interface RuntimeResetRequest {
    scope: string;
    confirm: string;
    dryRun: boolean;
}

export interface RuntimeTableCountResponse {
    schema: string;
    table: string;
    count: number;
}

export interface RuntimeResetResponse {
    generatedAtUtc: string;
    dryRun: boolean;
    status: string;
    message: string;
    before: RuntimeTableCountResponse[];
    after: RuntimeTableCountResponse[];
}

export interface ScenarioResponse {
    id: string;
    code: string;
    name: string;
    scenarioKind: string;
    configurationVersionNumber: number;
    description: string | null;
    baseScenarioCode: string | null;
    datasetBindingCount: number;
}


type LatLng = [number, number];
// ─── Areas  ─────────────────────
export interface NPArea { id: number; name: string; type: string; coords: LatLng[] }


// ─── Grid Centers ──────────────────────────────────────────────────────────────
export interface GridInfo { id: number; area_id: number; coords: LatLng[] }
