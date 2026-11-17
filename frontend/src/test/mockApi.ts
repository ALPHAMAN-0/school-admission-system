import { vi } from "vitest";

export const STUDENT_ID = "20152691234567890";

interface MockRequest {
  url: URL;
  method: string;
  headers: Headers;
  body: unknown;
}

type Handler = (request: MockRequest) => Response | Promise<Response>;

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });
}

export function problem(status: number, code: string, extra: Record<string, unknown> = {}): Response {
  return json({ status, code, title: code, ...extra }, status);
}

/**
 * Replaces global fetch with a tiny router keyed by "METHOD /path". Requests without a route
 * are recorded in `unhandled` so a test can fail loudly instead of passing silently.
 */
export function mockApi(routes: Record<string, Handler>) {
  const calls: MockRequest[] = [];
  const unhandled: string[] = [];

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(typeof input === "string" ? input : input instanceof URL ? input.href : input.url);
    const method = (init?.method ?? "GET").toUpperCase();
    const request: MockRequest = {
      url,
      method,
      headers: new Headers(init?.headers),
      body: typeof init?.body === "string" ? JSON.parse(init.body) : undefined,
    };
    calls.push(request);

    const handler = routes[`${method} ${url.pathname}`];
    if (!handler) {
      unhandled.push(`${method} ${url.pathname}`);
      return json({ code: "TEST_UNHANDLED" }, 599);
    }
    return handler(request);
  });

  vi.stubGlobal("fetch", fetchMock);
  return { calls, unhandled, fetchMock };
}
