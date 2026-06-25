#!/bin/sh
set -eu

origin="${FRONTEND_ORIGIN:?FRONTEND_ORIGIN is required}"
admin_user="${ADMIN_USERNAME:-admin}"
admin_password="${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"
case "$origin" in
  https://*|http://*) ;;
  *) echo "FRONTEND_ORIGIN must be an absolute HTTP(S) origin." >&2; exit 2 ;;
esac
origin="${origin%/}"
work="$(mktemp -d)"
cleanup() {
  if [ -n "${created_user_id:-}" ]; then
    curl -fsS -X DELETE \
      -H "Authorization: Bearer ${token:-invalid}" \
      "${origin}/api/users-roles/users/${created_user_id}" >/dev/null 2>&1 || true
  fi
  rm -rf "$work"
}
trap cleanup EXIT INT TERM

curl -fsS "${origin}/healthz" >"$work/frontend-health.txt"
curl -fsS "${origin}/" >"$work/frontend-index.html"

jq -n --arg username "$admin_user" --arg password "$admin_password" \
  '{usernameOrEmail:$username,password:$password}' >"$work/login.json"
curl -fsS -X POST -H 'Content-Type: application/json' \
  --data @"$work/login.json" \
  "${origin}/api/users-roles/login" >"$work/login-response.json"
token="$(jq -er '.token | select(type=="string" and length>20)' "$work/login-response.json")"

curl -fsS "${origin}/api/control/areas" >"$work/areas.json"
jq -e 'type == "array" or type == "object"' "$work/areas.json" >/dev/null

suffix="$(date +%s)-$$"
username="g81-smoke-${suffix}"
email="${username}@example.invalid"
password="G81-Smoke-${suffix}-A9!"
jq -n \
  --arg username "$username" \
  --arg password "$password" \
  --arg email "$email" \
  '{username:$username,password:$password,email:$email,organization:"G8.1 ephemeral smoke",roles:["Sim"]}' \
  >"$work/user.json"
curl -fsS -X POST \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $token" \
  --data @"$work/user.json" \
  "${origin}/api/users-roles/users" >"$work/user-created.json"
created_user_id="$(jq -er '.id | select(type=="string" and length>20)' "$work/user-created.json")"

curl -fsS \
  -H "Authorization: Bearer $token" \
  "${origin}/api/users-roles/users/${created_user_id}" >"$work/user-read.json"
jq -e --arg username "$username" '.username == $username' "$work/user-read.json" >/dev/null

curl -fsS -X DELETE \
  -H "Authorization: Bearer $token" \
  "${origin}/api/users-roles/users/${created_user_id}" >/dev/null
created_user_id=""

jq -n \
  --arg frontend_origin "$origin" \
  --arg checked_at "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  '{schema_version:1,status:"passed",frontend_origin:$frontend_origin,checked_at:$checked_at,checks:["frontend-health","frontend-index","proxy-login","jwt","database-read","database-write","database-delete"]}'
