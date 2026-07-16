import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';

export function DataContextPage() {
  const { copy } = useUiLocale();
  const { resolvedAreaCode } = useUiArea();

  return (
    <section className="ui-page">
      <PageHeader title={copy('context.title')} subtitle={copy('context.subtitle')} helpTopic="origin" />
      <div className="ui-two-column">
        {resolvedAreaCode ? <DataStatusSummary /> : <EmptyState title={copy('area.selectPrompt')} />}
        <AreaSelector compact />
      </div>
    </section>
  );
}
