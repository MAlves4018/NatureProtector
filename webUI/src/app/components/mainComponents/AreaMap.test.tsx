import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AreaMap } from './AreaMap';
import type { AreaCellResponse, SensorNodeResponse } from '../../types';

const leaflet = vi.hoisted(() => ({
  markerClicks: [] as Array<() => void>,
  circleMarker: vi.fn(),
}));

vi.mock('@chakra-ui/react', () => ({
  Box: React.forwardRef<HTMLDivElement, any>(({ children, ...props }, ref) => (
    <div ref={ref} {...domProps(props)}>
      {children}
    </div>
  )),
  Button: ({ children, ...props }: any) => <button {...domProps(props)}>{children}</button>,
  Text: ({ children, ...props }: any) => <span {...domProps(props)}>{children}</span>,
}));

vi.mock('leaflet', () => {
  class LatLng {
    constructor(
      public lat: number,
      public lng: number,
      public alt?: number,
    ) {}
  }

  const layer = () => ({
    addTo: vi.fn().mockReturnThis(),
    remove: vi.fn(),
  });

  const map = {
    invalidateSize: vi.fn(),
    stop: vi.fn(),
    off: vi.fn(),
    remove: vi.fn(),
    fitBounds: vi.fn(),
  };

  const tileLayer: any = vi.fn(() => layer());
  tileLayer.wms = vi.fn(() => layer());

  leaflet.circleMarker.mockImplementation(() => {
    const marker = {
      addTo: vi.fn().mockReturnThis(),
      on: vi.fn((event: string, handler: () => void) => {
        if (event === 'click') {
          leaflet.markerClicks.push(handler);
        }

        return marker;
      }),
      remove: vi.fn(),
    };
    return marker;
  });

  return {
    default: {
      Icon: {
        Default: {
          prototype: {},
          mergeOptions: vi.fn(),
        },
      },
      map: vi.fn(() => map),
      tileLayer,
      geoJSON: vi.fn(() => ({
        ...layer(),
        getBounds: vi.fn(() => ({ isValid: () => true })),
      })),
      circleMarker: leaflet.circleMarker,
    },
    LatLng,
  };
});

