from __future__ import annotations

from pathlib import Path

import geopandas as gpd
import pandas as pd

from geospatial_utils import (
    BASELINE_DIR,
    load_area,
    load_cells_attributes,
    load_grid,
    write_cells_attributes,
)


ROOT = Path(__file__).resolve().parents[2]
COS_GPKG = ROOT / "data" / "external" / "dgt" / "cos2018" / "COS2018v3-S2.gpkg"
COS_LAYER = "COS2018v3"

N1_MACROCLASS = {
    "1": "areas_artificiais",
    "2": "agricultura",
    "3": "pastagens",
    "4": "superficies_agrossilvicolas",
    "5": "florestas",
    "6": "matos",
    "7": "espacos_descobertos_ou_pouca_vegetacao",
    "8": "zonas_humidas",
    "9": "corpos_de_agua",
}


def ensure_land_cover_columns(frame):
    defaults = {
        "land_cover_class": "string",
        "land_cover_code": "string",
        "land_cover_label": "string",
        "land_cover_source": "string",
        "land_cover_macroclass": "string",
        "land_cover_pct_dominant": "Float64",
    }
    for column, dtype in defaults.items():
        if column not in frame.columns:
            frame[column] = pd.Series([pd.NA] * len(frame), dtype=dtype)
    return frame


def main() -> None:
    if not COS_GPKG.exists():
        raise FileNotFoundError(f"COS GeoPackage not found: {COS_GPKG}")

    area = load_area()
    grid = load_grid()[["cell_id", "cell_area_m2", "geometry"]].copy()
    attributes = load_cells_attributes()
    attributes = ensure_land_cover_columns(attributes)

    # Read only polygons that intersect the pilot area to keep the overlay bounded.
    cos = gpd.read_file(
        COS_GPKG,
        layer=COS_LAYER,
        columns=["COS18_n4_C", "COS18_n4_L"],
        mask=area.geometry,
    )

    cos = cos.to_crs(grid.crs)
    overlay = gpd.overlay(grid, cos, how="intersection", keep_geom_type=False)
    overlay["intersection_area_m2"] = overlay.geometry.area

    grouped = (
        overlay.groupby(["cell_id", "COS18_n4_C", "COS18_n4_L"], as_index=False)["intersection_area_m2"]
        .sum()
        .sort_values(["cell_id", "intersection_area_m2"], ascending=[True, False])
    )
    dominant = grouped.drop_duplicates(subset=["cell_id"], keep="first").copy()
    dominant["land_cover_macroclass"] = dominant["COS18_n4_C"].str.split(".").str[0].map(N1_MACROCLASS)
    dominant["land_cover_pct_dominant"] = dominant["intersection_area_m2"] / grid.set_index("cell_id").loc[
        dominant["cell_id"], "cell_area_m2"
    ].to_numpy() * 100.0

    merged = attributes.merge(
        dominant[
            [
                "cell_id",
                "COS18_n4_C",
                "COS18_n4_L",
                "land_cover_macroclass",
                "land_cover_pct_dominant",
            ]
        ],
        on="cell_id",
        how="left",
        suffixes=("", "_dominant"),
    )

    merged["land_cover_code"] = merged["COS18_n4_C"].astype("string")
    merged["land_cover_label"] = merged["COS18_n4_L"].astype("string")
    merged["land_cover_class"] = merged["land_cover_label"]
    merged["land_cover_source"] = "COS2018v3-S2"
    merged["land_cover_macroclass"] = merged["land_cover_macroclass_dominant"].astype("string")
    merged["land_cover_pct_dominant"] = merged["land_cover_pct_dominant_dominant"].astype("Float64")
    merged["attributes_status"] = "partial"

    merged = merged.drop(
        columns=[
            "COS18_n4_C",
            "COS18_n4_L",
            "land_cover_macroclass_dominant",
            "land_cover_pct_dominant_dominant",
        ]
    )
    write_cells_attributes(merged)

    print(f"Wrote land cover enrichment using {COS_GPKG.name}")
    print(f"Updated rows: {len(merged)}")
    print(f"Outputs: {BASELINE_DIR / 'cells_attributes.parquet'}")


if __name__ == "__main__":
    main()
