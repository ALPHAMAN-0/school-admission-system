import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { json, mockApi, problem, STUDENT_ID } from "../test/mockApi";
import { ApplicationPage } from "./ApplicationPage";

function renderPage(holdId: string) {
  return render(
    <ApplicationPage holdId={holdId} studentId={STUDENT_ID} navigate={vi.fn()} onNeedStudentId={vi.fn()} />,
  );
}

function hold(holdId: string, status: string, expiresAt: string) {
  return { holdId, classId: "c_1", className: "Class 6 - Morning Shift", status, expiresAt };
}

describe("ApplicationPage", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows EXPIRED and disables the pay button when time runs out, without a page reload", async () => {
    const expiresAt = new Date(Date.now() + 3_000).toISOString();
    // The server still says HELD: the page must not wait for it.
    const server = mockApi({ "GET /holds/h_1": () => json(hold("h_1", "HELD", expiresAt)) });
    renderPage("h_1");

    const pay = await screen.findByRole("button", { name: "ভর্তি ফি দিন" });
    expect(pay).toBeEnabled();
    expect(screen.getByTestId("hold-status")).toHaveTextContent("HELD");
    expect(screen.getByTestId("countdown")).toHaveTextContent("০০:০৩");

    await act(() => vi.advanceTimersByTimeAsync(4_000));

    expect(screen.getByTestId("hold-status")).toHaveTextContent("EXPIRED");
    expect(screen.getByRole("button", { name: "ভর্তি ফি দিন" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "বাতিল" })).toBeDisabled();
    expect(server.unhandled).toEqual([]);
  });

  it("confirms the admission when the fee button is pressed", async () => {
    const expiresAt = new Date(Date.now() + 600_000).toISOString();
    let status = "HELD";
    const server = mockApi({
      "GET /holds/h_2": () => json(hold("h_2", status, expiresAt)),
      "POST /holds/h_2/confirm": () => {
        status = "CONFIRMED";
        return json({ holdId: "h_2", status: "CONFIRMED" });
      },
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderPage("h_2");

    await user.click(await screen.findByRole("button", { name: "ভর্তি ফি দিন" }));

    expect(await screen.findByText(/অভিনন্দন! ভর্তি ফি জমা হয়েছে/)).toBeInTheDocument();
    expect(await screen.findByText("CONFIRMED")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ভর্তি ফি দিন" })).toBeDisabled();
    const confirmCall = server.calls.find((call) => call.method === "POST");
    expect(confirmCall?.headers.get("X-Student-Id")).toBe(STUDENT_ID);
    expect(server.unhandled).toEqual([]);
  });

  it("sends the payment only once even if the button is double-clicked", async () => {
    const expiresAt = new Date(Date.now() + 600_000).toISOString();
    const server = mockApi({
      "GET /holds/h_5": () => json(hold("h_5", "HELD", expiresAt)),
      "POST /holds/h_5/confirm": () => json({ holdId: "h_5", status: "CONFIRMED" }),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderPage("h_5");

    await user.dblClick(await screen.findByRole("button", { name: "ভর্তি ফি দিন" }));
    await screen.findByText(/অভিনন্দন/);

    expect(server.calls.filter((call) => call.method === "POST")).toHaveLength(1);
    // The page shows CONFIRMED at once, before the next poll.
    expect(screen.getByTestId("hold-status")).toHaveTextContent("CONFIRMED");
  });

  it("follows the server clock when the device clock is wrong", async () => {
    // Device clock is 5 minutes slow: the server says the hold has 30 s left.
    const serverNow = Date.now() + 5 * 60_000;
    const expiresAt = new Date(serverNow + 30_000).toISOString();
    mockApi({
      "GET /holds/h_6": () =>
        new Response(JSON.stringify(hold("h_6", "HELD", expiresAt)), {
          status: 200,
          headers: { "Content-Type": "application/json", Date: new Date(serverNow).toUTCString() },
        }),
    });
    renderPage("h_6");

    await screen.findByRole("button", { name: "ভর্তি ফি দিন" });
    await act(() => vi.advanceTimersByTimeAsync(1_000));

    // Without the server clock this would show 05:29 left.
    expect(screen.getByTestId("countdown").textContent).toMatch(/^০০:[২৩][০-৯]$/);
  });

  it("explains in plain language when the server says the time has run out", async () => {
    const expiresAt = new Date(Date.now() + 600_000).toISOString();
    mockApi({
      "GET /holds/h_3": () => json(hold("h_3", "HELD", expiresAt)),
      "POST /holds/h_3/confirm": () => problem(409, "HOLD_EXPIRED"),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderPage("h_3");

    await user.click(await screen.findByRole("button", { name: "ভর্তি ফি দিন" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/ভর্তি ফি জমা দেওয়ার সময় শেষ হয়ে গেছে/);
  });

  it("asks before cancelling and then cancels", async () => {
    const expiresAt = new Date(Date.now() + 600_000).toISOString();
    let status = "HELD";
    const server = mockApi({
      "GET /holds/h_4": () => json(hold("h_4", status, expiresAt)),
      "DELETE /holds/h_4": () => {
        status = "CANCELLED";
        return new Response(null, { status: 204 });
      },
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderPage("h_4");

    await user.click(await screen.findByRole("button", { name: "বাতিল" }));
    await user.click(screen.getByRole("button", { name: "হ্যাঁ, বাতিল করুন" }));

    expect(await screen.findByText("CANCELLED")).toBeInTheDocument();
    expect(server.calls.filter((call) => call.method === "DELETE")).toHaveLength(1);
  });

  it("shows a clear message when the application does not exist", async () => {
    mockApi({ "GET /holds/h_missing": () => problem(404, "HOLD_NOT_FOUND") });
    renderPage("h_missing");

    expect(await screen.findByRole("alert")).toHaveTextContent("আবেদনটি খুঁজে পাওয়া যায়নি।");
  });
});
