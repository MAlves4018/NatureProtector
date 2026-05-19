import { Box, Button, Text } from '@chakra-ui/react';
import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import 'leaflet/dist/leaflet.css';
import { MapProps, SensorInfo } from '../../types';
import L, { LatLng, LatLngExpression } from 'leaflet';
import { TILES } from '../../utils/utils';

const COPERNICUS_WMS = {
  corine: {
    url: 'https://image.discomap.eea.europa.eu/arcgis/services/Corine/CLC2018_WM/MapServer/WmsServer',
    layer: '1',
  },
};

type CellDashboardMetric = {
  key: 'temperature' | 'humidity' | 'wind';
  label: string;
  missingText: string;
  placeholder: string;
  sensorTypes: string[];
};

const CELL_DASHBOARD_METRICS: CellDashboardMetric[] = [
  { key: 'temperature', label: 'Temperature', missingText: 'No temperature sensor exposed for this cell', placeholder: '?t?', sensorTypes: ['temperature', 'temp'] },
  { key: 'humidity', label: 'Humidity', missingText: 'No humidity sensor exposed for this cell', placeholder: '?h?', sensorTypes: ['humidity', 'hum'] },
  { key: 'wind', label: 'Wind', missingText: 'No wind sensor exposed for this cell', placeholder: '?w?', sensorTypes: ['wind'] },
];

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

export function AreaMap({ areaId, mapType, geoJSON, cells }: MapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const tileRef = useRef<L.TileLayer | null>(null);
  const forestWmsRef = useRef<L.TileLayer.WMS | null>(null);
  const geoJSONLayerRef = useRef<L.GeoJSON | null>(null);
  const markersRef = useRef<L.CircleMarker[]>([]);
  const [cellCode, setCellCode] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);

  useEffect(() => {
    fetch('/cell_dashboards_links.txt')
      .then(response => response.text())
      .then(text => setDashboardLinks(text.split('\n').map(line => line.trim()).filter(Boolean)))
      .catch(() => setDashboardLinks([]));
  }, []);

  useEffect(() => {
    if (!containerRef.current || mapRef.current) {
      return;
    }

    const map = L.map(containerRef.current, {
      center: [39.5, -8.0],
      zoom: 6,
      zoomAnimation: false,
      fadeAnimation: false,
      markerZoomAnimation: false,
    });

    const { url, attr } = TILES[mapType];
    const tileLayer = L.tileLayer(url, { attribution: attr, crossOrigin: true });
    tileLayer.addTo(map);
    tileRef.current = tileLayer;

    const wms = L.tileLayer.wms(COPERNICUS_WMS.corine.url, {
      layers: COPERNICUS_WMS.corine.layer,
      format: 'image/png',
      transparent: true,
      opacity: 0.55,
      version: '1.3.0',
      attribution: 'Copernicus Land Monitoring Service / EEA - CORINE LC 2018',
    } as L.WMSOptions);
    wms.addTo(map);
    forestWmsRef.current = wms;
    mapRef.current = map;

    const resizeTimer = window.setTimeout(() => {
      if (mapRef.current === map) {
        map.invalidateSize();
      }
    }, 100);

    return () => {
      window.clearTimeout(resizeTimer);
      map.stop();
      map.off();
      markersRef.current.forEach(marker => marker.remove());
      markersRef.current = [];
      geoJSONLayerRef.current?.remove();
      tileRef.current?.remove();
      forestWmsRef.current?.remove();
      map.remove();
      mapRef.current = null;
      tileRef.current = null;
      forestWmsRef.current = null;
      geoJSONLayerRef.current = null;
    };
  }, [mapType]);

  useEffect(() => {
    if (!mapRef.current || !tileRef.current) {
      return;
    }

    tileRef.current.remove();
    const { url, attr } = TILES[mapType];
    tileRef.current = L.tileLayer(url, { attribution: attr }).addTo(mapRef.current);
  }, [mapType]);

  useEffect(() => {
    if (!mapRef.current || !geoJSON) {
      return;
    }

    geoJSONLayerRef.current?.remove();
    markersRef.current.forEach(marker => marker.remove());
    markersRef.current = [];

    try {
      const geoJSONLayer = L.geoJSON(geoJSON, {
        style: {
          color: '#ef4444',
          weight: 2,
          opacity: 0.8,
          fillOpacity: 0.1,
        },
      });
      geoJSONLayer.addTo(mapRef.current);
      geoJSONLayerRef.current = geoJSONLayer;

      cells
        ?.filter(cell => cell.sensorNodeCount > 0)
        .forEach(cell => {
          const cellCentroid: LatLngExpression = cell.altitudeMeters != null
            ? new LatLng(cell.centroidLatitude, cell.centroidLongitude, cell.altitudeMeters)
            : new LatLng(cell.centroidLatitude, cell.centroidLongitude);
          const marker = L.circleMarker(cellCentroid, {
            radius: 6,
            color: '#f63b3b',
            fillColor: '#f63b3b',
            fillOpacity: 0.5,
            className: `cell-marker-${cell.cellCode}`,
          }).addTo(mapRef.current!);

          marker.on('click', () => {
            setCellCode(cell.cellCode);
            setIsModalOpen(true);
          });
          markersRef.current.push(marker);
        });

      const bounds = geoJSONLayer.getBounds();
      if (bounds.isValid()) {
        mapRef.current.fitBounds(bounds, { padding: [50, 50] });
      }
    } catch (error) {
      console.warn('Error adding GeoJSON layer:', error);
    }
  }, [geoJSON, cells]);

  const dashboardItems = buildCellDashboards(areaId, cellCode, cells, dashboardLinks);

  return (
    <Box position="relative" w="100%" h="100%" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Box ref={containerRef} w="100%" h="100%" style={{ flex: 1, minHeight: 0, height: '100%' }} />

      {isModalOpen && (
        <Box
          position="fixed"
          top="0"
          left="0"
          right="0"
          bottom="0"
          bg="rgba(0, 0, 0, 0.5)"
          display="flex"
          alignItems="center"
          justifyContent="center"
          zIndex="1000"
          onClick={() => setIsModalOpen(false)}
        >
          <Box bg="white" borderRadius="md" boxShadow="lg" maxH="90vh" maxW="92vw" overflow="auto" onClick={event => event.stopPropagation()}>
            <Box display="flex" justifyContent="space-between" alignItems="center" p={6} borderBottom="1px solid #e2e8f0">
              <Box>
                <Text fontSize="lg" fontWeight="bold">Cell {cellCode} dashboards</Text>
                <Text fontSize="sm" color="#64748b">Dashboards are rendered only when a valid sensor mapping is exposed.</Text>
              </Box>
              <Button bg="transparent" onClick={() => setIsModalOpen(false)} _hover={{ bg: 'gray.100' }} p={0} minW="auto">
                <X size={24} />
              </Button>
            </Box>

            <Box p={6} overflow="auto" minW="720px" maxW="88vw">
              <Box display="grid" gridTemplateColumns="repeat(3, minmax(220px, 1fr))" gap={4}>
                {dashboardItems.map(item => (
                  <Box key={item.key} border="1px solid #e2e8f0" borderRadius="md" overflow="hidden" minH="420px" bg="#f8fafc">
                    <Box p={3} borderBottom="1px solid #e2e8f0">
                      <Text fontWeight="bold">{item.label}</Text>
                      <Text fontSize="sm" color="#64748b">Sensor: {item.sensorId || 'Not available'}</Text>
                    </Box>
                    {item.url ? (
                      <iframe
                        src={item.url}
                        width="100%"
                        height="360"
                        style={{ border: 0, display: 'block' }}
                        title={`${item.label} dashboard for ${cellCode}`}
                        loading="lazy"
                      />
                    ) : (
                      <Box p={4} h="360px" display="grid" placeItems="center">
                        <Text color="#64748b" textAlign="center">{item.message}</Text>
                      </Box>
                    )}
                  </Box>
                ))}
              </Box>
            </Box>
          </Box>
        </Box>
      )}
    </Box>
  );
}