describe('AreaMap', () => {
  beforeEach(() => {
    leaflet.markerClicks.length = 0;
    leaflet.circleMarker.mockClear();
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        text: async () =>
          [
            'https://grafana.local/d/temp?var-area_id=?1?&var-sensor_id=?t?',
            'https://grafana.local/d/humidity?var-area_id=?1?&var-sensor_id=?h?',
            'https://grafana.local/d/wind?var-area_id=?1?&var-sensor_id=?w?',
          ].join('\n'),
      })),
    );
  });

  it('opens cell dashboards with area and sensor scoped Grafana URLs', async () => {
    render(
      <AreaMap
        areaId="area-pt-11"
        showGrid
        mapType="standard"
        geoJSON={featureCollection()}
        cells={[cell('CELL-001', ['temperature-cell-001', 'humidity-cell-001', 'wind-cell-001'])]}
        sensorNodes={[
          sensor('temperature-cell-001', 'Temperature CELL 001', 'temperature', 'CELL-001'),
          sensor('humidity-cell-001', 'Humidity CELL 001', 'humidity', 'CELL-001'),
          sensor('wind-cell-001', 'Wind CELL 001', 'wind', 'CELL-001'),
        ]}
      />,
    );

    await waitFor(() => expect(leaflet.circleMarker).toHaveBeenCalledTimes(1));
    act(() => leaflet.markerClicks[0]());

    expect(await screen.findByText('Cell CELL-001 dashboards')).toBeInTheDocument();
    expect(screen.getByText('Sensor: Temperature CELL 001')).toBeInTheDocument();
    expect(screen.getByTitle('Temperature dashboard for CELL-001')).toHaveAttribute(
      'src',
      'https://grafana.local/d/temp?var-area_id=area-pt-11&var-sensor_id=temperature-cell-001&kiosk=',
    );
    expect(screen.getByTitle('Humidity dashboard for CELL-001')).toHaveAttribute(
      'src',
      'https://grafana.local/d/humidity?var-area_id=area-pt-11&var-sensor_id=humidity-cell-001&kiosk=',
    );
    expect(screen.getByTitle('Wind dashboard for CELL-001')).toHaveAttribute(
      'src',
      'https://grafana.local/d/wind?var-area_id=area-pt-11&var-sensor_id=wind-cell-001&kiosk=',
    );
  });

  it('shows explicit sensor mapping limitations instead of unresolved dashboards', async () => {
    render(
      <AreaMap
        areaId="area-pt-11"
        showGrid
        mapType="standard"
        geoJSON={featureCollection()}
        cells={[cell('CELL-002', [])]}
        sensorNodes={[]}
      />,
    );

    await waitFor(() => expect(leaflet.circleMarker).toHaveBeenCalledTimes(1));
    act(() => leaflet.markerClicks[0]());

    expect(await screen.findByText('Cell CELL-002 dashboards')).toBeInTheDocument();
    expect(screen.getAllByText('Sensor mapping not exposed for this cell')).toHaveLength(3);
    expect(screen.queryByTitle(/dashboard for CELL-002/i)).not.toBeInTheDocument();
  });

  it('reports dashboard configuration limitations when Grafana links cannot be loaded', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new Error('offline'))));

    render(
      <AreaMap
        areaId="area-pt-11"
        showGrid
        mapType="standard"
        geoJSON={featureCollection()}
        cells={[cell('CELL-003', ['temperature-cell-003'])]}
        sensorNodes={[sensor('temperature-cell-003', 'Temperature CELL 003', 'temperature', 'CELL-003')]}
      />,
    );

    await waitFor(() => expect(leaflet.circleMarker).toHaveBeenCalledTimes(1));
    act(() => leaflet.markerClicks[0]());

    expect(await screen.findByText('Cell CELL-003 dashboards')).toBeInTheDocument();
    expect(screen.getByText('Sensor: Temperature CELL 003')).toBeInTheDocument();
    expect(screen.getAllByText('Grafana dashboard not configured').length).toBeGreaterThan(0);
    expect(screen.queryByTitle(/dashboard for CELL-003/i)).not.toBeInTheDocument();
  });

  it('normalizes object sensor mappings and rejects unresolved dashboard templates', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        text: async () =>
          [
            'https://grafana.local/d/temp?var-area_id=?1?&var-sensor_id=?t?',
            'https://grafana.local/d/humidity?var-area_id=Enter value&var-sensor_id=?h?',
            'https://grafana.local/d/wind?var-area_id=?1?&var-sensor_id=',
          ].join('\n'),
      })),
    );

    render(
      <AreaMap
        areaId="area-pt-11"
        showGrid
        mapType="standard"
        geoJSON={featureCollection()}
        cells={[
          cell('CELL-004', [
            { id: 'temp-object-004', type: 'temperature', name: 'Object temperature 004' },
            { sensorId: 'humidity-object-004', sensorType: 'humidity' },
            { item1: 'wind-object-004', item2: 'wind' },
          ] as any),
        ]}
        sensorNodes={[
          sensor('temp-object-004', 'Temperature Object 004', 'temperature', 'CELL-004'),
          sensor('humidity-object-004', 'Humidity Object 004', 'humidity', 'CELL-004'),
          sensor('wind-object-004', 'Wind Object 004', 'wind', 'CELL-004'),
        ]}
      />,
    );

    await waitFor(() => expect(leaflet.circleMarker).toHaveBeenCalledTimes(1));
    act(() => leaflet.markerClicks[0]());

    expect(await screen.findByText('Cell CELL-004 dashboards')).toBeInTheDocument();
    expect(screen.getByText('Sensor: Temperature Object 004')).toBeInTheDocument();
    expect(screen.getByTitle('Temperature dashboard for CELL-004')).toHaveAttribute(
      'src',
      'https://grafana.local/d/temp?var-area_id=area-pt-11&var-sensor_id=temp-object-004&kiosk=',
    );
    expect(screen.getByText('Sensor: Humidity Object 004')).toBeInTheDocument();
    expect(screen.getByText('Sensor: Wind Object 004')).toBeInTheDocument();
    expect(screen.getAllByText('Grafana dashboard not configured')).toHaveLength(2);
  });

  it('closes the dashboard modal from the backdrop and close control', async () => {
    render(
      <AreaMap
        areaId="area-pt-11"
        showGrid
        mapType="standard"
        geoJSON={featureCollection()}
        cells={[cell('CELL-005', ['temperature-cell-005'])]}
        sensorNodes={[sensor('temperature-cell-005', 'Temperature CELL 005', 'temperature', 'CELL-005')]}
      />,
    );

    await waitFor(() => expect(leaflet.circleMarker).toHaveBeenCalledTimes(1));
    act(() => leaflet.markerClicks[0]());
    const title = await screen.findByText('Cell CELL-005 dashboards');

    fireEvent.click(title.closest('div')!);
    expect(screen.getByText('Cell CELL-005 dashboards')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button'));
    expect(screen.queryByText('Cell CELL-005 dashboards')).not.toBeInTheDocument();

    act(() => leaflet.markerClicks[0]());
    expect(await screen.findByText('Cell CELL-005 dashboards')).toBeInTheDocument();
    const backdrop = screen.getByText('Cell CELL-005 dashboards').closest('div')!.parentElement!.parentElement!
      .parentElement!;
    fireEvent.click(backdrop);
    expect(screen.queryByText('Cell CELL-005 dashboards')).not.toBeInTheDocument();
  });
});

