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

interface PollOptions {
  /** Fetch once more, then stop polling (e.g. the application reached a final status). */
  paused?: boolean;
}

/**
 * Fetches `fetcher` now and every `intervalMs`, without a page reload.
 * - Pass `key = null` to disable (e.g. no student ID yet).
 * - A tick is skipped while a request is still running, so a slow server is never cancelled by
 *   the next tick; polling also skips hidden tabs and catches up when the tab becomes visible.
 * - Each request gets an AbortSignal; responses that arrive after a key change are discarded.
 * - A failed refresh keeps the previous data and reports the error alongside it.
 */
export function usePolledQuery<T>(
  key: string | null,
  fetcher: (signal: AbortSignal) => Promise<T>,
  intervalMs: number,
  options: PollOptions = {},
): PolledQuery<T> {
  const paused = options.paused ?? false;
  const [state, setState] = useState<QueryState<T>>({ key: null });
  const [refreshToken, setRefreshToken] = useState(0);
  const fetcherRef = useRef(fetcher);

  useEffect(() => {
    fetcherRef.current = fetcher;
  });

  useEffect(() => {
    if (key === null) return;

    let disposed = false;
    let inFlight = false;
    const controller = new AbortController();

    const run = async () => {
      if (inFlight || disposed) return;
      inFlight = true;
      try {
        const data = await fetcherRef.current(controller.signal);
        if (!disposed) setState({ key, data, error: undefined, updatedAt: Date.now() });
      } catch (error) {
        if (disposed || isAbortError(error)) return;
        setState((previous) => (previous.key === key ? { ...previous, error } : { key, error }));
      } finally {
        inFlight = false;
      }
    };

    void run();
    if (paused) {
      return () => {
        disposed = true;
        controller.abort();
      };
    }

    const timer = window.setInterval(() => {
      if (document.visibilityState !== "hidden") void run();
    }, intervalMs);
    const onVisible = () => {
      if (document.visibilityState === "visible") void run();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      disposed = true;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      controller.abort();
    };
  }, [key, intervalMs, refreshToken, paused]);

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
