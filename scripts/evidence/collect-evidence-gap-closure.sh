#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-python3}"
BASELINE_ID="${1:?Usage: collect-evidence-gap-closure.sh BASELINE_ID [RUN_ID]}"
RUN_ID="${2:-$(date -u +%Y%m%dT%H%M%SZ)}"
OUTPUT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/11-evidence-gap-closure/$RUN_ID"
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-evidence-gap-closure.py" --repo "$REPO_ROOT" --baseline-id "$BASELINE_ID" --run-id "$RUN_ID" --output "$OUTPUT" --overwrite
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-evidence-gap-closure.py" "$OUTPUT"
