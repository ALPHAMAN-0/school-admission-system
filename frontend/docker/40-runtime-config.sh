#!/bin/sh
# Runs on every container start (nginx image runs /docker-entrypoint.d/*.sh).
# Writes the backend URL from the API_BASE_URL environment variable into config.js,
# so the same image works against any backend without rebuilding.
set -eu

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"

# Escape for a JavaScript string literal.
escaped=$(printf '%s' "$API_BASE_URL" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/</\\u003c/g')

cat > /usr/share/nginx/html/config.js <<EOF
window.__APP_CONFIG__ = { apiBaseUrl: "${escaped}" };
EOF

echo "runtime-config: frontend will call the backend at ${API_BASE_URL}"
