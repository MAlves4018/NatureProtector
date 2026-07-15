import { useState } from 'react';
import { Download, ShieldAlert } from 'lucide-react';
import { api } from '../services/api';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useEvidenceItems } from '../state/useUiSurfaces';
import type { UiEvidenceItem } from '../technicalSurfaces';

export function QualityEvidencePage() {
  const { copy } = useUiLocale();
  const evidenceItems = useEvidenceItems();
  const historical = evidenceItems.filter((item) => item.environment !== 'Current UI/API session');
  const runtimeEvidence = evidenceItems.filter((item) => item.environment === 'Current UI/API session');

  return (
    <section className="ui-page">
      <PageHeader title={copy('qa.title')} subtitle={copy('qa.subtitle')} helpTopic="qa" />
      <div className="ui-notice ui-warning" role="status">
        <ShieldAlert size={16} />
        <span>
          Only items loaded from the current runtime evidence API are current observations. Repository history is an
          unverified historical claim and is not revalidated by this page.
        </span>
      </div>
      <EvidenceSection title="Runtime evidence — current API session" items={runtimeEvidence} />
      <EvidenceSection title="Historical repository claims — not revalidated" items={historical} />
    </section>
  );
}

function EvidenceSection({ title, items }: { title: string; items: UiEvidenceItem[] }) {
  const { copy } = useUiLocale();

  return (
    <section className="ui-panel">
      <h3>{title}</h3>
      <div className="ui-grid">
        {items.map((item) => (
          <article className="ui-card" key={item.evidenceId}>
            <div className="ui-section-heading">
              <h4>{item.title}</h4>
              <StatusBadge label={item.availability} state={item.availability} />
            </div>
            <p>{item.scope}</p>
            <span className="ui-label">{copy('technical.supports')}</span>
            <ul>
              {item.supportsClaims.map((claim) => (
                <li key={claim}>{claim}</li>
              ))}
            </ul>
            <span className="ui-label">{copy('technical.notSupport')}</span>
            <ul>
              {item.doesNotSupportClaims.map((claim) => (
                <li key={claim}>{claim}</li>
              ))}
            </ul>
            {item.reference.startsWith('/api/') && item.availability === 'ready' && (
              <EvidenceDownloadButton item={item} />
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

function EvidenceDownloadButton({ item }: { item: UiEvidenceItem }) {
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
      <button type="button" className="ui-button" onClick={handleDownload} disabled={downloading}>
        <Download size={16} />
        {downloading ? 'Downloading evidence' : 'Download evidence'}
      </button>
      {downloadError && <p className="ui-notice ui-error">{downloadError}</p>}
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
  queueMicrotask(() => URL.revokeObjectURL(url));
}

function defaultEvidenceFilename(item: UiEvidenceItem) {
  return item.type.toLowerCase().includes('json') ? `${item.evidenceId}.json` : `${item.evidenceId}.txt`;
}
