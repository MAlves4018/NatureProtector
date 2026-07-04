from __future__ import annotations

from pathlib import Path

import numpy as np
import rasterio
from rasterio.mask import mask

from geospatial_utils import load_cells_attributes, load_grid, write_cells_attributes


ROOT = Path(__file__).resolve().parents[2]
HAZARD_RASTER = (
    ROOT
    / "data"
    / "external"
    / "icnf"
    / "geocatalog"
    / "perigosidade_estrutural_2020_2030"
    / "perigosidade_estrutural_2020_2030.tif"
)

HAZARD_LABELS = {
    1: "muito_baixa",
    2: "baixa",
    3: "media",
    4: "alta",
    5: "muito_alta",
}


def compute_mode(values: np.ndarray) -> int | None:
    if values.size == 0:
        return None

    unique, counts = np.unique(values.astype(np.int64), return_counts=True)
    return int(unique[np.argmax(counts)])


def main() -> None:
    if not HAZARD_RASTER.exists():
        raise FileNotFoundError(f"Structural hazard raster not found: {HAZARD_RASTER}")

    grid = load_grid()[["cell_id", "geometry"]].copy()
    attributes = load_cells_attributes()

    results: list[tuple[str, int | None]] = []

    with rasterio.open(HAZARD_RASTER) as dataset:
        grid = grid.to_crs(dataset.crs)

        for row in grid.itertuples(index=False):
            clipped, _ = mask(dataset, [row.geometry.__geo_interface__], crop=True, indexes=1)
            if np.ma.is_masked(clipped):
                valid = clipped.compressed()
            else:
                valid = clipped.reshape(-1)

            if dataset.nodata is not None:
                valid = valid[valid != dataset.nodata]

            valid = valid[~np.isnan(valid)] if valid.size else valid
            results.append((row.cell_id, compute_mode(valid)))

    codes = {cell_id: value for cell_id, value in results}
    attributes["structural_hazard_code"] = attributes["cell_id"].map(codes).astype("Int64")
    attributes["structural_hazard"] = (
        attributes["structural_hazard_code"].map(HAZARD_LABELS).astype("string")
    )
    attributes["attributes_status"] = attributes["attributes_status"].where(
        attributes["attributes_status"] != "seed",
        "partial",
    )
    write_cells_attributes(attributes)

    counts = attributes["structural_hazard"].value_counts(dropna=False).to_dict()
    print(f"Updated structural hazard from {HAZARD_RASTER}")
    print(counts)


if __name__ == "__main__":
    main()
