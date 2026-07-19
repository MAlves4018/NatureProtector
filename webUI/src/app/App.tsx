import { CircleHelp, LogIn, Menu, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { Suspense, lazy, useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Navigate, Outlet, RouterProvider, createBrowserRouter, useLocation, useNavigate } from 'react-router-dom';
import { UiNavigation } from './navigation/Navigation';
import { AlertBanner } from './components/AlertBanner';
import { ErrorBoundary } from './components/ErrorBoundary';
import { Breadcrumbs } from './components/Breadcrumbs';
import { Skeleton } from './components/Skeleton';
import type { UiNavTarget } from './capabilities';
import { trapDialogTab } from './components/dialogFocus';
import { defaultPageFor, findUiPageDefinition } from './navigation/pageRegistry';
import { useUiLocale, useUiCapabilities } from './state';
import { useUiActivity } from './state/ActivityContext';
import './theme/ui.css';
import { UiProvider } from './state/Provider';
import { NavBar } from './components/views/navBar';
import { LogInOut } from './components/views/LogInOut';

const PublicOverviewPage = lazy(() =>
  import('./pages/PublicOverviewPage').then((module) => ({ default: module.PublicOverviewPage })),
);
const OverviewPage = lazy(() => import('./pages/OverviewPage').then((module) => ({ default: module.OverviewPage })));
const DataContextPage = lazy(() =>
  import('./pages/DataContextPage').then((module) => ({ default: module.DataContextPage })),
);
const RiskPage = lazy(() => import('./pages/RiskPage').then((module) => ({ default: module.RiskPage })));
const RunsPage = lazy(() => import('./pages/RunsPage').then((module) => ({ default: module.RunsPage })));
const SimulationPage = lazy(() =>
  import('./pages/SimulationPage').then((module) => ({ default: module.SimulationPage })),
);
const ScenarioComparisonPage = lazy(() =>
  import('./pages/ScenarioComparisonPage').then((module) => ({ default: module.ScenarioComparisonPage })),
);
const DatabaseQueriesPage = lazy(() =>
  import('./pages/DatabaseQueriesPage').then((module) => ({ default: module.DatabaseQueriesPage })),
);
const AboutPage = lazy(() => import('./pages/AboutPage').then((module) => ({ default: module.AboutPage })));
const PipelinePage = lazy(() => import('./pages/PipelinePage').then((module) => ({ default: module.PipelinePage })));
const QualityEvidencePage = lazy(() =>
  import('./pages/QualityEvidencePage').then((module) => ({ default: module.QualityEvidencePage })),
);
const DeploymentHealthPage = lazy(() =>
  import('./pages/DeploymentHealthPage').then((module) => ({ default: module.DeploymentHealthPage })),
);
const AdminPage = lazy(() => import('./pages/AdminPage').then((module) => ({ default: module.AdminPage })));
const ExperimentalPage = lazy(() =>
  import('./pages/ExperimentalPage').then((module) => ({ default: module.ExperimentalPage })),
);
const MissionControlPage = lazy(() =>
  import('./pages/MissionControlPage').then((module) => ({ default: module.MissionControlPage })),
);
const QualityRunsPage = lazy(() =>
  import('./pages/QualityRunsPage').then((module) => ({ default: module.QualityRunsPage })),
);
const EvidenceExplorerPage = lazy(() =>
  import('./pages/EvidenceExplorerPage').then((module) => ({ default: module.EvidenceExplorerPage })),
);
const DeploymentsPage = lazy(() =>
  import('./pages/DeploymentsPage').then((module) => ({ default: module.DeploymentsPage })),
);
const DashboardsPage = lazy(() =>
  import('./pages/DashboardsPage').then((module) => ({ default: module.DashboardsPage })),
);
const CloudResourcesPage = lazy(() =>
  import('./pages/CloudResourcesPage').then((module) => ({ default: module.CloudResourcesPage })),
);
const ApprovalsPage = lazy(() => import('./pages/ApprovalsPage').then((module) => ({ default: module.ApprovalsPage })));
const UserRoleAdministrationPage = lazy(() =>
  import('./pages/UserRoleAdministrationPage').then((module) => ({ default: module.UserRoleAdministrationPage })),
);

export function App() {
  const [isDark, setIsDark] = useState(false);
  return <UiRouter isDark={isDark} setIsDark={setIsDark} />;
}

function UiRouter({
  isDark,
  setIsDark,
}: {
  isDark: boolean;
  setIsDark: React.Dispatch<React.SetStateAction<boolean>>;
}) {
  const router = useMemo(
    () =>
      createBrowserRouter([
        {
          element: (
            <UiProvider isDark={isDark}>
              <Outlet />
            </UiProvider>
          ),
          children: [
            {
              path: '/login',
              element: (
                <>
                  <NavBar isDark={isDark} setIsDark={setIsDark} />
                  <LogInOut isDark={isDark} mode="page" />
                </>
              ),
            },
            {
              path: '/',
              element: <UiShell isDark={isDark} setIsDark={setIsDark} />,
              children: [
                { index: true, element: <UiDefaultRedirect /> },
                { path: 'demo', element: protect('demo', <PublicOverviewPage />) },
                { path: 'about', element: protect('about', <AboutPage />) },
                { path: 'context', element: protect('context', <DataContextPage />) },
                { path: 'dashboard', element: protect('dashboard', <DashboardsPage />) },
                { path: 'overview', element: protect('overview', <OverviewPage />) },
                { path: 'mission', element: protect('mission', <MissionControlPage />) },
                { path: 'risk', element: protect('risk', <RiskPage />) },
                { path: 'runs', element: protect('runs', <RunsPage />) },
                { path: 'simulation', element: protect('simulation', <SimulationPage />) },
                { path: 'scenario-compare', element: protect('scenario-compare', <ScenarioComparisonPage />) },
                { path: 'queries', element: protect('queries', <DatabaseQueriesPage />) },
                { path: 'pipeline', element: protect('pipeline', <PipelinePage />) },
                { path: 'quality', element: protect('quality', <QualityRunsPage />) },
                { path: 'qa', element: protect('qa', <QualityEvidencePage />) },
                { path: 'qa-tests', element: protect('qa-tests', <UiRetiredOperationalSurface name="Browser QA execution" />) },
                { path: 'evidence', element: protect('evidence', <EvidenceExplorerPage />) },
                { path: 'deployments', element: protect('deployments', <DeploymentsPage />) },
                { path: 'deployment-health', element: protect('deployment-health', <DeploymentHealthPage />) },
                { path: 'cloud', element: protect('cloud', <CloudResourcesPage />) },
                { path: 'db-queries', element: <Navigate to="/queries" replace /> },
                { path: 'approvals', element: protect('approvals', <ApprovalsPage />) },
                { path: 'users', element: protect('users', <UserRoleAdministrationPage />) },
                { path: 'admin', element: protect('admin', <AdminPage />) },
                { path: 'p3', element: protect('p3', <ExperimentalPage />) },
                { path: '*', element: <UiDefaultRedirect /> },
              ],
            },
          ],
        },
      ]),
    [isDark, setIsDark],
  );

  return <RouterProvider router={router} />;
}

function UiShell({ isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }) {
  const {
    pages,
    capabilities,
    setActivePage,
    user,
    isPublic,
    capabilityAuthority,
    capabilitiesLoading,
    capabilitiesError,
  } = useUiCapabilities();
  const { selectedRunId } = useUiActivity();
  const { copy, locale, setLocale } = useUiLocale();
  const [helpTopic, setHelpTopic] = useState<string | null>(null);
  const [navigationOpen, setNavigationOpen] = useState(false);
  const [navigationCompact, setNavigationCompact] = useState(false);
  const helpCloseRef = useRef<HTMLButtonElement | null>(null);
  const helpReturnFocusRef = useRef<HTMLElement | null>(null);
  const navigate = useNavigate();
  const location = useLocation();
  const routePage = getRoutePage(location.pathname);
  const activePage =
    routePage && pages.some((page) => page.id === routePage) ? routePage : defaultPageFor(capabilities);
  const activePageDef = pages.find((p) => p.id === activePage);

  useEffect(() => {
    const handler = (event: Event) => {
      helpReturnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setHelpTopic(String((event as CustomEvent).detail ?? 'overview'));
    };
    window.addEventListener('np-ui-help', handler);
    return () => window.removeEventListener('np-ui-help', handler);
  }, []);

  useEffect(() => {
    const handler = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'F1') {
        return;
      }

      event.preventDefault();
      helpReturnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setHelpTopic(activePageDef?.helpTopic ?? 'overview');
    };

    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [activePageDef?.helpTopic]);

  useEffect(() => {
    if (!helpTopic) {
      return;
    }

    helpCloseRef.current?.focus();
    return () => {
      helpReturnFocusRef.current?.focus();
      helpReturnFocusRef.current = null;
    };
  }, [helpTopic]);

  useEffect(() => {
    setActivePage(activePage);
  }, [activePage, setActivePage]);

  const closeHelp = () => setHelpTopic(null);
  const handleHelpDialogKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeHelp();
      return;
    }

    trapDialogTab(event);
  };

  const handleNavigate = useCallback(
    (target: UiNavTarget) => {
      setActivePage(target);
      const search = selectedRunId ? `?runId=${encodeURIComponent(selectedRunId)}` : '';
      navigate({ pathname: '/' + target, search });
    },
    [navigate, selectedRunId, setActivePage],
  );

  const mainId = 'ui-main';

  const breadcrumbItems = (() => {
    if (!activePageDef) return [];
    return [
      { label: activePageDef.group.charAt(0).toUpperCase() + activePageDef.group.slice(1) },
      { label: copy(activePageDef.labelKey as Parameters<typeof copy>[0]) },
    ];
  })();

  return (
    <>
      <div className="ui-shell" data-theme={isDark ? 'dark' : 'light'}>
        <a
          className="ui-skip"
          href={`#${mainId}`}
          onClick={(event) => {
            event.preventDefault();
            document.getElementById(mainId)?.focus();
          }}
        >
          {copy('nav.skip')}
        </a>
        <aside
          className={`ui-sidebar ${navigationOpen ? 'ui-sidebar-open' : ''} ${
            navigationCompact ? 'ui-sidebar-compact' : ''
          }`}
        >
          <UiNavigation pages={pages} activePage={activePage} copy={copy} onSelect={handleNavigate} />
          <button
            type="button"
            className="ui-sidebar-collapse"
            onClick={() => setNavigationCompact((value) => !value)}
            aria-label={navigationCompact ? 'Expandir navegação' : 'Recolher navegação'}
          >
            {navigationCompact ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
            <span>Recolher</span>
          </button>
        </aside>
        {navigationOpen && (
          <button
            type="button"
            className="ui-sidebar-scrim"
            aria-label="Fechar navegação"
            onClick={() => setNavigationOpen(false)}
          />
        )}
        <div className={`ui-workspace ${navigationCompact ? 'ui-workspace-expanded' : ''}`}>
          <NavBar isDark={isDark} setIsDark={setIsDark} />
          <header className="ui-context-bar">
            <button
              type="button"
              className="ui-top-icon ui-mobile-menu"
              aria-label="Abrir navegação"
              onClick={() => setNavigationOpen(true)}
            >
              <Menu size={18} />
            </button>
            <div>
              <p className="ui-kicker">{activePageDef ? copy(activePageDef.labelKey) : copy('app.name')}</p>
              <p className="ui-context-copy">
                {isPublic
                  ? 'Leitura pública do protótipo e do estado dos dados.'
                  : `Perfil ${user?.roles.join(', ') || 'sem funções'} · ${
                      capabilitiesLoading ? 'capabilities a validar' : capabilityAuthority
                    }`}
              </p>
            </div>
            <div className="ui-context-actions">
              <div className="ui-language">
                <button
                  type="button"
                  className={locale === 'pt-PT' ? 'ui-segment-active' : 'ui-segment'}
                  onClick={() => setLocale('pt-PT')}
                >
                  PT
                </button>
                <button
                  type="button"
                  className={locale === 'en' ? 'ui-segment-active' : 'ui-segment'}
                  onClick={() => setLocale('en')}
                >
                  EN
                </button>
              </div>
              <button
                type="button"
                className="ui-top-icon"
                onClick={() => {
                  helpReturnFocusRef.current =
                    document.activeElement instanceof HTMLElement ? document.activeElement : null;
                  setHelpTopic(activePageDef?.helpTopic ?? 'overview');
                }}
                aria-label="Ajuda contextual"
              >
                <CircleHelp size={17} />
              </button>
              {isPublic && (
                <button type="button" className="ui-button" onClick={() => navigate('/login')}>
                  <LogIn size={16} />
                  {copy('nav.login')}
                </button>
              )}
            </div>
          </header>
          {capabilitiesError && (
            <div className="ui-notice ui-warning" role="status">
              Capability authority unavailable. Authenticated write and protected capabilities are disabled until the
              backend profile is confirmed.
            </div>
          )}
          <AlertBanner />
          <Breadcrumbs items={breadcrumbItems} onNavigate={handleNavigate} />
          <main id={mainId} className="ui-content" tabIndex={-1}>
            <ErrorBoundary key={activePage}>
              <Suspense fallback={<UiRouteLoading />}>
                <Outlet />
              </Suspense>
            </ErrorBoundary>
          </main>
        </div>
        {helpTopic && (
          <div className="ui-help-overlay">
            <section
              className="ui-help-dialog"
              role="dialog"
              aria-modal="true"
              aria-label={copy('help.title')}
              onKeyDown={handleHelpDialogKeyDown}
            >
              <h2 className="ui-page-title">{copy('help.title')}</h2>
              <p>{copy('help.intro')}</p>
              <p>{copy('help.browser')}</p>
              <button type="button" className="ui-button" onClick={closeHelp} ref={helpCloseRef}>
                {copy('help.close')}
              </button>
            </section>
          </div>
        )}
      </div>
    </>
  );
}

