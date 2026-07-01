#!/bin/sh
set -eu

origin="${FRONTEND_ORIGIN:?FRONTEND_ORIGIN is required}"
admin_user="${ADMIN_USERNAME:-admin}"
admin_password="${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"

case "$origin" in
  https://*|http://*)
    ;;
  *)
    echo "FRONTEND_ORIGIN must be an absolute HTTP(S) origin." >&2
    exit 2
    ;;
esac

origin="${origin%/}"
work="$(mktemp -d)"

created_user_id=""
token=""

cleanup() {
  if [ -n "${created_user_id:-}" ] && [ -n "${token:-}" ]; then
    curl \
      --silent \
      --show-error \
      --connect-timeout 15 \
      --max-time 60 \
      --request DELETE \
      --header "Authorization: Bearer $token" \
      "${origin}/api/users-roles/users/${created_user_id}" \
      >/dev/null 2>&1 || true
  fi

  rm -rf "$work"
}

trap cleanup EXIT HUP INT TERM

http_request() {
  stage="$1"
  method="$2"
  url="$3"
  output="$4"
  shift 4

  set +e

  status="$(
    curl \
      --silent \
      --show-error \
      --connect-timeout 15 \
      --max-time 60 \
      --request "$method" \
      --output "$output" \
      --write-out '%{http_code}' \
      "$@" \
      "$url"
  )"

  curl_rc=$?

  set -e

  if [ "$curl_rc" -ne 0 ]; then
    echo "FUNCTIONAL_SMOKE_STAGE=${stage}_TRANSPORT_FAILED" >&2
    echo "${stage}_CURL_EXIT=$curl_rc" >&2
    exit "$curl_rc"
  fi

  echo "${stage}_HTTP_STATUS=$status"

  case "$status" in
    2??)
      ;;
    *)
      echo "FUNCTIONAL_SMOKE_STAGE=${stage}_HTTP_FAILED" >&2
      echo "${stage}_HTTP_STATUS=$status" >&2
      exit 22
      ;;
  esac
}

http_request \
  "FRONTEND_HEALTH" \
  "GET" \
  "${origin}/healthz" \
  "$work/frontend-health.txt"

http_request \
  "FRONTEND_INDEX" \
  "GET" \
  "${origin}/" \
  "$work/frontend-index.html"

jq \
  -n \
  --arg username "$admin_user" \
  --arg password "$admin_password" \
  '{usernameOrEmail:$username,password:$password}' \
  >"$work/login.json"

http_request \
  "LOGIN" \
  "POST" \
  "${origin}/api/users-roles/login" \
  "$work/login-response.json" \
  --header 'Content-Type: application/json' \
  --data-binary @"$work/login.json"

if ! token="$(
  jq \
    -er \
    '.token | select(type=="string" and length>20)' \
    "$work/login-response.json" \
    2>/dev/null
)"; then
  echo "FUNCTIONAL_SMOKE_STAGE=LOGIN_TOKEN_MISSING" >&2
  exit 23
fi

echo "LOGIN_TOKEN_VALID=PASS"

http_request \
  "AREAS" \
  "GET" \
  "${origin}/api/control/areas" \
  "$work/areas.json"

if ! jq \
  -e \
  'type == "array" or type == "object"' \
  "$work/areas.json" \
  >/dev/null; then
  echo "FUNCTIONAL_SMOKE_STAGE=AREAS_JSON_INVALID" >&2
  exit 24
fi

echo "AREAS_JSON_VALID=PASS"

suffix="$(date +%s)-$$"
username="g81-smoke-${suffix}"
email="${username}@example.invalid"
password="G81-Smoke-${suffix}-A9!"

jq \
  -n \
  --arg username "$username" \
  --arg password "$password" \
  --arg email "$email" \
  '{
    username:$username,
    password:$password,
    email:$email,
    organization:"G8.1 ephemeral smoke",
    roles:["Sim"]
  }' \
  >"$work/user.json"

http_request \
  "USER_CREATE" \
  "POST" \
  "${origin}/api/users-roles/users" \
  "$work/user-created.json" \
  --header 'Content-Type: application/json' \
  --header "Authorization: Bearer $token" \
  --data-binary @"$work/user.json"

if ! created_user_id="$(
  jq \
    -er \
    '.id | select(type=="string" and length>20)' \
    "$work/user-created.json" \
    2>/dev/null
)"; then
  echo "FUNCTIONAL_SMOKE_STAGE=USER_CREATE_ID_MISSING" >&2
  exit 25
fi

echo "USER_CREATE_ID_VALID=PASS"

http_request \
  "USER_READ" \
  "GET" \
  "${origin}/api/users-roles/users/${created_user_id}" \
  "$work/user-read.json" \
  --header "Authorization: Bearer $token"

if ! jq \
  -e \
  --arg username "$username" \
  '.username == $username' \
  "$work/user-read.json" \
  >/dev/null; then
  echo "FUNCTIONAL_SMOKE_STAGE=USER_READ_CONTENT_INVALID" >&2
  exit 26
fi

echo "USER_READ_VALID=PASS"

http_request \
  "USER_DELETE" \
  "DELETE" \
  "${origin}/api/users-roles/users/${created_user_id}" \
  "$work/user-delete.txt" \
  --header "Authorization: Bearer $token"

created_user_id=""

echo "FUNCTIONAL_SMOKE=PASS"

jq \
  -n \
  --arg frontend_origin "$origin" \
  --arg checked_at "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  '{
    schema_version:1,
    status:"passed",
    frontend_origin:$frontend_origin,
    checked_at:$checked_at,
    checks:[
      "frontend-health",
      "frontend-index",
      "proxy-login",
      "jwt",
      "database-read",
      "database-write",
      "database-delete"
    ]
  }'
