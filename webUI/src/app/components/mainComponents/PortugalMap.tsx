import { useEffect, useRef } from 'react';
import {
  Box, HStack, Text
} from '@chakra-ui/react';
import {
  Layers, Mountain,
} from 'lucide-react';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';

export function PortugalMap({ zones, showGrid, showColorByDanger, showForests, mapType, isDark }: MapProps) {
  const containerRef  = useRef<HTMLDivElement>(null);
  const mapRef        = useRef<L.Map | null>(null);
  const tileRef       = useRef<L.TileLayer | null>(null);
  const dangerRef     = useRef<L.LayerGroup | null>(null);
  const forestRef     = useRef<L.LayerGroup | null>(null);
  const forestWmsRef  = useRef<L.TileLayer.WMS | null>(null);

  // Init once
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = L.map(containerRef.current, { center: [39.5, -8.0], zoom: 6 });
    const { url, attr } = TILES[mapType];
    tileRef.current = L.tileLayer(url, { attribution: attr }).addTo(map);
    forestRef.current  = L.layerGroup().addTo(map);
    dangerRef.current  = L.layerGroup().addTo(map);
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null; tileRef.current = null;
      dangerRef.current = null; forestRef.current = null;
      forestWmsRef.current = null;
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Swap tile layer when mapType changes
  useEffect(() => {
    if (!mapRef.current || !tileRef.current) return;
    tileRef.current.remove();
    const { url, attr } = TILES[mapType];
    tileRef.current = L.tileLayer(url, { attribution: attr }).addTo(mapRef.current);
  }, [mapType]);
  
  
  // Update danger circles
  useEffect(() => {
    if (!dangerRef.current) return;
    dangerRef.current.clearLayers();
    zones.forEach((zone) => {
      const col = showColorByDanger ? getDangerColor(zone.danger) : '#6b7280';
      L.circleMarker([zone.lat, zone.lng], {
        radius: 18, color: col, fillColor: col, fillOpacity: 0.45, weight: 2,
      })
      .bindPopup(`
        <div style="font-size:13px;min-width:130px;font-family:sans-serif;">
          <p style="font-weight:700;margin:0 0 5px;">${zone.name}</p>
          <div style="display:flex;align-items:center;gap:6px;margin-bottom:4px;">
            <span style="width:10px;height:10px;border-radius:50%;background:${col};display:inline-block;flex-shrink:0;"></span>
            <span style="color:#555;">${getDangerLabel(zone.danger)}</span>
          </div>
          <p style="color:#777;margin:0;">Índice: <strong>${zone.danger}/100</strong></p>
        </div>`)
      .addTo(dangerRef.current!);
    });
  }, [zones, showColorByDanger]);

  // Forest layer — Copernicus WMS (EEA DiscoMap) + named reference polygons
  useEffect(() => {
    if (!forestRef.current || !mapRef.current) return;

    // Remove existing Copernicus WMS
    if (forestWmsRef.current) {
      forestWmsRef.current.remove();
      forestWmsRef.current = null;
    }
    forestRef.current.clearLayers();

    if (!showForests) return;

    // ── Layer 1: Copernicus CORINE Land Cover 2018 via EEA WMS ──────────────
    // Shows real satellite-derived land cover (forest classes 311–313 in green)
    // Service: https://land.copernicus.eu/ hosted on EEA DiscoMap
    const wms = L.tileLayer.wms(
      COPERNICUS_WMS.corine.url,
      {
        layers: COPERNICUS_WMS.corine.layer,
        format: 'image/png',
        transparent: true,
        opacity: 0.55,
        version: '1.3.0',
        attribution: '© <a href="https://land.copernicus.eu/" target="_blank">Copernicus Land Monitoring Service</a> / EEA – CORINE LC 2018',
      } as L.WMSOptions
    );
    wms.addTo(mapRef.current);
    forestWmsRef.current = wms;

    // ── Layer 2: Named reference polygons (dashed outline, low fill) ────────
    // Identifies the major named forest / natural park areas
    forestAreas.forEach((area) => {
      L.polygon(area.coords, {
        color: '#166534',
        fillColor: '#16a34a',
        fillOpacity: 0.08,
        weight: 1.8,
        dashArray: '5 4',
      })
      .bindPopup(`
        <div style="font-size:13px;min-width:155px;font-family:sans-serif;">
          <div style="display:flex;align-items:center;gap:6px;margin-bottom:5px;">
            <span style="font-size:17px;">🌲</span>
            <p style="font-weight:700;margin:0;">${area.name}</p>
          </div>
          <p style="color:#555;margin:0 0 4px;font-size:12px;">${area.type}</p>
          <div style="border-top:1px solid #e5e7eb;margin-top:6px;padding-top:5px;display:flex;align-items:center;gap:4px;">
            <img src="https://land.copernicus.eu/favicon.ico" width="12" height="12" style="border-radius:2px;" onerror="this.style.display='none'"/>
            <span style="font-size:10px;color:#888;">Copernicus CORINE LC 2018</span>
          </div>
        </div>`)
      .addTo(forestRef.current!);
    });
  }, [showForests]);

  return (
    <Box position="relative" w="100%" h="100%">
      <Box ref={containerRef} w="100%" h="100%" />
      {showGrid && (
        <Box
          position="absolute" inset={0} pointerEvents="none"
          style={{
            zIndex: 500,
            backgroundImage: 'linear-gradient(rgba(100,100,100,0.15) 1px, transparent 1px), linear-gradient(90deg, rgba(100,100,100,0.15) 1px, transparent 1px)',
            backgroundSize: '60px 60px',
          }}
        />
      )}
      {/* Info badge */}
      <Box
        position="absolute" top={4} right={4}
        bg="rgba(255,255,255,0.93)" backdropFilter="blur(6px)"
        borderRadius="lg" shadow="md" border="1px solid #e5e7eb"
        px={3} py={2} style={{ zIndex: 1000 }}
      >
        <Text fontSize="xs" color="gray.500">Portugal</Text>
        <Text fontSize="sm" fontWeight="semibold" color="gray.800">Risco de Incêndio</Text>
        <Text fontSize="xs" color="gray.400">Atualizado hoje</Text>
      </Box>

      {/* Map type badge */}
      <Box
        position="absolute" bottom={8} right={4}
        bg="rgba(255,255,255,0.93)" backdropFilter="blur(6px)"
        borderRadius="md" shadow="sm" border="1px solid #e5e7eb"
        px={2} py={1} style={{ zIndex: 1000 }}
      >
        <HStack gap={1}>
          {mapType === 'terrain'
            ? <><Mountain size={12} color="#78716c" /><Text fontSize="xs" color="gray.500">DEM / Terreno</Text></>
            : <><Layers size={12} color="#78716c" /><Text fontSize="xs" color="gray.500">Standard</Text></>
          }
        </HStack>
      </Box>
    </Box>
  );
}


// ─── Tile layer URLs ───────────────────────────────────────────────────────────
const TILES = {
  standard: {
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attr: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
  },
  terrain: {
    url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
    attr: '&copy; <a href="https://opentopomap.org">OpenTopoMap</a> contributors',
  },
};
type MapType = 'standard' | 'terrain';

// ─── Portugal Map ──────────────────────────────────────────────────────────────
interface MapProps {
  zones: Zone[];
  showGrid: boolean;
  showColorByDanger: boolean;
  showForests: boolean;
  mapType: MapType;
  isDark: boolean;
}

// ─── Leaflet icon fix ──────────────────────────────────────────────────────────
delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl:       'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl:     'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// ─── Copernicus WMS endpoints (EEA DiscoMap — publicly accessible) ─────────────
// Primary:  HRL Forest 2018 (Tree Cover Density) — forest pixels only
// Fallback: CORINE Land Cover 2018 — full land-use map incl. forest classes
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

function getDangerColor(d: number) { return d<=60?'#22c55e':d<=75?'#f59e0b':'#ef4444'; }
function getDangerLabel(d: number) { return d<=60?'Baixo risco':d<=75?'Risco moderado':'Perigo elevado'; }