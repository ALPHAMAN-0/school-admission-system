#!/bin/sh
# Runs on every container start (the nginx image runs /docker-entrypoint.d/*.sh).
# Writes the backend URL from the API_BASE_URL environment variable into config.js, so the same
# image works against any backend without rebuilding, and limits the page's CSP connect-src to
# that backend's origin.
set -eu

# Control characters (e.g. a stray newline) would break the generated files.
API_BASE_URL=$(printf '%s' "${API_BASE_URL:-http://localhost:8080}" | tr -d '\000-\037\177')

case "$API_BASE_URL" in
  http://* | https://*) ;;
  *) echo "runtime-config: WARNING: API_BASE_URL should start with http:// or https:// (got '$API_BASE_URL')" >&2 ;;
esac

# 1) config.js: escape for a JavaScript string literal.
escaped=$(printf '%s' "$API_BASE_URL" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/</\\u003c/g')
cat > /usr/share/nginx/html/config.js <<EOF
window.__APP_CONFIG__ = { apiBaseUrl: "${escaped}" };
EOF

# 2) CSP connect-src: the backend origin (scheme://host[:port]) only if it is a plain origin.
origin=$(printf '%s' "$API_BASE_URL" | sed -n 's|^\(https\{0,1\}://[^/?#]*\).*|\1|p')
if ! printf '%s' "$origin" | grep -Eq '^https?://[A-Za-z0-9.-]+(:[0-9]+)?$|^https?://\[[0-9A-Fa-f:.]+\](:[0-9]+)?$'; then
  origin=""
fi
printf 'set $api_origin "%s";\n' "$origin" > /etc/nginx/conf.d/api-origin.inc

echo "runtime-config: frontend will call the backend at ${API_BASE_URL} (CSP connect-src: 'self' ${origin})"
