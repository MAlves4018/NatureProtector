#!/usr/bin/env bash
set -euo pipefail
: "${GH_TOKEN:?GH_TOKEN is required}"
if [[ $# -ne 6 ]]; then
  echo "usage: $0 RUN_ID WORKFLOW_PATH SOURCE_COMMIT DEFAULT_BRANCH REPOSITORY OUTPUT" >&2
  exit 2
fi
run_id="$1"; workflow_path="$2"; source_commit="$3"; default_branch="$4"; repository="$5"; output="$6"
[[ "$run_id" =~ ^[1-9][0-9]*$ ]]
[[ "$source_commit" =~ ^[0-9a-f]{40}$ ]]
mkdir -p "$(dirname "$output")"
raw="${output%.json}-raw.json"
gh api "repos/${repository}/actions/runs/${run_id}" > "$raw"
python scripts/cloud/Validate-G82RunMetadata.py \
  --input "$raw" --expected-run-id "$run_id" --expected-workflow-path "$workflow_path" \
  --expected-source-commit "$source_commit" --expected-branch "$default_branch" \
  --expected-repository "$repository" --output "$output"
