import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { App } from "./App";
import { STUDENT_ID_STORAGE_KEY } from "./hooks/useStudentId";
import { json, mockApi, STUDENT_ID } from "./test/mockApi";

describe("App", () => {
  it("remembers the student after a reload and still shows their applications", async () => {
    mockApi({
      "GET /classes": () => json([]),
      "GET /me/holds": ({ headers }) =>
        json(
          headers.get("X-Student-Id") === STUDENT_ID
            ? [{ holdId: "h_1", classId: "c_1", className: "Class 6 - Morning Shift", status: "HELD", expiresAt: "2030-01-01T00:00:00Z" }]
            : [],
        ),
    });
    const user = userEvent.setup();
    const firstVisit = render(<App />);

    // Bengali digits are accepted and normalised.
    await user.type(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/), "২০১৫২৬৯১২৩৪৫৬৭৮৯০");
    await user.click(screen.getByRole("button", { name: "ব্যবহার করুন" }));
    expect(screen.getByTestId("active-student-id")).toHaveTextContent(STUDENT_ID);
    expect(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBe(STUDENT_ID);

    // Simulate closing the tab and opening "My applications" again.
    firstVisit.unmount();
    window.location.hash = "#/my";
    render(<App />);

    expect(screen.getByTestId("active-student-id")).toHaveTextContent(STUDENT_ID);
    expect(await screen.findByText("Class 6 - Morning Shift")).toBeInTheDocument();
    expect(screen.getByText("HELD")).toBeInTheDocument();
  });

  it("rejects an invalid student ID with a clear message", async () => {
    mockApi({ "GET /classes": () => json([]) });
    const user = userEvent.setup();
    render(<App />);

    await user.type(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/), "12345");
    await user.click(screen.getByRole("button", { name: "ব্যবহার করুন" }));

    expect(screen.getByRole("alert")).toHaveTextContent("জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে");
    expect(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBeNull();
  });
});
