export interface AreaResponse {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  configurationVersionNumber: number;
  gridCellCount: number;
  sensorNodeCount: number;
  scenarioCount: number;
}

export interface AreaGeoJSONResponse {
  id: string;
  geometryGeoJson: string | null;
}

export type MapType = 'standard' | 'terrain';

export interface MapProps {
  areaId: string;
  showGrid: boolean;
  showColorByDanger?: boolean;
  mapType: MapType;
  isDark?: boolean;
  geoJSON?: any;
  cells?: AreaCellResponse[];
  sensorNodes?: SensorNodeResponse[];
}

export type SensorInfo =
  | string
  | {
      id?: string;
      code?: string;
      name?: string;
      type?: string;
      metric?: string;
      sensorType?: string;
      sensorId?: string;
      sensorName?: string;
      item1?: string;
      item2?: string;
    };
export interface AreaCellResponse {
  cellCode: string;
  sensorNodeIds: SensorInfo[];
  configurationVersionNumber: number;
  centroidLatitude: number;
  centroidLongitude: number;
  altitudeMeters: number | null;
  slopeDegrees: number | null;
  aspectDegrees: number | null;
  landCoverClass: string | null;
  dominantForestType: string | null;
  dominantFuelModel: string | null;
  treeCoverDensity: number | null;
  structuralHazard: string | null;
  conjuncturalHazard: string | null;
  sensorNodeCount: number;
}

export interface SensorNodeResponse {
  id: string;
  name: string;
  type: string;
  configurationVersionNumber: number;
  cellCode: string;
  profileName: string;
  sensorFamily: string | null;
  networkName: string | null;
  latitude: number;
  longitude: number;
  altitudeMeters: number | null;
  isActive: boolean;
  installationProfile: string | null;
}

type LatLng = [number, number];
export interface NPArea {
  id: number;
  name: string;
  type: string;
  coords: LatLng[];
}

// ─── Grid Centers ──────────────────────────────────────────────────────────────
export interface GridInfo {
  id: number;
  area_id: number;
  coords: LatLng[];
}
