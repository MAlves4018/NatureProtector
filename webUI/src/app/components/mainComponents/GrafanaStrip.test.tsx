import { render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getColors } from '../../utils/utils';
import { AreaRisk, GrafanaStrip } from './GrafanaStrip';

vi.mock('@chakra-ui/react', () => ({
  Box: ({ children, ...props }: { children?: ReactNode }) => <div {...props}>{children}</div>,
  Flex: ({ children, ...props }: { children?: ReactNode }) => <div {...props}>{children}</div>,
  Heading: ({ children }: { children?: ReactNode }) => <h2>{children}</h2>,
}));

const colors = getColors(false);

describe('GrafanaStrip and AreaRisk', () => {
  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  it('loads dashboard links and appends kiosk parameters while replacing area placeholders', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        text: () => Promise.resolve('https://grafana/d/one?var-area=???\nhttps://grafana/d/two\n'),
      }),
    );

    render(<GrafanaStrip isDark={false} areaId="AREA-1" {...colors} />);

    expect(screen.getByRole('heading', { name: 'Grafana Dashboards', level: 2 })).toBeInTheDocument();
    await waitFor(() => expect(screen.getAllByTitle(/Dashboard/i)).toHaveLength(2));
    expect(screen.getByTitle('Dashboard 0')).toHaveAttribute(
      'src',
      'https://grafana/d/one?var-area=AREA-1&kiosk&nav=false',
    );
    expect(screen.getByTitle('Dashboard 1')).toHaveAttribute('src', 'https://grafana/d/two?kiosk&nav=false');
  });

  it('renders risk dashboard links with area replacement and tolerates fetch failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce({ text: () => Promise.resolve('https://risk/???\n') }));
    const view = render(<AreaRisk isDark areaId="PT-11" {...colors} />);

    await waitFor(() =>
      expect(screen.getByTitle('Area Risk Dashboard 1')).toHaveAttribute('src', 'https://risk/PT-11?kiosk&nav=false'),
    );

    vi.stubGlobal('fetch', vi.fn().mockRejectedValueOnce(new Error('missing file')));
    view.rerender(<AreaRisk isDark areaId="PT-12" {...colors} />);
    await waitFor(() => expect(console.error).toHaveBeenCalledWith('Failed to load dashboards:', expect.any(Error)));
  });
});
