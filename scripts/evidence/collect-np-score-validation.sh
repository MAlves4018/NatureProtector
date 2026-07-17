#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-python3}"

BASELINE_ID=""
RUN_ID=""
OUTPUT=""
COLLECTOR_ARGS=("$@")
for ((i=0; i<${#COLLECTOR_ARGS[@]}; i++)); do
  if [[ "${COLLECTOR_ARGS[$i]}" == "--baseline-id" ]]; then BASELINE_ID="${COLLECTOR_ARGS[$((i+1))]}"; fi
  if [[ "${COLLECTOR_ARGS[$i]}" == "--run-id" ]]; then RUN_ID="${COLLECTOR_ARGS[$((i+1))]}"; fi
done
if [[ -z "$BASELINE_ID" ]]; then echo "--baseline-id is required" >&2; exit 2; fi
if [[ -z "$RUN_ID" ]]; then RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"; COLLECTOR_ARGS+=("--run-id" "$RUN_ID"); fi
OUTPUT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/09-np-score-validation/$RUN_ID"
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-np-score-validation.py" \
  --repo "$REPO_ROOT" --output "$OUTPUT" "${COLLECTOR_ARGS[@]}"
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-np-score-validation.py" "$OUTPUT" --require-complete
mkdir -p "$(dirname "$OUTPUT")"
printf '%s\n' "$RUN_ID" > "$(dirname "$OUTPUT")/LATEST.txt"
printf 'PHASE_9_OUTPUT=%s\n' "$OUTPUT"
