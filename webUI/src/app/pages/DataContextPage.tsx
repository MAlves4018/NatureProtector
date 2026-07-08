import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { useUiLocale } from '../state/LocaleContext';

export function DataContextPage() {
  const { copy } = useUiLocale();

  return (
    <section className="ui-page">
      <PageHeader title={copy('context.title')} subtitle={copy('context.subtitle')} helpTopic="origin" />
      <div className="ui-two-column">
        <DataStatusSummary />
        <AreaSelector compact />
      </div>
    </section>
  );
}
