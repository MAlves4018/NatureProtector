import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { QualityEvidencePage } from './QualityEvidencePage';

const downloadRuntimeEvidenceMock = vi.fn();

let evidenceItems: any[];

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle }: { title: string; subtitle: string }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </header>
  ),
}));

vi.mock('../components/StatusBadge', () => ({
  StatusBadge: ({ label, state }: { label: string; state: string }) => (
    <span data-testid={`status-${label}`}>
      {label}:{state}
    </span>
  ),
}));

vi.mock('../services/api', () => ({
  api: {
    downloadRuntimeEvidence: (evidenceId: string) => downloadRuntimeEvidenceMock(evidenceId),
  },
}));

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'qa.title': 'Evidência QA',
        'qa.subtitle': 'Estado auditável das provas',
        'technical.supports': 'Suporta',
        'technical.notSupport': 'Não suporta',
      })[key] ?? key,
  }),
}));

vi.mock('../state/useUiSurfaces', () => ({
  useEvidenceItems: () => evidenceItems,
}));

describe('QualityEvidencePage', () => {
  beforeEach(() => {
    downloadRuntimeEvidenceMock.mockReset();
    evidenceItems = [
      {
        evidenceId: 'runtime-json',
        title: 'Runtime JSON evidence',
        type: 'application/json',
        scope: 'SimulationRunId run-b',
        environment: 'Current UI/API session',
        availability: 'ready',
        reference: '/api/control/runtime/observability/evidence/runtime-json',
        supportsClaims: ['live accounting'],
        doesNotSupportClaims: ['production performance'],
      },
      {
        evidenceId: 'runtime-text',
        title: 'Runtime text evidence',
        type: 'text/plain',
        scope: 'Run notes',
        environment: 'Current UI/API session',
        availability: 'ready',
        reference: '/api/control/runtime/observability/evidence/runtime-text',
        supportsClaims: ['operator notes'],
        doesNotSupportClaims: ['latency percentiles'],
      },
      {
        evidenceId: 'runtime-pending',
        title: 'Runtime pending evidence',
        type: 'text/plain',
        scope: 'Pending export',
        environment: 'Current UI/API session',
        availability: 'pending',
        reference: '/api/control/runtime/observability/evidence/runtime-pending',
        supportsClaims: [],
        doesNotSupportClaims: ['current proof'],
      },
      {
        evidenceId: 'history-only',
        title: 'Historical repository evidence',
        type: 'markdown',
        scope: 'Repository file',
        environment: 'Repository history',
        availability: 'historical',
        reference: 'docs/history.md',
        supportsClaims: ['historical context'],
        doesNotSupportClaims: ['current runtime'],
      },
    ];
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:evidence'),
      revokeObjectURL: vi.fn(),
    });
  });

  it('separates current runtime evidence from historical repository claims', () => {
    render(<QualityEvidencePage />);

    expect(screen.getByRole('heading', { name: 'Evidência QA' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Only items loaded from the current runtime evidence API');
    expect(screen.getByRole('heading', { name: 'Runtime evidence — current API session' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Historical repository claims — not revalidated' })).toBeInTheDocument();
    expect(screen.getByText('Runtime JSON evidence')).toBeInTheDocument();
    expect(screen.getByText('Runtime pending evidence')).toBeInTheDocument();
    expect(screen.getByText('Historical repository evidence')).toBeInTheDocument();
    expect(screen.getByText('live accounting')).toBeInTheDocument();
    expect(screen.getByText('production performance')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /Download evidence/i })).toHaveLength(2);
  });

  it('downloads runtime evidence with explicit and default filenames', async () => {
    const clickMock = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = originalCreateElement(tagName);
      if (tagName === 'a') {
        Object.defineProperty(element, 'click', { value: clickMock });
      }
      return element;
    });
    downloadRuntimeEvidenceMock
      .mockResolvedValueOnce({ blob: new Blob(['json']), filename: 'explicit.json' })
      .mockResolvedValueOnce({ blob: new Blob(['text']), filename: null });

    render(<QualityEvidencePage />);

    fireEvent.click(screen.getAllByRole('button', { name: /Download evidence/i })[0]);
    await waitFor(() => expect(downloadRuntimeEvidenceMock).toHaveBeenCalledWith('runtime-json'));
    expect(clickMock).toHaveBeenCalledTimes(1);
    expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob));

    fireEvent.click(screen.getAllByRole('button', { name: /Download evidence/i })[1]);
    await waitFor(() => expect(downloadRuntimeEvidenceMock).toHaveBeenCalledWith('runtime-text'));
    expect(clickMock).toHaveBeenCalledTimes(2);
  });

  it('reports download failures without enabling historical downloads', async () => {
    downloadRuntimeEvidenceMock.mockRejectedValueOnce(new Error('download failed'));

    render(<QualityEvidencePage />);

    fireEvent.click(screen.getAllByRole('button', { name: /Download evidence/i })[0]);

    expect(await screen.findByText('download failed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Historical repository evidence/i })).not.toBeInTheDocument();
  });
});
