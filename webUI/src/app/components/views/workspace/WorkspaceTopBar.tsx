import type { Dispatch, SetStateAction } from 'react';
import { RefreshCw } from 'lucide-react';
import type { AreaResponse, RuntimeRunSummaryResponse } from '../../../types';
import { WINDOW_OPTIONS } from './workspaceConstants';
import { button, input, panel, Pill, SegmentedButtons, type Colors } from './WorkspaceShared';

export function WorkspaceTopBar(props: {
  colors: Colors;
  isDark: boolean;
  setIsDark: Dispatch<SetStateAction<boolean>>;
  areaCode: string;
  areas: AreaResponse[];
  latestRun: RuntimeRunSummaryResponse | null;
  recentMinutes: number;
  setRecentMinutes: (value: number) => void;
  lastUpdated: Date | null;
  loading: boolean;
  onAreaChange: (value: string) => void;
  onRefresh: () => void;
}) {
  const {
    colors,
    areaCode,
    areas,
    latestRun,
    recentMinutes,
    setRecentMinutes,
    lastUpdated,
    loading,
    onAreaChange,
    onRefresh,
  } = props;
  return (
    <section
      style={{
        ...panel(colors),
        marginBottom: '12px',
        display: 'grid',
        gridTemplateColumns: 'minmax(180px, 1fr) auto',
        gap: '12px',
        alignItems: 'center',
        position: 'sticky',
        top: 0,
        zIndex: 10,
      }}
    >
      <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
        <select
          style={{ ...input(colors), width: '190px' }}
          value={areaCode}
          onChange={(event) => onAreaChange(event.target.value)}
          aria-label="Area"
        >
          {areas.length === 0 && <option value={areaCode}>{areaCode}</option>}
          {areas.map((area) => (
            <option key={area.code} value={area.code}>
              {area.code}
            </option>
          ))}
        </select>
        <Pill colors={colors} label="Latest Run" value={latestRun?.scenarioCode ?? 'No data'} />
        <Pill colors={colors} label="Scenario" value={latestRun?.scenarioName ?? 'Not available'} />
        <Pill colors={colors} label="Status" value={latestRun?.status ?? 'No run'} />
        <Pill colors={colors} label="Updated" value={lastUpdated ? lastUpdated.toLocaleTimeString() : 'Pending'} />
      </div>
      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
        <SegmentedButtons
          values={WINDOW_OPTIONS}
          selected={recentMinutes}
          onSelect={setRecentMinutes}
          format={(value) => (value === 1440 ? '24h' : `${value}m`)}
          colors={colors}
        />
        <button type="button" style={button(colors)} onClick={onRefresh} disabled={loading}>
          <RefreshCw size={16} /> Refresh
        </button>
      </div>
    </section>
  );
}
