#!/usr/bin/env bash
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BASELINE_ID=""
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-python3}"
EXTRA_ARGS=()

usage() {
  cat <<'EOF'
Usage:
  bash scripts/evidence/collect-test-quality-evidence.sh \
    --baseline-id baseline-YYYYMMDDTHHMMSSZ \
    [--run-id 20260627T150000Z] \
    [--python /path/to/python] \
    [collector options]

Collector options include --skip-backend, --skip-frontend, --skip-npm-ci,
--include-e2e, --no-restore, --no-build, --timeout-seconds N and --quiet.
EOF
}

while (($#)); do
  case "$1" in
    --baseline-id)
      BASELINE_ID="${2:?missing value for --baseline-id}"
      shift 2
      ;;
    --run-id)
      RUN_ID="${2:?missing value for --run-id}"
      shift 2
      ;;
    --python)
      PYTHON_EXECUTABLE="${2:?missing value for --python}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      EXTRA_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ -z "$BASELINE_ID" ]]; then
  if [[ -f "$REPO_ROOT/artifacts/report-evidence/LATEST.txt" ]]; then
    LATEST_VALUE="$(tr -d '\r\n' < "$REPO_ROOT/artifacts/report-evidence/LATEST.txt")"
    BASELINE_ID="$(basename "$LATEST_VALUE")"
  else
    BASELINE_ID="$(find "$REPO_ROOT/artifacts/report-evidence" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' 2>/dev/null | grep -E '^[0-9]{8}T[0-9]{6}Z$' | sort | tail -n 1 || true)"
  fi
fi

if [[ -z "$BASELINE_ID" ]]; then
  echo "ERROR: could not infer the Phase 0 baseline ID; pass --baseline-id explicitly." >&2
  exit 2
fi

OUTPUT_ROOT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/02-tests/$RUN_ID"

set +e
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-test-quality-evidence.py" \
  --repo "$REPO_ROOT" \
  --baseline-id "$BASELINE_ID" \
  --run-id "$RUN_ID" \
  "${EXTRA_ARGS[@]}"
COLLECT_EXIT=$?
set -e

if [[ ! -f "$OUTPUT_ROOT/phase2-summary.json" ]]; then
  echo "ERROR: collector did not generate $OUTPUT_ROOT/phase2-summary.json" >&2
  exit "$COLLECT_EXIT"
fi

set +e
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-test-quality-evidence.py" \
  --evidence-root "$OUTPUT_ROOT"
VERIFY_EXIT=$?
set -e

if ((COLLECT_EXIT != 0)); then
  exit "$COLLECT_EXIT"
fi
exit "$VERIFY_EXIT"
