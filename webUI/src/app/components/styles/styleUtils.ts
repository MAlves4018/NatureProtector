import { getColors } from '../../utils/utils';

function primaryButton(_colors: ReturnType<typeof getColors>, tone: string) {
  const accent = tone === '#ef4444' ? '#f87171' : '#22c55e';
  return {
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '8px',
    width: '100%',
    padding: '10px 12px',
    borderRadius: '10px',
    border: `1px solid ${tone}`,
    background: `linear-gradient(135deg, ${tone}, ${accent})`,
    color: '#ffffff',
    fontWeight: 700,
    cursor: 'pointer',
  };
}

function wrapperStyle(_colors: ReturnType<typeof getColors>, isPanel: boolean) {
  return {
    position: 'relative' as const,
    minHeight: isPanel ? '520px' : 'calc(100vh - 58px)',
    display: 'grid',
    placeItems: 'center',
    padding: '32px 18px',
    overflow: 'hidden',
    fontFamily: "Figtree, 'IBM Plex Sans', sans-serif",
  };
}

function backdropStyle(surface: string) {
  return {
    position: 'absolute' as const,
    inset: 0,
    background: `radial-gradient(circle at top, rgba(34,197,94,0.12), transparent 50%), ${surface}`,
  };
}

function cardStyle(colors: ReturnType<typeof getColors>, isPanel: boolean) {
  return {
    position: 'relative' as const,
    zIndex: 1,
    width: 'min(520px, 100%)',
    background: colors.panelBg,
    border: `1px solid ${colors.panelBorder}`,
    borderRadius: '12px',
    padding: '26px 24px',
    boxShadow: isPanel ? '0 6px 24px rgba(15,23,42,0.10)' : '0 10px 36px rgba(15,23,42,0.16)',
  };
}

function iconRing(colors: ReturnType<typeof getColors>) {
  return {
    width: '38px',
    height: '38px',
    borderRadius: '999px',
    display: 'grid',
    placeItems: 'center',
    background: colors.sectionBg,
    border: `1px solid ${colors.panelBorder}`,
  };
}

function labelStyle(colors: ReturnType<typeof getColors>) {
  return { color: colors.textSecond, fontSize: '12px', fontWeight: 700, textTransform: 'uppercase' as const };
}

function inputStyle(colors: ReturnType<typeof getColors>) {
  return {
    width: '100%',
    border: `1px solid ${colors.inputBorder}`,
    background: colors.inputBg,
    color: colors.textPrimary,
    borderRadius: '10px',
    padding: '10px 12px',
    fontSize: '14px',
  };
}

export { primaryButton, wrapperStyle, backdropStyle, cardStyle, iconRing, labelStyle, inputStyle };
