from __future__ import annotations

from pathlib import Path

import requests


ROOT = Path(__file__).resolve().parents[2]
IPMA_SAMPLES_DIR = ROOT / "data" / "external" / "ipma" / "api-samples"

FILES = {
    "stations.json": "https://api.ipma.pt/open-data/observation/meteorology/stations/stations.json",
    "observations.json": "https://api.ipma.pt/open-data/observation/meteorology/stations/observations.json",
    "obs-surface.geojson": "https://api.ipma.pt/open-data/observation/meteorology/stations/obs-surface.geojson",
}


def main() -> None:
    IPMA_SAMPLES_DIR.mkdir(parents=True, exist_ok=True)

    session = requests.Session()
    for filename, url in FILES.items():
        response = session.get(url, timeout=120)
        response.raise_for_status()
        target = IPMA_SAMPLES_DIR / filename
        target.write_bytes(response.content)
        print(f"Downloaded: {target}")


if __name__ == "__main__":
    main()
