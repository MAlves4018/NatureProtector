#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  collect-integrated-runtime-evidence.sh --baseline-id ID [options]

Options:
  --repo PATH                    Repository root (default: auto-detect)
  --python PATH                  Python executable (default: python3)
  --run-id ID                    Phase run id (default: current UTC)
  --api-base-url URL             Runtime API URL (default: http://localhost:5254)
  --live                         Execute current API B/C collection
  --require-live                 Fail if current API collection is unavailable
  --reset-runtime                Explicitly reset runtime-only state before B/C
  --postgres-dsn-env NAME        Environment variable containing PostgreSQL DSN
  --require-database-trace       Require current PostgreSQL trace in verifier

Credentials are read by the Python collector from:
  NATUREPROTECTOR_RUNTIME_BEARER_TOKEN
or:
  NATUREPROTECTOR_RUNTIME_USERNAME
  NATUREPROTECTOR_RUNTIME_PASSWORD
No credential value is persisted.
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO=""
PYTHON="python3"
BASELINE_ID=""
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
API_BASE_URL="http://localhost:5254"
LIVE=0
REQUIRE_LIVE=0
RESET_RUNTIME=0
POSTGRES_DSN_ENV=""
REQUIRE_DB_TRACE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo) REPO="$2"; shift 2 ;;
    --python) PYTHON="$2"; shift 2 ;;
    --baseline-id) BASELINE_ID="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    --api-base-url) API_BASE_URL="$2"; shift 2 ;;
    --live) LIVE=1; shift ;;
    --require-live) LIVE=1; REQUIRE_LIVE=1; shift ;;
    --reset-runtime) RESET_RUNTIME=1; shift ;;
    --postgres-dsn-env) POSTGRES_DSN_ENV="$2"; shift 2 ;;
    --require-database-trace) REQUIRE_DB_TRACE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$BASELINE_ID" ]]; then
  echo "--baseline-id is required" >&2
  exit 2
fi

if [[ -z "$REPO" ]]; then
  REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
fi

OUTPUT="$REPO/artifacts/report-evidence/$BASELINE_ID/04-runtime/$RUN_ID"
ARGS=(
  "$REPO/scripts/evidence/collect-integrated-runtime-evidence.py"
  --repo "$REPO"
  --baseline-id "$BASELINE_ID"
  --run-id "$RUN_ID"
  --output "$OUTPUT"
  --api-base-url "$API_BASE_URL"
)
[[ "$LIVE" -eq 1 ]] && ARGS+=(--live)
[[ "$REQUIRE_LIVE" -eq 1 ]] && ARGS+=(--require-live)
[[ "$RESET_RUNTIME" -eq 1 ]] && ARGS+=(--reset-runtime)
[[ -n "$POSTGRES_DSN_ENV" ]] && ARGS+=(--postgres-dsn-env "$POSTGRES_DSN_ENV")

"$PYTHON" "${ARGS[@]}"

VERIFY_ARGS=("$OUTPUT")
[[ "$REQUIRE_LIVE" -eq 1 ]] && VERIFY_ARGS+=(--require-live)
[[ "$REQUIRE_DB_TRACE" -eq 1 ]] && VERIFY_ARGS+=(--require-database-trace)
"$PYTHON" "$REPO/scripts/evidence/verify-integrated-runtime-evidence.py" "${VERIFY_ARGS[@]}"

printf 'PHASE_4_OUTPUT=%s\n' "$OUTPUT"
