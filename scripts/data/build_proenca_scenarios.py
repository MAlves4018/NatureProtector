from __future__ import annotations

import json
import unicodedata
import uuid
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
SCENARIO_MANIFESTS_DIR = ROOT / "data" / "manifests" / "scenarios"
SCENARIO_OUTPUT_DIR = SCENARIO_MANIFESTS_DIR / "proenca-a-nova"

WEATHER_DAILY_REFERENCE_PARQUET = BASELINE_DIR / "weather_daily_reference.parquet"
SCENARIO_CANDIDATES_PARQUET = BASELINE_DIR / "scenario_candidates.parquet"
SCENARIO_TEMPLATE_JSON = SCENARIO_MANIFESTS_DIR / "proenca-a-nova-scenarios.template.json"
SCENARIO_CATALOG_JSON = SCENARIO_MANIFESTS_DIR / "proenca-a-nova-scenarios.generated.json"

UUID_NAMESPACE = uuid.UUID("5e2d75f7-89f1-46fa-85b4-3ac6a49fdb4b")
AREA_ID = uuid.UUID("b3f4fb84-bf17-5522-a5f3-70fd1212f381")


def normalize_text(value: str | None) -> str:
    if not value:
        return ""
    normalized = unicodedata.normalize("NFKD", value)
    return "".join(ch for ch in normalized if not unicodedata.combining(ch)).lower().replace("-", "").replace(" ", "")


def to_float(value: object) -> float | None:
    if pd.isna(value):
        return None
    return round(float(value), 3)


def to_int(value: object) -> int | None:
    if pd.isna(value):
        return None
    return int(value)


def build_base_scenario_candidate(weather: pd.DataFrame, blocked_dates: set[pd.Timestamp]) -> pd.Series:
    summer = weather.copy()
    summer["date_local"] = pd.to_datetime(summer["date_local"])
    summer = summer[
        summer["date_local"].dt.month.isin([6, 7, 8, 9])
        & (~summer["date_local"].isin(blocked_dates))
        & (summer["dry_day_flag"] == True)
        & (summer["fire_index_reference_percentile"] >= 0.30)
        & (summer["fire_index_reference_percentile"] <= 0.65)
    ].copy()

    if summer.empty:
        raise RuntimeError("Could not select a plausible base scenario day from weather_daily_reference.")

    summer["selection_distance"] = (
        (summer["fire_index_reference_percentile"] - 0.50).abs() * 3.0
        + (summer["temperature_max_c"] - 30.0).abs() / 15.0
        + (summer["relative_humidity_min_pct"] - 35.0).abs() / 25.0
        + (summer["wind_speed_max_ms"] - 5.0).abs() / 5.0
    )

    return summer.sort_values(
        ["selection_distance", "fire_index_reference_percentile", "date_local"],
        ascending=[True, True, True],
    ).iloc[0]


def build_high_risk_scenario_candidate(candidates: pd.DataFrame) -> pd.Series:
    ranked = candidates.copy()
    ranked["candidate_date"] = pd.to_datetime(ranked["candidate_date"])
    ranked["municipality_rank"] = ranked["source_municipality"].map(
        lambda value: 0 if normalize_text(str(value)) == "proencaanova" else 1
    )
    ranked["index_rank"] = ranked["candidate_index_kind"].map(
        {
            "critical_index_context": 0,
            "elevated_index_context": 1,
            "mild_index_context": 2,
            "missing_index_context": 3,
        }
    ).fillna(4)
    ranked["weather_rank"] = ranked["candidate_weather_kind"].map(
        {
            "critical_weather_context": 0,
            "elevated_weather_context": 1,
            "moderate_weather_context": 2,
            "mild_weather_context": 3,
            "missing_weather_context": 4,
        }
    ).fillna(5)

    ranked = ranked.sort_values(
        [
            "municipality_rank",
            "index_rank",
            "weather_rank",
            "fire_index_reference_score",
            "simple_weather_risk_score",
            "extent_ha",
            "confidence_flag",
            "candidate_date",
        ],
        ascending=[True, True, True, False, False, False, False, True],
    )

    if ranked.empty:
        raise RuntimeError("Could not select a high-risk scenario candidate from scenario_candidates.")

    return ranked.iloc[0]


