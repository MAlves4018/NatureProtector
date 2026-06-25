#!/usr/bin/env bash
set -euo pipefail
: "${MODE:?MODE is required}"
: "${RABBITMQ_MANAGEMENT_ORIGIN:?RABBITMQ_MANAGEMENT_ORIGIN is required}"
: "${RABBITMQ_USER:?RABBITMQ_USER is required}"
: "${RABBITMQ_PASSWORD:?RABBITMQ_PASSWORD is required}"
: "${OTEL_HEALTH_ORIGIN:?OTEL_HEALTH_ORIGIN is required}"
queue="${RABBITMQ_QUEUE:-np.ingestion.readings}"
phase="${PROBE_PHASE:-G5}"
profile="${PROBE_PROFILE:-unspecified}"
out="${OUTPUT_PATH:-/tmp/probe.json}"
rabbit(){ curl -fsS --retry 12 --retry-delay 5 -u "$RABBITMQ_USER:$RABBITMQ_PASSWORD" "$RABBITMQ_MANAGEMENT_ORIGIN$1"; }
health=$(rabbit /api/health/checks/ready)
curl -fsS --retry 12 --retry-delay 5 "$OTEL_HEALTH_ORIGIN/" >/dev/null
queue_json=$(rabbit "/api/queues/%2F/${queue}")
consumers=$(jq -r '.consumers // 0' <<<"$queue_json")
messages=$(jq -r '.messages // 0' <<<"$queue_json")
if [[ "$MODE" == pre ]]; then
  (( consumers >= 1 )) || { echo "No Prevention consumer attached" >&2; exit 1; }
  jq -cn --arg phase "$phase" --arg profile "$profile" --arg mode "$MODE" --argjson consumers "$consumers" --argjson messages "$messages" --arg status "passed" \
    '{schema_version:1,phase:$phase,profile:$profile,mode:$mode,status:$status,rabbitmq:{ready:true,consumers:$consumers,messages:$messages}}' | tee "$out"
  exit 0
fi
: "${FRONTEND_ORIGIN:?FRONTEND_ORIGIN is required in post mode}"
: "${ADMIN_USERNAME:?ADMIN_USERNAME is required in post mode}"
: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required in post mode}"
: "${AREA_CODE:?AREA_CODE is required in post mode}"
login=$(curl -fsS -H 'content-type: application/json' -d "{\"usernameOrEmail\":\"$ADMIN_USERNAME\",\"password\":\"$ADMIN_PASSWORD\"}" "$FRONTEND_ORIGIN/api/users-roles/login")
token=$(jq -er '.token // .accessToken' <<<"$login")
latest=$(curl -fsS -H "Authorization: Bearer $token" "$FRONTEND_ORIGIN/api/control/runtime/runs/latest?areaCode=$AREA_CODE")
run_id=$(jq -er '.id // .simulationRunId // .runId' <<<"$latest")
audit=$(curl -fsS -H "Authorization: Bearer $token" "$FRONTEND_ORIGIN/api/control/runtime/runs/$run_id/audit")
timings=$(curl -fsS -H "Authorization: Bearer $token" "$FRONTEND_ORIGIN/api/control/runtime/runs/$run_id/timings")
accepted=$(jq -r '.acceptedReadings // .accepted_readings // 0' <<<"$audit")
risk=$(jq -r '.riskAssessments // .risk_assessments // 0' <<<"$audit")
(( accepted > 0 )) || { echo "No accepted readings" >&2; exit 1; }
(( risk > 0 )) || { echo "No risk assessments" >&2; exit 1; }
for _ in $(seq 1 24); do
  queue_json=$(rabbit "/api/queues/%2F/${queue}")
  messages=$(jq -r '.messages // 0' <<<"$queue_json")
  [[ "$messages" == 0 ]] && break
  sleep 5
done
[[ "$messages" == 0 ]] || { echo "Queue did not drain" >&2; exit 1; }
jq -cn --arg phase "$phase" --arg profile "$profile" --arg mode "$MODE" --arg status passed --arg run_id "$run_id" --argjson accepted "$accepted" --argjson risk "$risk" \
  --argjson consumers "$consumers" --argjson messages "$messages" --argjson latest "$latest" --argjson audit "$audit" --argjson timings "$timings" \
  '{schema_version:1,phase:$phase,profile:$profile,mode:$mode,status:$status,simulation_run_id:$run_id,rabbitmq:{ready:true,consumers:$consumers,messages:$messages,drained:($messages==0)},pipeline:{accepted_readings:$accepted,risk_assessments:$risk},latest:$latest,audit:$audit,timings:$timings}' | tee "$out"
