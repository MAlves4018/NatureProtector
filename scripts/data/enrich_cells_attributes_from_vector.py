from __future__ import annotations

import argparse

import geopandas as gpd
import pandas as pd

from geospatial_utils import load_cells_attributes, load_grid, write_cells_attributes


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Enriquece cells_attributes com a classe dominante de uma camada vetorial."
    )
    parser.add_argument("--source", required=True, help="Caminho para o ficheiro vetorial de origem.")
    parser.add_argument("--layer", default=None, help="Layer a usar no source, quando aplicavel.")
    parser.add_argument("--class-column", required=True, help="Coluna da camada de origem com a classe.")
    parser.add_argument("--target-column", required=True, help="Coluna de destino em cells_attributes.")
    parser.add_argument(
        "--coverage-column",
        default=None,
        help="Coluna de destino para a percentagem dominante na celula.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    grid = load_grid()[["cell_id", "geometry"]].copy()
    attributes = load_cells_attributes()
    source = gpd.read_file(args.source, layer=args.layer)

    if args.class_column not in source.columns:
        raise KeyError(f"Column not found in source layer: {args.class_column}")

    source = source[[args.class_column, "geometry"]].dropna(subset=[args.class_column]).copy()
    source = source.to_crs(grid.crs)

    overlay = gpd.overlay(grid, source, how="intersection", keep_geom_type=False)
    if overlay.empty:
        raise ValueError("No intersections found between grid and source layer.")

    overlay["intersection_area_m2"] = overlay.geometry.area

    grouped = (
        overlay.groupby(["cell_id", args.class_column], as_index=False)["intersection_area_m2"]
        .sum()
        .sort_values(["cell_id", "intersection_area_m2"], ascending=[True, False])
    )
    dominant = grouped.drop_duplicates(subset=["cell_id"], keep="first")

    merged = attributes.merge(
        dominant[["cell_id", args.class_column, "intersection_area_m2"]],
        on="cell_id",
        how="left",
    )

    merged[args.target_column] = merged[args.class_column].astype("string")
    merged = merged.drop(columns=[args.class_column])

    if args.coverage_column:
        merged[args.coverage_column] = (
            merged["intersection_area_m2"] / merged["cell_area_m2"] * 100.0
        ).astype("Float64")

    merged["attributes_status"] = merged["attributes_status"].where(
        merged["attributes_status"] != "seed",
        "partial",
    )
    merged = merged.drop(columns=["intersection_area_m2"])
    write_cells_attributes(merged)

    print(f"Updated {args.target_column} from {args.source}")
    if args.coverage_column:
        print(f"Updated {args.coverage_column}")


if __name__ == "__main__":
    main()
