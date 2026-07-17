#!/usr/bin/env bash
set -Eeuo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
baseline=""
run_id="$(date -u +%Y%m%dT%H%M%SZ)"
require_ready=0
overwrite=0
while (($#)); do
  case "$1" in
    --repo) repo="$2"; shift 2 ;;
    --baseline-id) baseline="$2"; shift 2 ;;
    --run-id) run_id="$2"; shift 2 ;;
    --require-ready) require_ready=1; shift ;;
    --overwrite) overwrite=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$baseline" ]] || { echo "--baseline-id is required" >&2; exit 2; }
output="$repo/artifacts/report-evidence/$baseline/10-evidence-intelligence/$run_id"
args=("$repo/scripts/evidence/collect-evidence-intelligence.py" --repo "$repo" --baseline-id "$baseline" --run-id "$run_id" --output "$output")
((overwrite)) && args+=(--overwrite)
python3 "${args[@]}"
verify=("$repo/scripts/evidence/verify-evidence-intelligence.py" "$output")
((require_ready)) && verify+=(--require-ready)
python3 "${verify[@]}"
