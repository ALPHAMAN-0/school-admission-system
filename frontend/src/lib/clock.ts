let offsetMs = 0;

/**
 * Remembers how far the device clock is from the server clock, using the HTTP Date header of
 * API responses. Countdowns then follow the server, so a wrong phone/PC clock cannot make a
 * hold look EXPIRED early (or still payable late).
 */
export function recordServerDate(dateHeader: string | null, receivedAt = Date.now()): void {
  if (!dateHeader) return;
  const serverTime = Date.parse(dateHeader);
  if (Number.isNaN(serverTime)) return;
  // Date has 1-second resolution: +500 ms is the expected midpoint; ignore drift inside the noise.
  const difference = serverTime + 500 - receivedAt;
  offsetMs = Math.abs(difference) > 2000 ? difference : 0;
}

/** Current time on the server's clock, in ms. */
export function serverNow(): number {
  return Date.now() + offsetMs;
}

export function resetServerClock(): void {
  offsetMs = 0;
}
