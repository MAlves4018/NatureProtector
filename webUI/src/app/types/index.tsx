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

type LatLng = [number, number];
// ─── Areas  ─────────────────────
export interface NPArea { id: number; name: string; type: string; coords: LatLng[] }


// ─── Grid Centers ──────────────────────────────────────────────────────────────
export interface GridInfo { id: number; area_id: number; coords: LatLng[]}
