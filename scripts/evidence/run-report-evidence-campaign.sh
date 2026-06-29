#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-python3}"

"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/run-report-evidence-campaign.py" --repo "$REPO_ROOT" "$@"

BASELINE_ID=""
RUN_ID=""
for ((i=1; i<=$#; i++)); do
  arg="${!i}"
  if [[ "$arg" == "--baseline-id" ]]; then j=$((i+1)); BASELINE_ID="${!j}"; fi
  if [[ "$arg" == "--run-id" ]]; then j=$((i+1)); RUN_ID="${!j}"; fi
done
if [[ -z "$RUN_ID" && -n "$BASELINE_ID" ]]; then
  RUN_ID="$(tr -d '\r\n' < "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/08-campaign/LATEST.txt")"
fi
if [[ -n "$BASELINE_ID" && -n "$RUN_ID" ]]; then
  "$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-report-evidence-campaign.py" \
    "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/08-campaign/$RUN_ID"
fi
