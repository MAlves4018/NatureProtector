import { useState } from 'react';
import { Activity, Cloud, FileCheck2, GitBranch, ShieldCheck, Gauge, History } from 'lucide-react';
import type { ReactNode } from 'react';
import { PageHeader } from '../components/PageHeader';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';
import { buildReadiness } from '../utils/missionControlPageHelper';

export function MissionControlPage() {
  const { catalog, operations, environments, loading, error, refresh } = useOperations();
  const latest = operations.slice(0, 4);
  const counts = {
    quality: catalog.filter((item) => item.category === 'quality' && item.availability === 'implemented').length,
    evidence: catalog.filter((item) => item.category === 'evidence' && item.availability === 'implemented').length,
    deployment: catalog.filter((item) => item.category === 'deployment' && item.availability === 'implemented').length,
    cloud: catalog.filter((item) => item.category === 'cloud' && item.availability === 'implemented').length,
  };
  const readiness = buildReadiness(catalog, operations, environments);
  const [tab, setTab] = useState<'flow' | 'readiness' | 'operations'>('flow');

  return (
    <section className="ui-page">
      <PageHeader
        title="Mission Control"
        subtitle="Code → Quality → Evidence → Release → Staging → Production, com autorização e limitações explícitas."
        helpTopic="qa"
      />
      <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
        <button
          type="button"
          className={tab === 'flow' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'flow'}
          onClick={() => setTab('flow')}
        >
          <GitBranch size={16} />
          Lifecycle
        </button>
        <button
          type="button"
          className={tab === 'readiness' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'readiness'}
          onClick={() => setTab('readiness')}
        >
          <Gauge size={16} />
          Readiness
        </button>
        <button
          type="button"
          className={tab === 'operations' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'operations'}
          onClick={() => setTab('operations')}
        >
          <History size={16} />
          Operações
        </button>
      </div>
      {tab === 'flow' && (
        <>
          <ul className="ui-mission-flow" aria-label="Engineering lifecycle">
            <MissionNode icon={<GitBranch size={18} />} label="Code" detail="ref imutável" />
            <MissionNode icon={<Activity size={18} />} label="Quality" detail={`${counts.quality} operações`} />
            <MissionNode icon={<FileCheck2 size={18} />} label="Evidence" detail={`${counts.evidence} campanhas`} />
            <MissionNode icon={<ShieldCheck size={18} />} label="Release" detail={`${counts.deployment} ações`} />
            <MissionNode icon={<Cloud size={18} />} label="Cloud" detail={`${environments.length} ambientes declarados`} />
          </ul>
          <div className="ui-notice">
            A UI pede operações fechadas. O backend autoriza e audita. GitHub Actions e os runners executam. Credenciais não
            chegam ao browser.
          </div>
        </>
      )}
      {tab === 'readiness' && (
        <section aria-labelledby="release-readiness-title">
          <div className="ui-section-heading">
            <div>
              <p className="ui-kicker">Derivado de estados e evidence</p>
              <h2 id="release-readiness-title">Release readiness</h2>
            </div>
          </div>
          <div className="ui-readiness-grid">
            {readiness.map((item) => (
              <article className="ui-card ui-readiness-card" key={item.label}>
                <div className="ui-section-heading">
                  <strong>{item.label}</strong>
                  <span className="ui-risk" data-risk={item.risk}>
                    {item.status}
                  </span>
                </div>
                <p>{item.detail}</p>
              </article>
            ))}
          </div>
        </section>
      )}
      {tab === 'operations' && (
        <>
          <div className="ui-section-heading">
            <h2>Operações recentes</h2>
            <button type="button" className="ui-secondary" onClick={() => void refresh()} disabled={loading}>
              {loading ? 'A atualizar…' : 'Atualizar'}
            </button>
          </div>
          {error && <p className="ui-notice ui-error">{error.message}</p>}
          {latest.length === 0 ? (
            <p className="ui-notice">Ainda não existem operações registadas neste store.</p>
          ) : (
            <div className="ui-grid">
              {latest.map((operation) => (
                <OperationStatus key={operation.id} operation={operation} compact />
              ))}
            </div>
          )}
        </>
      )}
    </section>
  );
}

function MissionNode({ icon, label, detail }: { icon: ReactNode; label: string; detail: string }) {
  return (
    <li className="ui-mission-node">
      {icon}
      <strong>{label}</strong>
      <span>{detail}</span>
    </li>
  );
}