import { ExternalLink } from 'lucide-react';
import { BETA_CAPABILITIES } from '../content/betaParity';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { localize } from '../types';

export function BetaParityLinks({ ids }: { ids?: readonly string[] }) {
  const { copy, locale } = useUiLocale();
  const { resolvedAreaCode } = useUiArea();
  const links = ids ? BETA_CAPABILITIES.filter((item) => ids.includes(item.id)) : BETA_CAPABILITIES;

  return (
    <section className="ui-panel ui-panel-muted">
      <h3>Beta parity</h3>
      <p>{copy('footer.beta')}</p>
      <div className="ui-beta-list">
        {links.map((item) => {
          const href = item.href(resolvedAreaCode);
          return (
            <div key={item.id} className="ui-beta-item">
              <strong>{localize(locale, item.label)}</strong>
              <span>{localize(locale, item.description)}</span>
              {href ? (
                <a className="ui-link-button" href={href}>
                  <ExternalLink size={15} />
                  Abrir beta
                </a>
              ) : (
                <span className="ui-badge">Seleciona uma area</span>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
