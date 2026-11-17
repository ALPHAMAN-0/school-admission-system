import { useEffect, useState } from "react";
import { serverNow } from "../lib/clock";

/** Current time (on the server's clock) in ms, refreshed every `intervalMs`; drives countdowns. */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(serverNow);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(serverNow()), intervalMs);
    return () => window.clearInterval(timer);
  }, [intervalMs]);

  return now;
}
