from __future__ import annotations

import argparse
from statistics import fmean

import numpy as np
import rasterio
from rasterio.mask import mask

from geospatial_utils import load_cells_attributes, load_grid, write_cells_attributes


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Enriquece cells_attributes com estatistica zonal simples a partir de um raster."
    )
    parser.add_argument("--source", required=True, help="Caminho para o raster de origem.")
    parser.add_argument("--target-column", required=True, help="Coluna de destino em cells_attributes.")
    parser.add_argument("--band", type=int, default=1, help="Banda do raster a usar.")
    parser.add_argument(
        "--stat",
        choices=["mean", "median", "min", "max"],
        default="mean",
        help="Estatistica a calcular por celula.",
    )
    return parser.parse_args()


def compute_stat(values: np.ndarray, stat: str) -> float | None:
    if values.size == 0:
        return None

    if stat == "mean":
        return float(fmean(values.tolist()))
    if stat == "median":
        return float(np.median(values))
    if stat == "min":
        return float(np.min(values))
    if stat == "max":
        return float(np.max(values))
    raise ValueError(f"Unsupported stat: {stat}")


def main() -> None:
    args = parse_args()

    grid = load_grid()[["cell_id", "geometry"]].copy()
    attributes = load_cells_attributes()

    results: list[tuple[str, float | None]] = []

    with rasterio.open(args.source) as dataset:
        grid = grid.to_crs(dataset.crs)

        for row in grid.itertuples(index=False):
            clipped, _ = mask(dataset, [row.geometry.__geo_interface__], crop=True, indexes=args.band)
            if np.ma.is_masked(clipped):
                valid = clipped.compressed()
            else:
                valid = clipped.reshape(-1)

            if dataset.nodata is not None:
                valid = valid[valid != dataset.nodata]

            valid = valid[~np.isnan(valid)] if valid.size else valid
            results.append((row.cell_id, compute_stat(valid, args.stat)))

    values = {cell_id: value for cell_id, value in results}
    attributes[args.target_column] = attributes["cell_id"].map(values).astype("Float64")
    attributes["attributes_status"] = attributes["attributes_status"].where(
        attributes["attributes_status"] != "seed",
        "partial",
    )
    write_cells_attributes(attributes)

    print(f"Updated {args.target_column} from {args.source} using {args.stat}")


if __name__ == "__main__":
    main()
