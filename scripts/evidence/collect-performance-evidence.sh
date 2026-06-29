#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  scripts/evidence/collect-performance-evidence.sh BASELINE_ID [options]

Options:
  --python PATH                  Python executable (default: python3, then python)
  --run-id VALUE                Explicit Phase 5 run id
  --output PATH                 Explicit evidence output directory
  --api-base-url URL            Backoffice API base URL
  --run-http                    Run the portable read-only HTTP workload
  --http-profile PROFILE        Calibration, B0, B1 or B2
  --include-web                 Include web root and /ui-v2 probes
  --run-microbenchmarks         Run BenchmarkDotNet directly through dotnet
  --benchmark-profile PROFILE   B0, B1 or B2
  --benchmark-run-directory DIR Ingest an existing benchmark output directory
  --http-run-directory DIR      Ingest an existing HTTP output directory
  --system-run-directory DIR    Ingest an existing system-capacity output directory
  --require-http                Fail verification unless current HTTP evidence passes
  --require-microbenchmarks     Fail verification unless microbenchmarks pass
  --require-system              Fail verification unless system workload evidence passes

Default mode is static and non-invasive. It does not start Docker, services,
databases, cloud resources, migrations or deployment actions.
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
ARGS=()
VERIFY_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --python) PYTHON_EXECUTABLE="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; ARGS+=(--run-id "$2"); shift 2 ;;
    --output) OUTPUT="$2"; ARGS+=(--output "$2"); shift 2 ;;
    --api-base-url|--http-profile|--benchmark-profile|--benchmark-run-directory|--http-run-directory|--system-run-directory)
      ARGS+=("$1" "$2"); shift 2 ;;
    --run-http|--include-web|--run-microbenchmarks)
      ARGS+=("$1"); shift ;;
    --require-http|--require-microbenchmarks|--require-system)
      ARGS+=("$1"); VERIFY_ARGS+=("$1"); shift ;;
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

"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/collect-performance-evidence.py" \
  --repo "$REPO_ROOT" \
  --baseline-id "$BASELINE_ID" \
  "${ARGS[@]}"

if [[ -n "$OUTPUT" ]]; then
  EVIDENCE_ROOT="$OUTPUT"
elif [[ -n "$RUN_ID" ]]; then
  EVIDENCE_ROOT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/05-performance/$RUN_ID"
else
  EVIDENCE_ROOT="$(cat "$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/05-performance/LATEST.txt")"
fi

"$PYTHON_EXECUTABLE" "$SCRIPT_DIR/verify-performance-evidence.py" "$EVIDENCE_ROOT" "${VERIFY_ARGS[@]}"
