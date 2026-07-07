import { DashBoards } from '../components/views/dashBoards';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiArea } from '../state/AreaContext';

export function DashboardsPage() {
  const { isDark } = useUiCapabilities();
  const { selectedAreaCode } = useUiArea();

  return <DashBoards isDark={isDark} areaCode={selectedAreaCode} />;
}
