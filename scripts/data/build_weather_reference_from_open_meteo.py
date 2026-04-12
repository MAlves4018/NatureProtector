from __future__ import annotations

"""Constrói a referência meteorológica horária da área piloto via Open-Meteo.

O script usa a estação IPMA mais próxima já identificada, pede ao Open-Meteo a
série histórica horária e grava um snapshot estruturado em parquet e CSV para
alimentar as etapas seguintes de preparação e seleção de cenários.
"""

import json
from pathlib import Path

import pandas as pd
import requests


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
OPEN_METEO_DIR = ROOT / "data" / "external" / "open-meteo" / "proenca-a-nova"
IPMA_NEARBY_STATIONS_CSV = BASELINE_DIR / "ipma_nearby_stations.csv"
WEATHER_REFERENCE_PARQUET = BASELINE_DIR / "weather_reference.parquet"
WEATHER_REFERENCE_CSV = BASELINE_DIR / "weather_reference.csv"

ARCHIVE_API_URL = "https://archive-api.open-meteo.com/v1/archive"
START_DATE = "2017-01-01"
END_DATE = "2025-12-31"
TIMEZONE = "Europe/Lisbon"
PRIMARY_MODEL = "era5_land"
FALLBACK_MODEL = "era5_seamless"

HOURLY_VARIABLES = [
    "temperature_2m",
    "relative_humidity_2m",
    "precipitation",
    "wind_speed_10m",
    "wind_direction_10m",
    "wind_gusts_10m",
]
CRITICAL_HOURLY_VARIABLES = [
    "precipitation",
    "wind_speed_10m",
    "wind_direction_10m",
    "wind_gusts_10m",
]


def load_reference_station() -> pd.Series:
    """Escolhe a estação IPMA mais próxima da área piloto."""
    stations = pd.read_csv(IPMA_NEARBY_STATIONS_CSV)
    if stations.empty:
        raise RuntimeError("No IPMA nearby stations were found.")
    return stations.sort_values("distance_km", ascending=True).iloc[0]


def build_params(station: pd.Series, model: str) -> dict[str, str]:
    """Monta os parâmetros HTTP usados na chamada ao arquivo Open-Meteo."""
    return {
        "latitude": f"{station['station_lat']:.6f}",
        "longitude": f"{station['station_lon']:.6f}",
        "start_date": START_DATE,
        "end_date": END_DATE,
        "timezone": TIMEZONE,
        "hourly": ",".join(HOURLY_VARIABLES),
        "wind_speed_unit": "ms",
        "precipitation_unit": "mm",
        "models": model,
    }


def payload_has_critical_values(payload: dict) -> bool:
    """Confirma se o payload contém variáveis críticas com dados utilizáveis."""
    hourly = payload.get("hourly") or {}
    for name in CRITICAL_HOURLY_VARIABLES:
        values = hourly.get(name)
        if isinstance(values, list) and any(value is not None for value in values):
            return True
    return False


def fetch_payload(session: requests.Session, station: pd.Series, model: str) -> dict:
    """Executa a chamada HTTP ao arquivo Open-Meteo para o modelo pedido."""
    response = session.get(ARCHIVE_API_URL, params=build_params(station, model), timeout=180)
    response.raise_for_status()
    return response.json()


def main() -> None:
    """Descarrega, normaliza e persiste a referência meteorológica horária."""
    if not IPMA_NEARBY_STATIONS_CSV.exists():
        raise FileNotFoundError(f"IPMA nearby stations csv not found: {IPMA_NEARBY_STATIONS_CSV}")

    station = load_reference_station()
    OPEN_METEO_DIR.mkdir(parents=True, exist_ok=True)

    with requests.Session() as session:
        payload = fetch_payload(session, station, PRIMARY_MODEL)
        source_model = PRIMARY_MODEL

        # Alguns modelos devolvem blocos vazios em variáveis críticas; neste caso
        # o script troca explicitamente para o fallback configurado.
        if not payload_has_critical_values(payload):
            payload = fetch_payload(session, station, FALLBACK_MODEL)
            source_model = FALLBACK_MODEL

    raw_json = OPEN_METEO_DIR / f"{source_model}_hourly_2017_2025_nearest_ipma_station.json"
    raw_json.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    hourly = payload.get("hourly")
    if not isinstance(hourly, dict) or "time" not in hourly:
        raise RuntimeError("Open-Meteo payload does not contain an hourly block with time values.")

    frame = pd.DataFrame(hourly)
    frame["time_local"] = pd.to_datetime(frame["time"])

# A etapa posterior do fluxo precisa de timestamps UTC normalizados, mas também de
    # preservar a leitura local usada na interpretação meteorológica.
    frame["time_utc"] = (
        frame["time_local"]
        .dt.tz_localize(TIMEZONE, ambiguous="NaT", nonexistent="shift_forward")
        .dt.tz_convert("UTC")
        .dt.tz_localize(None)
    )
    frame["date_local"] = frame["time_local"].dt.date
    frame["hour_local"] = frame["time_local"].dt.hour
    frame["month_local"] = frame["time_local"].dt.month

    output = pd.DataFrame(
        {
            "area_id": "proenca-a-nova",
            "reference_kind": "nearest_ipma_station",
            "reference_station_id": int(station["station_id"]),
            "reference_station_name": station["station_name"],
            "reference_station_distance_km": station["distance_km"],
            "requested_latitude": float(station["station_lat"]),
            "requested_longitude": float(station["station_lon"]),
            "model_latitude": payload.get("latitude"),
            "model_longitude": payload.get("longitude"),
            "model_elevation_m": payload.get("elevation"),
            "source_dataset": "open_meteo_historical_api",
            "requested_model": PRIMARY_MODEL,
            "source_model": source_model,
            "timezone_local": payload.get("timezone"),
            "utc_offset_seconds": payload.get("utc_offset_seconds"),
            "time_local": frame["time_local"],
            "time_utc": frame["time_utc"],
            "date_local": frame["date_local"],
            "hour_local": frame["hour_local"],
            "month_local": frame["month_local"],
            "temperature_c": pd.Series(frame["temperature_2m"], dtype="Float64"),
            "relative_humidity_pct": pd.Series(frame["relative_humidity_2m"], dtype="Float64"),
            "precipitation_mm": pd.Series(frame["precipitation"], dtype="Float64"),
            "wind_speed_ms": pd.Series(frame["wind_speed_10m"], dtype="Float64"),
            "wind_direction_deg": pd.Series(frame["wind_direction_10m"], dtype="Float64"),
            "wind_gust_ms": pd.Series(frame["wind_gusts_10m"], dtype="Float64"),
        }
    )

    output.to_parquet(WEATHER_REFERENCE_PARQUET, index=False)
    output.to_csv(WEATHER_REFERENCE_CSV, index=False)

    print(f"Rows: {len(output)}")
    print(f"Station: {station['station_name']} ({int(station['station_id'])})")
    print(f"Requested coords: {station['station_lat']}, {station['station_lon']}")
    print(f"Model coords: {payload.get('latitude')}, {payload.get('longitude')}")
    print(f"Requested model: {PRIMARY_MODEL}")
    print(f"Used model: {source_model}")
    print(f"Time range: {output['time_local'].min()} -> {output['time_local'].max()}")
    print(f"Wrote: {raw_json}")
    print(f"Wrote: {WEATHER_REFERENCE_PARQUET}")
    print(f"Wrote: {WEATHER_REFERENCE_CSV}")
    print(output.head(12).to_string(index=False))


if __name__ == "__main__":
    main()
