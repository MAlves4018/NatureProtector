#!/bin/sh
set -eu

origin="${BACKOFFICE_API_ORIGIN:-}"
case "$origin" in
  http://*|https://*) ;;
  *) echo "BACKOFFICE_API_ORIGIN must be an absolute http(s) origin." >&2; exit 1 ;;
esac

case "$origin" in
  *'|'*|*';'*|*'$'*|*'`'*) echo "BACKOFFICE_API_ORIGIN contains unsupported characters." >&2; exit 1 ;;
esac
without_newlines=$(printf '%s' "$origin" | tr -d '\r\n')
if [ "$without_newlines" != "$origin" ]; then
  echo "BACKOFFICE_API_ORIGIN contains unsupported line breaks." >&2
  exit 1
fi

escaped=$(printf '%s' "$origin" | sed 's/[&|]/\\&/g')
sed "s|__BACKOFFICE_API_ORIGIN__|$escaped|g" \
  /opt/natureprotector/nginx.template.conf > /tmp/natureprotector-default.conf

exec nginx -c /opt/natureprotector/nginx-main.conf -g 'daemon off;'
