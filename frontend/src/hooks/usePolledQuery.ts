import { useCallback, useEffect, useRef, useState } from "react";
import { isAbortError } from "../api/errors";

interface QueryState<T> {
  key: string | null;
  data?: T;
  error?: unknown;
  updatedAt?: number;
}

export interface PolledQuery<T> {
  data: T | undefined;
  /** Error of the most recent attempt; `data` keeps the last good value meanwhile. */
  error: unknown;
  loading: boolean;
  updatedAt: number | undefined;
  refetch: () => void;
}

/**
 * Fetches `fetcher` now and every `intervalMs`, without a page reload.
 * - Pass `key = null` to pause (e.g. no student ID yet).
 * - Each request gets an AbortSignal; stale responses are discarded.
 * - A failed refresh keeps the previous data and reports the error alongside it.
 */
export function usePolledQuery<T>(
  key: string | null,
  fetcher: (signal: AbortSignal) => Promise<T>,
  intervalMs: number,
): PolledQuery<T> {
  const [state, setState] = useState<QueryState<T>>({ key: null });
  const [refreshToken, setRefreshToken] = useState(0);
  const fetcherRef = useRef(fetcher);

  useEffect(() => {
    fetcherRef.current = fetcher;
  });

  useEffect(() => {
    if (key === null) return;

    let disposed = false;
    let controller: AbortController | undefined;

    const run = async () => {
      controller?.abort();
      const current = new AbortController();
      controller = current;
      try {
        const data = await fetcherRef.current(current.signal);
        if (!disposed && controller === current) {
          setState({ key, data, error: undefined, updatedAt: Date.now() });
        }
      } catch (error) {
        if (disposed || isAbortError(error) || controller !== current) return;
        setState((previous) =>
          previous.key === key ? { ...previous, error } : { key, error },
        );
      }
    };

    void run();
    const timer = window.setInterval(() => void run(), intervalMs);
    const onVisible = () => {
      if (document.visibilityState === "visible") void run();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      disposed = true;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      controller?.abort();
    };
  }, [key, intervalMs, refreshToken]);

  const refetch = useCallback(() => setRefreshToken((token) => token + 1), []);

  // Results that belong to a previous key (e.g. a different student) are never shown.
  const current = state.key === key ? state : undefined;
  return {
    data: current?.data,
    error: current?.error,
    loading: key !== null && current === undefined,
    updatedAt: current?.updatedAt,
    refetch,
  };
}
