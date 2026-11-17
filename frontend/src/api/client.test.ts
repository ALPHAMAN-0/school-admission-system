import { afterEach, describe, expect, it, vi } from "vitest";
import { json, mockApi, problem, STUDENT_ID } from "../test/mockApi";
import { api } from "./client";
import { ApiError, toUserMessage } from "./errors";

describe("api client", () => {
  afterEach(() => {
    delete window.__APP_CONFIG__;
  });

  it("turns an unreachable server into a friendly Bengali message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    const error = await api.listClasses().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe("NETWORK_ERROR");
    expect(toUserMessage(error)).toMatch(/সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না/);
  });

  it("maps API error codes to plain-language messages", async () => {
    mockApi({ "POST /classes/c_1/holds": () => problem(409, "NO_SEATS_AVAILABLE") });

    const error = await api.applyForSeat("c_1", STUDENT_ID).catch((caught: unknown) => caught);

    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).code).toBe("NO_SEATS_AVAILABLE");
    expect(toUserMessage(error)).toBe("দুঃখিত, এই ক্লাসে এখন কোনো খালি আসন নেই।");
  });

  it("handles a non-JSON error page from a proxy", async () => {
    mockApi({ "GET /classes": () => new Response("<html>Bad Gateway</html>", { status: 502 }) });

    const error = await api.listClasses().catch((caught: unknown) => caught);

    expect((error as ApiError).code).toBe("HTTP_502");
    expect(toUserMessage(error)).toMatch(/সার্ভারে একটি সমস্যা হয়েছে/);
  });

  it("treats a 200 that is not the expected JSON (e.g. a wrong API_BASE_URL) as an error, not empty data", async () => {
    mockApi({ "GET /classes": () => new Response("<!doctype html><html></html>", { status: 200 }) });
    const htmlError = await api.listClasses().catch((caught: unknown) => caught);

    mockApi({ "GET /me/holds": () => json({ unexpected: true }) });
    const shapeError = await api.myHolds(STUDENT_ID).catch((caught: unknown) => caught);

    expect((htmlError as ApiError).code).toBe("BAD_RESPONSE");
    expect((shapeError as ApiError).code).toBe("BAD_RESPONSE");
    expect(toUserMessage(htmlError)).toMatch(/অপ্রত্যাশিত উত্তর/);
  });

  it("sends the student ID header and uses the runtime API URL", async () => {
    window.__APP_CONFIG__ = { apiBaseUrl: "http://api.example.test:9000/" };
    const server = mockApi({ "DELETE /holds/h_1": () => new Response(null, { status: 204 }) });

    await expect(api.cancelHold("h_1", STUDENT_ID)).resolves.toBeUndefined();

    const [call] = server.calls;
    expect(call?.url.origin).toBe("http://api.example.test:9000");
    expect(call?.headers.get("X-Student-Id")).toBe(STUDENT_ID);
  });

  it("parses successful JSON responses", async () => {
    mockApi({ "GET /me/holds": () => json([{ holdId: "h_1", classId: "c_1", status: "HELD", expiresAt: "2026-11-01T04:10:00Z" }]) });

    const holds = await api.myHolds(STUDENT_ID);

    expect(holds).toHaveLength(1);
    expect(holds[0]?.status).toBe("HELD");
  });
});
