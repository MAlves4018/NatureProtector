from __future__ import annotations

import math
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
BASELINE_DIR = ROOT / "data" / "baseline" / "areas" / "proenca-a-nova"
WEATHER_REFERENCE_PARQUET = BASELINE_DIR / "weather_reference.parquet"
WEATHER_DAILY_REFERENCE_PARQUET = BASELINE_DIR / "weather_daily_reference.parquet"
WEATHER_DAILY_REFERENCE_CSV = BASELINE_DIR / "weather_daily_reference.csv"

DMC_DAY_LENGTH_FACTORS = [6.5, 7.5, 9.0, 12.8, 13.9, 13.9, 12.4, 10.9, 9.4, 8.0, 7.0, 6.0]
DC_DRYING_FACTORS = [-1.6, -1.6, -1.6, 0.9, 3.8, 5.8, 6.4, 5.0, 2.4, 0.4, -1.6, -1.6]

INITIAL_FFMC = 85.0
INITIAL_DMC = 6.0
INITIAL_DC = 15.0
INITIAL_KBDI = 0.0
NOON_TARGET_HOUR = 12


def percentile_rank(series: pd.Series) -> pd.Series:
    valid = series.astype("Float64")
    return valid.rank(method="average", pct=True).astype("Float64")


def classify_fwi(value: float | pd.NA) -> str:
    if pd.isna(value):
        return "missing"
    if value < 5:
        return "low"
    if value < 12:
        return "moderate"
    if value < 21:
        return "high"
    if value < 38:
        return "very_high"
    return "extreme"


def classify_kbdi(value: float | pd.NA) -> str:
    if pd.isna(value):
        return "missing"
    if value < 200:
        return "low"
    if value < 400:
        return "moderate"
    if value < 600:
        return "high"
    return "extreme"


def classify_index_context(value: float | pd.NA) -> str:
    if pd.isna(value):
        return "missing_index_context"
    if value >= 0.9:
        return "critical_index_context"
    if value >= 0.7:
        return "elevated_index_context"
    if value <= 0.3:
        return "mild_index_context"
    return "moderate_index_context"


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def compute_ffmc(temp_c: float, rh_pct: float, wind_kmh: float, rain_mm: float, previous_ffmc: float) -> float:
    mo = 147.2 * (101.0 - previous_ffmc) / (59.5 + previous_ffmc)

    if rain_mm > 0.5:
        effective_rain = rain_mm - 0.5
        if mo > 150.0:
            mo = (
                mo
                + 42.5
                * effective_rain
                * math.exp(-100.0 / (251.0 - mo))
                * (1.0 - math.exp(-6.93 / effective_rain))
                + 0.0015 * (mo - 150.0) ** 2 * math.sqrt(effective_rain)
            )
        else:
            mo = (
                mo
                + 42.5
                * effective_rain
                * math.exp(-100.0 / (251.0 - mo))
                * (1.0 - math.exp(-6.93 / effective_rain))
            )
        mo = min(mo, 250.0)

    ed = (
        0.942 * (rh_pct**0.679)
        + 11.0 * math.exp((rh_pct - 100.0) / 10.0)
        + 0.18 * (21.1 - temp_c) * (1.0 - math.exp(-0.115 * rh_pct))
    )

    if mo < ed:
        ew = (
            0.618 * (rh_pct**0.753)
            + 10.0 * math.exp((rh_pct - 100.0) / 10.0)
            + 0.18 * (21.1 - temp_c) * (1.0 - math.exp(-0.115 * rh_pct))
        )
        if mo <= ew:
            m = mo
        else:
            kl = (
                0.424 * (1.0 - (((100.0 - rh_pct) / 100.0) ** 1.7))
                + 0.0694 * math.sqrt(wind_kmh) * (1.0 - (((100.0 - rh_pct) / 100.0) ** 8))
            )
            kw = kl * 0.581 * math.exp(0.0365 * temp_c)
            m = ew - (ew - mo) / (10.0**kw)
    else:
        kl = (
            0.424 * (1.0 - ((rh_pct / 100.0) ** 1.7))
            + 0.0694 * math.sqrt(wind_kmh) * (1.0 - ((rh_pct / 100.0) ** 8))
        )
        kw = kl * 0.581 * math.exp(0.0365 * temp_c)
        m = ed + (mo - ed) / (10.0**kw)

    ffmc = 59.5 * (250.0 - m) / (147.2 + m)
    return clamp(ffmc, 0.0, 101.0)


