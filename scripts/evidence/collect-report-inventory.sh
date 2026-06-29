#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BASELINE_ID="${BASELINE_ID:-}"
OUTPUT_ROOT="${OUTPUT_ROOT:-}"
PYTHON_EXECUTABLE="${PYTHON_EXECUTABLE:-}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)
      REPO_ROOT="$2"; shift 2 ;;
    --baseline-id)
      BASELINE_ID="$2"; shift 2 ;;
    --output)
      OUTPUT_ROOT="$2"; shift 2 ;;
    --python)
      PYTHON_EXECUTABLE="$2"; shift 2 ;;
    -h|--help)
      cat <<'EOF'
Usage:
  bash scripts/evidence/collect-report-inventory.sh [options]

Options:
  --repo PATH          NatureProtector repository root.
  --baseline-id ID     Phase 0 campaign ID.
  --output PATH        Explicit evidence output directory.
  --python PATH        Explicit Python 3 executable.
EOF
      exit 0 ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2 ;;
  esac
done

if [[ -z "$BASELINE_ID" ]]; then
  latest_file="$REPO_ROOT/artifacts/report-evidence/LATEST.txt"
  if [[ -f "$latest_file" ]]; then
    latest_value="$(tr -d '\r\n' < "$latest_file")"
    BASELINE_ID="$(basename "$latest_value")"
  else
    echo "BASELINE_ID is required because artifacts/report-evidence/LATEST.txt was not found." >&2
    exit 2
  fi
fi

if [[ -z "$OUTPUT_ROOT" ]]; then
  OUTPUT_ROOT="$REPO_ROOT/artifacts/report-evidence/$BASELINE_ID/01-inventory"
fi

python_args=()
if [[ -n "$PYTHON_EXECUTABLE" ]]; then
  python_cmd="$PYTHON_EXECUTABLE"
elif command -v python3 >/dev/null 2>&1; then
  python_cmd="python3"
elif command -v python >/dev/null 2>&1; then
  python_cmd="python"
elif command -v py >/dev/null 2>&1; then
  python_cmd="py"
  python_args=(-3)
else
  echo "Python 3 was not found. Pass --python with the executable path." >&2
  exit 2
fi

"$python_cmd" "${python_args[@]}" "$SCRIPT_DIR/collect-report-inventory.py" \
  --repo "$REPO_ROOT" \
  --baseline-id "$BASELINE_ID" \
  --output "$OUTPUT_ROOT"

"$python_cmd" "${python_args[@]}" "$SCRIPT_DIR/verify-report-inventory.py" \
  --inventory-root "$OUTPUT_ROOT"
