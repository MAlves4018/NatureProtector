import { Home, ChevronRight } from 'lucide-react';
import type { UiNavTarget } from '../capabilities';

interface Breadcrumb {
  label: string;
  target?: UiNavTarget;
}

interface BreadcrumbsProps {
  items: Breadcrumb[];
  onNavigate: (target: UiNavTarget) => void;
}

export function Breadcrumbs({ items, onNavigate }: BreadcrumbsProps) {
  if (items.length === 0) return null;

  return (
    <nav className="ui-breadcrumbs" aria-label="Breadcrumb">
      <button type="button" className="ui-breadcrumb-link" onClick={() => onNavigate('demo')}>
        <Home size={14} />
      </button>
      {items.map((item, i) => (
        <span key={i} className="ui-breadcrumb-item">
          <ChevronRight size={12} className="ui-breadcrumb-sep" />
          {item.target && i < items.length - 1 ? (
            <button type="button" className="ui-breadcrumb-link" onClick={() => item.target && onNavigate(item.target)}>
              {item.label}
            </button>
          ) : (
            <span className="ui-breadcrumb-current">{item.label}</span>
          )}
        </span>
      ))}
    </nav>
  );
}
