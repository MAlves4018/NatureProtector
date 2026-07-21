import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AboutPage } from './AboutPage';
import { DashboardsPage } from './DashboardsPage';
import { QualityRunsPage } from './QualityRunsPage';

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'about.title': 'Sobre',
        'about.subtitle': 'Contexto do produto',
        'about.body': 'NatureProtector protege claims com evidence.',
      })[key] ?? key,
  }),
}));

vi.mock('../state/CapabilityContext', () => ({
  useUiCapabilities: () => ({ isDark: true }),
}));

vi.mock('../state/AreaContext', () => ({
  useUiArea: () => ({ selectedAreaCode: 'PT-11' }),
}));

vi.mock('../components/views/dashBoards', () => ({
  DashBoards: ({ isDark, areaCode }: { isDark: boolean; areaCode: string }) => (
    <div data-testid="dashboards">
      {isDark ? 'dark' : 'light'}:{areaCode}
    </div>
  ),
}));

vi.mock('./OperationCategoryPage', () => ({
  OperationCategoryPage: ({ category, title, subtitle }: { category: string; title: string; subtitle: string }) => (
    <section aria-label={category}>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </section>
  ),
}));

describe('simple page wrappers', () => {
  it('renders about copy through the locale authority', () => {
    render(<AboutPage />);

    expect(screen.getByRole('heading', { name: 'Sobre', level: 2 })).toBeInTheDocument();
    expect(screen.getByText('Contexto do produto')).toBeInTheDocument();
    expect(screen.getByText('NatureProtector protege claims com evidence.')).toBeInTheDocument();
  });

  it('passes dashboard theme and selected area to the dashboard view', () => {
    render(<DashboardsPage />);

    expect(screen.getByTestId('dashboards')).toHaveTextContent('dark:PT-11');
  });

  it('binds quality runs to the quality operation category', () => {
    render(<QualityRunsPage />);

    expect(screen.getByRole('region', { name: 'quality' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Execuções de qualidade', level: 1 })).toBeInTheDocument();
    expect(screen.getByText(/não aceita comandos arbitrários/i)).toBeInTheDocument();
  });
});
