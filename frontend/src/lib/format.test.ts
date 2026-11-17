import { describe, expect, it } from "vitest";
import { effectiveStatus, formatRemaining, isValidStudentId, normalizeStudentId, toBengaliNumber } from "./format";

describe("student ID", () => {
  it("accepts Bengali digits, spaces and dashes", () => {
    expect(normalizeStudentId("২০১৫ ২৬৯১-২৩৪৫৬৭৮৯০")).toBe("20152691234567890");
  });

  it("is valid only with exactly 17 digits", () => {
    expect(isValidStudentId("20152691234567890")).toBe(true);
    expect(isValidStudentId("2015269123456789")).toBe(false);
    expect(isValidStudentId("201526912345678901")).toBe(false);
    expect(isValidStudentId("2015269123456789A")).toBe(false);
    expect(isValidStudentId("")).toBe(false);
  });
});

describe("formatting", () => {
  it("writes numbers with Bengali digits", () => {
    expect(toBengaliNumber(120)).toBe("১২০");
  });

  it("formats the remaining time and never goes negative", () => {
    expect(formatRemaining(9 * 60_000 + 58_000)).toBe("০৯:৫৮");
    expect(formatRemaining(3_725_000)).toBe("১:০২:০৫");
    expect(formatRemaining(-5_000)).toBe("০০:০০");
  });
});

describe("effectiveStatus", () => {
  const expiresAt = "2026-11-01T04:10:00Z";
  const expiry = Date.parse(expiresAt);

  it("treats a HELD application as EXPIRED once its time is up", () => {
    expect(effectiveStatus({ status: "HELD", expiresAt }, expiry - 1)).toBe("HELD");
    expect(effectiveStatus({ status: "HELD", expiresAt }, expiry)).toBe("EXPIRED");
  });

  it("never changes a confirmed or cancelled application", () => {
    expect(effectiveStatus({ status: "CONFIRMED", expiresAt }, expiry + 60_000)).toBe("CONFIRMED");
    expect(effectiveStatus({ status: "CANCELLED", expiresAt }, expiry + 60_000)).toBe("CANCELLED");
  });
});
