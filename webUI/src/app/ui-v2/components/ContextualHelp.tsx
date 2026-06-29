import { HelpCircle, X } from 'lucide-react';
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { findHelpTopic } from '../content/helpTopics';
import type { HelpTopicId } from '../types';
import { localize } from '../types';
import { useUiV2 } from '../state/UiV2Context';
import { trapDialogTab } from './dialogFocus';

export function ContextualHelp({ topicId, mode = 'popover' }: { topicId: HelpTopicId; mode?: 'popover' | 'dialog' }) {
  const [open, setOpen] = useState(false);
  const { locale } = useUiV2();
  const topic = findHelpTopic(topicId);
  const title = localize(locale, topic.title);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const closeRef = useRef<HTMLButtonElement | null>(null);
  const suppressNextFocusOpenRef = useRef(false);

  useEffect(() => {
    if (open && mode === 'dialog') {
      closeRef.current?.focus();
    }
  }, [mode, open]);

  const closeDialog = () => {
    setOpen(false);
    suppressNextFocusOpenRef.current = true;
    triggerRef.current?.focus();
  };

  const openFromFocus = () => {
    if (suppressNextFocusOpenRef.current) {
      suppressNextFocusOpenRef.current = false;
      return;
    }

    setOpen(true);
  };

  const handleDialogKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeDialog();
      return;
    }

    trapDialogTab(event);
  };

  return (
    <span className="ui-v2-help">
      <button
        ref={triggerRef}
        type="button"
        className="ui-v2-icon-button"
        aria-label={`${title}: ajuda`}
        onClick={() => setOpen((value) => !value)}
        onFocus={openFromFocus}
      >
        <HelpCircle size={16} />
      </button>
      {open && mode === 'popover' && (
        <div className="ui-v2-help-popover" role="note">
          <strong>{title}</strong>
          <p>{localize(locale, topic.summary)}</p>
          <p>
            <span className="ui-v2-label">Fonte</span>
            {localize(locale, topic.source)}
          </p>
          <p>
            <span className="ui-v2-label">Limite</span>
            {localize(locale, topic.limitation)}
          </p>
          <button type="button" className="ui-v2-secondary" onClick={() => setOpen(false)}>
            Fechar
          </button>
        </div>
      )}
      {open && mode === 'dialog' && (
        <div className="ui-v2-help-overlay">
          <section
            className="ui-v2-help-dialog"
            role="dialog"
            aria-modal="true"
            aria-label={title}
            onKeyDown={handleDialogKeyDown}
          >
            <div className="ui-v2-page-header">
              <h2 className="ui-v2-page-title">{title}</h2>
              <button
                type="button"
                className="ui-v2-icon-button"
                onClick={closeDialog}
                aria-label="Fechar ajuda"
                ref={closeRef}
              >
                <X size={16} />
              </button>
            </div>
            <p>{localize(locale, topic.explanation)}</p>
            <p>
              <span className="ui-v2-label">Fonte</span>
              {localize(locale, topic.source)}
            </p>
            <p>
              <span className="ui-v2-label">Limite</span>
              {localize(locale, topic.limitation)}
            </p>
          </section>
        </div>
      )}
    </span>
  );
}
