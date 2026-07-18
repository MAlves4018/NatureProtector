#!/usr/bin/env python3
from __future__ import annotations
import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "scripts/common"))
from np_line_protocol import point, write_lines


def load_json(source):
    if isinstance(source, Path) or not str(source).startswith(("http://", "https://")):
        return json.loads(Path(source).read_text())
    req = urllib.request.Request(str(source), headers={"User-Agent": "NatureProtector/1.0"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode())


def station_index(value):
    if isinstance(value, dict) and value.get("type") == "FeatureCollection":
        records = value.get("features", [])
    elif isinstance(value, list):
        records = value
    elif isinstance(value, dict):
        records = value.get("data", value.get("stations", []))
    else:
        records = []
    out = {}
    for rec in records:
        props = rec.get("properties", {}) if isinstance(rec, dict) else {}
        geom = rec.get("geometry", {}) if isinstance(rec, dict) else {}
        coords = geom.get("coordinates") or []
        ident = (
            props.get("idEstacao") or props.get("id") or rec.get("idEstacao") or rec.get("id") or rec.get("stationId")
        )
        if ident is None:
            continue
        out[str(ident)] = {
            "idEstacao": ident,
            "localEstacao": props.get("localEstacao")
            or props.get("name")
            or rec.get("localEstacao")
            or rec.get("name"),
            "longitude": coords[0] if len(coords) > 1 else rec.get("longitude"),
            "latitude": coords[1] if len(coords) > 1 else rec.get("latitude"),
        }
    return out


def iter_observations(value):
    if isinstance(value, dict) and isinstance(value.get("data"), list):
        for r in value["data"]:
            ts = r.get("observedAt") or r.get("timestamp") or r.get("time")
            st = r.get("idEstacao") or r.get("stationId") or r.get("id")
            if ts and st is not None:
                yield str(ts), str(st), r
    elif isinstance(value, dict):
        for ts, stations in value.items():
            if isinstance(stations, dict):
                for sid, r in stations.items():
                    if isinstance(r, dict):
                        yield str(ts), str(sid), r


def normalize(observations, stations, metrics, watermarks):
    index = station_index(stations)
    lines = []
    pending = []
    new = dict(watermarks)
    for ts, sid, raw in iter_observations(observations):
        previous = new.get(sid)
        if previous and ts <= previous:
            continue
        station = index.get(sid, {})
        produced = False
        for key, d in metrics.items():
            value = raw.get(key)
            if value is None or value in {-99, "-99", "-"}:
                continue
            raw_hash = hashlib.sha256(json.dumps(raw, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
            lines.append(
                point(
                    "external_observation",
                    {"provider": "IPMA", "station_id": sid, "metric": d["name"], "source_kind": "EXTERNAL"},
                    {
                        "value": float(value),
                        "unit": d["unit"],
                        "station_name": station.get("localEstacao") or sid,
                        "latitude": station.get("latitude"),
                        "longitude": station.get("longitude"),
                        "observed_at": ts,
                        "raw_payload_hash": raw_hash,
                    },
                    ts,
                )
            )
            produced = True
        if produced:
            pending.append((sid, ts))
            new[sid] = max(ts, new.get(sid, ""))
    return lines, new, pending


def read_state(path):
    try:
        v = json.loads(path.read_text()) if path.is_file() else {}
        return {"watermarks": dict(v.get("watermarks", {}))}
    except (OSError, json.JSONDecodeError):
        corrupt = path.with_suffix(path.suffix + ".corrupt-" + str(int(time.time())))
        if path.exists():
            path.replace(corrupt)
        return {"watermarks": {}}


def atomic_write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    with tmp.open("w", encoding="utf-8") as f:
        json.dump(value, f, indent=2)
        f.write("\n")
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, path)


def with_retry(fn, attempts=5, base=1.0):
    last = None
    for i in range(attempts):
        try:
            return fn()
        except Exception as e:
            last = e
            if i + 1 < attempts:
                time.sleep(min(30, base * (2**i)))
    raise last


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", type=Path, default=REPO / "config/external-data/ipma.json")
    ap.add_argument("--observations")
    ap.add_argument("--stations")
    ap.add_argument("--state", type=Path, default=REPO / "data/runtime/ipma/cursor.json")
    ap.add_argument("--output", type=Path, default=REPO / "artifacts/external-data/ipma.lineprotocol")
    ap.add_argument("--write-influx", action="store_true")
    ap.add_argument("--once", action="store_true")
    a = ap.parse_args()
    cfg = json.loads(a.config.read_text())
    interval = int(cfg.get("pollSeconds", 300))
    while True:
        observations = with_retry(lambda: load_json(a.observations or cfg["observationsUrl"]))
        stations = with_retry(lambda: load_json(a.stations or cfg["stationsUrl"]))
        state = read_state(a.state)
        lines, new, pending = normalize(observations, stations, cfg["metrics"], state["watermarks"])
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(("\n".join(lines) + "\n") if lines else "")
        if a.write_influx and lines:
            with_retry(
                lambda: write_lines(
                    os.environ["INFLUXDB_URL"],
                    os.environ.get("INFLUXDB_DATABASE", "np_telemetry"),
                    os.environ["INFLUXDB_TOKEN"],
                    lines,
                )
            )
        # Commit cursor only after successful write.
        atomic_write_json(
            a.state,
            {
                "schemaVersion": 2,
                "updatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "watermarks": new,
            },
        )
        print(json.dumps({"provider": "IPMA", "points": len(lines), "stations": len(new)}))
        if a.once:
            return 0
        time.sleep(interval)


if __name__ == "__main__":
    raise SystemExit(main())
