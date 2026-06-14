import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { useUiV2 } from '../state/UiV2Context';

export function DataContextPage() {
  const { copy } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('context.title')} subtitle={copy('context.subtitle')} helpTopic="origin" />
      <div className="ui-v2-two-column">
        <DataStatusSummary />
        <AreaSelector compact />
      </div>
    </section>
  );
}
