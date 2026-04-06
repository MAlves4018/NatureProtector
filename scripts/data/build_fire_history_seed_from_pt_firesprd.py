from __future__ import annotations

import unicodedata
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
PT_FIRESPRD_METADATA = ROOT / "data" / "external" / "pt-firesprd" / "pt_firesprd_metadata.parquet"
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
FIRE_HISTORY_PARQUET = BASELINE_DIR / "fire_history.parquet"
FIRE_HISTORY_CSV = BASELINE_DIR / "fire_history.csv"


def normalize_text(value: object) -> str:
    text = "" if value is None else str(value)
    text = unicodedata.normalize("NFKD", text)
    text = "".join(char for char in text if not unicodedata.combining(char))
    return text.casefold().strip()


NEARBY_MUNICIPALITIES = [
    "Proenca-a-Nova",
    "Oleiros",
    "Serta",
    "Vila de Rei",
    "Macao",
    "Castelo Branco",
    "Fundao",
    "Idanha-a-Nova",
    "Covilha",
]

CANONICAL_MUNICIPALITY_NAMES = {
    normalize_text(value): value for value in NEARBY_MUNICIPALITIES
}


def main() -> None:
    if not PT_FIRESPRD_METADATA.exists():
        raise FileNotFoundError(f"PT-FireSprd metadata parquet not found: {PT_FIRESPRD_METADATA}")

    frame = pd.read_parquet(PT_FIRESPRD_METADATA)
    frame["municipality_norm"] = frame["municipality"].map(normalize_text)
    nearby = {normalize_text(value) for value in NEARBY_MUNICIPALITIES}
    selected = frame[frame["municipality_norm"].isin(nearby)].copy().reset_index(drop=True)
    selected["municipality_clean"] = selected["municipality_norm"].map(CANONICAL_MUNICIPALITY_NAMES)

    output = pd.DataFrame(
        {
            "area_id": "proenca-a-nova",
            "source_dataset": "PT-FireSprd_v2.0",
            "source_fire_name": selected["fire_name"],
            "source_municipality": selected["municipality_clean"],
            "start_date": pd.to_datetime(selected["start_date"]),
            "end_date": pd.to_datetime(selected["end_date"]),
            "extent_ha": selected["extent_ha"],
            "confidence_flag": selected["confidence_flag"],
            "incident_id": selected["incident_id"].astype("string"),
            "burned_area_context_flag": True,
            "hotspot_flag": pd.Series([pd.NA] * len(selected), dtype="boolean"),
            "pt_firesprd_flag": True,
            "proximity_basis": "nearby_municipality_seed",
            "history_kind": "large_fire_progression",
            "notes": "Seed gerado apenas a partir de PT-FireSprd e proximidade municipal.",
        }
    ).sort_values(["start_date", "source_municipality", "source_fire_name"]).reset_index(drop=True)

    output.to_parquet(FIRE_HISTORY_PARQUET, index=False)
    output.to_csv(FIRE_HISTORY_CSV, index=False)

    print(f"Rows: {len(output)}")
    print(f"Wrote: {FIRE_HISTORY_PARQUET}")
    print(f"Wrote: {FIRE_HISTORY_CSV}")


if __name__ == "__main__":
    main()
