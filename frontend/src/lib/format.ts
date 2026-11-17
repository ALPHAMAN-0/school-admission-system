import type { Hold, HoldStatus } from "../api/types";

const bnNumber = new Intl.NumberFormat("bn-BD", { useGrouping: false });
const BENGALI_DIGITS = "০১২৩৪৫৬৭৮৯";

/** 120 -> "১২০" */
export function toBengaliNumber(value: number): string {
  return bnNumber.format(value);
}

/** Bengali digits to ASCII: "১২০" -> "120". */
export function normalizeDigits(raw: string): string {
  return raw.replace(/[০-৯]/g, (digit) => String(BENGALI_DIGITS.indexOf(digit)));
}

/** Accepts Bengali digits too, drops spaces and dashes: "২০১৫ ২৬৯..." -> "2015269..." */
export function normalizeStudentId(raw: string): string {
  return normalizeDigits(raw).replace(/[\s-]/g, "");
}

export function isValidStudentId(value: string): boolean {
  return /^[0-9]{17}$/.test(value);
}

/** Milliseconds -> "০৯:৫৮" (or "১:০৯:৫৮" past an hour). Never negative. */
export function formatRemaining(ms: number): string {
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const two = (n: number) => toBengaliNumber(n).padStart(2, "০");
  return hours > 0
    ? `${toBengaliNumber(hours)}:${two(minutes)}:${two(seconds)}`
    : `${two(minutes)}:${two(seconds)}`;
}

/** The status to show now: a HELD hold whose time has passed is EXPIRED even before the server says so. */
export function effectiveStatus(hold: Pick<Hold, "status" | "expiresAt">, now: number): HoldStatus {
  if (hold.status === "HELD" && now >= Date.parse(hold.expiresAt)) return "EXPIRED";
  return hold.status;
}

export const STATUS_LABELS: Record<HoldStatus, string> = {
  HELD: "আসন সংরক্ষিত, ফি বাকি",
  CONFIRMED: "ভর্তি নিশ্চিত",
  EXPIRED: "মেয়াদ শেষ",
  CANCELLED: "বাতিল",
};

export function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("bn-BD", { hour: "numeric", minute: "2-digit" });
}
