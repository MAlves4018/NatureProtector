#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  scripts/evidence/collect-reliability-evidence.sh BASELINE_ID [options]

Options:
  --python PATH                       Python executable (default: python3, then python)
  --run-id VALUE                     Explicit Phase 6 run id
  --output PATH                      Explicit evidence output directory
  --api-base-url URL                 Backoffice API base URL
  --execute-p3                       Execute controlled validation P3
  --acknowledge-non-production       Confirm the target is Development/Evidence
  --p3-run-label VALUE               Explicit unique P3 run label
  --timeout-seconds N                P3 timeout
  --audit-directory PATH             Ingest PostgreSQL audit outputs for the exact run label
  --require-p3                       Fail unless P3 execution was accepted
  --require-audit                    Fail unless the PostgreSQL P3 audit passes

Default mode is static and non-invasive. P3 execution publishes controlled
messages and writes runtime evidence, so it requires explicit non-production
acknowledgement and NP_RELIABILITY_AUTH_TOKEN.
EOF
}

if [[ $# -lt 1 ]]; then usage >&2; exit 2; fi
BASELINE_ID="$1"; shift
PYTHON_EXECUTABLE=""; RUN_ID=""; OUTPUT=""; ARGS=(); VERIFY_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --python) PYTHON_EXECUTABLE="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; ARGS+=(--run-id "$2"); shift 2 ;;
    --output) OUTPUT="$2"; ARGS+=(--output "$2"); shift 2 ;;
    --api-base-url|--p3-run-label|--timeout-seconds|--audit-directory) ARGS+=("$1" "$2"); shift 2 ;;
    --execute-p3|--acknowledge-non-production) ARGS+=("$1"); shift ;;
    --require-p3|--require-audit) ARGS+=("$1"); VERIFY_ARGS+=("$1"); shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
if [[ -z "$PYTHON_EXECUTABLE" ]]; then
  if command -v python3 >/dev/null 2>&1; then PYTHON_EXECUTABLE="$(command -v python3)";
  elif command -v python >/dev/null 2>&1; then PYTHON_EXECUTABLE="$(command -v python)";
  else echo "ERROR: Python 3 was not found." >&2; exit 127; fi
fi
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-reliability-evidence.py" --repo "$REPO_ROOT" --baseline-id "$BASELINE_ID" "${ARGS[@]}"
if [[ -n "$OUTPUT" ]]; then EVIDENCE_ROOT="$OUTPUT";
elif [[ -n "$RUN_ID" ]]; then EVIDENCE_ROOT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/06-reliability/$RUN_ID";
else EVIDENCE_ROOT="$(cat "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/06-reliability/LATEST.txt")"; fi
"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-reliability-evidence.py" "$EVIDENCE_ROOT" "${VERIFY_ARGS[@]}"