def compute_dmc(temp_c: float, rh_pct: float, rain_mm: float, previous_dmc: float, month: int) -> float:
    temperature = max(temp_c, -1.1)
    dmc = previous_dmc

    if rain_mm > 1.5:
        effective_rain = 0.92 * rain_mm - 1.27
        moisture_content = 20.0 + math.exp(5.6348 - previous_dmc / 43.43)
        if previous_dmc <= 33.0:
            b = 100.0 / (0.5 + 0.3 * previous_dmc)
        elif previous_dmc <= 65.0:
            b = 14.0 - 1.3 * math.log(previous_dmc)
        else:
            b = 6.2 * math.log(previous_dmc) - 17.2
        revised_moisture = moisture_content + (1000.0 * effective_rain) / (48.77 + b * effective_rain)
        dmc = 244.72 - 43.43 * math.log(revised_moisture - 20.0)
        dmc = max(dmc, 0.0)

    drying_rate = (
        1.894 * (temperature + 1.1) * (100.0 - rh_pct) * DMC_DAY_LENGTH_FACTORS[month - 1] * 0.000001
    )
    return max(dmc + 100.0 * max(drying_rate, 0.0), 0.0)


def compute_dc(temp_c: float, rain_mm: float, previous_dc: float, month: int) -> float:
    dc = previous_dc

    if rain_mm > 2.8:
        effective_rain = 0.83 * rain_mm - 1.27
        moisture_equivalent = 800.0 * math.exp(-previous_dc / 400.0)
        revised_moisture = moisture_equivalent + 3.937 * effective_rain
        dc = 400.0 * math.log(800.0 / revised_moisture)
        dc = max(dc, 0.0)

    temperature = max(temp_c, -2.8)
    drying = 0.36 * (temperature + 2.8) + DC_DRYING_FACTORS[month - 1]
    return max(dc + 0.5 * max(drying, 0.0), 0.0)


def compute_isi(ffmc: float, wind_kmh: float) -> float:
    moisture = 147.2 * (101.0 - ffmc) / (59.5 + ffmc)
    wind_function = math.exp(0.05039 * wind_kmh)
    fine_fuel_function = 91.9 * math.exp(-0.1386 * moisture) * (1.0 + (moisture**5.31) / 49_300_000.0)
    return 0.208 * wind_function * fine_fuel_function


def compute_bui(dmc: float, dc: float) -> float:
    if dmc <= 0.4 * dc:
        bui = (0.8 * dmc * dc) / (dmc + 0.4 * dc) if (dmc + 0.4 * dc) > 0 else 0.0
    else:
        bui = dmc - (1.0 - (0.8 * dc) / (dmc + 0.4 * dc)) * (0.92 + (0.0114 * dmc) ** 1.7)
    return max(bui, 0.0)


def compute_fwi(isi: float, bui: float) -> float:
    if bui <= 80.0:
        fuel_available = 0.626 * (bui**0.809) + 2.0
    else:
        fuel_available = 1000.0 / (25.0 + 108.64 * math.exp(-0.023 * bui))
    spread = 0.1 * isi * fuel_available
    if spread <= 1.0:
        return max(spread, 0.0)
    return math.exp(2.72 * ((0.434 * math.log(spread)) ** 0.647))


def compute_kbdi(temp_c: float, rain_mm: float, previous_kbdi: float, mean_annual_rain_in: float) -> float:
    kbdi = previous_kbdi
    effective_rain_mm = max(rain_mm - 5.08, 0.0)
    if effective_rain_mm > 0.0:
        kbdi = max(kbdi - (effective_rain_mm / 0.254), 0.0)

    temp_f = temp_c * 9.0 / 5.0 + 32.0
    drought_factor = (
        ((800.0 - kbdi) * (0.968 * math.exp(0.0486 * temp_f) - 8.30) * 0.001)
        / (1.0 + 10.88 * math.exp(-0.0441 * mean_annual_rain_in))
    )
    return clamp(kbdi + max(drought_factor, 0.0), 0.0, 800.0)