def scenario_uuid(name: str) -> str:
    return str(uuid.uuid5(UUID_NAMESPACE, f"proenca-a-nova:{name}"))


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8-sig")


def build_simulator_options(
    *,
    scenario_id: str,
    scenario_name: str,
    scenario_description: str,
    scenario_category: str,
    candidate_date: str,
    base_temperature: float | None,
    base_humidity: float | None,
    base_wind_speed: float | None,
    failure_rate: float,
    noise_level: float,
    time_acceleration: float,
) -> dict[str, object]:
    start_local = datetime.fromisoformat(f"{candidate_date}T12:00:00")

    return {
        "AreaId": str(AREA_ID),
        "ScenarioId": scenario_id,
        "ScenarioName": scenario_name,
        "ScenarioDescription": scenario_description,
        "ScenarioCategory": scenario_category,
        "StartTimestamp": start_local.isoformat(),
        "BaseTemperature": base_temperature,
        "BaseHumidity": base_humidity,
        "BaseWindSpeed": base_wind_speed,
        "FailureRate": round(failure_rate, 3),
        "NoiseLevel": round(noise_level, 3),
        "TimeAcceleration": round(time_acceleration, 3),
        "NumberOfCycles": 288,
        "IntervalSeconds": 5,
        "LogicalStepMinutes": 5,
    }


def scenario_payload(
    *,
    scenario_key: str,
    label: str,
    status: str,
    scenario_category: str,
    candidate_date: str,
    selected_reason: str,
    fault_profile: str,
    source_context: dict[str, object],
    daily_reference: dict[str, object],
    simulator_options: dict[str, object],
    base_scenario_id: str | None = None,
    future_fault_injections: list[str] | None = None,
) -> dict[str, object]:
    payload = {
        "scenario_id": scenario_uuid(scenario_key),
        "scenario_key": scenario_key,
        "label": label,
        "status": status,
        "candidate_date": candidate_date,
        "scenario_category": scenario_category,
        "selected_reason": selected_reason,
        "fault_profile": fault_profile,
        "source_context": source_context,
        "daily_reference": daily_reference,
        "simulator_options": simulator_options,
    }
    if base_scenario_id is not None:
        payload["base_scenario_id"] = base_scenario_id
    if future_fault_injections:
        payload["future_fault_injections"] = future_fault_injections
    return payload


