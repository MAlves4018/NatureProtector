import { useState } from 'react';
import { Check, X } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';

export function ApprovalsPage() {
  const { pendingApprovals, decide } = useOperations();
  const [comment, setComment] = useState('');
  const [message, setMessage] = useState<string | null>(null);

  const submit = async (id: string, decision: 'approve' | 'reject') => {
    setMessage(null);
    try {
      const result = await decide(id, decision, comment || undefined);
      setMessage(`${result.displayName}: ${result.status}`);
    } catch (value) {
      setMessage(value instanceof Error ? value.message : 'Decision failed.');
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Approvals"
        subtitle="Produção, rollback e operações destrutivas exigem uma decisão separada e auditável."
        helpTopic="requestedResolved"
      />
      <label className="ui-field">
        <span>Comentário da decisão</span>
        <textarea value={comment} onChange={(event) => setComment(event.target.value)} rows={3} />
      </label>
      {message && <p className="ui-notice">{message}</p>}
      {pendingApprovals.length === 0 ? (
        <p className="ui-notice">Não existem operações à espera de aprovação.</p>
      ) : (
        pendingApprovals.map((operation) => (
          <div key={operation.id} className="ui-approval-item">
            <OperationStatus operation={operation} />
            <div className="ui-button-row">
              <button type="button" className="ui-button" onClick={() => void submit(operation.id, 'approve')}>
                <Check size={16} /> Aprovar
              </button>
              <button type="button" className="ui-secondary" onClick={() => void submit(operation.id, 'reject')}>
                <X size={16} /> Rejeitar
              </button>
            </div>
          </div>
        ))
      )}
    </section>
  );
}


