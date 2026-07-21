import { FileCheck2 } from "lucide-react";

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
