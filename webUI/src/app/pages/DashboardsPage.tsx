import { DashBoards } from '../components/views/dashBoards';
import { EmptyState } from '../components/EmptyState';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiArea } from '../state/AreaContext';
import { useUiLocale } from '../state/LocaleContext';
import { Area } from 'recharts';
import { AreaSelector } from '../components/AreaSelector';
import { PageHeader } from '../components/PageHeader';

export function DashboardsPage() {
  const { isDark } = useUiCapabilities();
  const { selectedAreaCode, resolvedAreaCode } = useUiArea();
  const { copy } = useUiLocale();

  if (!resolvedAreaCode) {
    return (
      <>
        <PageHeader
          title="P]agina de dashboards"
          subtitle="Visualizações de dados relativos à área selecionada"
          helpTopic="overview"
        />
        <AreaSelector compact />
        <section className="ui-page">
          <EmptyState title={copy('area.selectPrompt')} />
        </section>
      </>
    );
  }

  return <DashBoards isDark={isDark} areaCode={selectedAreaCode} />;
}
