import { ArrowRight, Flame, GitBranch, ShieldCheck } from "lucide-react";
import { useEffect, useReducer, useState } from "react";
import type { ReactNode } from "react";
import { useNavigate } from "react-router";
import { areaReducer, initialAreaState } from "../../hooks/AreaIdReducer";
import { api } from "../../services/api";
import { getColors } from "../../utils/utils";

export function MainPage({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);
  const [selected, setSelected] = useState("");
  const [hovered, setHovered] = useState(false);
  const [areas, setAreas] = useState<{ value: string; label: string }[]>([]);
  const [state, dispatch] = useReducer(areaReducer, { ...initialAreaState });
  const navigate = useNavigate();
  const canEnter = state.code != null && state.code === selected;

  const handleChange = async (event: React.ChangeEvent<HTMLSelectElement>) => {
    const value = event.target.value;
    setSelected(value);
    const area = areas.find(item => item.value === value);
    if (area) {
      dispatch({ type: "SET_CODE", payload: area.value });
    }
  };

  useEffect(() => {
    api.getAreas().then(result => {
      setAreas(result.map(area => ({
        value: area.code,
        label: `${area.name} (${area.countryCode})`,
      })));
    });
  }, []);

  return (
    <div style={{
      minHeight: "calc(100vh - 58px)",
      width: "100%",
      position: "relative",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      overflow: "auto",
      fontFamily: "system-ui, -apple-system, sans-serif",
      padding: "32px 18px",
    }}>

      <div style={{ position: "absolute", inset: 0, background: c.pageBg }} />


      <div style={{
        position: "relative",
        zIndex: 1,
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
        gap: "24px",
        maxWidth: "1180px",
        width: "100%",
        alignItems: "center",
      }}>
        <section style={{ display: "flex", flexDirection: "column", gap: "18px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "14px" }}>
            <div style={{
              width: "64px",
              height: "64px",
              borderRadius: "50%",
              border: "2.5px solid #22c55e",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              background: "rgba(22,163,74,0.12)",
              boxShadow: "0 0 32px rgba(34,197,94,0.20)",
              flexShrink: 0,
            }}>
              <img src="images/NPIconNoBg.png" width="42px" height="42px" />
            </div>
            <div>
              <div style={{ color: c.textPrimary, fontSize: "38px", fontWeight: 800, lineHeight: 1, letterSpacing: 0 }}>
                Nature Protector
              </div>
              <div style={{ color: c.textSecond, fontSize: "15px", marginTop: "8px" }}>
                Sistema de monitorização preventiva e simulação operacional para risco de incêndio florestal.
              </div>
            </div>
          </div>

          <div style={{ color: c.textPrimary, fontSize: "18px", lineHeight: 1.55, maxWidth: "820px" }}>
            O Nature Protector permite simular cenários ambientais, processar leituras de sensores, calcular risco operacional,
            emitir alertas e analisar evidência runtime de forma rastreável.
          </div>

          <div style={{ color: c.textSecond, fontSize: "14px", lineHeight: 1.65 }}>
            Projeto preventivo, operacional, simulado e rastreável para demonstração académica. A interface apresenta
            evidência de execução e estados persistidos; não afirma previsão científica de incêndios reais.
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(190px, 1fr))", gap: "12px" }}>
            <ValueCard colors={c} icon={<Flame size={18} />} title="Simulação controlada" text="Cenários com sensores, ciclos, seed e degradação configurável." />
            <ValueCard colors={c} icon={<GitBranch size={18} />} title="Pipeline rastreável" text="Eventos, inbox, retries, quarantine, risco, projeções e alertas visíveis." />
            <ValueCard colors={c} icon={<ShieldCheck size={18} />} title="Evidência runtime" text="Auditoria, comparação B/C e dados para relatório e demo." />
          </div>

          <div style={{ ...panel(c), display: "grid", gap: "10px" }}>
            <div style={{ color: c.textPrimary, fontWeight: 800 }}>Participants</div>
            <div style={{ color: c.textSecond, fontSize: "14px", lineHeight: 1.6 }}>
              Projeto e Seminário, Licenciatura em Engenharia Informática e de Computadores.
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "8px" }}>
              <SmallFact colors={c} label="Authors" value="Miguel Alves; Gabriel Mano" />
              <SmallFact colors={c} label="Advisors" value="Nuno Leite; Artur Ferreira" />
              <SmallFact colors={c} label="Focus" value="Report -> diagrams -> backend/runtime -> UI" />
            </div>
          </div>
        </section>

        <section style={{
          width: "100%",
          background: c.panelBg,
          border: `1px solid ${c.panelBorder}`,
          borderRadius: "8px",
          padding: "32px 28px",
          boxShadow: "0 8px 28px rgba(15,23,42,0.10)",
        }}>
          <div style={{ color: c.textPrimary, fontSize: "16px", fontWeight: 700, marginBottom: "6px" }}>
            Area Selection
          </div>
          <div style={{ color: c.textSecond, fontSize: "13px", marginBottom: "18px" }}>
            Escolha a área para entrar no Workspace operacional.
          </div>

          <div style={{ position: "relative", marginBottom: "20px" }}>
            <select
              value={selected}
              onChange={handleChange}
              style={{
                width: "100%",
                appearance: "none",
                WebkitAppearance: "none",
                background: c.inputBg,
                border: `1px solid ${selected ? "#16a34a" : c.inputBorder}`,
                borderRadius: "8px",
                color: selected ? "#16a34a" : c.textSecond,
                fontSize: "14px",
                padding: "12px 44px 12px 16px",
                cursor: "pointer",
                outline: "none",
                transition: "border-color 0.2s, box-shadow 0.2s",
                boxShadow: selected ? "0 0 0 2px rgba(22,163,74,0.20)" : "none",
              }}
            >
              <option value="" disabled style={{ color: c.textSecond }}>Select an area</option>
              {areas.map(area => (
                <option key={area.value} value={area.value} style={{ color: c.textPrimary, background: c.sectionBg }}>
                  {area.label}
                </option>
              ))}
            </select>
            <div style={{
              position: "absolute",
              right: "14px",
              top: "50%",
              transform: "translateY(-50%)",
              pointerEvents: "none",
              color: c.textSecond,
            }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </div>
          </div>

          <button
            disabled={!canEnter}
            onClick={() => navigate("/workspace/" + state.code)}
            onMouseEnter={() => setHovered(true)}
            onMouseLeave={() => setHovered(false)}
            style={{
              width: "100%",
              padding: "12px",
              borderRadius: "8px",
              border: "none",
              background: canEnter
                ? hovered
                  ? "linear-gradient(135deg, #15803d, #16a34a)"
                  : "linear-gradient(135deg, #16a34a, #22c55e)"
                : "rgba(100,116,139,0.20)",
              color: canEnter ? "#ffffff" : c.textMuted,
              fontSize: "14px",
              fontWeight: 700,
              cursor: canEnter ? "pointer" : "not-allowed",
              transition: "all 0.2s",
              boxShadow: canEnter && hovered ? "0 4px 20px rgba(34,197,94,0.28)" : "none",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: "8px",
            }}
          >
            Enter Monitoring Panel
            <ArrowRight size={15} />
          </button>

          <div style={{ marginTop: "18px", color: c.textMuted, fontSize: "12px", lineHeight: 1.5 }}>
            Light/dark mode is available in the global top bar. The Workspace preserves monitoring, maps, dashboards,
            scenario execution, diagnostics and evidence views.
          </div>
        </section>
      </div>
    </div>
  );
}