def build_noon_reference(hourly: pd.DataFrame) -> pd.DataFrame:
    hourly = hourly.copy()
    hourly["date_local"] = pd.to_datetime(hourly["date_local"])
    hourly["time_local"] = pd.to_datetime(hourly["time_local"])
    hourly["hour_local"] = pd.to_numeric(hourly["hour_local"], errors="coerce")
    hourly["noon_distance"] = (hourly["hour_local"] - NOON_TARGET_HOUR).abs()

    noon = (
        hourly.sort_values(["date_local", "noon_distance", "hour_local"])
        .groupby("date_local", as_index=False)
        .first()
    )

    return pd.DataFrame(
        {
            "date_local": noon["date_local"],
            "noon_time_local": noon["time_local"],
            "noon_observation_hour_local": noon["hour_local"].astype("Int64"),
            "noon_temperature_c": pd.Series(noon["temperature_c"], dtype="Float64"),
            "noon_relative_humidity_pct": pd.Series(noon["relative_humidity_pct"], dtype="Float64"),
            "noon_wind_speed_ms": pd.Series(noon["wind_speed_ms"], dtype="Float64"),
        }
    )


def main() -> None:
    if not WEATHER_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Weather reference parquet not found: {WEATHER_REFERENCE_PARQUET}")
    if not WEATHER_DAILY_REFERENCE_PARQUET.exists():
        raise FileNotFoundError(f"Weather daily reference parquet not found: {WEATHER_DAILY_REFERENCE_PARQUET}")

    hourly = pd.read_parquet(WEATHER_REFERENCE_PARQUET)
    daily = pd.read_parquet(WEATHER_DAILY_REFERENCE_PARQUET)

    daily["date_local"] = pd.to_datetime(daily["date_local"])
    noon = build_noon_reference(hourly)

    generated_columns = [
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
    daily = daily.drop(columns=[column for column in generated_columns if column in daily.columns], errors="ignore")
    daily = daily.merge(noon, on="date_local", how="left")
    daily["noon_wind_speed_kmh"] = (daily["noon_wind_speed_ms"] * 3.6).astype("Float64")

    annual_precipitation_mm = (
        daily.assign(year=daily["date_local"].dt.year)
        .groupby("year", as_index=False)
        .agg(annual_precipitation_mm=("precipitation_total_mm", "sum"))
    )
    mean_annual_rain_in = max(annual_precipitation_mm["annual_precipitation_mm"].mean() / 25.4, 0.1)

    ffmc = INITIAL_FFMC
    dmc = INITIAL_DMC
    dc = INITIAL_DC
    kbdi = INITIAL_KBDI

    rows = daily.sort_values("date_local").reset_index(drop=True)
    ffmc_values: list[float] = []
    dmc_values: list[float] = []
    dc_values: list[float] = []
    isi_values: list[float] = []
    bui_values: list[float] = []
    fwi_values: list[float] = []
    kbdi_values: list[float] = []
    input_temp_values: list[float] = []
    input_rh_values: list[float] = []
    input_wind_values: list[float] = []
    input_rain_values: list[float] = []

    for row in rows.itertuples(index=False):
        month = pd.Timestamp(row.date_local).month
        temp_c = float(row.noon_temperature_c if pd.notna(row.noon_temperature_c) else row.temperature_max_c)
        rh_pct = float(
            row.noon_relative_humidity_pct
            if pd.notna(row.noon_relative_humidity_pct)
            else row.relative_humidity_min_pct
        )
        wind_kmh = float(row.noon_wind_speed_kmh if pd.notna(row.noon_wind_speed_kmh) else row.wind_speed_max_ms * 3.6)
        rain_mm = float(row.precipitation_total_mm if pd.notna(row.precipitation_total_mm) else 0.0)

        temp_c = clamp(temp_c, -20.0, 60.0)
        rh_pct = clamp(rh_pct, 0.0, 100.0)
        wind_kmh = max(wind_kmh, 0.0)
        rain_mm = max(rain_mm, 0.0)

        ffmc = compute_ffmc(temp_c, rh_pct, wind_kmh, rain_mm, ffmc)
        dmc = compute_dmc(temp_c, rh_pct, rain_mm, dmc, month)
        dc = compute_dc(temp_c, rain_mm, dc, month)
        isi = compute_isi(ffmc, wind_kmh)
        bui = compute_bui(dmc, dc)
        fwi = compute_fwi(isi, bui)
        kbdi = compute_kbdi(float(row.temperature_max_c), rain_mm, kbdi, mean_annual_rain_in)

        input_temp_values.append(round(temp_c, 3))
        input_rh_values.append(round(rh_pct, 3))
        input_wind_values.append(round(wind_kmh, 3))
        input_rain_values.append(round(rain_mm, 3))
        ffmc_values.append(round(ffmc, 3))
        dmc_values.append(round(dmc, 3))
        dc_values.append(round(dc, 3))
        isi_values.append(round(isi, 3))
        bui_values.append(round(bui, 3))
        fwi_values.append(round(fwi, 3))
        kbdi_values.append(round(kbdi, 3))

    rows["fwi_input_temperature_c"] = pd.Series(input_temp_values, dtype="Float64")
    rows["fwi_input_relative_humidity_pct"] = pd.Series(input_rh_values, dtype="Float64")
    rows["fwi_input_wind_kmh"] = pd.Series(input_wind_values, dtype="Float64")
    rows["fwi_input_rain_mm"] = pd.Series(input_rain_values, dtype="Float64")
    rows["ffmc_reference"] = pd.Series(ffmc_values, dtype="Float64")
    rows["dmc_reference"] = pd.Series(dmc_values, dtype="Float64")
    rows["dc_reference"] = pd.Series(dc_values, dtype="Float64")
    rows["isi_reference"] = pd.Series(isi_values, dtype="Float64")
    rows["bui_reference"] = pd.Series(bui_values, dtype="Float64")
    rows["fwi_reference"] = pd.Series(fwi_values, dtype="Float64")
    rows["kbdi_reference"] = pd.Series(kbdi_values, dtype="Float64")
    rows["fwi_reference_percentile"] = percentile_rank(rows["fwi_reference"])
    rows["kbdi_reference_percentile"] = percentile_rank(rows["kbdi_reference"])
    rows["fire_index_reference_score"] = (
        rows["fwi_reference_percentile"] * 0.7 + rows["kbdi_reference_percentile"] * 0.3
    ).round(6)
    rows["fire_index_reference_percentile"] = percentile_rank(rows["fire_index_reference_score"])
    rows["fwi_reference_class"] = rows["fwi_reference"].apply(classify_fwi).astype("string")
    rows["kbdi_reference_class"] = rows["kbdi_reference"].apply(classify_kbdi).astype("string")
    rows["fire_index_reference_kind"] = rows["fire_index_reference_percentile"].apply(classify_index_context).astype("string")

    rows.to_parquet(WEATHER_DAILY_REFERENCE_PARQUET, index=False)
    rows.to_csv(WEATHER_DAILY_REFERENCE_CSV, index=False)

    print(f"Rows: {len(rows)}")
    print(f"Mean annual precipitation used for KBDI: {annual_precipitation_mm['annual_precipitation_mm'].mean():.2f} mm")
    print(f"Wrote: {WEATHER_DAILY_REFERENCE_PARQUET}")
    print(f"Wrote: {WEATHER_DAILY_REFERENCE_CSV}")
    print(
        rows[
            [
                "date_local",
                "noon_observation_hour_local",
                "fwi_reference",
                "fwi_reference_class",
                "kbdi_reference",
                "kbdi_reference_class",
                "fire_index_reference_kind",
            ]
        ].tail(12).to_string(index=False)
    )


if __name__ == "__main__":
    main()
