#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-python3}"

"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/run-report-evidence-campaign.py" --repo "$REPO_ROOT" "$@"

BASELINE_ID=""
RUN_ID=""
EXECUTE_REQUESTED=0
for ((i=1; i<=$#; i++)); do
  arg="${!i}"
  if [[ "$arg" == "--baseline-id" ]]; then j=$((i+1)); BASELINE_ID="${!j}"; fi
  if [[ "$arg" == "--run-id" ]]; then j=$((i+1)); RUN_ID="${!j}"; fi
  if [[ "$arg" == "--execute" ]]; then EXECUTE_REQUESTED=1; fi
done
if [[ -z "$RUN_ID" && -n "$BASELINE_ID" ]]; then
  RUN_ID="$(tr -d '\r\n' < "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/08-campaign/LATEST.txt")"
fi
if [[ -n "$BASELINE_ID" && -n "$RUN_ID" ]]; then
  "$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-report-evidence-campaign.py" \
    "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/08-campaign/$RUN_ID"
  if [[ "$EXECUTE_REQUESTED" == "1" ]]; then
    PHASE10_OUTPUT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/10-evidence-intelligence/$RUN_ID"
    "$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-evidence-intelligence.py" \
      --repo "$REPO_ROOT" --baseline-id "$BASELINE_ID" --run-id "$RUN_ID" \
      --output "$PHASE10_OUTPUT" --overwrite
    "$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-evidence-intelligence.py" "$PHASE10_OUTPUT"
  fi
fi
