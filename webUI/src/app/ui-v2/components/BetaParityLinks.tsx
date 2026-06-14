import { ExternalLink } from 'lucide-react';
import { BETA_CAPABILITIES } from '../content/betaParity';
import { useUiV2 } from '../state/UiV2Context';
import { localize } from '../types';

export function BetaParityLinks({ ids }: { ids?: readonly string[] }) {
  const { locale, resolvedAreaCode, copy } = useUiV2();
  const links = ids ? BETA_CAPABILITIES.filter(item => ids.includes(item.id)) : BETA_CAPABILITIES;

  return (
    <section className="ui-v2-panel ui-v2-panel-muted">
      <h3>Beta parity</h3>
      <p>{copy('footer.beta')}</p>
      <div className="ui-v2-beta-list">
        {links.map(item => {
          const href = item.href(resolvedAreaCode);
          return (
            <div key={item.id} className="ui-v2-beta-item">
              <strong>{localize(locale, item.label)}</strong>
              <span>{localize(locale, item.description)}</span>
              {href ? (
                <a className="ui-v2-link-button" href={href}>
                  <ExternalLink size={15} />
                  Abrir beta
                </a>
              ) : (
                <span className="ui-v2-badge">Seleciona uma area</span>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
