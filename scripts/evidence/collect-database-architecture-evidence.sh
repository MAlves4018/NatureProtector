#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  scripts/evidence/collect-database-architecture-evidence.sh BASELINE_ID [options]

Options:
  --python PATH       Python executable (default: python3, then python)
  --run-id VALUE      Explicit Phase 3 run id
  --output PATH       Explicit evidence output directory
  --dsn VALUE         PostgreSQL DSN for optional read-only live inventory
  --require-live      Fail unless live PostgreSQL inventory passes

The script does not run Git, Docker, migrations, tests, cloud commands or the app.
EOF
}

if [[ $# -lt 1 ]]; then
  usage >&2
  exit 2
fi

BASELINE_ID="$1"
shift
PYTHON_EXECUTABLE=""
RUN_ID=""
OUTPUT=""
DSN=""
REQUIRE_LIVE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --python) PYTHON_EXECUTABLE="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    --output) OUTPUT="$2"; shift 2 ;;
    --dsn) DSN="$2"; shift 2 ;;
    --require-live) REQUIRE_LIVE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

if [[ -z "$PYTHON_EXECUTABLE" ]]; then
  if command -v python3 >/dev/null 2>&1; then
    PYTHON_EXECUTABLE="$(command -v python3)"
  elif command -v python >/dev/null 2>&1; then
    PYTHON_EXECUTABLE="$(command -v python)"
  else
    echo "ERROR: Python 3 was not found." >&2
    exit 127
  fi
fi

ARGS=(
  "$SCRIPT_DIR/collect-database-architecture-evidence.py"
  --repo "$REPO_ROOT"
  --baseline-id "$BASELINE_ID"
)
[[ -n "$RUN_ID" ]] && ARGS+=(--run-id "$RUN_ID")
[[ -n "$OUTPUT" ]] && ARGS+=(--output "$OUTPUT")
[[ -n "$DSN" ]] && ARGS+=(--dsn "$DSN")
[[ "$REQUIRE_LIVE" -eq 1 ]] && ARGS+=(--require-live)

"$PYTHON_EXECUTABLE" "${ARGS[@]}"

if [[ -n "$OUTPUT" ]]; then
  EVIDENCE_ROOT="$OUTPUT"
elif [[ -n "$RUN_ID" ]]; then
  EVIDENCE_ROOT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/03-database/$RUN_ID"
else
  EVIDENCE_ROOT="$(cat "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/03-database/LATEST.txt")"
fi

VERIFY_ARGS=("$SCRIPT_DIR/verify-database-architecture-evidence.py" "$EVIDENCE_ROOT")
[[ "$REQUIRE_LIVE" -eq 1 ]] && VERIFY_ARGS+=(--require-live)
"$PYTHON_EXECUTABLE" "${VERIFY_ARGS[@]}"
