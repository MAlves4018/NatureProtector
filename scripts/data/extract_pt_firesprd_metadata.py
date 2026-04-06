from __future__ import annotations

import re
import zipfile
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
PT_FIRESPRD_DIR = ROOT / "data" / "external" / "pt-firesprd"
PT_FIRESPRD_ZIP = PT_FIRESPRD_DIR / "ZENODO_PT-FireSprd_v2.0.zip"
PT_FIRESPRD_XLS = PT_FIRESPRD_DIR / "PT-FireSprd_Metadata_ZENODO.xls"
PT_FIRESPRD_PARQUET = PT_FIRESPRD_DIR / "pt_firesprd_metadata.parquet"
PT_FIRESPRD_CSV = PT_FIRESPRD_DIR / "pt_firesprd_metadata.csv"


def to_snake_case(value: str) -> str:
    value = value.strip().lower()
    value = value.replace("%", "pct")
    value = re.sub(r"[^a-z0-9]+", "_", value)
    value = re.sub(r"_+", "_", value)
    return value.strip("_")


def main() -> None:
    if not PT_FIRESPRD_ZIP.exists():
        raise FileNotFoundError(f"PT-FireSprd zip not found: {PT_FIRESPRD_ZIP}")

    if not PT_FIRESPRD_XLS.exists():
        with zipfile.ZipFile(PT_FIRESPRD_ZIP) as archive:
            archive.extract("PT-FireSprd_Metadata_ZENODO.xls", PT_FIRESPRD_DIR)

    frame = pd.read_excel(PT_FIRESPRD_XLS, sheet_name="Sheet1")
    frame.columns = [to_snake_case(column) for column in frame.columns]
    for date_column in ["start_date", "end_date"]:
        if date_column in frame.columns:
            frame[date_column] = pd.to_datetime(frame[date_column], errors="coerce")
    object_columns = [column for column in frame.columns if frame[column].dtype == "object"]
    for column in object_columns:
        frame[column] = frame[column].astype("string")
    frame["source_dataset"] = "PT-FireSprd_v2.0"

    frame.to_parquet(PT_FIRESPRD_PARQUET, index=False)
    frame.to_csv(PT_FIRESPRD_CSV, index=False)

    print(f"Rows: {len(frame)}")
    print(f"Wrote: {PT_FIRESPRD_PARQUET}")
    print(f"Wrote: {PT_FIRESPRD_CSV}")


if __name__ == "__main__":
    main()