function buildCellDashboards(areaId: string, cellCode: string, cells: MapProps['cells'], dashboardLinks: string[]) {
  const sensors = cells?.find(cell => cell.cellCode === cellCode)?.sensorNodeIds ?? [];
  if (sensors.length === 0) {
    return CELL_DASHBOARD_METRICS.map(metric => ({
      ...metric,
      sensorId: null,
      url: null,
      message: 'Sensor mapping not exposed for this cell',
    }));
  }

  return CELL_DASHBOARD_METRICS.map(metric => {
    const sensorId = resolveSensorId(sensors, metric);
    const template = dashboardLinks.find(link => link.includes(metric.placeholder));

    if (!sensorId) {
      return { ...metric, sensorId: null, url: null, message: metric.missingText };
    }

    if (!template || !areaId) {
      return { ...metric, sensorId, url: null, message: 'Grafana dashboard not configured' };
    }

    const rawUrl = template
      .replace(/\?1\?/g, encodeURIComponent(areaId))
      .replace(metric.placeholder, encodeURIComponent(sensorId));
    const url = normalizeGrafanaUrl(rawUrl);

    return url
      ? { ...metric, sensorId, url, message: null }
      : { ...metric, sensorId, url: null, message: 'Grafana dashboard not configured' };
  });
}

function resolveSensorId(sensors: SensorInfo[], metric: CellDashboardMetric) {
  const sensor = sensors.find(item => {
    const type = String(item.type ?? '').toLowerCase();
    return metric.sensorTypes.some(expected => type.includes(expected));
  });

  return sensor?.id || null;
}

function normalizeGrafanaUrl(url: string) {
  if (!url || url.includes('Enter value') || /\?t\?|\?h\?|\?w\?|\?1\?|\?\?\?/.test(url)) {
    return null;
  }

  let parsed: URL;
  try {
    parsed = new URL(url, window.location.origin);
  } catch {
    return null;
  }

  const sensorId = parsed.searchParams.get('var-sensor_id');
  if (!sensorId || sensorId.trim().length === 0) {
    return null;
  }

  if (!parsed.searchParams.has('kiosk')) {
    parsed.searchParams.set('kiosk', '');
  }

  return parsed.toString();
}
