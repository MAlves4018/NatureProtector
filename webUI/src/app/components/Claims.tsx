import { ShieldCheck } from "lucide-react";
import { FileCheck2 } from "lucide-react";
import { RuntimeOperationResponse, RuntimeRunAuditResponse, RuntimeRunTimingSummaryResponse} from "../types";


export function Claims({ selectedRunId, runAudit, runOperation, runTimings }: {
    selectedRunId: string;
    runAudit: RuntimeRunAuditResponse | null;
    runOperation: RuntimeOperationResponse | null;
    runTimings: RuntimeRunTimingSummaryResponse | null;
}) {
    return (<section className="ui-card">
        <div className="ui-section-heading">
            <div>
                <span className="ui-eyebrow">SimulationRunId</span>
                <h3>{selectedRunId || 'Selecione uma execução'}</h3>
            </div>
            <ShieldCheck size={22} />
        </div>
        <div className="ui-table-wrap">
            <table className="ui-table">
                <thead>
                    <tr>
                        <th>Claim</th>
                        <th>Resultado</th>
                        <th>Fonte</th>
                        <th>Timestamp</th>
                        <th>Artefacto</th>
                        <th>Classe</th>
                        <th>Verificação</th>
                    </tr>
                </thead>
                <tbody>
                    <ClaimRow
                        claim="Accounting run-scoped"
                        result={
                            runAudit
                                ? `${runAudit.acceptedReadings}/${runAudit.expectedEvents ?? '—'} aceites; ${runAudit.riskAssessments} avaliados`
                                : null
                        }
                        source="GET /runtime/runs/{id}/audit"
                        timestamp={runAudit?.dataScope?.observedAt}
                        artifact={runAudit ? `run-${selectedRunId}-audit.json` : null}
                        verified={Boolean(runAudit)}
                    />
                    <ClaimRow
                        claim="Lifecycle e settlement"
                        result={runOperation ? `${runOperation.state}; settled=${runOperation.accounting.settled}` : null}
                        source="GET /runtime/runs/{id}/operation"
                        timestamp={runOperation?.updatedAt}
                        artifact={runOperation?.evidenceId}
                        verified={Boolean(runOperation)}
                    />
                    <ClaimRow
                        claim="Timings persistidos"
                        result={
                            runTimings?.runDurationMs == null
                                ? null
                                : `${runTimings.runDurationMs.toFixed(1)} ms; ${runTimings.attempts.attemptCount} tentativas`
                        }
                        source="GET /runtime/runs/{id}/timings"
                        timestamp={runTimings?.dataScope?.observedAt}
                        artifact={runTimings ? `run-${selectedRunId}-timings.json` : null}
                        verified={Boolean(runTimings)}
                    />
                    <ClaimRow
                        claim="Índices científicos persistidos"
                        result={
                            runAudit?.scoreComponents
                                ? `NP=${runAudit.scoreComponents.npScore}; FWI=${runAudit.indexComparison?.fireWeatherIndex}; KBDI=${runAudit.indexComparison?.keetchByramDroughtIndex}`
                                : null
                        }
                        source="GET /runtime/runs/{id}/audit"
                        timestamp={runAudit?.scoreComponents?.latestAssessmentTimestamp}
                        artifact={runOperation?.evidenceId}
                        verified={Boolean(runAudit?.scoreComponents)}
                    />
                </tbody>
            </table>
        </div>
    </section>)
}

export function EvidenceMetric({ label, value }: { label: string; value: number }) {
  return (
    <article className="ui-metric-card">
      <span className="ui-metric-icon">
        <FileCheck2 size={17} />
      </span>
      <strong>{value}</strong>
      <small>{label}</small>
    </article>
  );
}

export function ClaimRow({
  claim,
  result,
  source,
  timestamp,
  artifact,
  verified,
}: {
  claim: string;
  result: string | null;
  source: string;
  timestamp?: string | null;
  artifact?: string | null;
  verified: boolean;
}) {
  return (
    <tr>
      <td>{claim}</td>
      <td>{result ?? 'Indisponível para esta run'}</td>
      <td>{source}</td>
      <td>{timestamp ? new Date(timestamp).toLocaleString('pt-PT') : 'Indisponível'}</td>
      <td>{artifact ?? 'Sem artefacto associado'}</td>
      <td>Live local</td>
      <td>{verified ? 'Verificado pela resposta API' : 'Não verificado'}</td>
    </tr>
  );
}
