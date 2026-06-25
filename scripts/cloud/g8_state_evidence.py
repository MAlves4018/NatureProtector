from __future__ import annotations

import json
from dataclasses import dataclass
from json import JSONDecodeError
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class RequiredJsonResult:
    path: Path
    data: Any | None
    error: str | None


def _relative(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def load_required_json(path: Path, root: Path) -> RequiredJsonResult:
    relative = _relative(path, root)
    if not path.is_file():
        return RequiredJsonResult(path=path, data=None, error=f"missing:{relative}")

    try:
        return RequiredJsonResult(
            path=path,
            data=json.loads(path.read_text(encoding="utf-8")),
            error=None,
        )
    except JSONDecodeError as exc:
        return RequiredJsonResult(
            path=path,
            data=None,
            error=f"json:{relative}:{exc.msg} at line {exc.lineno} column {exc.colno}",
        )
    except OSError as exc:
        return RequiredJsonResult(path=path, data=None, error=f"json:{relative}:{exc}")


def validate_g8_state_document(document: Any, phase: str) -> list[str]:
    if not isinstance(document, dict):
        return [f"state:{phase}:not-object"]

    errors: list[str] = []
    expected = {
        "phase": phase,
        "cloud_provisioned": False,
        "production_authorized": False,
        "production_deployed": False,
    }
    for key, value in expected.items():
        if document.get(key) != value:
            errors.append(f"state:{phase}:{key}")

    schema_version = document.get("schema_version")
    if not isinstance(schema_version, int) or schema_version < 1:
        errors.append(f"state:{phase}:schema_version")

    if phase == "G8.1":
        if document.get("projects_created") is not False:
            errors.append("state:G8.1:projects_created")
        if document.get("runtime_validation_executed") is not False:
            errors.append("state:G8.1:runtime_validation_executed")
        if document.get("claim_limit") != "CLOUD_NOT_PROVISIONED_AND_PRODUCTION_NO_GO":
            errors.append("state:G8.1:claim_limit")
    elif phase == "G8.2":
        if document.get("runtime_qualification_executed") is not False:
            errors.append("state:G8.2:runtime_qualification_executed")
        if document.get("independent_review_executed") is not False:
            errors.append("state:G8.2:independent_review_executed")
        if document.get("cn_resources_allowed") is not False:
            errors.append("state:G8.2:cn_resources_allowed")
    else:
        errors.append(f"state:{phase}:unknown-phase")

    return errors
