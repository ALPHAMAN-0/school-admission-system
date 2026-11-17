import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { json, mockApi, problem, STUDENT_ID } from "../test/mockApi";
import { CLASS_POLL_MS, ClassListPage } from "./ClassListPage";

function schoolClass(available: number, total = 5) {
  return {
    id: "c_1",
    name: "Class 6 - Morning Shift",
    totalSeats: total,
    heldSeats: total - available,
    confirmedSeats: 0,
    availableSeats: available,
  };
}

describe("ClassListPage", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows seat counts and refreshes them every few seconds without a reload", async () => {
    let available = 5;
    mockApi({ "GET /classes": () => json([schoolClass(available)]) });
    render(<ClassListPage studentId={STUDENT_ID} navigate={vi.fn()} onNeedStudentId={vi.fn()} />);

    const row = await screen.findByTestId("class-row-c_1");
    expect(within(row).getByRole("rowheader")).toHaveTextContent("Class 6 - Morning Shift");
    expect(row).toHaveTextContent("৫০০৫"); // total ৫, held ০, confirmed ০, available ৫

    available = 2;
    await act(() => vi.advanceTimersByTimeAsync(CLASS_POLL_MS));

    expect(screen.getByTestId("class-row-c_1")).toHaveTextContent("৫৩০২");
  });

  it("shows a friendly message instead of a blank page when the server is down", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    render(<ClassListPage studentId={STUDENT_ID} navigate={vi.fn()} onNeedStudentId={vi.fn()} />);

    expect(await screen.findByText(/সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না/)).toBeInTheDocument();
  });

  it("asks for the student ID before applying", async () => {
    mockApi({ "GET /classes": () => json([schoolClass(5)]) });
    const onNeedStudentId = vi.fn();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ClassListPage studentId={null} navigate={vi.fn()} onNeedStudentId={onNeedStudentId} />);

    await user.click(await screen.findByRole("button", { name: /আবেদন করুন/ }));

    expect(screen.getByRole("alert")).toHaveTextContent(/জন্ম নিবন্ধন নম্বর দিন/);
    expect(onNeedStudentId).toHaveBeenCalled();
  });

  it("applies and opens the application page", async () => {
    const server = mockApi({
      "GET /classes": () => json([schoolClass(5)]),
      "POST /classes/c_1/holds": () =>
        json({ holdId: "h_9", classId: "c_1", studentId: STUDENT_ID, status: "HELD", expiresAt: "2030-01-01T00:00:00Z" }, 201),
    });
    const navigate = vi.fn();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ClassListPage studentId={STUDENT_ID} navigate={navigate} onNeedStudentId={vi.fn()} />);

    await user.click(await screen.findByRole("button", { name: /আবেদন করুন/ }));

    expect(navigate).toHaveBeenCalledWith({ page: "application", holdId: "h_9" });
    expect(server.calls.find((call) => call.method === "POST")?.headers.get("X-Student-Id")).toBe(STUDENT_ID);
  });

  it("explains when the class filled up a moment ago", async () => {
    mockApi({
      "GET /classes": () => json([schoolClass(1)]),
      "POST /classes/c_1/holds": () => problem(409, "NO_SEATS_AVAILABLE"),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ClassListPage studentId={STUDENT_ID} navigate={vi.fn()} onNeedStudentId={vi.fn()} />);

    await user.click(await screen.findByRole("button", { name: /আবেদন করুন/ }));

    expect(await screen.findByRole("alert")).toHaveTextContent("দুঃখিত, এই ক্লাসে এখন কোনো খালি আসন নেই।");
  });

  it("disables applying when no seat is free", async () => {
    mockApi({ "GET /classes": () => json([schoolClass(0)]) });
    render(<ClassListPage studentId={STUDENT_ID} navigate={vi.fn()} onNeedStudentId={vi.fn()} />);

    expect(await screen.findByRole("button", { name: /আবেদন করুন/ })).toBeDisabled();
    expect(screen.getByText("আসন নেই")).toBeInTheDocument();
  });
});
