import { MapPin, RefreshCw } from 'lucide-react';
import { useUiV2 } from '../state/UiV2Context';
import { StatusBadge } from './StatusBadge';

export function AreaSelector({ compact = false }: { compact?: boolean }) {
  const {
    copy,
    areas,
    areasLoading,
    selectedAreaCode,
    setSelectedAreaCode,
    areaResolution,
    reloadAreaContext,
  } = useUiV2();
  const area = areaResolution.resolvedArea;

  return (
    <section className={compact ? 'ui-v2-panel ui-v2-area-selector' : 'ui-v2-card ui-v2-area-selector'}>
      <div className="ui-v2-section-heading">
        <MapPin size={18} />
        <h3>{copy('area.title')}</h3>
        <StatusBadge label={areaResolution.resolutionReason} state={areaResolution.resolutionStatus === 'resolved' ? 'ready' : 'partial'} />
      </div>
      <label className="ui-v2-field">
        <span>{copy('area.selectLabel')}</span>
        <select
          className="ui-v2-select"
          value={selectedAreaCode}
          onChange={event => setSelectedAreaCode(event.target.value)}
          disabled={areasLoading}
        >
          <option value="">{areasLoading ? copy('state.loading') : copy('area.placeholder')}</option>
          {areas.map(item => (
            <option key={item.code} value={item.code}>{item.name} ({item.code})</option>
          ))}
        </select>
      </label>
      <div className="ui-v2-fact-list">
        <span><strong>{copy('area.requested')}</strong>{areaResolution.requestedArea ?? copy('area.notSelected')}</span>
        <span><strong>{copy('area.resolved')}</strong>{area ? `${area.name} (${area.code})` : copy('value.notAvailable')}</span>
        <span><strong>{copy('area.available')}</strong>{areas.length}</span>
        {area && <span><strong>Grid/sensores</strong>{area.gridCellCount} / {area.sensorNodeCount}</span>}
      </div>
      <button type="button" className="ui-v2-secondary" onClick={reloadAreaContext}>
        <RefreshCw size={16} />
        {copy('risk.reload')}
      </button>
    </section>
  );
}
