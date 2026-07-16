import { DashBoards } from '../components/views/dashBoards';
import { EmptyState } from '../components/EmptyState';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiArea } from '../state/AreaContext';
import { useUiLocale } from '../state/LocaleContext';

export function DashboardsPage() {
  const { isDark } = useUiCapabilities();
  const { selectedAreaCode, resolvedAreaCode } = useUiArea();
  const { copy } = useUiLocale();

  if (!resolvedAreaCode) {
    return (
      <section className="ui-page">
        <EmptyState title={copy('area.selectPrompt')} />
      </section>
    );
  }

  return <DashBoards isDark={isDark} areaCode={selectedAreaCode} />;
}
