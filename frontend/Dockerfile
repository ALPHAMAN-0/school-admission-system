# syntax=docker/dockerfile:1

# ---- build: install exact locked versions (no install scripts) and build static files ----
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY . .
RUN npm run build

# ---- test: `docker compose --profile test run --rm frontend-tests` ----
FROM build AS test
CMD ["npm", "test"]

# ---- runtime: nginx serves the static app ----
FROM nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Writes config.js from API_BASE_URL every time the container starts (no rebuild needed).
COPY --chmod=0755 docker/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
