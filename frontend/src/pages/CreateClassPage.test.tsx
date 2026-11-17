import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { json, mockApi, problem } from "../test/mockApi";
import { CreateClassPage } from "./CreateClassPage";

describe("CreateClassPage", () => {
  it("shows field errors and focuses the first invalid field", async () => {
    const server = mockApi({});
    const user = userEvent.setup();
    render(<CreateClassPage navigate={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "ক্লাস তৈরি করুন" }));

    expect(screen.getByText(/ক্লাসের নাম দিন/)).toBeInTheDocument();
    expect(screen.getByText(/আসন সংখ্যা একটি পূর্ণ সংখ্যা হতে হবে/)).toBeInTheDocument();
    expect(screen.getByLabelText("ক্লাসের নাম")).toHaveFocus();
    expect(server.calls).toHaveLength(0);
  });

  it("creates a class, accepting the seat count in Bengali digits", async () => {
    const server = mockApi({
      "POST /classes": ({ body }) => json({ id: "c_1", ...(body as object), heldSeats: 0, confirmedSeats: 0, availableSeats: 120 }, 201),
    });
    const user = userEvent.setup();
    render(<CreateClassPage navigate={vi.fn()} />);

    await user.type(screen.getByLabelText("ক্লাসের নাম"), "Class 6 - Morning Shift");
    await user.type(screen.getByLabelText("মোট আসন সংখ্যা"), "১২০");
    await user.click(screen.getByRole("button", { name: "ক্লাস তৈরি করুন" }));

    expect(await screen.findByText(/ক্লাস তৈরি হয়েছে/)).toBeInTheDocument();
    expect(server.calls[0]?.body).toEqual({ name: "Class 6 - Morning Shift", totalSeats: 120 });
    expect(screen.getByLabelText("ক্লাসের নাম")).toHaveValue("");
  });

  it("shows the server's validation result in plain language", async () => {
    mockApi({ "POST /classes": () => problem(400, "VALIDATION_FAILED", { errors: { name: ["bad"] } }) });
    const user = userEvent.setup();
    render(<CreateClassPage navigate={vi.fn()} />);

    await user.type(screen.getByLabelText("ক্লাসের নাম"), "Class 6");
    await user.type(screen.getByLabelText("মোট আসন সংখ্যা"), "10");
    await user.click(screen.getByRole("button", { name: "ক্লাস তৈরি করুন" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/দেওয়া তথ্যে ভুল আছে/);
    expect(screen.getByText("ক্লাসের নামটি সঠিক নয়।")).toBeInTheDocument();
  });
});
