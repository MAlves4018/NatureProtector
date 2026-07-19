import {
  Activity,
  Beaker,
  ClipboardList,
  Database,
  FlaskConical,
  Home,
  Info,
  LayoutDashboard,
  ListChecks,
  PlayCircle,
  ShieldCheck,
  Gauge,
  Rocket,
  CloudCog,
  BadgeCheck,
  Users,
  FileSearch,
  GitCompareArrows,
  ChevronRight,
} from 'lucide-react';
import { Image } from '@chakra-ui/react';
import type { UiMessageKey } from '../i18n';
import type { Copy } from '../types';
import type { UiNavTarget } from '../capabilities';
import type { UiPageDefinition } from './pageRegistry';

export function UiNavigation({
  pages,
  activePage,
  copy,
  onSelect,
}: {
  pages: readonly UiPageDefinition[];
  activePage: UiNavTarget;
  copy: Copy;
  onSelect: (page: UiNavTarget) => void;
}) {
  const groups = Array.from(new Set(pages.map((page) => page.group)));
  const activePageDef = pages.find((page) => page.id === activePage);
  const activeGroup = activePageDef?.group ?? groups[0];
  const activeGroupPages = pages.filter((page) => page.group === activeGroup);

  return (
    <nav className="ui-nav" aria-label="Navegação principal">
      <div className="ui-nav-brand">
        <Image
          src="/images/NPIconNoBg.png"
          boxSize="40px"
          borderRadius="full"
          fit="scale-down"
          alt="Nature Protector Logo"
        />
        <span>
          <strong>NatureProtector</strong>
          <small>Cockpit operacional</small>
        </span>
      </div>
      <fieldset className="ui-nav-groups">
        <legend className="ui-visually-hidden">Áreas de trabalho</legend>
        {groups.map((group) => (
          <button
            key={group}
            className={activeGroup === group ? 'ui-nav-item ui-nav-item-active' : 'ui-nav-item'}
            type="button"
            aria-current={activeGroup === group ? 'true' : undefined}
            onClick={() => {
              const firstPage = pages.find((page) => page.group === group);
              if (firstPage) {
                onSelect(firstPage.id);
              }
            }}
          >
            {groupIcon(group)}
            <span>{copy(`group.${group}` as UiMessageKey)}</span>
            <ChevronRight className="ui-nav-chevron" size={14} aria-hidden="true" />
          </button>
        ))}
      </fieldset>
      <div className="ui-nav-divider" />
      <div className="ui-nav-section-label">{copy(`group.${activeGroup}` as UiMessageKey)}</div>
      {activeGroupPages.length > 0 && (
        <fieldset className="ui-nav-sub">
          <legend className="ui-visually-hidden">Destinos</legend>
          {activeGroupPages.map((page) => (
            <button
              key={page.id}
              className={activePage === page.id ? 'ui-nav-item ui-nav-item-active' : 'ui-nav-item'}
              type="button"
              aria-current={activePage === page.id ? 'page' : undefined}
              onClick={() => onSelect(page.id)}
            >
              {navIcon(page.id)}
              <span>{copy(page.labelKey)}</span>
            </button>
          ))}
        </fieldset>
      )}
      <div className="ui-nav-footer">
        <span className="ui-status-dot" aria-hidden="true" />
        Contratos backend ativos
      </div>
    </nav>
  );
}

function groupIcon(group: string) {
  switch (group) {
    case 'public':
      return <Home size={16} />;
    case 'operate':
      return <Activity size={16} />;
    case 'technical':
      return <Database size={16} />;
    case 'release':
      return <Rocket size={16} />;
    case 'simulate':
      return <PlayCircle size={16} />;
    case 'admin':
      return <ShieldCheck size={16} />;
    case 'about':
      return <Info size={16} />;
    default:
      return <Home size={16} />;
  }
}

function navIcon(target: UiNavTarget) {
  switch (target) {
    case 'demo':
      return <Home size={16} />;
    case 'dashboard':
    case 'overview':
      return <LayoutDashboard size={16} />;
    case 'mission':
      return <Gauge size={16} />;
    case 'risk':
      return <Activity size={16} />;
    case 'simulation':
      return <PlayCircle size={16} />;
    case 'runs':
      return <ClipboardList size={16} />;
    case 'pipeline':
    case 'queries':
      return <Database size={16} />;
    case 'quality':
    case 'qa':
    case 'qa-tests':
      return <ListChecks size={16} />;
    case 'evidence':
      return <FileSearch size={16} />;
    case 'deployments':
      return <Rocket size={16} />;
    case 'deployment-health':
      return <Activity size={16} />;
    case 'cloud':
      return <CloudCog size={16} />;
    case 'approvals':
      return <BadgeCheck size={16} />;
    case 'users':
      return <Users size={16} />;
    case 'admin':
      return <ShieldCheck size={16} />;
    case 'p3':
      return <FlaskConical size={16} />;
    case 'about':
      return <Info size={16} />;
    case 'context':
      return <Beaker size={16} />;
    case 'scenario-compare':
      return <GitCompareArrows size={16} />;
    default:
      return <Home size={16} />;
  }
}
