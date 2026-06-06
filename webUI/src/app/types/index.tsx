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


export interface User {
    id: string;
    username: string;
    fullName: string | null;
    email: string | null;
    roles: string[];
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
    sensorNodes?: SensorNodeResponse[];
}

export type SensorInfo = string | {
    id?: string;
    code?: string;
    name?: string;
    type?: string;
    metric?: string;
    sensorType?: string;
    sensorId?: string;
    sensorName?: string;
    item1?: string;
    item2?: string;
};
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
    scoreComponents: RuntimeScoreComponentSummaryResponse | null;
    indexComparison: RuntimeIndexComparisonSummaryResponse | null;
    limitations: RuntimeLimitationResponse[];
    warnings: string[];
}

export interface RuntimeScoreComponentSummaryResponse {
    npScore: number | null;
    baseRisk: number | null;
    adjustedScore: number | null;
    score100: number | null;
    meteorologyComponent: number | null;
    droughtComponent: number | null;
    territoryComponent: number | null;
    hazardComponent: number | null;
    fuelComponent: number | null;
    geomorphologyComponent: number | null;
    confidenceFactor: number | null;
    integrityFactor: number | null;
    dominantDriver: string | null;
    parameterSetVersion: string | null;
    calculationStatus: string | null;
    limitations: string | null;
    latestAssessmentTimestamp: string | null;
    npRiskClass: string | null;
    npRiskClassLabel: string | null;
}

export interface RuntimeIndexComparisonSummaryResponse {
    fireWeatherIndex: number | null;
    normalizedFireWeatherIndex: number | null;
    fireWeatherCalculationStatus: string | null;
    keetchByramDroughtIndex: number | null;
    normalizedKeetchByramDroughtIndex: number | null;
    kbdiCalculationStatus: string | null;
    provenance: string | null;
    limitations: string | null;
    dailyPrecipitationMillimeters: number | null;
    logicalDate: string | null;
    calculatedFireWeatherIndex: number | null;
    referenceFireWeatherIndex: number | null;
    fireWeatherIndexValueSource: string | null;
    fireWeatherIpmaClass: string | null;
    fireWeatherIpmaClassLabel: string | null;
    fireWeatherEffisClass: string | null;
    fireWeatherThresholdDistanceToNextClass: number | null;
    fireWeatherNextIpmaClass: string | null;
    calculatedKeetchByramDroughtIndex: number | null;
    referenceKeetchByramDroughtIndex: number | null;
    kbdiValueSource: string | null;
    kbdiDrynessClass: string | null;
    kbdiDrynessClassLabel: string | null;
    kbdiAntecedentHistoryQuality: string | null;
    kbdiAntecedentDays: number | null;
    portugueseContextRiskProxyClass: string | null;
    portugueseContextRiskProxyLabel: string | null;
    territorialHazardProxyClass: string | null;
    localFwiPercentileStatus: string | null;
    localFwiPercentile: number | null;
    localFwiPercentileReason: string | null;
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

export interface RuntimeRunAuditResponse {
    run: RuntimeRunSummaryResponse;
    expectedEvents: number | null;
    acceptedReadings: number;
    missingEvents: number | null;
    rejected: number;
    quarantined: number;
    retryAttempts: number;
    riskAssessments: number;
    qualityFlagsSummary: RuntimeStatusCountResponse[];
    eligibilitySummary: RuntimeStatusCountResponse[];
    areaSnapshot: RuntimeAreaSnapshotAuditResponse | null;
    limitations: RuntimeLimitationResponse[];
    scoreComponents: RuntimeScoreComponentSummaryResponse | null;
    indexComparison: RuntimeIndexComparisonSummaryResponse | null;
}

export interface RuntimeRunTimingSummaryResponse {
    simulationRunId: string;
    runDurationMs: number | null;
    startedAt: string | null;
    endedAt: string | null;
    firstInboxReceivedAt: string | null;
    firstProcessingAttemptStartedAt: string | null;
    lastProcessingAttemptFinishedAt: string | null;
    firstRiskAssessmentCreatedAt: string | null;
    firstAlertTriggeredAt: string | null;
    timeToFirstInboxMs: number | null;
    timeToFirstProcessingAttemptMs: number | null;
    timeToFirstRiskAssessmentMs: number | null;
    timeToFirstAlertMs: number | null;
    attempts: RuntimeAttemptTimingSummaryResponse;
    stages: RuntimeStageTimingSummaryResponse[];
    limitations: string[];
}

export interface RuntimeAttemptTimingSummaryResponse {
    attemptCount: number;
    successfulAttempts: number;
    failedAttempts: number;
    quarantinedAttempts: number;
    minDurationMs: number | null;
    avgDurationMs: number | null;
    maxDurationMs: number | null;
}

export interface RuntimeStageTimingSummaryResponse {
    stage: string;
    outcome: string;
    errorCode: string | null;
    count: number;
    firstStartedAt: string | null;
    lastFinishedAt: string | null;
    minDurationMs: number | null;
    avgDurationMs: number | null;
    maxDurationMs: number | null;
}

export interface RuntimeAreaSnapshotAuditResponse {
    snapshotTimestamp: string;
    aggregateRiskScore: number;
    aggregateRiskLevel: string;
    assessmentCount: number;
    summary: string | null;
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
    degradationProfiles: string[] | null;
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
    coverageStatus: string | null;
    freshnessStatus: string | null;
    carryForwardStatus: string | null;
    lastAssessmentTimestamp: string | null;
    lastProjectionUpdatedAt: string | null;
    operationalStatusReason: string | null;
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
    degradationProfiles: string[] | null;
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

export interface ControlledValidationP3AvailabilityResponse {
    phase: string;
    environment: string;
    available: boolean;
    message: string;
    messageCount: number;
    executableCases: number;
    blockedCases: number;
}

export interface ControlledValidationP3RunRequest {
    runLabel: string | null;
    waitForCompletion: boolean;
    collectEvidence: boolean;
    runAuditAfterCompletion: boolean;
    timeoutSeconds: number;
}

export interface ControlledValidationP3RunResponse {
    requestId: string;
    runLabel: string;
    phase: string;
    status: string;
    environment: string;
    message: string;
    requestedAtUtc: string;
    messageCount: number;
    executableCases: number;
    blockedCases: number;
    evidencePath: string | null;
    queryPackPath: string | null;
    auditRequired: boolean;
    run: RuntimeRunSummaryResponse | null;
    notes: string[];
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


export type LogInOutProps = {
	isDark: boolean;
	message?: string;
	onAuthChange?: (signedIn: boolean) => void;
	mode?: "page" | "panel";
};
