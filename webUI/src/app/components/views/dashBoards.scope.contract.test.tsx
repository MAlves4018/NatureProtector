import { act, render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AreaCellResponse, AreaGeoJSONResponse, SensorNodeResponse } from '../../types';
import { api } from '../../services/api';
import { DashBoards } from './dashBoards';

vi.mock('@chakra-ui/react', () => ({
  Box: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Flex: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
}));
vi.mock('../mainComponents/GrafanaStrip', () => ({
  AreaRisk: ({ areaId }: { areaId: string }) => <output data-testid="risk">{areaId}</output>,
  GrafanaStrip: ({ areaId }: { areaId: string }) => <output data-testid="grafana">{areaId}</output>,
}));
vi.mock('../mainComponents/AreaMap', () => ({
  AreaMap: ({
    areaId,
    geoJSON,
    cells,
    sensorNodes,
  }: {
    areaId: string;
    geoJSON: { area?: string } | null;
    cells: AreaCellResponse[];
    sensorNodes: SensorNodeResponse[];
  }) => <output data-testid="map">{[areaId, geoJSON?.area, cells[0]?.cellCode, sensorNodes[0]?.id].join('|')}</output>,
}));
vi.mock('../../utils/utils', () => ({ getColors: () => ({}) }));

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

beforeEach(() => vi.restoreAllMocks());

describe('R1M-002 dashboard area generation contract', () => {
  it('does not let late responses from area A overwrite area B', async () => {
    const aGeo = deferred<AreaGeoJSONResponse>();
    const aCells = deferred<AreaCellResponse[]>();
    const aSensors = deferred<SensorNodeResponse[]>();
    vi.spyOn(api, 'getAreaGeoJSON').mockImplementation(async (area) =>
      area === 'area-a' ? aGeo.promise : { id: 'id-area-b', geometryGeoJson: '{"area":"area-b"}' },
    );
    vi.spyOn(api, 'getAreaCells').mockImplementation(async (area) =>
      area === 'area-a' ? aCells.promise : ([{ cellCode: 'cell-area-b' }] as AreaCellResponse[]),
    );
    vi.spyOn(api, 'getAreaSensorNodes').mockImplementation(async (area) =>
      area === 'area-a' ? aSensors.promise : ([{ id: 'sensor-area-b' }] as SensorNodeResponse[]),
    );

    const view = render(<DashBoards isDark={false} areaCode="area-a" />);
    view.rerender(<DashBoards isDark={false} areaCode="area-b" />);
    await waitFor(() =>
      expect(screen.getByTestId('map')).toHaveTextContent('id-area-b|area-b|cell-area-b|sensor-area-b'),
    );

    await act(async () => {
      aGeo.resolve({ id: 'id-area-a', geometryGeoJson: '{"area":"area-a"}' });
      aCells.resolve([{ cellCode: 'cell-area-a' }] as AreaCellResponse[]);
      aSensors.resolve([{ id: 'sensor-area-a' }] as SensorNodeResponse[]);
      await Promise.all([aGeo.promise, aCells.promise, aSensors.promise]);
    });
    expect(screen.getByTestId('risk')).toHaveTextContent('id-area-b');
    expect(screen.getByTestId('grafana')).toHaveTextContent('id-area-b');
    expect(screen.getByTestId('map')).toHaveTextContent('id-area-b|area-b|cell-area-b|sensor-area-b');
  });
});
