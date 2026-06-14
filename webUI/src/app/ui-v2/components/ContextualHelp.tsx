import { HelpCircle, X } from 'lucide-react';
import { useState } from 'react';
import { findHelpTopic } from '../content/helpTopics';
import type { HelpTopicId } from '../types';
import { localize } from '../types';
import { useUiV2 } from '../state/UiV2Context';

export function ContextualHelp({ topicId, mode = 'popover' }: { topicId: HelpTopicId; mode?: 'popover' | 'dialog' }) {
  const [open, setOpen] = useState(false);
  const { locale } = useUiV2();
  const topic = findHelpTopic(topicId);
  const title = localize(locale, topic.title);

  return (
    <span className="ui-v2-help">
      <button
        type="button"
        className="ui-v2-icon-button"
        aria-label={`${title}: ajuda`}
        onClick={() => setOpen(value => !value)}
        onFocus={() => setOpen(true)}
      >
        <HelpCircle size={16} />
      </button>
      {open && mode === 'popover' && (
        <div className="ui-v2-help-popover" role="note">
          <strong>{title}</strong>
          <p>{localize(locale, topic.summary)}</p>
          <p><span className="ui-v2-label">Fonte</span>{localize(locale, topic.source)}</p>
          <p><span className="ui-v2-label">Limite</span>{localize(locale, topic.limitation)}</p>
          <button type="button" className="ui-v2-secondary" onClick={() => setOpen(false)}>Fechar</button>
        </div>
      )}
      {open && mode === 'dialog' && (
        <div className="ui-v2-help-overlay">
          <section className="ui-v2-help-dialog" role="dialog" aria-modal="true" aria-label={title}>
            <div className="ui-v2-page-header">
              <h2 className="ui-v2-page-title">{title}</h2>
              <button type="button" className="ui-v2-icon-button" onClick={() => setOpen(false)} aria-label="Fechar ajuda">
                <X size={16} />
              </button>
            </div>
            <p>{localize(locale, topic.explanation)}</p>
            <p><span className="ui-v2-label">Fonte</span>{localize(locale, topic.source)}</p>
            <p><span className="ui-v2-label">Limite</span>{localize(locale, topic.limitation)}</p>
          </section>
        </div>
      )}
    </span>
  );
}
