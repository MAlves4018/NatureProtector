import { LogIn, Moon, Sun } from 'lucide-react';
import { useEffect, useState } from 'react';
import { UiV2Navigation } from './navigation/UiV2Navigation';
import { UiV2Provider, useUiV2 } from './state/UiV2Context';
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
import type { UiV2NavTarget } from './capabilities';
import './theme/ui-v2.css';

export function UiV2App({ isDark = false }: { isDark?: boolean }) {
  return (
    <UiV2Provider>
      <UiV2Shell isDark={isDark} />
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
  } = useUiV2();
  const [helpTopic, setHelpTopic] = useState<string | null>(null);

  useEffect(() => {
    const handler = (event: Event) => {
      setHelpTopic(String((event as CustomEvent).detail ?? 'overview'));
    };
    window.addEventListener('np-ui-v2-help', handler);
    return () => window.removeEventListener('np-ui-v2-help', handler);
  }, []);

  const mainId = 'ui-v2-main';

  return (
    <div className="ui-v2-shell" data-theme={isDark ? 'dark' : 'light'}>
      <a
        className="ui-v2-skip"
        href={`#${mainId}`}
        onClick={event => {
          event.preventDefault();
          document.getElementById(mainId)?.focus();
        }}
      >
        {copy('nav.skip')}
      </a>
      <header className="ui-v2-hero">
        <div>
          <p className="ui-v2-kicker">{copy('app.prototype')} / {copy('app.readOnly')}</p>
          <h1 className="ui-v2-title">{copy('app.name')}</h1>
          <p className="ui-v2-lead">
            {isPublic
              ? 'Entrada publica orientada a produto: proposito, limites e estado dos dados sem surfaces internas.'
              : `Perfil ativo: ${user?.roles.join(', ') || 'sem roles'}. Navegacao organizada por tarefas e capabilities reais.`}
          </p>
        </div>
        <div className="ui-v2-hero-actions">
          <div className="ui-v2-language" aria-label="Idioma">
            <button type="button" className={locale === 'pt-PT' ? 'ui-v2-button' : 'ui-v2-secondary'} onClick={() => setLocale('pt-PT')}>{copy('language.pt')}</button>
            <button type="button" className={locale === 'en' ? 'ui-v2-button' : 'ui-v2-secondary'} onClick={() => setLocale('en')}>{copy('language.en')}</button>
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
          <section className="ui-v2-help-dialog" role="dialog" aria-modal="true" aria-label={copy('help.title')}>
            <h2 className="ui-v2-page-title">{copy('help.title')}</h2>
            <p>{copy('help.intro')}</p>
            <p>{copy('help.browser')}</p>
            <button type="button" className="ui-v2-button" onClick={() => setHelpTopic(null)}>{copy('help.close')}</button>
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
    case 'risk':
      return <RiskPage />;
    case 'runs':
      return <RunsPage />;
    case 'simulation':
      return <SimulationPage />;
    case 'pipeline':
      return <PipelinePage />;
    case 'qa':
      return <QualityEvidencePage />;
    case 'admin':
      return <AdminPage />;
    case 'p3':
      return <ExperimentalPage />;
    case 'evidence':
      return <QualityEvidencePage />;
    default:
      return <OverviewPage />;
  }
}

export default UiV2App;
