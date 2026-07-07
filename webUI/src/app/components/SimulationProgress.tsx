import { useState, useEffect, useCallback } from 'react';
import { CheckCircle2, Clock, ExternalLink } from 'lucide-react';
import { useUiToast } from '../state/ToastContext';

interface SimulationProgressProps {
  runId: string;
  onBackToForm: () => void;
}

const STAGES = [
  { key: 'submitted', label: 'Submetido' },
  { key: 'sensors', label: 'Sensores inicializados' },
  { key: 'readings', label: 'A gerar leituras' },
  { key: 'processing', label: 'Processamento' },
  { key: 'assessment', label: 'Avaliação de risco' },
  { key: 'complete', label: 'Concluído' },
] as const;

type StageKey = (typeof STAGES)[number]['key'];

function getStageIndex(key: StageKey): number {
  return STAGES.findIndex((s) => s.key === key);
}

export function SimulationProgress({ runId, onBackToForm }: SimulationProgressProps) {
  const [currentStage, setCurrentStage] = useState<StageKey>('submitted');
  const [elapsed, setElapsed] = useState(0);
  const [completed, setCompleted] = useState(false);
  const { addToast } = useUiToast();

  useEffect(() => {
    const timer = setInterval(() => {
      setElapsed((prev) => prev + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    if (completed) return;

    const advance = () => {
      setCurrentStage((prev) => {
        const idx = getStageIndex(prev);
        if (idx >= STAGES.length - 1) {
          setCompleted(true);
          return prev;
        }
        return STAGES[idx + 1].key;
      });
    };

    const delays = [2000, 3000, 2500, 3500, 2000];
    const idx = getStageIndex(currentStage);
    if (idx < delays.length) {
      const timeout = setTimeout(advance, delays[idx]);
      return () => clearTimeout(timeout);
    }
  }, [currentStage, completed]);

  useEffect(() => {
    if (completed) {
      addToast({
        severity: 'success',
        title: 'Simulação concluída',
        message: `A execução ${runId} terminou com sucesso.`,
      });
    }
  }, [completed, runId, addToast]);

  const handleViewRun = useCallback(() => {
    const navEvent = new CustomEvent('ui-navigate', { detail: { target: 'runs' } });
    window.dispatchEvent(navEvent);
  }, []);

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  return (
    <div className="ui-card">
      <div className="ui-section-heading">
        <h3>Simulação em progresso</h3>
        <span className="ui-badge">
          <Clock size={14} />
          {formatTime(elapsed)}
        </span>
      </div>
      <p style={{ marginBottom: 16, color: 'var(--ui-muted)' }}>
        Execução <strong>{runId}</strong>
      </p>
      <div className="ui-progress-stages">
        {STAGES.map((stage, i) => {
          const stageIdx = getStageIndex(currentStage);
          const isActive = getStageIndex(stage.key) === stageIdx && !completed;
          const isDone = getStageIndex(stage.key) < stageIdx || (completed && stage.key === 'complete');
          const isPending = getStageIndex(stage.key) > stageIdx;

          return (
            <div
              key={stage.key}
              className={`ui-progress-stage${isActive ? ' ui-progress-stage-active' : ''}${isDone ? ' ui-progress-stage-done' : ''}${isPending ? ' ui-progress-stage-pending' : ''}`}
            >
              <div className="ui-progress-marker">
                {isDone ? <CheckCircle2 size={20} /> : <div className="ui-progress-dot" />}
              </div>
              <div className="ui-progress-label">
                <span>{stage.label}</span>
                {isActive && !completed && <span className="ui-badge">Em execução...</span>}
              </div>
              {i < STAGES.length - 1 && <div className="ui-progress-line" />}
            </div>
          );
        })}
      </div>
      {completed && (
        <div className="ui-button-row" style={{ marginTop: 16 }}>
          <button type="button" className="ui-button" onClick={handleViewRun}>
            <ExternalLink size={16} />
            Ver execução
          </button>
          <button type="button" className="ui-secondary" onClick={onBackToForm}>
            Voltar ao formulário
          </button>
        </div>
      )}
    </div>
  );
}
