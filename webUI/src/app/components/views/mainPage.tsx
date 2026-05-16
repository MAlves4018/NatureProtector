import { Flame, Leaf } from "lucide-react";
import { useEffect, useReducer, useState } from "react";
import { api } from "../../services/api";
import { useNavigate } from "react-router";
import { areaReducer, initialAreaState } from "../../hooks/AreaIdReducer";
import { getColors } from "../../utils/utils";


export function MainPage({ isDark }: { isDark: boolean }) {

  const c = getColors(isDark);

  const [selected, setSelected] = useState('');
  const [hovered, setHovered] = useState(false);
  const [areas, setAreas] = useState<{ value: string, label: string }[]>([]);


  const [state, dispatch] = useReducer(areaReducer, {
    ...initialAreaState,
  });

  const canEnter = state.code != null && state.code === selected;

  const navigate = useNavigate();

  const handleChange = async (e: React.ChangeEvent<HTMLSelectElement, HTMLSelectElement>) => {
    const value = e.target.value;

    setSelected(value);

    console.log('Selected area code:', value);
    console.log('Available areas:', areas);

    const area = areas.find(a => a.value == value);

    if (!area) {
      console.error('Selected area not found in list:', value);
      return;
    }

    dispatch({ type: 'SET_CODE', payload: area.value });
  };

  useEffect(() => {
    api.getAreas().then(ars => {
      setAreas(ars.map(a => ({
        value: a.code,
        label: `${a.name} (${a.countryCode})`
      })))
    })
  }, []);

  return (
    <div
      style={{
        minHeight: '100vh',
        width: '100%',
        position: 'relative',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        fontFamily: 'system-ui, -apple-system, sans-serif',
      }}
    >

      <div
        style={{
          position: 'absolute', inset: 0,
          background: c.pageBg,
        }}
      />

      {/* Content card */}
      <div
        style={{
          position: 'relative', zIndex: 1,
          display: 'flex', flexDirection: 'column', alignItems: 'center',
          gap: '32px', maxWidth: '480px', width: '100%', padding: '0 24px',
        }}
      >
        {/* Logo + brand */}
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '14px' }}>
          <div
            style={{
              width: '68px', height: '68px', borderRadius: '50%',
              border: '2.5px solid #22c55e',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              background: 'rgba(22,163,74,0.12)',
              boxShadow: '0 0 32px rgba(34,197,94,0.25)',
            }}
          >
            <Leaf size={30} color="#22c55e" />
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ color: c.textPrimary, fontSize: '26px', fontWeight: 700, letterSpacing: '0.02em', marginBottom: '6px' }}>
              Nature Protector
            </div>
            <div style={{ color: c.textSecond, fontSize: '14px', letterSpacing: '0.03em' }}>
              Monitorização de Incêndios Florestais em Portugal
            </div>
          </div>
        </div>

        {/* Card */}
        <div
          style={{
            width: '100%',
            background: c.panelBg,
            border: `1px solid ${c.panelBorder}`,
            borderRadius: '18px',
            padding: '32px 28px',
            backdropFilter: 'blur(18px)',
            boxShadow: '0 8px 40px rgba(0,0,0,0.55)',
          }}
        >
          <div style={{ color: c.textPrimary, fontSize: '16px', fontWeight: 600, marginBottom: '6px' }}>
            Selecionar área de monitorização
          </div>
          <div style={{ color: c.textSecond, fontSize: '13px', marginBottom: '18px' }}>
            Escolha a região ou distrito que pretende monitorizar
          </div>

          {/* Dropdown */}
          <div style={{ position: 'relative', marginBottom: '20px' }}>
            <select
              value={selected}
              onChange={e => { handleChange(e) }}
              style={{
                width: '100%',
                appearance: 'none',
                WebkitAppearance: 'none',
                background: c.inputBg,
                border: `1px solid ${selected ? '#16a34a' : c.inputBorder}`,
                borderRadius: '10px',
                color: selected ? '#16a34a' : c.textSecond,
                fontSize: '14px',
                padding: '12px 44px 12px 16px',
                cursor: 'pointer',
                outline: 'none',
                transition: 'border-color 0.2s, box-shadow 0.2s',
                boxShadow: selected ? '0 0 0 2px rgba(22,163,74,0.20)' : 'none',
              }}
            >
              <option value="" disabled style={{ color: c.textSecond }}>— Escolha uma área —</option>

              <optgroup style={{ color: c.textSecond, fontStyle: 'normal' }}>
                {areas.map(a => (
                  <option key={a.value} value={a.value} style={{ color: c.textPrimary, background: c.sectionBg }}>
                    {a.label}
                  </option>
                ))}
              </optgroup>
            </select>

            {/* Chevron icon */}
            <div
              style={{
                position: 'absolute', right: '14px', top: '50%', transform: 'translateY(-50%)',
                pointerEvents: 'none', color: c.textSecond,
              }}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </div>
          </div>

          {/* Enter button */}
          <button
            disabled={!canEnter}
            onClick={() => navigate('/dashboards/' + state.code)}
            onMouseEnter={() => setHovered(true)}
            onMouseLeave={() => setHovered(false)}
            style={{
              width: '100%',
              padding: '12px',
              borderRadius: '10px',
              border: 'none',
              background: canEnter
                ? hovered
                  ? 'linear-gradient(135deg, #15803d, #16a34a)'
                  : 'linear-gradient(135deg, #16a34a, #22c55e)'
                : 'rgba(30,35,48,0.6)',
              color: canEnter ? '#ffffff' : '#3d4760',
              fontSize: '14px',
              fontWeight: 600,
              cursor: canEnter ? 'pointer' : 'not-allowed',
              letterSpacing: '0.04em',
              transition: 'all 0.2s',
              boxShadow: canEnter && hovered ? '0 4px 20px rgba(34,197,94,0.35)' : 'none',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px',
            }}
          >
            <Flame size={15} />
            Entrar no painel de monitorização
          </button>
        </div>

        {/* Footer note */}
        <div style={{ color: c.textMuted, fontSize: '12px', textAlign: 'center' }}>
          Dados em tempo real
        </div>
      </div>
    </div>
  );
}