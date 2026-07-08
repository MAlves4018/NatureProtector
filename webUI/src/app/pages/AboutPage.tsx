import { PageHeader } from '../components/PageHeader';
import { useUiLocale } from '../state/LocaleContext';

export function AboutPage() {
  const { copy } = useUiLocale();

  return (
    <section className="ui-page">
      <PageHeader title={copy('about.title')} subtitle={copy('about.subtitle')} />
      <div className="ui-card">
        <p>{copy('about.body')}</p>
      </div>
    </section>
  );
}
