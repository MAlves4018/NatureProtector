import type { ReactNode } from 'react';
import type { HelpTopicId } from '../types';
import { ContextualHelp } from './ContextualHelp';

export function PageHeader({
  title,
  subtitle,
  helpTopic,
  actions,
}: {
  title: string;
  subtitle?: string;
  helpTopic?: HelpTopicId;
  actions?: ReactNode;
}) {
  return (
    <header className="ui-page-header">
      <div>
        <h2 className="ui-page-title">{title}</h2>
        {subtitle && <p className="ui-page-subtitle">{subtitle}</p>}
      </div>
      <div className="ui-toolbar">
        {helpTopic && <ContextualHelp topicId={helpTopic} />}
        {actions}
      </div>
    </header>
  );
}