function featureCollection() {
  return {
    type: 'FeatureCollection',
    features: [
      {
        type: 'Feature',
        properties: { cell_id: 'CELL-001' },
        geometry: {
          type: 'Polygon',
          coordinates: [
            [
              [-7.9, 39.7],
              [-7.8, 39.7],
              [-7.8, 39.8],
              [-7.9, 39.8],
              [-7.9, 39.7],
            ],
          ],
        },
      },
    ],
  };
}

function cell(cellCode: string, sensorNodeIds: AreaCellResponse['sensorNodeIds']): AreaCellResponse {
  return {
    cellCode,
    sensorNodeIds,
    configurationVersionNumber: 1,
    centroidLatitude: 39.75,
    centroidLongitude: -7.85,
    altitudeMeters: 420,
    slopeDegrees: null,
    aspectDegrees: null,
    landCoverClass: null,
    dominantForestType: null,
    dominantFuelModel: null,
    treeCoverDensity: null,
    structuralHazard: null,
    conjuncturalHazard: null,
    sensorNodeCount: Math.max(1, sensorNodeIds.length),
  };
}

function sensor(id: string, name: string, type: string, cellCode: string): SensorNodeResponse {
  return {
    id,
    name,
    type,
    configurationVersionNumber: 1,
    cellCode,
    profileName: `${type}-profile`,
    sensorFamily: type,
    networkName: 'pilot-network',
    latitude: 39.75,
    longitude: -7.85,
    altitudeMeters: 420,
    isActive: true,
    installationProfile: 'standard',
  };
}

function domProps(props: Record<string, unknown>) {
  const allowed = new Set(['className', 'style', 'onClick', 'type', 'title', 'src', 'width', 'height', 'loading']);
  return Object.fromEntries(
    Object.entries(props).filter(
      ([key]) =>
        allowed.has(key) ||
        key.startsWith('aria-') ||
        key.startsWith('data-') ||
        key.startsWith('on'),
    ),
  );
}
