import { useState } from 'react';
import { Download } from 'lucide-react';
import { api } from '../../services/api';
import { BetaParityLinks } from '../components/BetaParityLinks';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

type EvidenceItem = ReturnType<typeof useUiV2>['evidenceItems'][number];

export function QualityEvidencePage() {
  const { copy, qaSuites, evidenceItems } = useUiV2();
  const latest = qaSuites[0] ?? null;
  const historical = evidenceItems.filter(item => item.environment !== 'Current UI/API session');
  const runtimeEvidence = evidenceItems.filter(item => item.environment === 'Current UI/API session');

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('qa.title')} subtitle={copy('qa.subtitle')} helpTopic="qa" />
      {latest && (
        <section className="ui-v2-card">
          <div className="ui-v2-section-heading">
            <h3>Latest test execution</h3>
            <StatusBadge label={latest.status} state="partial" />
          </div>
          <p>{latest.suiteName}</p>
          <div className="ui-v2-fact-list">
            <span><strong>{copy('technical.testDefinition')}</strong>{latest.testDefinition}</span>
            <span><strong>{copy('technical.testExecution')}</strong>{latest.testExecution}</span>
            <span><strong>{copy('technical.environment')}</strong>{latest.environment}</span>
            <span><strong>{copy('technical.coverage')}</strong>{latest.coverage}</span>
          </div>
        </section>
      )}
      <EvidenceSection title="Runtime evidence" items={runtimeEvidence} />
      <EvidenceSection title="Historical evidence" items={historical} />
      <BetaParityLinks ids={['evidence-comparison']} />
    </section>
  );
}

function EvidenceSection({ title, items }: { title: string; items: ReturnType<typeof useUiV2>['evidenceItems'] }) {
  const { copy } = useUiV2();

  return (
    <section className="ui-v2-panel">
      <h3>{title}</h3>
      <div className="ui-v2-grid">
        {items.map(item => (
          <article className="ui-v2-card" key={item.evidenceId}>
            <div className="ui-v2-section-heading">
              <h4>{item.title}</h4>
              <StatusBadge label={item.availability} state={item.availability} />
            </div>
            <p>{item.scope}</p>
            <span className="ui-v2-label">{copy('technical.supports')}</span>
            <ul>{item.supportsClaims.map(claim => <li key={claim}>{claim}</li>)}</ul>
            <span className="ui-v2-label">{copy('technical.notSupport')}</span>
            <ul>{item.doesNotSupportClaims.map(claim => <li key={claim}>{claim}</li>)}</ul>
            {item.reference.startsWith('/api/') && item.availability === 'ready' && <EvidenceDownloadButton item={item} />}
          </article>
        ))}
      </div>
    </section>
  );
}

function EvidenceDownloadButton({ item }: { item: EvidenceItem }) {
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const handleDownload = async () => {
    setDownloading(true);
    setDownloadError(null);
    try {
      const result = await api.downloadRuntimeEvidence(item.evidenceId);
      triggerBrowserDownload(result.blob, result.filename ?? defaultEvidenceFilename(item));
    } catch (err) {
      setDownloadError(err instanceof Error ? err.message : 'Download failed');
    } finally {
      setDownloading(false);
    }
  };

  return (
    <>
      <button type="button" className="ui-v2-button" onClick={handleDownload} disabled={downloading}>
        <Download size={16} />
        {downloading ? 'Downloading evidence' : 'Download evidence'}
      </button>
      {downloadError && <p className="ui-v2-notice ui-v2-error">{downloadError}</p>}
    </>
  );
}

function triggerBrowserDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

function defaultEvidenceFilename(item: EvidenceItem) {
  return item.type.toLowerCase().includes('json')
    ? `${item.evidenceId}.json`
    : `${item.evidenceId}.txt`;
}
