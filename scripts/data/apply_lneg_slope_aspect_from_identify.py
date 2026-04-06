from __future__ import annotations

import math
from pathlib import Path

import requests

from geospatial_utils import load_cells_attributes, load_grid, write_cells_attributes


ROOT = Path(__file__).resolve().parents[2]

SLOPE_IDENTIFY_URL = "https://sig.lneg.pt/server/rest/services/EUDEM_PT_Continente_Slope/MapServer/identify"
ASPECT_IDENTIFY_URL = "https://sig.lneg.pt/server/rest/services/EUDEM_PT_Continente_Aspect/MapServer/identify"


def build_request_params(x: float, y: float, map_extent: str, image_display: str) -> dict[str, str]:
    return {
        "f": "pjson",
        "geometry": f"{x},{y}",
        "geometryType": "esriGeometryPoint",
        "sr": "3763",
        "layers": "all:0",
        "tolerance": "1",
        "mapExtent": map_extent,
        "imageDisplay": image_display,
        "returnGeometry": "false",
    }


def extract_pixel_value(payload: dict) -> float | None:
    results = payload.get("results") or []
    if not results:
        return None

    value = results[0].get("attributes", {}).get("Classify.Pixel Value")
    return None if value is None else float(value)


def fetch_identify_value(
    session: requests.Session,
    url: str,
    x: float,
    y: float,
    map_extent: str,
    image_display: str,
) -> float | None:
    response = session.get(
        url,
        params=build_request_params(x, y, map_extent, image_display),
        timeout=60,
    )
    response.raise_for_status()
    return extract_pixel_value(response.json())


def main() -> None:
    grid = load_grid()[["cell_id", "geometry"]].to_crs(3763).copy()
    attributes = load_cells_attributes()

    minx, miny, maxx, maxy = grid.total_bounds
    width = math.ceil((maxx - minx) / 25.0)
    height = math.ceil((maxy - miny) / 25.0)
    map_extent = f"{minx},{miny},{maxx},{maxy}"
    image_display = f"{width},{height},96"

    centroids = grid.geometry.centroid
    results: list[tuple[str, float | None, float | None]] = []

    with requests.Session() as session:
        for index, row in enumerate(grid.itertuples(index=False), start=1):
            centroid = centroids.iloc[index - 1]
            slope = fetch_identify_value(session, SLOPE_IDENTIFY_URL, centroid.x, centroid.y, map_extent, image_display)
            aspect = fetch_identify_value(session, ASPECT_IDENTIFY_URL, centroid.x, centroid.y, map_extent, image_display)
            results.append((row.cell_id, slope, aspect))

            if index % 50 == 0 or index == len(grid):
                print(f"Processed {index}/{len(grid)} cells")

    slope_values = {cell_id: slope for cell_id, slope, _ in results}
    aspect_values = {cell_id: aspect for cell_id, _, aspect in results}

    attributes["slope_deg"] = attributes["cell_id"].map(slope_values).astype("Float64")
    attributes["aspect_deg"] = attributes["cell_id"].map(aspect_values).astype("Float64")
    attributes["attributes_status"] = attributes["attributes_status"].where(
        attributes["attributes_status"] != "seed",
        "partial",
    )
    write_cells_attributes(attributes)

    print("Updated slope_deg and aspect_deg from official LNEG identify services")
    print(attributes[["slope_deg", "aspect_deg"]].describe().to_string())


if __name__ == "__main__":
    main()
