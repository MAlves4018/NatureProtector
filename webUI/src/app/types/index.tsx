export interface ErrorResponse {
    title: string,
    status: number,
    message: string,
}

export interface AreaResponse {
    id: string,
    code: string,
    name: string,
    countryCode: string,
    configurationVersionNumber: number,
    gridCellCount: number,
    sensorNodeCount: number,
    scenarioCount: number
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
}

export interface SensorInfo { 
    id: string; 
    type: string;
}
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


type LatLng = [number, number];
// ─── Areas  ─────────────────────
export interface NPArea { id: number; name: string; type: string; coords: LatLng[] }


// ─── Grid Centers ──────────────────────────────────────────────────────────────
export interface GridInfo { id: number; area_id: number; coords: LatLng[] }
