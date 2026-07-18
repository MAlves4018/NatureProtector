"""Small InfluxDB 3 line-protocol helpers used by repository collectors."""
from __future__ import annotations

import json
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from typing import Any


def escape_measurement(value: str) -> str:
    return value.replace("\\", "\\\\").replace(" ", "\\ ").replace(",", "\\,")


def escape_tag(value: Any) -> str:
    return str(value).replace("\\", "\\\\").replace(" ", "\\ ").replace(",", "\\,").replace("=", "\\=")


def escape_field_key(value: str) -> str:
    return escape_measurement(value).replace("=", "\\=")


def field_value(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return f"{value}i"
    if isinstance(value, float):
        return repr(value)
    return json.dumps(str(value), ensure_ascii=False)


def timestamp_ns(value: str | datetime) -> int:
    moment = datetime.fromisoformat(value.replace("Z", "+00:00")) if isinstance(value, str) else value
    if moment.tzinfo is None:
        moment = moment.replace(tzinfo=timezone.utc)
    return int(moment.timestamp() * 1_000_000_000)


def point(measurement: str, tags: dict[str, Any], fields: dict[str, Any], timestamp: str | datetime) -> str:
    if not fields:
        raise ValueError("At least one field is required.")
    tagset = "".join(f",{escape_tag(key)}={escape_tag(value)}" for key, value in sorted(tags.items()) if value is not None)
    fieldset = ",".join(f"{escape_field_key(key)}={field_value(value)}" for key, value in sorted(fields.items()) if value is not None)
    return f"{escape_measurement(measurement)}{tagset} {fieldset} {timestamp_ns(timestamp)}"


def write_lines(url: str, database: str, token: str, lines: list[str], timeout: float = 30.0) -> int:
    if not lines:
        return 0
    query = urllib.parse.urlencode({"db": database, "precision": "nanosecond"})
    request = urllib.request.Request(
        f"{url.rstrip('/')}/api/v3/write_lp?{query}",
        data=("\n".join(lines) + "\n").encode("utf-8"),
        headers={"Authorization": f"Bearer {token}", "Content-Type": "text/plain; charset=utf-8"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        if not 200 <= response.status < 300:
            raise RuntimeError(f"InfluxDB write returned HTTP {response.status}")
    return len(lines)
