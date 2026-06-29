import { useCallback, useEffect, useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../../services/api';
import type {
  AreaCellResponse,
  AreaResponse,
  ControlledValidationP3AvailabilityResponse,
  ControlledValidationP3RunRequest,
  ControlledValidationP3RunResponse,
  RuntimeDiagnosticDefinitionResponse,
  RuntimeDiagnosticResultResponse,
  RuntimeResetResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SensorNodeResponse,
} from '../../types';
import { getColors } from '../../utils/utils';
import { useToken } from '../../context/TokenContext';
import { LoggedOutBlock } from '../components/LoggedOutBlock';
import {
  DEFAULT_AREA,
  EVIDENCE_TABS,
  FLOW_TABS,
  MAIN_TABS,
  MODEL_TABS,
  MONITORING_TABS,
  SCENARIO_TABS,
} from './workspace/workspaceConstants';
import {
  AlertsView,
  AreaRiskView,
  CodeMapping,
  CompareBvsC,
  ControlledValidationEvidenceView,
  DataChain,
  DataProvenance,
  DeploymentServices,
  DiagnosticsView,
  DomainModel,
  ExportEvidence,
  LatestRunAuditView,
  LatestRunView,
  MapAndCells,
  MonitoringOverview,
  NominalFlow,
  P3NegativePipelinePanel,
  PersistenceViews,
  ProcessingPipeline,
  RetryQuarantine,
  RunOrchestrator,
  RunTimings,
  RuntimeChainView,
  RuntimeStateControl,
  ScenarioDefinition,
  SensorDashboards,
  TerritorialContext,
  V3ReadinessView,
} from './workspace/WorkspaceSections';
import { WorkspaceTopBar } from './workspace/WorkspaceTopBar';
import {
  Banner,
  buildP3RunLabel,
  formatError,
  isBlockedDegradationProfile,
  normalizeProfiles,
  parseJson,
  Tabs,
  WorkspacePanel,
} from './workspace/WorkspaceShared';

export function Workspace({ isDark, setIsDark }: { isDark: boolean; setIsDark: Dispatch<SetStateAction<boolean>> }) {
  const { token, user } = useToken();
  const colors = getColors(isDark);
  const navigate = useNavigate();
  const { areaCode: areaCodeParam } = useParams<{ areaCode: string }>();
  const [areaCode, setAreaCode] = useState(areaCodeParam || DEFAULT_AREA);
  const [areas, setAreas] = useState<AreaResponse[]>([]);
  const [mainTab, setMainTab] = useState<(typeof MAIN_TABS)[number]>('Monitoring');
  const [monitoringTab, setMonitoringTab] = useState<(typeof MONITORING_TABS)[number]>('Overview');
  const [scenarioTab, setScenarioTab] = useState<(typeof SCENARIO_TABS)[number]>('Run Orchestrator');
  const [evidenceTab, setEvidenceTab] = useState<(typeof EVIDENCE_TABS)[number]>('Latest Run Audit');
  const [flowTab, setFlowTab] = useState<(typeof FLOW_TABS)[number]>('Runtime Chain');
  const [modelTab, setModelTab] = useState<(typeof MODEL_TABS)[number]>('Data Chain');
  const [recentMinutes, setRecentMinutes] = useState(30);
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [runAudit, setRunAudit] = useState<RuntimeRunAuditResponse | null>(null);
  const [runTimings, setRunTimings] = useState<RuntimeRunTimingSummaryResponse | null>(null);
  const [runTimingsMessage, setRunTimingsMessage] = useState<string | null>(null);
  const [diagnostics, setDiagnostics] = useState<RuntimeDiagnosticDefinitionResponse[]>([]);
  const [diagnosticResult, setDiagnosticResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [selectedDiagnostic, setSelectedDiagnostic] = useState('runtime-table-counts');
  const [compareResult, setCompareResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [tableCounts, setTableCounts] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [scenarios, setScenarios] = useState<ScenarioResponse[]>([]);
  const [sensorNodes, setSensorNodes] = useState<SensorNodeResponse[]>([]);
  const [areaId, setAreaId] = useState('');
  const [geoJSON, setGeoJSON] = useState<any>(null);
  const [cells, setCells] = useState<AreaCellResponse[]>([]);
  const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);
  const [areaRiskDashboardLink, setAreaRiskDashboardLink] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [runResult, setRunResult] = useState<RuntimeRunStartResponse | null>(null);
  const [runMessage, setRunMessage] = useState<string | null>(null);
  const [submittingRun, setSubmittingRun] = useState(false);
  const [p3Availability, setP3Availability] = useState<ControlledValidationP3AvailabilityResponse | null>(null);
  const [p3RunRequest, setP3RunRequest] = useState<ControlledValidationP3RunRequest>(() => ({
    runLabel: buildP3RunLabel(),
    waitForCompletion: true,
    collectEvidence: true,
    runAuditAfterCompletion: false,
    timeoutSeconds: 300,
  }));
  const [p3RunResult, setP3RunResult] = useState<ControlledValidationP3RunResponse | null>(null);
  const [p3RunMessage, setP3RunMessage] = useState<string | null>(null);
  const [submittingP3Run, setSubmittingP3Run] = useState(false);
  const [resetResult, setResetResult] = useState<RuntimeResetResponse | null>(null);
  const [confirm, setConfirm] = useState('');
  const canAccessScenarioLab = Boolean(user && (user.roles.includes('Sim') || user.roles.includes('Admin')));
  const visibleMainTabs = canAccessScenarioLab ? MAIN_TABS : MAIN_TABS.filter((tab) => tab !== 'Scenario Lab');
  const [runForm, setRunForm] = useState<RuntimeRunStartRequest>({
    areaCode,
    scenarioCode: 'scenario_b',
    sensorCount: 6,
    numberOfCycles: 5,
    intervalSeconds: 5,
    seed: 12345,
    degradationProfile: 'none',
    degradationProfiles: ['none'],
    collectEvidence: false,
    waitForCompletion: false,
    timeoutSeconds: 180,
    allowParallelRun: false,
    runLabel: 'scenario-b-from-ui',
  });

  useEffect(() => {
    if (areaCodeParam && areaCodeParam !== areaCode) {
      setAreaCode(areaCodeParam);
    }
  }, [areaCodeParam, areaCode]);

  useEffect(() => {
    setRunForm((value) => ({ ...value, areaCode }));
  }, [areaCode]);

  useEffect(() => {
    fetch('/area_dashboards_links.txt')
      .then((response) => response.text())
      .then((text) =>
        setDashboardLinks(
          text
            .split('\n')
            .map((line) => line.trim())
            .filter(Boolean),
        ),
      )
      .catch(() => setDashboardLinks([]));

    fetch('/area_risk_link.txt')
      .then((response) => response.text())
      .then((text) =>
        setAreaRiskDashboardLink(
          text
            .split('\n')
            .map((line) => line.trim())
            .find(Boolean) ?? null,
        ),
      )
      .catch(() => setAreaRiskDashboardLink(null));
  }, []);

  const loadPublicWorkspace = useCallback(async () => {
    setLoading(true);
    try {
      const areaList = await api.getAreas();
      setAreas(areaList);

      const [sensorsResult, geoResult, cellsResult] = await Promise.allSettled([
        api.getAreaSensorNodes(areaCode),
        api.getAreaGeoJSON(areaCode),
        api.getAreaCells(areaCode),
      ]);

      const errors: string[] = [];
      if (sensorsResult.status === 'fulfilled') {
        setSensorNodes(sensorsResult.value);
      } else {
        errors.push(`Area sensors unavailable: ${formatError(sensorsResult.reason)}`);
      }

      if (geoResult.status === 'fulfilled') {
        setAreaId(geoResult.value.id);
        setGeoJSON(parseJson(geoResult.value.geometryGeoJson));
      } else {
        errors.push(`Area map unavailable: ${formatError(geoResult.reason)}`);
      }

      if (cellsResult.status === 'fulfilled') {
        setCells(cellsResult.value);
      } else {
        errors.push(`Area cells unavailable: ${formatError(cellsResult.reason)}`);
      }

      setMessage(errors.length > 0 ? errors.join(' ') : null);
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  }, [areaCode]);

  const loadWorkspacePipeline = useCallback(async () => {
    setLoading(true);
    try {
      const [summaryResult, diagnosticCatalog, areaScenarios] = await Promise.all([
        api.getRuntimeSummary(areaCode, recentMinutes),
        api.getRuntimeDiagnostics(),
        api.getAreaScenarios(areaCode),
      ]);

      setSummary(summaryResult);
      setDiagnostics(diagnosticCatalog.diagnostics);
      setScenarios(areaScenarios);
      setLastUpdated(new Date());
      setMessage(null);

      return summaryResult;
    } catch (error) {
      setMessage(formatError(error));
      return null;
    } finally {
      setLoading(false);
    }
  }, [areaCode, recentMinutes]);

  const loadWorkspaceSim = useCallback(
    async (latestRunId?: string | null) => {
      setLoading(true);

      try {
        const [diagnosticCatalog, p3AvailabilityResult] = await Promise.all([
          api.getRuntimeDiagnostics(),
          api.getControlledValidationP3Availability().catch((error) => {
            setP3RunMessage(`P3 endpoint availability unavailable: ${formatError(error)}`);
            return null;
          }),
        ]);

        setDiagnostics(diagnosticCatalog.diagnostics);
        setP3Availability(p3AvailabilityResult);
        setLastUpdated(new Date());
        setMessage(null);

        if (latestRunId) {
          setRunAudit(await api.getRuntimeRunAudit(latestRunId));
          try {
            setRunTimings(await api.getRuntimeRunTimings(latestRunId));
            setRunTimingsMessage(null);
          } catch (error) {
            setRunTimings(null);
            setRunTimingsMessage(
              `Run timings endpoint unavailable; using runtime summary fallback. ${formatError(error)}`,
            );
          }
        } else {
          setRunAudit(null);
          setRunTimings(null);
          setRunTimingsMessage(null);
        }

        const compare = await api.executeRuntimeDiagnostic('compare-latest-b-vs-c', {
          areaCode,
          recentMinutes,
          scenarioCode: 'scenario_b',
        });
        setCompareResult(compare);
        const counts = await api.executeRuntimeDiagnostic('runtime-table-counts', { areaCode, recentMinutes });
        setTableCounts(counts);
        setDiagnosticResult((current) => current ?? counts);
      } catch (error) {
        setMessage(formatError(error));
      } finally {
        setLoading(false);
      }
    },
    [areaCode, recentMinutes],
  );

  useEffect(() => {
    void loadPublicWorkspace();

    if (!user) {
      return;
    }

    if (user.roles.includes('Sim') || user.roles.includes('Admin')) {
      void loadWorkspacePipeline().then((summaryResult) => loadWorkspaceSim(summaryResult?.latestRun?.id ?? null));
    } else if (user.roles.includes('Pipeline')) {
      void loadWorkspacePipeline();
    }
  }, [loadWorkspacePipeline, loadWorkspaceSim, loadPublicWorkspace, user]);

  const displayRun = summary?.currentRun ?? summary?.latestRun ?? null;
  const activeSensorCount = sensorNodes.filter((sensor) => sensor.isActive).length;
  const sensorCountTooHigh =
    runForm.sensorCount != null && activeSensorCount > 0 && runForm.sensorCount > activeSensorCount;

  const changeArea = (nextArea: string) => {
    setAreaCode(nextArea);
    navigate(`/workspace/${nextArea}`);
  };

  const executeDiagnostic = async (id = selectedDiagnostic) => {
    if (!id) return;
    setLoading(true);
    try {
      const result = await api.executeRuntimeDiagnostic(id, {
        areaCode,
        recentMinutes,
        scenarioCode: runForm.scenarioCode,
      });
      setDiagnosticResult(result);
      setSelectedDiagnostic(id);
      setMessage(null);
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  };

  const startRun = async () => {
    if (sensorCountTooHigh) {
      setRunMessage(
        `sensorCount ${runForm.sensorCount} exceeds ${activeSensorCount} active sensor(s) for area '${areaCode}'.`,
      );
      return;
    }
    const blockedProfiles = normalizeProfiles(runForm.degradationProfiles, runForm.degradationProfile).filter(
      isBlockedDegradationProfile,
    );
    if (blockedProfiles.length > 0) {
      setRunMessage(`Blocked profile(s) cannot be started from the UI: ${blockedProfiles.join(', ')}.`);
      return;
    }

    setSubmittingRun(true);
    setRunMessage('Submitting run request...');
    try {
      const result = await api.startRuntimeRun({ ...runForm, areaCode });
      setRunResult(result);
      setRunMessage(result.message);

      const refreshedSummary = await loadWorkspacePipeline();
      await loadWorkspaceSim(refreshedSummary?.currentRun?.id ?? refreshedSummary?.latestRun?.id ?? null);
    } catch (error) {
      setRunMessage(formatError(error));
    } finally {
      setSubmittingRun(false);
    }
  };

  const startControlledValidationP3 = async () => {
    if (!p3Availability?.available) {
      setP3RunMessage(p3Availability?.message ?? 'P3 endpoint availability has not been confirmed by the backend.');
      return;
    }

    setSubmittingP3Run(true);
    setP3RunMessage('Submitting controlled validation P3 request...');
    try {
      const result = await api.startControlledValidationP3(p3RunRequest);
      setP3RunResult(result);
      setP3RunMessage(result.message);

      const refreshedSummary = await loadWorkspacePipeline();
      await loadWorkspaceSim(
        result.run?.id ?? refreshedSummary?.currentRun?.id ?? refreshedSummary?.latestRun?.id ?? null,
      );
    } catch (error) {
      setP3RunMessage(formatError(error));
    } finally {
      setSubmittingP3Run(false);
    }
  };

  const resetRuntime = async (dryRun: boolean) => {
    setLoading(true);
    try {
      const result = await api.resetRuntimeState({ scope: 'runtime-only', confirm, dryRun });
      setResetResult(result);
      setMessage(result.message);
      await loadWorkspaceSim();
    } catch (error) {
      setMessage(formatError(error));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main
      style={{
        minHeight: 'calc(100vh - 58px)',
        background: colors.pageBg,
        color: colors.textPrimary,
        padding: '18px',
        fontFamily: 'system-ui, -apple-system, sans-serif',
      }}
    >
      <WorkspaceTopBar
        colors={colors}
        isDark={isDark}
        setIsDark={setIsDark}
        areaCode={areaCode}
        areas={areas}
        latestRun={displayRun}
        recentMinutes={recentMinutes}
        setRecentMinutes={setRecentMinutes}
        lastUpdated={lastUpdated}
        loading={loading}
        onAreaChange={changeArea}
        onRefresh={() => {
          if (user?.roles.includes('Sim') || user?.roles.includes('Admin')) {
            loadWorkspaceSim();
            loadWorkspacePipeline();
          } else if (user?.roles.includes('Pipeline')) {
            loadWorkspacePipeline();
          }
          loadPublicWorkspace();
        }}
      />

      {message && (
        <Banner colors={colors} tone="#b45309">
          {message}
        </Banner>
      )}

      <Tabs values={visibleMainTabs} selected={mainTab} onSelect={setMainTab} colors={colors} />

      {mainTab === 'Monitoring' && (
        <WorkspacePanel colors={colors}>
          <Tabs values={MONITORING_TABS} selected={monitoringTab} onSelect={setMonitoringTab} colors={colors} compact />
          {monitoringTab === 'Overview' && (
            <MonitoringOverview
              colors={colors}
              summary={summary}
              run={displayRun}
              audit={runAudit}
              geoJSON={geoJSON}
              cells={cells}
              sensorNodes={sensorNodes}
              areaId={areaId}
            />
          )}
          {monitoringTab === 'Map & Cells' && (
            <MapAndCells colors={colors} areaId={areaId} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes} />
          )}
          {monitoringTab === 'Sensor Dashboards' && (
            <SensorDashboards colors={colors} areaId={areaId} dashboardLinks={dashboardLinks} />
          )}
          {monitoringTab === 'Area Risk' && (
            <AreaRiskView colors={colors} areaId={areaId} summary={summary} dashboardLink={areaRiskDashboardLink} />
          )}
          {monitoringTab === 'Alerts' && <AlertsView colors={colors} alerts={summary?.activeAlerts ?? []} />}
        </WorkspacePanel>
      )}

      {mainTab === 'Scenario Lab' && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={SCENARIO_TABS} selected={scenarioTab} onSelect={setScenarioTab} colors={colors} compact />
              {scenarioTab === 'Run Orchestrator' && (
                <RunOrchestrator
                  colors={colors}
                  scenarios={scenarios}
                  sensorCountTooHigh={sensorCountTooHigh}
                  activeSensorCount={activeSensorCount}
                  runForm={runForm}
                  setRunForm={setRunForm}
                  startRun={startRun}
                  submittingRun={submittingRun}
                  runResult={runResult}
                  runMessage={runMessage}
                  areaCode={areaCode}
                  setMainTab={setMainTab}
                  setScenarioTab={setScenarioTab}
                />
              )}
              {scenarioTab === 'Scenario Definition' && <ScenarioDefinition colors={colors} scenarios={scenarios} />}
              {scenarioTab === 'P3 Negative Pipeline' && (
                <P3NegativePipelinePanel
                  colors={colors}
                  summary={summary}
                  p3Availability={p3Availability}
                  p3RunRequest={p3RunRequest}
                  setP3RunRequest={setP3RunRequest}
                  p3RunResult={p3RunResult}
                  p3RunMessage={p3RunMessage}
                  submittingP3Run={submittingP3Run}
                  startControlledValidationP3={startControlledValidationP3}
                  setMainTab={setMainTab}
                  setEvidenceTab={setEvidenceTab}
                  setFlowTab={setFlowTab}
                />
              )}
              {scenarioTab === 'Latest Run' && <LatestRunView colors={colors} run={displayRun} />}
              {scenarioTab === 'Runtime State Control' && (
                <RuntimeStateControl
                  colors={colors}
                  confirm={confirm}
                  setConfirm={setConfirm}
                  resetRuntime={resetRuntime}
                  loading={loading}
                  resetResult={resetResult}
                />
              )}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === 'Evidence & Comparison' && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={EVIDENCE_TABS} selected={evidenceTab} onSelect={setEvidenceTab} colors={colors} compact />
              {evidenceTab === 'Latest Run Audit' && <LatestRunAuditView colors={colors} audit={runAudit} />}
              {evidenceTab === 'Controlled Validation' && <ControlledValidationEvidenceView colors={colors} />}
              {evidenceTab === 'Compare B vs C' && <CompareBvsC colors={colors} compare={compareResult} />}
              {evidenceTab === 'Run Timings' && (
                <RunTimings
                  colors={colors}
                  run={displayRun}
                  summary={summary}
                  audit={runAudit}
                  timings={runTimings}
                  timingsMessage={runTimingsMessage}
                />
              )}
              {evidenceTab === 'Diagnostics' && (
                <DiagnosticsView
                  colors={colors}
                  diagnostics={diagnostics}
                  selectedDiagnostic={selectedDiagnostic}
                  diagnosticResult={diagnosticResult}
                  executeDiagnostic={executeDiagnostic}
                  loading={loading}
                />
              )}
              {evidenceTab === 'Export Evidence' && (
                <ExportEvidence colors={colors} audit={runAudit} compare={compareResult} summary={summary} />
              )}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === 'Flow Explorer' && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={FLOW_TABS} selected={flowTab} onSelect={setFlowTab} colors={colors} compact />
              {flowTab === 'Runtime Chain' && (
                <RuntimeChainView
                  colors={colors}
                  summary={summary}
                  onNavigate={(target) => {
                    if (target === 'retry') setFlowTab('Retry & Quarantine');
                    if (target === 'state') setFlowTab('Persistence Views');
                    if (target === 'services') setFlowTab('Deployment & Services');
                    if (target === 'risk') {
                      setMainTab('Monitoring');
                      setMonitoringTab('Area Risk');
                    }
                    if (target === 'alerts') {
                      setMainTab('Monitoring');
                      setMonitoringTab('Alerts');
                    }
                  }}
                />
              )}
              {flowTab === 'Processing Pipeline' && <ProcessingPipeline colors={colors} summary={summary} />}
              {flowTab === 'Retry & Quarantine' && <RetryQuarantine colors={colors} summary={summary} />}
              {flowTab === 'Persistence Views' && <PersistenceViews colors={colors} tableCounts={tableCounts} />}
              {flowTab === 'Deployment & Services' && <DeploymentServices colors={colors} summary={summary} />}
              {flowTab === 'Nominal Flow' && (
                <NominalFlow colors={colors} summary={summary} audit={runAudit} runResult={runResult} />
              )}
            </>
          )}
        </WorkspacePanel>
      )}

      {mainTab === 'Model & Provenance' && (
        <WorkspacePanel colors={colors}>
          {!token ? (
            <LoggedOutBlock isDark={isDark} message="Please sign in to access this section." />
          ) : (
            <>
              <Tabs values={MODEL_TABS} selected={modelTab} onSelect={setModelTab} colors={colors} compact />
              {modelTab === 'Domain Model' && <DomainModel colors={colors} />}
              {modelTab === 'Data Chain' && <DataChain colors={colors} />}
              {modelTab === 'Data Provenance' && <DataProvenance colors={colors} summary={summary} />}
              {modelTab === 'V3 Readiness' && <V3ReadinessView colors={colors} />}
              {modelTab === 'Territorial & Weather Context' && (
                <TerritorialContext colors={colors} cells={cells} sensors={sensorNodes} summary={summary} />
              )}
              {modelTab === 'Code Mapping' && <CodeMapping colors={colors} />}
            </>
          )}
        </WorkspacePanel>
      )}
    </main>
  );
}
