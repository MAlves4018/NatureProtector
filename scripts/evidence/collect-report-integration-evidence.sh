#!/usr/bin/env bash
set -euo pipefail
REPO="${1:-$(pwd)}"
BASELINE_ID="${2:-}"
RUN_ID="${3:-}"
if [[ -z "$BASELINE_ID" ]]; then
  echo "Usage: $0 <repo> <baseline-id> [run-id]" >&2
  exit 2
fi
ARGS=(--repo "$REPO" --baseline-id "$BASELINE_ID")
[[ -n "$RUN_ID" ]] && ARGS+=(--run-id "$RUN_ID")
python3 "$REPO/scripts/evidence/collect-report-integration-evidence.py" "${ARGS[@]}"
python3 "$REPO/scripts/evidence/verify-report-integration-evidence.py" --repo "$REPO" --baseline-id "$BASELINE_ID" ${RUN_ID:+--run-id "$RUN_ID"}
