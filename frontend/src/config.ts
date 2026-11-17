declare global {
  interface Window {
    __APP_CONFIG__?: { apiBaseUrl?: string };
  }
}

const DEFAULT_API_BASE_URL = "http://localhost:8080";

/**
 * Backend base URL. Order of precedence:
 * 1. runtime `window.__APP_CONFIG__.apiBaseUrl` (written by the Docker container from API_BASE_URL),
 * 2. build-time `VITE_API_BASE_URL`,
 * 3. http://localhost:8080.
 */
export function getApiBaseUrl(): string {
  const runtime = window.__APP_CONFIG__?.apiBaseUrl?.trim();
  const buildTime = import.meta.env.VITE_API_BASE_URL?.trim();
  return (runtime || buildTime || DEFAULT_API_BASE_URL).replace(/\/+$/, "");
}
