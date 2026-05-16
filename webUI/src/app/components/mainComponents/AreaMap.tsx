import { Box, Text, HStack, Button } from '@chakra-ui/react';
import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import 'leaflet/dist/leaflet.css';
import { MapProps } from '../../types';
import { Layers, Mountain } from 'lucide-react';
import L, { LatLng, LatLngExpression, map, marker } from 'leaflet';
import { TILES } from '../../utils/utils';

const COPERNICUS_WMS = {
  hrlForest: {
    url: 'https://image.discomap.eea.europa.eu/arcgis/services/ForestHRL/HRL_TCD_2018/ImageServer/WMSServer',
    layer: '0',
    label: 'HRL Forest TCD 2018',
  },
  corine: {
    url: 'https://image.discomap.eea.europa.eu/arcgis/services/Corine/CLC2018_WM/MapServer/WmsServer',
    layer: '1',
    label: 'CORINE LC 2018',
  },
};

// ─── Leaflet icon fix ──────────────────────────────────────────────────────
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

  const [cellCode, setCellCode] = useState<string>("");
  const [isModalOpen, setIsModalOpen] = useState(false);

  const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);
  useEffect(() => {
    fetch('/cell_dashboards_links.txt')
      .then(r => r.text())
      .then(text => {
        const links = text.split('\n').filter(line => line.trim());
        setDashboardLinks(links);
      })
      .catch(err => console.error('Failed to load dashboards:', err));
  }, []);

  // Initialize map once
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const map = L.map(containerRef.current, {
      center: [39.5, -8.0],
      zoom: 6,
    });

    const { url, attr } = TILES[mapType];

    const tileLayer = L.tileLayer(url, {
      attribution: attr,
      crossOrigin: true,
    });

    tileLayer.on('load', () => console.log('✅ Tiles loaded successfully'));
    tileLayer.on('tileload', () => console.log('📍 Tile loaded'));
    tileLayer.on('tileerror', (error) => console.error('❌ Tile error:', error));
    tileLayer.on('error', (error) => console.error('❌ TileLayer error:', error));

    tileLayer.addTo(map);
    tileRef.current = tileLayer;

    //Note: Skipping Copernicus WMS for now to debug base tiles
    const wms = L.tileLayer.wms(COPERNICUS_WMS.corine.url, {
      layers: COPERNICUS_WMS.corine.layer,
      format: 'image/png',
      transparent: true,
      opacity: 0.55,
      version: '1.3.0',
      attribution: '© <a href="https://land.copernicus.eu/" target="_blank">Copernicus Land Monitoring Service</a> / EEA – CORINE LC 2018',
    } as L.WMSOptions);

    wms.on('error', (error) => console.error('WMS loading error:', error));
    wms.addTo(map);
    forestWmsRef.current = wms;

    mapRef.current = map;

    // Force map to resize and redraw
    setTimeout(() => {
      map.invalidateSize();
    }, 100);

    return () => {
      map.remove();
      mapRef.current = null;
      tileRef.current = null;
      forestWmsRef.current = null;
      geoJSONLayerRef.current = null;
    };
  }, [mapType]);

  // Update tile layer when mapType changes
  useEffect(() => {
    if (!mapRef.current || !tileRef.current) return;
    tileRef.current.remove();
    const { url, attr } = TILES[mapType];
    tileRef.current = L.tileLayer(url, { attribution: attr }).addTo(mapRef.current);
  }, [mapType]);

  // Add GeoJSON layer when data is available
  useEffect(() => {
    if (!mapRef.current || !geoJSON) return;

    // Remove existing GeoJSON layer if it exists
    if (geoJSONLayerRef.current) {
      mapRef.current.removeLayer(geoJSONLayerRef.current);
    }

    try {
      // Add new GeoJSON layer
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

      // Fit map bounds to GeoJSON
      const bounds = geoJSONLayer.getBounds();

      if (cells) {
        cells.filter(cell => cell.sensorNodeCount > 0)
        .forEach(cell => {
          let cellCentroid: LatLngExpression;
          if (cell.altitudeMeters != undefined) {
            cellCentroid = new LatLng(cell.centroidLatitude, cell.centroidLongitude, cell.altitudeMeters) as LatLngExpression;
          } else {
            cellCentroid = new LatLng(cell.centroidLatitude, cell.centroidLongitude) as LatLngExpression;
          }
          const marker = L.circleMarker(cellCentroid, {
            radius: 6,
            color: '#f63b3b',
            fillColor: '#f63b3b',
            fillOpacity: 0.5,
            className: 'cell-marker' + (cell.cellCode),
          }).addTo(mapRef.current);

          marker.on('click', () => {
            setCellCode(cell.cellCode);
            setIsModalOpen(true);
          });

        });
      }

      if (bounds.isValid()) {
        mapRef.current.fitBounds(bounds, { padding: [50, 50] });
      }
    } catch (error) {
      console.error('Error adding GeoJSON layer:', error);
    }
  }, [geoJSON, cells]);

  const changeDashes = (areaId: string, cellCode: string) => {
    const sensors = cells.find(c => c.cellCode === cellCode)?.sensorNodeIds || [];
    return dashboardLinks.map(link => {
      if (typeof link === 'string') {
        const updatedLink = link
          .split('&')
          .map(part => {
            
            if (part.includes('?1?')) return part.replace(/\?1\?/g, areaId);
            if (part.includes('?w?')) {
              return part.replace(/\?w\?/g, sensors.find(sensor => sensor.type === 'Wind')?.id || '');
            }
            if (part.includes('?h?')) {
              return part.replace(/\?h\?/g, sensors.find(sensor => sensor.type === 'Humidity')?.id || '');
            }
            if (part.includes('?t?')) {
              return part.replace(/\?t\?/g, sensors.find(sensor => sensor.type === 'Temperature')?.id || '');
            }
            return part;
          })
          .join('&') + '&kiosk';
        link = updatedLink;
      }
      return link;
    });
  };

  return (
    <Box position="relative" w="100%" h="100%" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Box
        ref={containerRef}
        w="100%"
        h="100%"
        style={{ flex: 1, minHeight: 0, height: '100%' }}
      />

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
          <Box
            bg="white"
            borderRadius="md"
            boxShadow="lg"
            maxH="90vh"
            maxW="90vw"
            overflow="auto"
            onClick={(e) => e.stopPropagation()}
          >
            <Box display="flex" justifyContent="space-between" alignItems="center" p={6} borderBottom="1px solid #e2e8f0">
              <Text fontSize="lg" fontWeight="bold">Cell {cellCode} Dashboards</Text>
              <Button
                bg="transparent"
                onClick={() => setIsModalOpen(false)}
                _hover={{ bg: 'gray.100' }}
                p={0}
                minW="auto"
              >
                <X size={24} />
              </Button>
            </Box>

            <Box p={6} h="600px" resize="both" overflow="auto" minW="600px" minH="600px">
              <HStack spacing={4} align="stretch" overflow="auto" pb={4} h="100%" w="100%">
                {changeDashes(areaId, cellCode).map((dash, index) => {
                  return (
                    < Box
                      key={index}
                      flex="1"
                      minW="200px"
                      h="100%"
                      borderRadius="md"
                      overflow="hidden"
                      border="1px solid #e2e8f0"
                    >
                      <iframe
                        src={dash}
                        width="100%"
                        height="100%"
                        display="flex"
                        style={{ border: 0, display: 'block', height: '100%' }}
                        title={`Dashboard ${index}`}
                        loading="lazy"
                      ></iframe>
                    </Box>
                  )
                })
                }
              </HStack>
            </Box>
          </Box>
        </Box >
      )
      }
    </Box >
  );
}
