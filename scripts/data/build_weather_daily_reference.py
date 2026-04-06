from __future__ import annotations

from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
WEATHER_REFERENCE_PARQUET = BASELINE_DIR / "weather_reference.parquet"
WEATHER_DAILY_REFERENCE_PARQUET = BASELINE_DIR / "weather_daily_reference.parquet"
WEATHER_DAILY_REFERENCE_CSV = BASELINE_DIR / "weather_daily_reference.csv"


def percentile_rank(series: pd.Series) -> pd.Series:
    return series.rank(method="average", pct=True).astype("Float64")


def main() -> None:
    if not WEATHER_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Weather reference parquet not found: {WEATHER_REFERENCE_PARQUET}")

    hourly = pd.read_parquet(WEATHER_REFERENCE_PARQUET)
    hourly["date_local"] = pd.to_datetime(hourly["date_local"])

    grouped = (
        hourly.groupby("date_local", as_index=False)
        .agg(
            area_id=("area_id", "first"),
            reference_kind=("reference_kind", "first"),
            reference_station_id=("reference_station_id", "first"),
            reference_station_name=("reference_station_name", "first"),
            source_dataset=("source_dataset", "first"),
            source_model=("source_model", "first"),
            requested_model=("requested_model", "first"),
            hourly_observation_count=("time_local", "count"),
            temperature_min_c=("temperature_c", "min"),
            temperature_mean_c=("temperature_c", "mean"),
            temperature_max_c=("temperature_c", "max"),
            relative_humidity_min_pct=("relative_humidity_pct", "min"),
            relative_humidity_mean_pct=("relative_humidity_pct", "mean"),
            relative_humidity_max_pct=("relative_humidity_pct", "max"),
            precipitation_total_mm=("precipitation_mm", "sum"),
            wind_speed_mean_ms=("wind_speed_ms", "mean"),
            wind_speed_max_ms=("wind_speed_ms", "max"),
            wind_gust_max_ms=("wind_gust_ms", "max"),
        )
        .sort_values("date_local")
        .reset_index(drop=True)
    )

    grouped["dry_day_flag"] = (grouped["precipitation_total_mm"] <= 0.1).astype("boolean")
    grouped["temperature_max_percentile"] = percentile_rank(grouped["temperature_max_c"])
    grouped["relative_humidity_min_percentile"] = percentile_rank(
        100.0 - grouped["relative_humidity_min_pct"]
    )
    grouped["wind_speed_max_percentile"] = percentile_rank(grouped["wind_speed_max_ms"])
    grouped["wind_gust_max_percentile"] = percentile_rank(grouped["wind_gust_max_ms"])
    grouped["precipitation_total_percentile"] = percentile_rank(grouped["precipitation_total_mm"])

    # Simple comparative score for early candidate ranking before FWI/KBDI exist.
    grouped["simple_weather_risk_score"] = (
        grouped["temperature_max_percentile"] * 0.35
        + grouped["relative_humidity_min_percentile"] * 0.30
        + grouped["wind_speed_max_percentile"] * 0.20
        + grouped["wind_gust_max_percentile"] * 0.15
    ).astype("Float64")
    grouped["simple_weather_risk_score"] = grouped["simple_weather_risk_score"].round(6)
    grouped["simple_weather_risk_percentile"] = percentile_rank(grouped["simple_weather_risk_score"])

    grouped.to_parquet(WEATHER_DAILY_REFERENCE_PARQUET, index=False)
    grouped.to_csv(WEATHER_DAILY_REFERENCE_CSV, index=False)

    print(f"Rows: {len(grouped)}")
    print(f"Wrote: {WEATHER_DAILY_REFERENCE_PARQUET}")
    print(f"Wrote: {WEATHER_DAILY_REFERENCE_CSV}")
    print(grouped.head(12).to_string(index=False))


if __name__ == "__main__":
    main()
