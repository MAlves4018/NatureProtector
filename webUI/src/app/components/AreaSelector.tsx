import { MapPin, RefreshCw } from 'lucide-react';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { StatusBadge } from './StatusBadge';

export function AreaSelector({ compact = false }: { compact?: boolean }) {
  const { copy } = useUiLocale();
  const { areas, areasLoading, selectedAreaCode, setSelectedAreaCode, areaResolution, reloadAreaContext } = useUiArea();
  const area = areaResolution.resolvedArea;

  return (
    <section className={compact ? 'ui-panel ui-area-selector' : 'ui-card ui-area-selector'}>
      <div className="ui-section-heading">
        <MapPin size={18} />
        <h3>{copy('area.title')}</h3>
        <StatusBadge
          label={areaResolution.resolutionReason}
          state={areaResolution.resolutionStatus === 'resolved' ? 'ready' : 'partial'}
        />
      </div>
      <label className="ui-field">
        <span>{copy('area.selectLabel')}</span>
        <select
          className="ui-select"
          value={selectedAreaCode}
          onChange={(event) => setSelectedAreaCode(event.target.value)}
          disabled={areasLoading}
        >
          <option value="">{areasLoading ? copy('state.loading') : copy('area.placeholder')}</option>
          {areas.map((item) => (
            <option key={item.code} value={item.code}>
              {item.name} ({item.code})
            </option>
          ))}
        </select>
      </label>
      <div className="ui-fact-list">
        <span>
          <strong>{copy('area.requested')}</strong>
          {areaResolution.requestedArea ?? copy('area.notSelected')}
        </span>
        <span>
          <strong>{copy('area.resolved')}</strong>
          {area ? `${area.name} (${area.code})` : copy('value.notAvailable')}
        </span>
        <span>
          <strong>{copy('area.available')}</strong>
          {areas.length}
        </span>
        {area && (
          <span>
            <strong>Grid/sensores</strong>
            {area.gridCellCount} / {area.sensorNodeCount}
          </span>
        )}
      </div>
      <button type="button" className="ui-secondary" onClick={reloadAreaContext}>
        <RefreshCw size={16} />
        {copy('risk.reload')}
      </button>
    </section>
  );
}
