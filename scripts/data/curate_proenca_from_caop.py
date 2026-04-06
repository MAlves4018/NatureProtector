from __future__ import annotations

import math
import unicodedata
import zipfile
from datetime import datetime, timezone
from pathlib import Path

import geopandas as gpd
import pandas as pd
from shapely.geometry import box


ROOT = Path(__file__).resolve().parents[2]
RAW_CAOP_DIR = ROOT / "data" / "external" / "dgt" / "caop2025"
RAW_CAOP_ZIP = RAW_CAOP_DIR / "CAOP_Continente_2025-gpkg.zip"
RAW_CAOP_GPKG = RAW_CAOP_DIR / "Continente_CAOP2025.gpkg"
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"

TARGET_LAYER = "cont_municipios"
TARGET_MUNICIPALITY = "Proença-a-Nova"
TARGET_DISTRICT = "Castelo Branco"
CANONICAL_CRS = 3763
EXPORT_CRS = 4326
CELL_SIZE_METERS = 1000


def normalize_text(value: object) -> str:
    text = "" if value is None else str(value)
    text = unicodedata.normalize("NFKD", text)
    text = "".join(char for char in text if not unicodedata.combining(char))
    return text.casefold().strip()


def ensure_caop_gpkg() -> Path:
    if RAW_CAOP_GPKG.exists():
        return RAW_CAOP_GPKG

    if not RAW_CAOP_ZIP.exists():
        raise FileNotFoundError(f"CAOP zip not found: {RAW_CAOP_ZIP}")

    with zipfile.ZipFile(RAW_CAOP_ZIP) as archive:
        archive.extractall(RAW_CAOP_DIR)

    if not RAW_CAOP_GPKG.exists():
        raise FileNotFoundError(f"Extracted CAOP geopackage not found: {RAW_CAOP_GPKG}")

    return RAW_CAOP_GPKG


def load_target_area(gpkg_path: Path) -> gpd.GeoDataFrame:
    municipalities = gpd.read_file(gpkg_path, layer=TARGET_LAYER)
    municipalities["municipio_norm"] = municipalities["municipio"].map(normalize_text)
    municipalities["distrito_norm"] = municipalities["distrito_ilha"].map(normalize_text)

    municipio_norm = normalize_text(TARGET_MUNICIPALITY)
    distrito_norm = normalize_text(TARGET_DISTRICT)

    selected = municipalities[
        (municipalities["municipio_norm"] == municipio_norm)
        & (municipalities["distrito_norm"] == distrito_norm)
    ].copy()

    if selected.empty:
        raise ValueError(
            f"Municipality '{TARGET_MUNICIPALITY}' in district '{TARGET_DISTRICT}' not found in {gpkg_path}"
        )

    if len(selected) > 1:
        selected = selected.dissolve()
        selected = selected.reset_index(drop=True)

    if "dtmn" in municipalities.columns and "dtmn" not in selected.columns:
        dtmn_values = municipalities.loc[
            (municipalities["municipio_norm"] == municipio_norm)
            & (municipalities["distrito_norm"] == distrito_norm),
            "dtmn",
        ].dropna()
        if not dtmn_values.empty:
            selected["dtmn"] = dtmn_values.iloc[0]

    selected["area_id"] = "proenca-a-nova"
    selected["municipio"] = TARGET_MUNICIPALITY
    selected["distrito_ilha"] = TARGET_DISTRICT
    selected["source_layer"] = TARGET_LAYER
    selected["source_dataset"] = "CAOP_Continente_2025"
    selected["curated_at_utc"] = datetime.now(timezone.utc).isoformat()
    selected = selected.to_crs(CANONICAL_CRS)
    return selected


def create_grid(area_gdf: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    area_union = area_gdf.geometry.union_all()
    minx, miny, maxx, maxy = area_union.bounds

    start_x = math.floor(minx / CELL_SIZE_METERS) * CELL_SIZE_METERS
    start_y = math.floor(miny / CELL_SIZE_METERS) * CELL_SIZE_METERS
    end_x = math.ceil(maxx / CELL_SIZE_METERS) * CELL_SIZE_METERS
    end_y = math.ceil(maxy / CELL_SIZE_METERS) * CELL_SIZE_METERS

    records: list[dict[str, object]] = []

    for x in range(int(start_x), int(end_x), CELL_SIZE_METERS):
        for y in range(int(start_y), int(end_y), CELL_SIZE_METERS):
            candidate = box(x, y, x + CELL_SIZE_METERS, y + CELL_SIZE_METERS)
            if not candidate.intersects(area_union):
                continue

            clipped = candidate.intersection(area_union)
            if clipped.is_empty:
                continue

            records.append(
                {
                    "grid_x_min": x,
                    "grid_y_min": y,
                    "grid_x_max": x + CELL_SIZE_METERS,
                    "grid_y_max": y + CELL_SIZE_METERS,
                    "cell_area_m2": clipped.area,
                    "coverage_ratio": clipped.area / float(CELL_SIZE_METERS * CELL_SIZE_METERS),
                    "geometry": clipped,
                }
            )

    grid = gpd.GeoDataFrame(records, geometry="geometry", crs=CANONICAL_CRS)
    grid = grid.sort_values(["grid_y_min", "grid_x_min"], ascending=[False, True]).reset_index(drop=True)
    grid["cell_seq"] = pd.RangeIndex(start=1, stop=len(grid) + 1)
    grid["cell_id"] = grid["cell_seq"].map(lambda value: f"proenca-a-nova-{value:04d}")
    centroids = grid.geometry.centroid
    grid["centroid_x"] = centroids.x
    grid["centroid_y"] = centroids.y

    centroids_geo = gpd.GeoSeries(centroids, crs=CANONICAL_CRS).to_crs(EXPORT_CRS)
    grid["centroid_lon"] = centroids_geo.x
    grid["centroid_lat"] = centroids_geo.y
    grid["area_id"] = "proenca-a-nova"
    grid["cell_size_m"] = CELL_SIZE_METERS
    return grid


def write_outputs(area_gdf: gpd.GeoDataFrame, grid_gdf: gpd.GeoDataFrame) -> None:
    BASELINE_DIR.mkdir(parents=True, exist_ok=True)

    area_metric_path = BASELINE_DIR / "area.gpkg"
    area_geojson_path = BASELINE_DIR / "area.geojson"
    grid_metric_path = BASELINE_DIR / "grid_1km.gpkg"
    grid_geojson_path = BASELINE_DIR / "grid_1km.geojson"

    area_gdf.to_file(area_metric_path, layer="area", driver="GPKG")
    area_gdf.to_crs(EXPORT_CRS).to_file(area_geojson_path, driver="GeoJSON")

    grid_gdf.to_file(grid_metric_path, layer="grid_1km", driver="GPKG")
    grid_gdf.to_crs(EXPORT_CRS).to_file(grid_geojson_path, driver="GeoJSON")


def main() -> None:
    gpkg_path = ensure_caop_gpkg()
    area_gdf = load_target_area(gpkg_path)
    grid_gdf = create_grid(area_gdf)
    write_outputs(area_gdf, grid_gdf)

    print(f"CAOP source: {gpkg_path}")
    print(f"Area features: {len(area_gdf)}")
    print(f"Grid cells: {len(grid_gdf)}")
    print(f"Wrote: {BASELINE_DIR / 'area.gpkg'}")
    print(f"Wrote: {BASELINE_DIR / 'grid_1km.gpkg'}")


if __name__ == "__main__":
    main()
