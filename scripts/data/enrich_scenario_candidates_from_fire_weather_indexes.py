from __future__ import annotations

from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
SCENARIO_CANDIDATES_PARQUET = BASELINE_DIR / "scenario_candidates.parquet"
SCENARIO_CANDIDATES_CSV = BASELINE_DIR / "scenario_candidates.csv"
WEATHER_DAILY_REFERENCE_PARQUET = BASELINE_DIR / "weather_daily_reference.parquet"

INDEX_COLUMNS = [
    "noon_time_local",
    "noon_observation_hour_local",
    "noon_temperature_c",
    "noon_relative_humidity_pct",
    "noon_wind_speed_ms",
    "noon_wind_speed_kmh",
    "fwi_input_temperature_c",
    "fwi_input_relative_humidity_pct",
    "fwi_input_wind_kmh",
    "fwi_input_rain_mm",
    "ffmc_reference",
    "dmc_reference",
    "dc_reference",
    "isi_reference",
    "bui_reference",
    "fwi_reference",
    "fwi_reference_percentile",
    "fwi_reference_class",
    "kbdi_reference",
    "kbdi_reference_percentile",
    "kbdi_reference_class",
    "fire_index_reference_score",
    "fire_index_reference_percentile",
    "fire_index_reference_kind",
    "candidate_index_kind",
    "index_context_source",
]

INDEX_NOTE = "Index context enriched from daily fire weather reference."


def drop_existing_columns(frame: pd.DataFrame, target_columns: list[str]) -> pd.DataFrame:
    suffix_matches = {f"{column}_x" for column in target_columns} | {f"{column}_y" for column in target_columns}
    to_drop = [column for column in frame.columns if column in target_columns or column in suffix_matches]
    return frame.drop(columns=to_drop, errors="ignore")


def normalize_notes(series: pd.Series) -> pd.Series:
    cleaned = series.astype("string").fillna("")
    cleaned = cleaned.str.replace(INDEX_NOTE, "", regex=False)
    cleaned = cleaned.str.replace(r"\s+", " ", regex=True).str.strip()
    return cleaned


def main() -> None:
    if not SCENARIO_CANDIDATES_PARQUET.exists():
        raise FileNotFoundError(f"Scenario candidates parquet not found: {SCENARIO_CANDIDATES_PARQUET}")
    if not WEATHER_DAILY_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Daily weather reference parquet not found: {WEATHER_DAILY_REFERENCE_PARQUET}")

    candidates = pd.read_parquet(SCENARIO_CANDIDATES_PARQUET)
    weather = pd.read_parquet(WEATHER_DAILY_REFERENCE_PARQUET)

    candidates = drop_existing_columns(candidates, INDEX_COLUMNS)
    candidates["candidate_date"] = pd.to_datetime(candidates["candidate_date"])
    weather["date_local"] = pd.to_datetime(weather["date_local"])

    merged = candidates.merge(
        weather[
            [
                "date_local",
                "noon_time_local",
                "noon_observation_hour_local",
                "noon_temperature_c",
                "noon_relative_humidity_pct",
                "noon_wind_speed_ms",
                "noon_wind_speed_kmh",
                "fwi_input_temperature_c",
                "fwi_input_relative_humidity_pct",
                "fwi_input_wind_kmh",
                "fwi_input_rain_mm",
                "ffmc_reference",
                "dmc_reference",
                "dc_reference",
                "isi_reference",
                "bui_reference",
                "fwi_reference",
                "fwi_reference_percentile",
                "fwi_reference_class",
                "kbdi_reference",
                "kbdi_reference_percentile",
                "kbdi_reference_class",
                "fire_index_reference_score",
                "fire_index_reference_percentile",
                "fire_index_reference_kind",
            ]
        ],
        left_on="candidate_date",
        right_on="date_local",
        how="left",
    ).drop(columns=["date_local"])

    merged["candidate_index_kind"] = merged["fire_index_reference_kind"].fillna("missing_index_context").astype("string")
    merged["index_context_source"] = pd.Series(["weather_daily_reference"] * len(merged), dtype="string")
    merged["notes"] = normalize_notes(merged["notes"])
    merged["notes"] = (merged["notes"] + " " + INDEX_NOTE).str.strip()

    merged.to_parquet(SCENARIO_CANDIDATES_PARQUET, index=False)
    merged.to_csv(SCENARIO_CANDIDATES_CSV, index=False)

    print(f"Rows: {len(merged)}")
    print(f"Wrote: {SCENARIO_CANDIDATES_PARQUET}")
    print(f"Wrote: {SCENARIO_CANDIDATES_CSV}")
    print(
        merged[
            [
                "candidate_date",
                "source_fire_name",
                "fwi_reference",
                "kbdi_reference",
                "candidate_index_kind",
            ]
        ].head(12).to_string(index=False)
    )


if __name__ == "__main__":
    main()
