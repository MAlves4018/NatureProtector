from __future__ import annotations

from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
SCENARIO_CANDIDATES_PARQUET = BASELINE_DIR / "scenario_candidates.parquet"
SCENARIO_CANDIDATES_CSV = BASELINE_DIR / "scenario_candidates.csv"
WEATHER_DAILY_REFERENCE_PARQUET = BASELINE_DIR / "weather_daily_reference.parquet"

WEATHER_COLUMNS = [
    "hourly_observation_count",
    "temperature_min_c",
    "temperature_mean_c",
    "temperature_max_c",
    "relative_humidity_min_pct",
    "relative_humidity_mean_pct",
    "relative_humidity_max_pct",
    "precipitation_total_mm",
    "wind_speed_mean_ms",
    "wind_speed_max_ms",
    "wind_gust_max_ms",
    "dry_day_flag",
    "temperature_max_percentile",
    "relative_humidity_min_percentile",
    "wind_speed_max_percentile",
    "wind_gust_max_percentile",
    "precipitation_total_percentile",
    "simple_weather_risk_score",
    "simple_weather_risk_percentile",
    "candidate_weather_kind",
    "weather_context_source",
]

WEATHER_NOTE = "Weather context enriched from daily weather reference."


def candidate_weather_kind(row: pd.Series) -> str:
    score_pct = row.get("simple_weather_risk_percentile")
    rain = row.get("precipitation_total_mm")

    if pd.isna(score_pct):
        return "missing_weather_context"
    if pd.notna(rain) and rain > 5:
        return "rain_affected"
    if score_pct >= 0.9:
        return "critical_weather_context"
    if score_pct >= 0.7:
        return "elevated_weather_context"
    if score_pct <= 0.3:
        return "mild_weather_context"
    return "moderate_weather_context"


def drop_existing_columns(frame: pd.DataFrame, target_columns: list[str]) -> pd.DataFrame:
    suffix_matches = {f"{column}_x" for column in target_columns} | {f"{column}_y" for column in target_columns}
    to_drop = [column for column in frame.columns if column in target_columns or column in suffix_matches]
    return frame.drop(columns=to_drop, errors="ignore")


def normalize_notes(series: pd.Series) -> pd.Series:
    cleaned = series.astype("string").fillna("")
    cleaned = cleaned.str.replace(WEATHER_NOTE, "", regex=False)
    cleaned = cleaned.str.replace(r"\s+", " ", regex=True).str.strip()
    return cleaned


def main() -> None:
    if not SCENARIO_CANDIDATES_PARQUET.exists():
        raise FileNotFoundError(f"Scenario candidates parquet not found: {SCENARIO_CANDIDATES_PARQUET}")
    if not WEATHER_DAILY_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Daily weather reference parquet not found: {WEATHER_DAILY_REFERENCE_PARQUET}")

    candidates = pd.read_parquet(SCENARIO_CANDIDATES_PARQUET)
    weather = pd.read_parquet(WEATHER_DAILY_REFERENCE_PARQUET)

    candidates = drop_existing_columns(candidates, WEATHER_COLUMNS)

    candidates["candidate_date"] = pd.to_datetime(candidates["candidate_date"])
    weather["date_local"] = pd.to_datetime(weather["date_local"])

    merged = candidates.merge(
        weather[
            [
                "date_local",
                "hourly_observation_count",
                "temperature_min_c",
                "temperature_mean_c",
                "temperature_max_c",
                "relative_humidity_min_pct",
                "relative_humidity_mean_pct",
                "relative_humidity_max_pct",
                "precipitation_total_mm",
                "wind_speed_mean_ms",
                "wind_speed_max_ms",
                "wind_gust_max_ms",
                "dry_day_flag",
                "temperature_max_percentile",
                "relative_humidity_min_percentile",
                "wind_speed_max_percentile",
                "wind_gust_max_percentile",
                "precipitation_total_percentile",
                "simple_weather_risk_score",
                "simple_weather_risk_percentile",
            ]
        ],
        left_on="candidate_date",
        right_on="date_local",
        how="left",
    ).drop(columns=["date_local"])

    merged["candidate_weather_kind"] = merged.apply(candidate_weather_kind, axis=1).astype("string")
    merged["weather_context_source"] = pd.Series(
        ["weather_daily_reference"] * len(merged), dtype="string"
    )
    merged["notes"] = normalize_notes(merged["notes"])
    merged["notes"] = (merged["notes"] + " " + WEATHER_NOTE).str.strip()

    merged.to_parquet(SCENARIO_CANDIDATES_PARQUET, index=False)
    merged.to_csv(SCENARIO_CANDIDATES_CSV, index=False)

    print(f"Rows: {len(merged)}")
    print(f"Wrote: {SCENARIO_CANDIDATES_PARQUET}")
    print(f"Wrote: {SCENARIO_CANDIDATES_CSV}")
    print(merged.head(12).to_string(index=False))


if __name__ == "__main__":
    main()
