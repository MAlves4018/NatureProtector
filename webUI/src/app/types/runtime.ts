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

export interface SimulationRunResponse {
  id: string;
  areaCode: string;
  scenarioCode: string;
  scenarioName: string;
  status: string;
  configurationVersionNumber: number;
  createdAt: string;
  startedAt: string | null;
  endedAt: string | null;
  logicalStartTimestamp: string;
  intervalSeconds: number;
  numberOfCycles: number;
  executionSeed: number | null;
  metadataJson: string | null;
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
  dataScope?: RuntimeDataScopeResponse | null;
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
  dataScope?: RuntimeDataScopeResponse | null;
  timeline?: RuntimeTimelinePointResponse[] | null;
}

export interface RuntimeDataScopeResponse {
  requestedRunId: string;
  resolvedRunId: string | null;
  dataRunId: string | null;
  observedAt: string;
  source: string;
  scope: string;
  limitations: RuntimeLimitationResponse[];
}

export interface RuntimeTimelinePointResponse {
  stage: string;
  timestamp: string;
  source: string;
  scope: string;
  eventId: string | null;
  status: string | null;
}

export interface RuntimeAttemptTimingSummaryResponse {
  attemptCount: number;
  successfulAttempts: number;
  failedAttempts: number;
  quarantinedAttempts: number;
  minDurationMs: number | null;
  avgDurationMs: number | null;
  maxDurationMs: number | null;
  p50DurationMs: number | null;
  p95DurationMs: number | null;
  p99DurationMs: number | null;
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
  aggregateRiskScore: number | null;
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

export interface RuntimeOperationalHealthResponse {
  observedAt: string;
  components: RuntimeOperationalHealthComponentResponse[];
  rabbitMq: RabbitMqMetricsResponse;
  limitations: RuntimeLimitationResponse[];
}

export interface RuntimeOperationalHealthComponentResponse {
  component: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unknown' | 'NotInstrumented' | 'NotApplicable' | string;
  observedAt: string;
  source: string;
  reason: string;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  ageSeconds: number | null;
  scope: string;
  limitation: string | null;
}

export interface RabbitMqMetricsResponse {
  observedAt: string;
  source: string;
  collectionStatus: 'Measured' | 'Unavailable' | 'Error' | 'NotApplicable' | string;
  queues: RabbitMqQueueMetricResponse[];
  limitations: RuntimeLimitationResponse[];
}

export interface RabbitMqQueueMetricResponse {
  queueName: string;
  queueRole: 'PrimaryWorkQueue' | 'AuxiliaryDiagnosticQueue' | string;
  enabled: boolean;
  consumerRequired: boolean;
  blocksRuntimeHealth: boolean;
  messagesReady: number | null;
  messagesUnacknowledged: number | null;
  messagesTotal: number | null;
  consumers: number | null;
  observedAt: string;
  source: string;
  collectionStatus: 'Measured' | 'Unavailable' | 'Error' | 'NotApplicable' | string;
  limitation: string | null;
}

export interface RuntimeEvidenceCatalogResponse {
  observedAt: string;
  items: RuntimeEvidenceCatalogItemResponse[];
  limitations: RuntimeLimitationResponse[];
}

export interface RuntimeEvidenceCatalogItemResponse {
  evidenceId: string;
  title: string;
  type: string;
  generatedAt: string | null;
  environment: string;
  scope: string;
  version: string | null;
  contentAvailable: boolean;
  downloadAvailable: boolean;
  size: number;
  status: string;
  limitation: string | null;
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
  operationId?: string | null;
}

export interface RuntimeOperationAccountingResponse {
  expectedObservations: number;
  acceptedObservations: number;
  pendingInbox: number;
  processingInbox: number;
  retryPendingInbox: number;
  processedInbox: number;
  quarantinedInbox: number;
  settled: boolean;
}

export interface RuntimeOperationResponse {
  operationId: string;
  requestId: string;
  correlationId: string;
  simulationRunId: string | null;
  requestedState: string;
  providerState: string;
  runState: string;
  processingState: string;
  state: string;
  terminalOutcome: string | null;
  acceptedAt: string;
  updatedAt: string;
  startedAt: string | null;
  producerCompletedAt: string | null;
  systemCompletedAt: string | null;
  finishedAt: string | null;
  failureCode: string | null;
  failureDetail: string | null;
  evidenceId: string | null;
  evidenceLocation: string | null;
  accounting: RuntimeOperationAccountingResponse;
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

export interface AlertStateResponse {
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

export interface ROQueryRequest {
  type?: string;
  table: string;
  columns?: string[];
  limit?: number;
  offset?: number;
}

export interface ROQueryResponse {
  columns: string[];
  rows: Record<string, string | null>[];
  limitations: string[];
}
