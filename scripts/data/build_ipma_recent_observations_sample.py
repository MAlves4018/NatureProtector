from __future__ import annotations

import json
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
IPMA_SAMPLES_DIR = ROOT / "data" / "external" / "ipma" / "api-samples"

IPMA_NEARBY_STATIONS_CSV = BASELINE_DIR / "ipma_nearby_stations.csv"
IPMA_OBSERVATIONS_JSON = IPMA_SAMPLES_DIR / "observations.json"
OUTPUT_PARQUET = BASELINE_DIR / "ipma_recent_observations.parquet"
OUTPUT_CSV = BASELINE_DIR / "ipma_recent_observations.csv"

MAX_DISTANCE_KM = 80.0
NO_DATA_VALUE = -99.0


def normalize_value(value: object) -> object:
    if value == NO_DATA_VALUE:
        return pd.NA
    return value


def main() -> None:
    if not IPMA_NEARBY_STATIONS_CSV.exists():
        raise FileNotFoundError(f"Nearby stations csv not found: {IPMA_NEARBY_STATIONS_CSV}")
    if not IPMA_OBSERVATIONS_JSON.exists():
        raise FileNotFoundError(f"Observations json not found: {IPMA_OBSERVATIONS_JSON}")

    stations = pd.read_csv(IPMA_NEARBY_STATIONS_CSV)
    nearby = stations[stations["distance_km"] <= MAX_DISTANCE_KM].copy()
    nearby["station_id"] = nearby["station_id"].astype("Int64").astype("string")
    nearby_station_ids = set(nearby["station_id"])

    payload = json.loads(IPMA_OBSERVATIONS_JSON.read_text(encoding="utf-8"))
    records: list[dict[str, object]] = []

    for observation_time, station_map in payload.items():
        for station_id, values in station_map.items():
            if station_id not in nearby_station_ids:
                continue
            if not isinstance(values, dict):
                continue

            station_row = nearby.loc[nearby["station_id"] == station_id].iloc[0]
            records.append(
                {
                    "area_id": "proenca-a-nova",
                    "observation_time": pd.to_datetime(observation_time),
                    "station_id": station_id,
                    "station_name": station_row["station_name"],
                    "distance_km": station_row["distance_km"],
                    "temperature_c": normalize_value(values.get("temperatura")),
                    "humidity_pct": normalize_value(values.get("humidade")),
                    "wind_speed_ms": normalize_value(values.get("intensidadeVento")),
                    "wind_speed_kmh": normalize_value(values.get("intensidadeVentoKM")),
                    "wind_direction_id": normalize_value(values.get("idDireccVento")),
                    "precip_accumulated_mm": normalize_value(values.get("precAcumulada")),
                    "pressure_hpa": normalize_value(values.get("pressao")),
                    "radiation_wm2": normalize_value(values.get("radiacao")),
                    "source_dataset": "ipma_open_data_recent_obs",
                }
            )

    if not records:
        raise RuntimeError("No recent IPMA observations were matched to the nearby station shortlist.")

    frame = pd.DataFrame(records).sort_values(["observation_time", "distance_km", "station_name"]).reset_index(drop=True)
    frame.to_parquet(OUTPUT_PARQUET, index=False)
    frame.to_csv(OUTPUT_CSV, index=False)

    print(f"Rows: {len(frame)}")
    print(f"Stations within {MAX_DISTANCE_KM:.0f} km: {nearby['station_id'].nunique()}")
    print(f"Wrote: {OUTPUT_PARQUET}")
    print(f"Wrote: {OUTPUT_CSV}")
    print(frame.head(12).to_string(index=False))


if __name__ == "__main__":
    main()
