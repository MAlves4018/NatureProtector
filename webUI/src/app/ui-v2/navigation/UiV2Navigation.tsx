import { Activity, Beaker, ClipboardList, Database, FileText, FlaskConical, Home, ListChecks, PlayCircle, ShieldCheck } from 'lucide-react';
import type { Copy } from '../types';
import type { UiV2NavTarget } from '../capabilities';
import type { UiV2PageDefinition } from './pageRegistry';

export function UiV2Navigation({
  pages,
  activePage,
  copy,
  onSelect,
}: {
  pages: readonly UiV2PageDefinition[];
  activePage: UiV2NavTarget;
  copy: Copy;
  onSelect: (page: UiV2NavTarget) => void;
}) {
  const groups = Array.from(new Set(pages.map(page => page.group)));

  return (
    <nav className="ui-v2-nav" aria-label="UI v2">
      {groups.map(group => (
        <div className="ui-v2-nav-group" key={group}>
          {pages.filter(page => page.group === group).map(page => (
            <button
              key={page.id}
              className={activePage === page.id ? 'ui-v2-nav-item ui-v2-nav-item-active' : 'ui-v2-nav-item'}
              type="button"
              aria-current={activePage === page.id ? 'page' : undefined}
              onClick={() => onSelect(page.id)}
            >
              {navIcon(page.id)}
              <span>{copy(page.labelKey)}</span>
            </button>
          ))}
        </div>
      ))}
    </nav>
  );
}

function navIcon(target: UiV2NavTarget) {
  switch (target) {
    case 'demo':
      return <Home size={16} />;
    case 'risk':
      return <Activity size={16} />;
    case 'simulation':
      return <PlayCircle size={16} />;
    case 'runs':
      return <ClipboardList size={16} />;
    case 'pipeline':
      return <Database size={16} />;
    case 'qa':
      return <ListChecks size={16} />;
    case 'evidence':
      return <FileText size={16} />;
    case 'admin':
      return <ShieldCheck size={16} />;
    case 'p3':
      return <FlaskConical size={16} />;
    case 'context':
      return <Beaker size={16} />;
    default:
      return <Home size={16} />;
  }
}