function ValueCard({ colors, icon, title, text }: { colors: ReturnType<typeof getColors>; icon: ReactNode; title: string; text: string }) {
  return (
    <div style={{ ...panel(colors), minHeight: "126px" }}>
      <div style={{ color: "#16a34a", marginBottom: "12px" }}>{icon}</div>
      <div style={{ color: colors.textPrimary, fontWeight: 800, marginBottom: "6px" }}>{title}</div>
      <div style={{ color: colors.textSecond, fontSize: "13px", lineHeight: 1.45 }}>{text}</div>
    </div>
  );
}

function SmallFact({ colors, label, value }: { colors: ReturnType<typeof getColors>; label: string; value: string }) {
  return (
    <div style={{ background: colors.sectionBg, border: `1px solid ${colors.panelBorder}`, borderRadius: "8px", padding: "10px" }}>
      <div style={{ color: colors.textMuted, fontSize: "12px" }}>{label}</div>
      <div style={{ color: colors.textPrimary, fontSize: "13px", fontWeight: 700, marginTop: "3px" }}>{value}</div>
    </div>
  );
}

function panel(colors: ReturnType<typeof getColors>) {
  return {
    background: colors.panelBg,
    border: `1px solid ${colors.panelBorder}`,
    borderRadius: "8px",
    padding: "14px",
  };
}
