from __future__ import annotations

import unicodedata
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
PT_FIRESPRD_METADATA = ROOT / "data" / "external" / "pt-firesprd" / "pt_firesprd_metadata.parquet"
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
SCENARIO_CANDIDATES_PARQUET = BASELINE_DIR / "scenario_candidates.parquet"
SCENARIO_CANDIDATES_CSV = BASELINE_DIR / "scenario_candidates.csv"


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


def season_phase(date_value: pd.Timestamp) -> str:
    if pd.isna(date_value):
        return "unknown"
    if date_value.month in (6, 7):
        return "early_summer"
    if date_value.month == 8:
        return "peak_summer"
    if date_value.month == 9:
        return "late_summer"
    return "off_window"


def main() -> None:
    if not PT_FIRESPRD_METADATA.exists():
        raise FileNotFoundError(f"PT-FireSprd metadata parquet not found: {PT_FIRESPRD_METADATA}")

    metadata = pd.read_parquet(PT_FIRESPRD_METADATA)
    metadata["municipality_norm"] = metadata["municipality"].map(normalize_text)
    target_municipalities = {normalize_text(value) for value in NEARBY_MUNICIPALITIES}

    selected = metadata[metadata["municipality_norm"].isin(target_municipalities)].copy().reset_index(drop=True)
    selected["municipality_clean"] = selected["municipality_norm"].map(CANONICAL_MUNICIPALITY_NAMES)
    selected["candidate_date"] = pd.to_datetime(selected["start_date"]).dt.date
    selected["season_phase"] = pd.to_datetime(selected["start_date"]).map(season_phase)
    selected["fwi_reference"] = pd.Series([pd.NA] * len(selected), dtype="Float64")
    selected["kbdi_reference"] = pd.Series([pd.NA] * len(selected), dtype="Float64")
    selected["hotspot_flag"] = pd.Series([pd.NA] * len(selected), dtype="boolean")
    selected["burned_area_context_flag"] = True
    selected["pt_firesprd_flag"] = True
    selected["candidate_kind"] = "pt_firesprd_nearby_event"
    selected["source_dataset"] = "PT-FireSprd_v2.0"
    selected["source_municipality"] = selected["municipality_clean"]
    selected["source_fire_name"] = selected["fire_name"]
    selected["notes"] = "Seed gerado apenas a partir de PT-FireSprd e proximidade municipal."

    output = selected[
        [
            "candidate_date",
            "season_phase",
            "fwi_reference",
            "kbdi_reference",
            "hotspot_flag",
            "burned_area_context_flag",
            "pt_firesprd_flag",
            "candidate_kind",
            "source_dataset",
            "source_municipality",
            "source_fire_name",
            "start_date",
            "end_date",
            "extent_ha",
            "confidence_flag",
            "incident_id",
            "notes",
        ]
    ].sort_values(["candidate_date", "source_municipality", "source_fire_name"])

    output.to_parquet(SCENARIO_CANDIDATES_PARQUET, index=False)
    output.to_csv(SCENARIO_CANDIDATES_CSV, index=False)

    print(f"Rows: {len(output)}")
    print(f"Wrote: {SCENARIO_CANDIDATES_PARQUET}")
    print(f"Wrote: {SCENARIO_CANDIDATES_CSV}")


if __name__ == "__main__":
    main()
