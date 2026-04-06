from __future__ import annotations

from datetime import datetime, timezone

import pandas as pd

from geospatial_utils import load_area, load_grid, write_cells_attributes


def main() -> None:
    area = load_area()
    grid = load_grid()

    area_row = area.iloc[0]
    curated_at = datetime.now(timezone.utc).isoformat()

    frame = pd.DataFrame(
        {
            "area_id": grid["area_id"],
            "cell_id": grid["cell_id"],
            "cell_seq": grid["cell_seq"],
            "cell_size_m": grid["cell_size_m"],
            "grid_x_min": grid["grid_x_min"],
            "grid_y_min": grid["grid_y_min"],
            "grid_x_max": grid["grid_x_max"],
            "grid_y_max": grid["grid_y_max"],
            "cell_area_m2": grid["cell_area_m2"],
            "coverage_ratio": grid["coverage_ratio"],
            "centroid_x": grid["centroid_x"],
            "centroid_y": grid["centroid_y"],
            "centroid_lon": grid["centroid_lon"],
            "centroid_lat": grid["centroid_lat"],
            "municipio": area_row["municipio"],
            "distrito_ilha": area_row["distrito_ilha"],
            "nuts1": area_row["nuts1"],
            "nuts2": area_row["nuts2"],
            "nuts3": area_row["nuts3"],
            "nuts3_cod": area_row["nuts3_cod"],
            "source_boundary_dataset": area_row["source_dataset"],
            "boundary_curated_at_utc": area_row["curated_at_utc"],
            "cells_attributes_curated_at_utc": curated_at,
            "altitude_m": pd.Series([pd.NA] * len(grid), dtype="Float64"),
            "slope_deg": pd.Series([pd.NA] * len(grid), dtype="Float64"),
            "aspect_deg": pd.Series([pd.NA] * len(grid), dtype="Float64"),
            "land_cover_class": pd.Series([pd.NA] * len(grid), dtype="string"),
            "land_cover_code": pd.Series([pd.NA] * len(grid), dtype="string"),
            "land_cover_label": pd.Series([pd.NA] * len(grid), dtype="string"),
            "land_cover_source": pd.Series([pd.NA] * len(grid), dtype="string"),
            "land_cover_macroclass": pd.Series([pd.NA] * len(grid), dtype="string"),
            "land_cover_pct_dominant": pd.Series([pd.NA] * len(grid), dtype="Float64"),
            "dominant_forest_type": pd.Series([pd.NA] * len(grid), dtype="string"),
            "dominant_fuel_model": pd.Series([pd.NA] * len(grid), dtype="string"),
            "tree_cover_density": pd.Series([pd.NA] * len(grid), dtype="Float64"),
            "structural_hazard": pd.Series([pd.NA] * len(grid), dtype="string"),
            "conjunctural_hazard": pd.Series([pd.NA] * len(grid), dtype="string"),
            "attributes_status": pd.Series(["seed"] * len(grid), dtype="string"),
        }
    )

    write_cells_attributes(frame)

    print(f"Area: {area_row['municipio']}")
    print(f"Cells: {len(frame)}")
    print("Wrote seed cells_attributes parquet/csv")


if __name__ == "__main__":
    main()
