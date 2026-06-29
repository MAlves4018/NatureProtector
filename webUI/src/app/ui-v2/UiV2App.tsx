import { LogIn, Moon, Sun } from 'lucide-react';
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { UiV2Navigation } from './navigation/UiV2Navigation';
import { UiV2Provider, useUiV2 } from './state/UiV2Context';
import { OperationsProvider } from './operations/OperationsContext';
import { PublicOverviewPage } from './pages/PublicOverviewPage';
import { DataContextPage } from './pages/DataContextPage';
import { OverviewPage } from './pages/OverviewPage';
import { RiskPage } from './pages/RiskPage';
import { RunsPage } from './pages/RunsPage';
import { SimulationPage } from './pages/SimulationPage';
import { PipelinePage } from './pages/PipelinePage';
import { QualityEvidencePage } from './pages/QualityEvidencePage';
import { AdminPage } from './pages/AdminPage';
import { ExperimentalPage } from './pages/ExperimentalPage';
import { MissionControlPage } from './pages/MissionControlPage';
import { QualityRunsPage } from './pages/QualityRunsPage';
import { EvidenceExplorerPage } from './pages/EvidenceExplorerPage';
import { DeploymentsPage } from './pages/DeploymentsPage';
import { CloudResourcesPage } from './pages/CloudResourcesPage';
import { ApprovalsPage } from './pages/ApprovalsPage';
import { UserRoleAdministrationPage } from './pages/UserRoleAdministrationPage';
import type { UiV2NavTarget } from './capabilities';
import { trapDialogTab } from './components/dialogFocus';
import './theme/ui-v2.css';

export function UiV2App({ isDark = false }: { isDark?: boolean }) {
  return (
    <UiV2Provider>
      <OperationsProvider>
        <UiV2Shell isDark={isDark} />
      </OperationsProvider>
    </UiV2Provider>
  );
}

function UiV2Shell({ isDark }: { isDark: boolean }) {
  const {
    copy,
    locale,
    setLocale,
    pages,
    activePage,
    setActivePage,
    user,
    isPublic,
    capabilityAuthority,
    capabilitiesLoading,
  } = useUiV2();
  const [helpTopic, setHelpTopic] = useState<string | null>(null);
  const helpCloseRef = useRef<HTMLButtonElement | null>(null);
  const helpReturnFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    const handler = (event: Event) => {
      helpReturnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setHelpTopic(String((event as CustomEvent).detail ?? 'overview'));
    };
    window.addEventListener('np-ui-v2-help', handler);
    return () => window.removeEventListener('np-ui-v2-help', handler);
  }, []);

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

  const closeHelp = () => setHelpTopic(null);
  const handleHelpDialogKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeHelp();
      return;
    }

    trapDialogTab(event);
  };

  const mainId = 'ui-v2-main';

  return (
    <div className="ui-v2-shell" data-theme={isDark ? 'dark' : 'light'}>
      <a
        className="ui-v2-skip"
        href={`#${mainId}`}
        onClick={(event) => {
          event.preventDefault();
          document.getElementById(mainId)?.focus();
        }}
      >
        {copy('nav.skip')}
      </a>
      <header className="ui-v2-hero">
        <div>
          <p className="ui-v2-kicker">
            {copy('app.prototype')} / {copy('app.readOnly')}
          </p>
          <h1 className="ui-v2-title">{copy('app.name')}</h1>
          <p className="ui-v2-lead">
            {isPublic
              ? 'Entrada publica orientada a produto: proposito, limites e estado dos dados sem surfaces internas.'
              : `Perfil ativo: ${user?.roles.join(', ') || 'sem roles'}. Autorizacao: ${
                  capabilitiesLoading ? 'a validar no backend' : capabilityAuthority
                }.`}
          </p>
        </div>
        <div className="ui-v2-hero-actions">
          <div className="ui-v2-language">
            <button
              type="button"
              className={locale === 'pt-PT' ? 'ui-v2-button' : 'ui-v2-secondary'}
              onClick={() => setLocale('pt-PT')}
            >
              {copy('language.pt')}
            </button>
            <button
              type="button"
              className={locale === 'en' ? 'ui-v2-button' : 'ui-v2-secondary'}
              onClick={() => setLocale('en')}
            >
              {copy('language.en')}
            </button>
          </div>
          <span className="ui-v2-badge">
            {isDark ? <Moon size={14} /> : <Sun size={14} />}
            {isDark ? 'Dark' : 'Light'}
          </span>
          {isPublic && (
            <a className="ui-v2-button" href="/login">
              <LogIn size={16} />
              {copy('nav.login')}
            </a>
          )}
        </div>
      </header>
      <UiV2Navigation pages={pages} activePage={activePage} copy={copy} onSelect={setActivePage} />
      <main id={mainId} className="ui-v2-content" tabIndex={-1}>
        {renderPage(activePage)}
      </main>
      <footer className="ui-v2-footer">{copy('footer.beta')}</footer>
      {helpTopic && (
        <div className="ui-v2-help-overlay">
          <section
            className="ui-v2-help-dialog"
            role="dialog"
            aria-modal="true"
            aria-label={copy('help.title')}
            onKeyDown={handleHelpDialogKeyDown}
          >
            <h2 className="ui-v2-page-title">{copy('help.title')}</h2>
            <p>{copy('help.intro')}</p>
            <p>{copy('help.browser')}</p>
            <button type="button" className="ui-v2-button" onClick={closeHelp} ref={helpCloseRef}>
              {copy('help.close')}
            </button>
          </section>
        </div>
      )}
    </div>
  );
}

function renderPage(activePage: UiV2NavTarget) {
  switch (activePage) {
    case 'demo':
      return <PublicOverviewPage />;
    case 'context':
      return <DataContextPage />;
    case 'mission':
      return <MissionControlPage />;
    case 'risk':
      return <RiskPage />;
    case 'runs':
      return <RunsPage />;
    case 'simulation':
      return <SimulationPage />;
    case 'pipeline':
      return <PipelinePage />;
    case 'quality':
      return <QualityRunsPage />;
    case 'qa':
      return <QualityEvidencePage />;
    case 'admin':
      return <AdminPage />;
    case 'p3':
      return <ExperimentalPage />;
    case 'evidence':
      return <EvidenceExplorerPage />;
    case 'deployments':
      return <DeploymentsPage />;
    case 'cloud':
      return <CloudResourcesPage />;
    case 'approvals':
      return <ApprovalsPage />;
    case 'users':
      return <UserRoleAdministrationPage />;
    default:
      return <OverviewPage />;
  }
}

export default UiV2App;
