from __future__ import annotations

"""Helpers geoespaciais partilhados pelos scripts de preparação da baseline.

As funções deste módulo assumem a convenção de caminhos da área piloto de
Proença-a-Nova e encapsulam o acesso aos artefactos geoespaciais mais usados
pelos scripts de preparação de dados.
"""

from pathlib import Path

import geopandas as gpd
import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
AREA_GPKG = BASELINE_DIR / "area.gpkg"
GRID_GPKG = BASELINE_DIR / "grid_1km.gpkg"
CELLS_ATTRIBUTES_PARQUET = BASELINE_DIR / "cells_attributes.parquet"
CELLS_ATTRIBUTES_CSV = BASELINE_DIR / "cells_attributes.csv"


def load_area() -> gpd.GeoDataFrame:
    """Carrega a geometria da área piloto."""
    return gpd.read_file(AREA_GPKG, layer="area")


def load_grid() -> gpd.GeoDataFrame:
    """Carrega a grelha 1 km da área piloto."""
    return gpd.read_file(GRID_GPKG, layer="grid_1km")


def load_cells_attributes() -> pd.DataFrame:
    """Carrega o dataset preparado de atributos por célula."""
    return pd.read_parquet(CELLS_ATTRIBUTES_PARQUET)


def write_cells_attributes(frame: pd.DataFrame) -> None:
    """Persiste atributos de células em parquet e CSV com ordenação estável."""
    ordered = frame.sort_values("cell_id").reset_index(drop=True)
    ordered.to_parquet(CELLS_ATTRIBUTES_PARQUET, index=False)
    ordered.to_csv(CELLS_ATTRIBUTES_CSV, index=False)
