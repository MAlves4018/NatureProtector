from __future__ import annotations

from pathlib import Path

import geopandas as gpd
import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
AREA_GPKG = BASELINE_DIR / "area.gpkg"
FIRE_HISTORY_PARQUET = BASELINE_DIR / "fire_history.parquet"
FIRE_HISTORY_CSV = BASELINE_DIR / "fire_history.csv"
ICNF_GEOCATALOG_DIR = ROOT / "data" / "external" / "icnf" / "geocatalog"


def build_records_from_shapefile(shapefile: Path, area: gpd.GeoDataFrame) -> pd.DataFrame:
    year = shapefile.stem.split("_")[-1]
    source_dataset = f"ICNF_ardida_{year}"

    burned = gpd.read_file(shapefile)
    if burned.empty:
        return pd.DataFrame()

    if burned.crs != area.crs:
        burned = burned.to_crs(area.crs)

    clipped = gpd.overlay(burned, area[["geometry"]], how="intersection")
    if clipped.empty:
        return pd.DataFrame()

    clipped["intersected_area_ha"] = clipped.geometry.area / 10000.0
    clipped["start_date"] = pd.to_datetime(clipped["DH_Inicio"], errors="coerce")
    clipped["end_date"] = pd.to_datetime(clipped.get("DH_Fim"), errors="coerce")
    clipped["incident_id"] = clipped.get("Cod_SGIF", pd.Series([pd.NA] * len(clipped))).astype("string")
    clipped["source_municipality"] = clipped.get("PI_Conc", pd.Series([pd.NA] * len(clipped))).astype("string")

    clipped = clipped.reset_index(drop=True)
    clipped["source_fire_name"] = clipped.apply(
        lambda row: (
            f"ICNF_{year}_{row['incident_id']}"
            if pd.notna(row["incident_id"])
            else f"ICNF_{year}_{row.name + 1:03d}"
        ),
        axis=1,
    )

    notes = (
        "Area ardida ICNF intersectada com Proenca-a-Nova; "
        + "extent_ha corresponde a area recortada ao concelho."
    )

    return pd.DataFrame(
        {
            "area_id": "proenca-a-nova",
            "source_dataset": source_dataset,
            "source_fire_name": clipped["source_fire_name"],
            "source_municipality": clipped["source_municipality"],
            "start_date": clipped["start_date"],
            "end_date": clipped["end_date"],
            "extent_ha": clipped["intersected_area_ha"].round(6),
            "confidence_flag": pd.Series([pd.NA] * len(clipped), dtype="Int64"),
            "incident_id": clipped["incident_id"],
            "burned_area_context_flag": True,
            "hotspot_flag": pd.Series([pd.NA] * len(clipped), dtype="boolean"),
            "pt_firesprd_flag": False,
            "proximity_basis": "municipality_intersection",
            "history_kind": "icnf_burned_area_intersection",
            "notes": notes,
        }
    )


def main() -> None:
    if not AREA_GPKG.exists():
        raise FileNotFoundError(f"Area geopackage not found: {AREA_GPKG}")
    if not FIRE_HISTORY_PARQUET.exists():
        raise FileNotFoundError(f"Fire history parquet not found: {FIRE_HISTORY_PARQUET}")

    area = gpd.read_file(AREA_GPKG, layer="area").to_crs(3763)
    shapefiles = sorted(ICNF_GEOCATALOG_DIR.glob("ardida_*\\ardida_*.shp"))

    if not shapefiles:
        raise FileNotFoundError(f"No downloaded ICNF burned-area shapefiles found under: {ICNF_GEOCATALOG_DIR}")

    generated_frames: list[pd.DataFrame] = []
    for shapefile in shapefiles:
        records = build_records_from_shapefile(shapefile, area)
        if not records.empty:
            generated_frames.append(records)

    generated = pd.concat(generated_frames, ignore_index=True) if generated_frames else pd.DataFrame()
    existing = pd.read_parquet(FIRE_HISTORY_PARQUET)
    existing = existing[~existing["source_dataset"].astype("string").str.startswith("ICNF_ardida_", na=False)].copy()

    combined = pd.concat([existing, generated], ignore_index=True)
    combined = combined.sort_values(["start_date", "source_dataset", "source_fire_name"]).reset_index(drop=True)
    combined.to_parquet(FIRE_HISTORY_PARQUET, index=False)
    combined.to_csv(FIRE_HISTORY_CSV, index=False)

    print(f"Downloaded burned-area shapefiles found: {len(shapefiles)}")
    print(f"New ICNF records added: {len(generated)}")
    if not generated.empty:
        print(generated["source_dataset"].value_counts().to_string())
    print(f"Wrote: {FIRE_HISTORY_PARQUET}")
    print(f"Wrote: {FIRE_HISTORY_CSV}")


if __name__ == "__main__":
    main()