def main() -> None:
    if not WEATHER_DAILY_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Missing weather_daily_reference parquet: {WEATHER_DAILY_REFERENCE_PARQUET}")
    if not SCENARIO_CANDIDATES_PARQUET.exists():
        raise FileNotFoundError(f"Missing scenario_candidates parquet: {SCENARIO_CANDIDATES_PARQUET}")
    if not SCENARIO_TEMPLATE_JSON.exists():
        raise FileNotFoundError(f"Missing scenario template json: {SCENARIO_TEMPLATE_JSON}")

    weather = pd.read_parquet(WEATHER_DAILY_REFERENCE_PARQUET)
    candidates = pd.read_parquet(SCENARIO_CANDIDATES_PARQUET)

    blocked_dates = set(pd.to_datetime(candidates["candidate_date"], errors="coerce").dropna())
    scenario_a_row = build_base_scenario_candidate(weather, blocked_dates)
    scenario_b_row = build_high_risk_scenario_candidate(candidates)

    scenario_a_date = pd.Timestamp(scenario_a_row["date_local"]).date().isoformat()
    scenario_b_date = pd.Timestamp(scenario_b_row["candidate_date"]).date().isoformat()

    scenario_a = scenario_payload(
        scenario_key="scenario_a",
        label="Base",
        status="generated",
        scenario_category="Base",
        candidate_date=scenario_a_date,
        selected_reason="Dia seco de verão escolhido por representar um contexto operacional plausível e próximo do centro da distribuição local de risco diário.",
        fault_profile="none",
        source_context={
            "selection_source": "weather_daily_reference",
            "source_dataset": scenario_a_row["source_dataset"],
            "reference_station_name": scenario_a_row["reference_station_name"],
            "reference_kind": scenario_a_row["reference_kind"],
        },
        daily_reference={
            "temperature_min_c": to_float(scenario_a_row["temperature_min_c"]),
            "temperature_mean_c": to_float(scenario_a_row["temperature_mean_c"]),
            "temperature_max_c": to_float(scenario_a_row["temperature_max_c"]),
            "relative_humidity_min_pct": to_float(scenario_a_row["relative_humidity_min_pct"]),
            "precipitation_total_mm": to_float(scenario_a_row["precipitation_total_mm"]),
            "wind_speed_max_ms": to_float(scenario_a_row["wind_speed_max_ms"]),
            "fwi_reference": to_float(scenario_a_row["fwi_reference"]),
            "kbdi_reference": to_float(scenario_a_row["kbdi_reference"]),
            "fire_index_reference_kind": scenario_a_row["fire_index_reference_kind"],
        },
        simulator_options=build_simulator_options(
            scenario_id=scenario_uuid("scenario_a"),
            scenario_name="Scenario A - Base Proenca-a-Nova",
            scenario_description="Cenário base plausível para a área piloto, derivado da referência meteorológica diária e sem injeção de falhas.",
            scenario_category="Base",
            candidate_date=scenario_a_date,
            base_temperature=to_float(scenario_a_row["noon_temperature_c"]) or to_float(scenario_a_row["temperature_mean_c"]),
            base_humidity=to_float(scenario_a_row["noon_relative_humidity_pct"]) or to_float(scenario_a_row["relative_humidity_min_pct"]),
            base_wind_speed=to_float(scenario_a_row["noon_wind_speed_ms"]) or to_float(scenario_a_row["wind_speed_mean_ms"]),
            failure_rate=0.02,
            noise_level=0.08,
            time_acceleration=1.0,
        ),
    )

    scenario_b = scenario_payload(
        scenario_key="scenario_b",
        label="HighRisk",
        status="generated",
        scenario_category="HighRisk",
        candidate_date=scenario_b_date,
        selected_reason="Evento selecionado por combinar contexto crítico de índices e ligação direta à área piloto ou ao seu entorno operacional imediato.",
        fault_profile="none",
        source_context={
            "selection_source": "scenario_candidates",
            "source_dataset": scenario_b_row["source_dataset"],
            "source_municipality": scenario_b_row["source_municipality"],
            "source_fire_name": scenario_b_row["source_fire_name"],
            "extent_ha": to_float(scenario_b_row["extent_ha"]),
            "confidence_flag": to_int(scenario_b_row["confidence_flag"]),
            "candidate_weather_kind": scenario_b_row["candidate_weather_kind"],
            "candidate_index_kind": scenario_b_row["candidate_index_kind"],
        },
        daily_reference={
            "temperature_min_c": to_float(scenario_b_row["temperature_min_c"]),
            "temperature_mean_c": to_float(scenario_b_row["temperature_mean_c"]),
            "temperature_max_c": to_float(scenario_b_row["temperature_max_c"]),
            "relative_humidity_min_pct": to_float(scenario_b_row["relative_humidity_min_pct"]),
            "precipitation_total_mm": to_float(scenario_b_row["precipitation_total_mm"]),
            "wind_speed_max_ms": to_float(scenario_b_row["wind_speed_max_ms"]),
            "fwi_reference": to_float(scenario_b_row["fwi_reference"]),
            "kbdi_reference": to_float(scenario_b_row["kbdi_reference"]),
            "fire_index_reference_kind": scenario_b_row["fire_index_reference_kind"],
        },
        simulator_options=build_simulator_options(
            scenario_id=scenario_uuid("scenario_b"),
            scenario_name="Scenario B - High Risk Proenca-a-Nova",
            scenario_description="Cenário de risco elevado derivado de um candidato histórico forte e enriquecido com contexto de índices diários.",
            scenario_category="HighRisk",
            candidate_date=scenario_b_date,
            base_temperature=to_float(scenario_b_row["noon_temperature_c"]) or to_float(scenario_b_row["temperature_mean_c"]),
            base_humidity=to_float(scenario_b_row["noon_relative_humidity_pct"]) or to_float(scenario_b_row["relative_humidity_min_pct"]),
            base_wind_speed=to_float(scenario_b_row["noon_wind_speed_ms"]) or to_float(scenario_b_row["wind_speed_mean_ms"]),
            failure_rate=0.05,
            noise_level=0.10,
            time_acceleration=1.0,
        ),
    )

    scenario_c = scenario_payload(
        scenario_key="scenario_c",
        label="DegradedPipeline",
        status="generated",
        scenario_category="Failure",
        candidate_date=scenario_b_date,
        selected_reason="Reutiliza o mesmo contexto físico do cenário B e muda apenas o perfil de degradação para testar a pipeline e os sensores sob falhas.",
        fault_profile="measurement_and_transport_faults",
        base_scenario_id=scenario_b["scenario_id"],
        source_context=scenario_b["source_context"],
        daily_reference=scenario_b["daily_reference"],
        simulator_options=build_simulator_options(
            scenario_id=scenario_uuid("scenario_c"),
            scenario_name="Scenario C - Degraded Pipeline Proenca-a-Nova",
            scenario_description="Mesmo contexto físico do cenário B, mas com aumento de falhas e ruído para ensaiar cenários degradados.",
            scenario_category="Failure",
            candidate_date=scenario_b_date,
            base_temperature=scenario_b["simulator_options"]["BaseTemperature"],
            base_humidity=scenario_b["simulator_options"]["BaseHumidity"],
            base_wind_speed=scenario_b["simulator_options"]["BaseWindSpeed"],
            failure_rate=0.18,
            noise_level=0.16,
            time_acceleration=1.0,
        ),
        future_fault_injections=[
            "invalid_sensor_state",
            "delayed_delivery",
            "duplicate_delivery",
            "burst_outage",
            "out_of_order_delivery",
        ],
    )

    generated = {
        "area_id": "proenca-a-nova",
        "version": "0.2.0",
        "generated_at_utc": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "source_files": {
            "template": str(SCENARIO_TEMPLATE_JSON.relative_to(ROOT)).replace("\\", "/"),
            "weather_daily_reference": str(WEATHER_DAILY_REFERENCE_PARQUET.relative_to(ROOT)).replace("\\", "/"),
            "scenario_candidates": str(SCENARIO_CANDIDATES_PARQUET.relative_to(ROOT)).replace("\\", "/"),
        },
        "selection_policy": {
            "scenario_a": "dia seco de verão fora da shortlist de incêndios, próximo do centro da distribuição local de risco diário",
            "scenario_b": "melhor candidato histórico com prioridade para ligação direta à área piloto e contexto crítico de índices",
            "scenario_c": "mesma base física do cenário B com degradação explícita",
        },
        "scenarios": [scenario_a, scenario_b, scenario_c],
    }

    SCENARIO_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    write_json(SCENARIO_CATALOG_JSON, generated)
    write_json(SCENARIO_OUTPUT_DIR / "scenario_a.base.json", scenario_a)
    write_json(SCENARIO_OUTPUT_DIR / "scenario_b.high-risk.json", scenario_b)
    write_json(SCENARIO_OUTPUT_DIR / "scenario_c.degraded-pipeline.json", scenario_c)

    print(f"Wrote: {SCENARIO_CATALOG_JSON}")
    print(f"Wrote: {SCENARIO_OUTPUT_DIR / 'scenario_a.base.json'}")
    print(f"Wrote: {SCENARIO_OUTPUT_DIR / 'scenario_b.high-risk.json'}")
    print(f"Wrote: {SCENARIO_OUTPUT_DIR / 'scenario_c.degraded-pipeline.json'}")
    print(
        pd.DataFrame(
            [
                {
                    "scenario_key": item["scenario_key"],
                    "candidate_date": item["candidate_date"],
                    "label": item["label"],
                    "scenario_category": item["scenario_category"],
                    "fwi_reference": item["daily_reference"]["fwi_reference"],
                    "kbdi_reference": item["daily_reference"]["kbdi_reference"],
                }
                for item in generated["scenarios"]
            ]
        ).to_string(index=False)
    )


if __name__ == "__main__":
    main()
