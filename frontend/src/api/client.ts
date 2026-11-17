import { getApiBaseUrl } from "../config";
import { ApiError, NETWORK_ERROR, isAbortError } from "./errors";
import type { Hold, ProblemDetails, SchoolClass } from "./types";

interface RequestOptions {
  method?: "GET" | "POST" | "DELETE";
  body?: unknown;
  studentId?: string;
  signal?: AbortSignal;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
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

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null; // e.g. an HTML error page from a proxy
    }
  }

  if (!response.ok) {
    const problem = data !== null && typeof data === "object" ? (data as ProblemDetails) : {};
    throw new ApiError(response.status, problem.code ?? `HTTP_${response.status}`, problem);
  }

  return data as T;
}

const segment = (value: string) => encodeURIComponent(value);

export const api = {
  listClasses: (signal?: AbortSignal) => request<SchoolClass[]>("/classes", { signal }),

  createClass: (name: string, totalSeats: number) =>
    request<SchoolClass>("/classes", { method: "POST", body: { name, totalSeats } }),

  applyForSeat: (classId: string, studentId: string) =>
    request<Hold>(`/classes/${segment(classId)}/holds`, { method: "POST", studentId }),

  getHold: (holdId: string, signal?: AbortSignal) => request<Hold>(`/holds/${segment(holdId)}`, { signal }),

  confirmHold: (holdId: string, studentId: string) =>
    request<Pick<Hold, "holdId" | "status">>(`/holds/${segment(holdId)}/confirm`, { method: "POST", studentId }),

  cancelHold: (holdId: string, studentId: string) =>
    request<void>(`/holds/${segment(holdId)}`, { method: "DELETE", studentId }),

  myHolds: (studentId: string, signal?: AbortSignal) => request<Hold[]>("/me/holds", { studentId, signal }),
};