function getRoutePage(pathname: string): UiNavTarget | undefined {
  const segments = pathname.split('/').filter(Boolean);
  const candidate = segments[0] === 'ui-v2' ? segments[1] : segments[0];
  return candidate && findUiPageDefinition(candidate) ? (candidate as UiNavTarget) : undefined;
}

function protect(page: UiNavTarget, element: React.ReactNode) {
  return <UiCapabilityRoute page={page}>{element}</UiCapabilityRoute>;
}

function UiCapabilityRoute({ page, children }: { page: UiNavTarget; children: React.ReactNode }) {
  const { capabilities, capabilitiesLoading, capabilityAuthority } = useUiCapabilities();
  const definition = findUiPageDefinition(page);

  if (capabilitiesLoading) return <UiRouteLoading />;

  const authorized = Boolean(definition?.requiredCapabilities.every((capability) => capabilities.has(capability)));
  if (!authorized) {
    return (
      <section className="ui-page" role="alert">
        <h2>Acesso negado</h2>
        <p>Esta rota requer capabilities confirmadas pelo backend.</p>
        <p className="ui-notice">Authority: {capabilityAuthority}</p>
      </section>
    );
  }

  return <>{children}</>;
}

function UiRetiredOperationalSurface({ name }: { name: string }) {
  return (
    <section className="ui-page" role="alert">
      <h2>Superfície operacional indisponível</h2>
      <p>{name} was removed from delivery because it had no authoritative backend execution path.</p>
      <p className="ui-notice ui-warning">No result, timing, row count or pass/fail state is generated here.</p>
    </section>
  );
}

function UiDefaultRedirect() {
  const { pages, capabilities, capabilitiesLoading } = useUiCapabilities();
  if (capabilitiesLoading) {
    return <UiRouteLoading />;
  }
  const fallbackPage = defaultPageFor(capabilities);
  const targetPage = pages.some((page) => page.id === fallbackPage) ? fallbackPage : 'demo';

  return <Navigate to={'/' + targetPage} replace />;
}

function UiRouteLoading() {
  return (
    <section className="ui-page" aria-busy="true" aria-live="polite">
      <Skeleton width="44%" height="30px" />
      <Skeleton count={3} />
    </section>
  );
}
