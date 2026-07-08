import { LogIn, Moon, Sun } from 'lucide-react';
import { Suspense, lazy, useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Navigate, Outlet, RouterProvider, createBrowserRouter, useLocation, useNavigate } from 'react-router-dom';
import { UiNavigation } from './navigation/Navigation';
import { AlertBanner } from './components/AlertBanner';
import { ErrorBoundary } from './components/ErrorBoundary';
import { Breadcrumbs } from './components/Breadcrumbs';
import { Skeleton } from './components/Skeleton';
import type { UiNavTarget } from './capabilities';
import { trapDialogTab } from './components/dialogFocus';
import { defaultPageFor } from './navigation/pageRegistry';
import { useUiLocale, useUiCapabilities } from './state';
import './theme/ui.css';
import { UiProvider } from './state/Provider';
import { NavBar } from './components/views/navBar';
import { LogInOut } from './components/views/LogInOut';

const PublicOverviewPage = lazy(() =>
  import('./pages/PublicOverviewPage').then((module) => ({ default: module.PublicOverviewPage })),
);
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
const AboutPage = lazy(() => import('./pages/AboutPage').then((module) => ({ default: module.AboutPage })));
const PipelinePage = lazy(() => import('./pages/PipelinePage').then((module) => ({ default: module.PipelinePage })));
const QualityEvidencePage = lazy(() =>
  import('./pages/QualityEvidencePage').then((module) => ({ default: module.QualityEvidencePage })),
);
const QaTestSuitePage = lazy(() =>
  import('./pages/QaTestSuitePage').then((module) => ({ default: module.QaTestSuitePage })),
);
const DatabaseQueriesPage = lazy(() =>
  import('./pages/DatabaseQueriesPage').then((module) => ({ default: module.DatabaseQueriesPage })),
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
  return (
    <UiProvider isDark={isDark}>
      <UiRouter isDark={isDark} setIsDark={setIsDark} />
    </UiProvider>
  );
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
            { path: 'demo', element: <PublicOverviewPage /> },
            { path: 'about', element: <AboutPage /> },
            { path: 'context', element: <DataContextPage /> },
            { path: 'dashboard', element: <DashboardsPage /> },
            { path: 'mission', element: <MissionControlPage /> },
            { path: 'risk', element: <RiskPage /> },
            { path: 'runs', element: <RunsPage /> },
            { path: 'simulation', element: <SimulationPage /> },
            { path: 'scenario-compare', element: <ScenarioComparisonPage /> },
            { path: 'pipeline', element: <PipelinePage /> },
            { path: 'quality', element: <QualityRunsPage /> },
            { path: 'qa', element: <QualityEvidencePage /> },
            { path: 'qa-tests', element: <QaTestSuitePage /> },
            { path: 'evidence', element: <EvidenceExplorerPage /> },
            { path: 'deployments', element: <DeploymentsPage /> },
            { path: 'deployment-health', element: <DeploymentHealthPage /> },
            { path: 'cloud', element: <CloudResourcesPage /> },
            { path: 'db-queries', element: <DatabaseQueriesPage /> },
            { path: 'approvals', element: <ApprovalsPage /> },
            { path: 'users', element: <UserRoleAdministrationPage /> },
            { path: 'admin', element: <AdminPage /> },
            { path: 'p3', element: <ExperimentalPage /> },
            { path: '*', element: <UiDefaultRedirect /> },
          ],
        },
      ]),
    [isDark, setIsDark],
  );

  return <RouterProvider router={router} />;
}

function UiShell({ isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }) {
  const { pages, capabilities, setActivePage, user, isPublic, capabilityAuthority, capabilitiesLoading } =
    useUiCapabilities();
  const { copy, locale, setLocale } = useUiLocale();
  const [helpTopic, setHelpTopic] = useState<string | null>(null);
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
      navigate('/' + target);
    },
    [navigate, setActivePage],
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
      <NavBar isDark={isDark} setIsDark={setIsDark} />
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
        <header className="ui-hero">
          <div>
            <p className="ui-kicker">
              {copy('app.prototype')} / {copy('app.readOnly')}
            </p>
            <h1 className="ui-title">{copy('app.name')}</h1>
            <p className="ui-lead">
              {isPublic
                ? 'Entrada pública orientada ao produto: propósito, limites e estado dos dados sem superfícies internas.'
                : `Perfil ativo: ${user?.roles.join(', ') || 'sem funções'}. Autorização: ${
                    capabilitiesLoading ? 'a validar no backend' : capabilityAuthority
                  }.`}
            </p>
          </div>
          <div className="ui-hero-actions">
            <div className="ui-language">
              <button
                type="button"
                className={locale === 'pt-PT' ? 'ui-button' : 'ui-secondary'}
                onClick={() => setLocale('pt-PT')}
              >
                {copy('language.pt')}
              </button>
              <button
                type="button"
                className={locale === 'en' ? 'ui-button' : 'ui-secondary'}
                onClick={() => setLocale('en')}
              >
                {copy('language.en')}
              </button>
            </div>
            <span className="ui-badge">
              {isDark ? <Moon size={14} /> : <Sun size={14} />}
              {isDark ? 'Dark' : 'Light'}
            </span>
            {isPublic && (
              <button type="button" className="ui-button" onClick={() => navigate('/login')}>
                <LogIn size={16} />
                {copy('nav.login')}
              </button>
            )}
          </div>
        </header>
        <UiNavigation pages={pages} activePage={activePage} copy={copy} onSelect={handleNavigate} />
        <AlertBanner />
        <Breadcrumbs items={breadcrumbItems} onNavigate={handleNavigate} />
        <main id={mainId} className="ui-content" tabIndex={-1}>
          <ErrorBoundary key={activePage}>
            <Suspense fallback={<UiRouteLoading />}>
              <Outlet />
            </Suspense>
          </ErrorBoundary>
        </main>
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
  return (segments[0] === 'ui-v2' ? segments[1] : segments[0]) as UiNavTarget | undefined;
}

function UiDefaultRedirect() {
  const { pages, capabilities } = useUiCapabilities();
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
