from __future__ import annotations

import json
from pathlib import Path

import geopandas as gpd
import pandas as pd
from pyproj import Geod
from shapely.geometry import shape


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
AREA_GPKG = BASELINE_DIR / "area.gpkg"
IPMA_STATIONS_JSON = ROOT / "data" / "external" / "ipma" / "api-samples" / "stations.json"
IPMA_NEARBY_STATIONS_CSV = BASELINE_DIR / "ipma_nearby_stations.csv"


def main() -> None:
    if not AREA_GPKG.exists():
        raise FileNotFoundError(f"Area geopackage not found: {AREA_GPKG}")
    if not IPMA_STATIONS_JSON.exists():
        raise FileNotFoundError(f"IPMA stations json not found: {IPMA_STATIONS_JSON}")

    area = gpd.read_file(AREA_GPKG, layer="area").to_crs(4326)
    centroid = area.geometry.union_all().centroid
    geod = Geod(ellps="WGS84")

    payload = json.loads(IPMA_STATIONS_JSON.read_text(encoding="utf-8"))
    records: list[dict[str, object]] = []

    for feature in payload:
        geom = shape(feature["geometry"])
        props = feature["properties"]
        _, _, distance_m = geod.inv(centroid.x, centroid.y, geom.x, geom.y)
        records.append(
            {
                "area_id": "proenca-a-nova",
                "station_id": props["idEstacao"],
                "station_name": props["localEstacao"],
                "station_lon": geom.x,
                "station_lat": geom.y,
                "distance_km": distance_m / 1000.0,
            }
        )

    frame = pd.DataFrame(records).sort_values(["distance_km", "station_name"]).reset_index(drop=True)
    frame.to_csv(IPMA_NEARBY_STATIONS_CSV, index=False)

    print(f"Area centroid: {centroid.y:.6f}, {centroid.x:.6f}")
    print(f"Wrote: {IPMA_NEARBY_STATIONS_CSV}")
    print(frame.head(10).to_string(index=False))


if __name__ == "__main__":
    main()
