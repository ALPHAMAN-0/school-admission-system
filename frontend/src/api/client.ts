import { getApiBaseUrl } from "../config";
import { recordServerDate } from "../lib/clock";
import { ApiError, BAD_RESPONSE, NETWORK_ERROR, isAbortError } from "./errors";
import type { Hold, ProblemDetails, SchoolClass } from "./types";

interface RequestOptions {
  method?: "GET" | "POST" | "DELETE";
  body?: unknown;
  studentId?: string;
  signal?: AbortSignal;
}

async function request(path: string, options: RequestOptions = {}): Promise<unknown> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (options.studentId) headers["X-Student-Id"] = options.studentId;

  let response: Response;
  try {
    response = await fetch(`${getApiBaseUrl()}${path}`, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch (error) {
    if (isAbortError(error)) throw error;
    // fetch only rejects when the server cannot be reached at all.
    throw new ApiError(0, NETWORK_ERROR);
  }

  recordServerDate(response.headers.get("Date"));
  if (response.status === 204) return undefined;

  const text = await response.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null; // e.g. an HTML page from a proxy or a wrong API_BASE_URL
    }
  }

  if (!response.ok) {
    const problem = isObject(data) ? (data as ProblemDetails) : {};
    throw new ApiError(response.status, problem.code ?? `HTTP_${response.status}`, problem);
  }

  if (data === null) throw new ApiError(response.status, BAD_RESPONSE);
  return data;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

/** Never hand the UI something it cannot render: a wrong shape becomes a friendly error. */
function expectArray<T>(value: unknown): T[] {
  if (!Array.isArray(value)) throw new ApiError(200, BAD_RESPONSE);
  return value as T[];
}

function expectObjectWith<T>(value: unknown, key: string): T {
  if (!isObject(value) || !(key in value)) throw new ApiError(200, BAD_RESPONSE);
  return value as T;
}

const segment = (value: string) => encodeURIComponent(value);

export const api = {
  listClasses: async (signal?: AbortSignal) =>
    expectArray<SchoolClass>(await request("/classes", { signal })),

  createClass: async (name: string, totalSeats: number) =>
    expectObjectWith<SchoolClass>(await request("/classes", { method: "POST", body: { name, totalSeats } }), "id"),

  applyForSeat: async (classId: string, studentId: string) =>
    expectObjectWith<Hold>(
      await request(`/classes/${segment(classId)}/holds`, { method: "POST", studentId }),
      "holdId",
    ),

  getHold: async (holdId: string, signal?: AbortSignal) =>
    expectObjectWith<Hold>(await request(`/holds/${segment(holdId)}`, { signal }), "holdId"),

  confirmHold: async (holdId: string, studentId: string) =>
    expectObjectWith<Pick<Hold, "holdId" | "status">>(
      await request(`/holds/${segment(holdId)}/confirm`, { method: "POST", studentId }),
      "status",
    ),

  cancelHold: async (holdId: string, studentId: string): Promise<void> => {
    await request(`/holds/${segment(holdId)}`, { method: "DELETE", studentId });
  },

  myHolds: async (studentId: string, signal?: AbortSignal) =>
    expectArray<Hold>(await request("/me/holds", { studentId, signal })),
};
