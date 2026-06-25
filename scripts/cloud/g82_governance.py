#!/usr/bin/env python3
"""Pure semantic evaluation for G8.2 signed governance artifacts."""
from __future__ import annotations

import subprocess
from datetime import timedelta
from pathlib import Path
from typing import Any

from g82_common import parse_datetime, utc_now

REVIEW_NAMESPACE = "natureprotector-g82-independent-review"
AUTHORIZATION_NAMESPACE = "natureprotector-g82-production-authorization"


def verify_ssh_signature(
    data_path: str | Path,
    signature_path: str | Path,
    allowed_signers_path: str | Path,
    identity: str,
    namespace: str,
) -> tuple[bool, str]:
    if not identity:
        return False, "empty signer identity"
    try:
        process = subprocess.run(
            [
                "ssh-keygen",
                "-Y",
                "verify",
                "-f",
                str(allowed_signers_path),
                "-I",
                identity,
                "-n",
                namespace,
                "-s",
                str(signature_path),
            ],
            input=Path(data_path).read_bytes(),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
    except FileNotFoundError:
        return False, "ssh-keygen is not available"
    detail = (process.stdout + process.stderr).decode(errors="replace")[-2000:]
    return process.returncode == 0, detail


def evaluate_review_semantics(review: dict[str, Any]) -> dict[str, bool]:
    try:
        started = parse_datetime(review["started_at"], field="review.started_at")
        completed = parse_datetime(review["completed_at"], field="review.completed_at")
        timestamps = started <= completed <= utc_now() + timedelta(minutes=5)
    except Exception:
        timestamps = False

    identities = {
        review.get("reviewer_identity"),
        review.get("executor_identity"),
        review.get("authorizer_identity"),
    }
    identity_values = [value for value in identities if isinstance(value, str) and value]
    independence = len(identity_values) == 3

    findings = review.get("findings", [])
    conditions = review.get("conditions", [])
    open_findings = [item for item in findings if item.get("status") == "open"]
    blocking_findings = [
        item
        for item in open_findings
        if item.get("severity") in {"high", "critical"}
    ]
    due_dates_valid = True
    for condition in conditions:
        try:
            due_dates_valid = due_dates_valid and parse_datetime(
                condition["due_at"], field="condition.due_at"
            ) > utc_now()
        except Exception:
            due_dates_valid = False

    decision = review.get("decision")
    if decision == "ACCEPT":
        decision_semantics = not open_findings and not conditions
    elif decision == "ACCEPT_WITH_CONDITIONS":
        decision_semantics = (
            bool(conditions)
            and not blocking_findings
            and len(conditions) >= len(open_findings)
            and due_dates_valid
        )
    else:
        decision_semantics = False

    return {
        "signed_status": review.get("status") == "SIGNED",
        "timestamps": timestamps,
        "independence": independence,
        "no_blocking_findings": not blocking_findings,
        "decision_semantics": decision_semantics,
        "conditions_reduce_risk_only": all(
            item.get("risk_reduction_only") is True for item in conditions
        ),
        "not_authorized_or_deployed": review.get("production_authorized") is False
        and review.get("production_deployed") is False,
    }


def evaluate_authorization_semantics(
    request: dict[str, Any], decision: dict[str, Any]
) -> dict[str, bool]:
    now = utc_now()
    try:
        issued = parse_datetime(decision["issued_at"], field="decision.issued_at")
        expires = parse_datetime(decision["expires_at"], field="decision.expires_at")
        duration = (expires - issued).total_seconds() / 3600
        validity = (
            issued <= now < expires
            and 0 < duration <= 168
            and duration <= request["requested_validity_hours"]
        )
    except Exception:
        validity = False

    identities = {
        decision.get("authorizer_identity"),
        decision.get("independent_reviewer_identity"),
        decision.get("executor_identity"),
    }
    identity_values = [value for value in identities if isinstance(value, str) and value]
    independence = len(identity_values) == 3
    conditions = decision.get("conditions", [])
    conditions_ok = all(
        item.get("risk_reduction_only") is True for item in conditions
    )
    go_semantics = (
        decision.get("status") == "SIGNED"
        and decision.get("decision") == "GO"
        and decision.get("production_authorized") is True
        and decision.get("production_deployed") is False
        and bool(decision.get("rollback_owner"))
        and conditions_ok
    )
    return {
        "go_semantics": go_semantics,
        "independence": independence,
        "validity": validity,
        "conditions_reduce_risk_only": conditions_ok,
        "not_deployed": decision.get("production_deployed") is False,
    }
