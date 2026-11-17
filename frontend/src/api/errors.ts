import type { ProblemDetails } from "./types";

export const NETWORK_ERROR = "NETWORK_ERROR";

/** Any failed API call. `code` is the API's stable error code, or NETWORK_ERROR / HTTP_<status>. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly problem: ProblemDetails;

  constructor(status: number, code: string, problem: ProblemDetails = {}) {
    super(problem.detail ?? problem.title ?? code);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.problem = problem;
  }
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

const MESSAGES: Record<string, string> = {
  [NETWORK_ERROR]:
    "সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট সংযোগ ও সার্ভার চালু আছে কি না দেখে একটু পরে আবার চেষ্টা করুন।",
  NO_SEATS_AVAILABLE: "দুঃখিত, এই ক্লাসে এখন কোনো খালি আসন নেই।",
  ALREADY_APPLIED: "এই শিক্ষার্থী ইতিমধ্যে এই ক্লাসে আবেদন করেছে।",
  HOLD_EXPIRED:
    "ভর্তি ফি জমা দেওয়ার সময় শেষ হয়ে গেছে। আসনটি অন্যদের জন্য ছেড়ে দেওয়া হয়েছে। চাইলে আবার আবেদন করতে পারেন।",
  HOLD_CANCELLED: "এই আবেদনটি আগেই বাতিল করা হয়েছে।",
  HOLD_ALREADY_CONFIRMED: "ভর্তি ফি আগেই জমা হয়েছে, তাই এই আবেদন আর বাতিল করা যাবে না।",
  NOT_HOLD_OWNER:
    "এই আবেদনটি অন্য একজন শিক্ষার্থীর। উপরের জন্ম নিবন্ধন নম্বরটি ঠিক আছে কি না দেখুন।",
  CLASS_NOT_FOUND: "ক্লাসটি খুঁজে পাওয়া যায়নি। তালিকাটি আবার দেখুন।",
  HOLD_NOT_FOUND: "আবেদনটি খুঁজে পাওয়া যায়নি।",
  CLASS_NAME_TAKEN: "এই নামে একটি ক্লাস আগে থেকেই আছে। অন্য নাম দিন।",
  VALIDATION_FAILED: "দেওয়া তথ্যে ভুল আছে। লাল চিহ্নিত ঘরগুলো ঠিক করে আবার চেষ্টা করুন।",
  MISSING_STUDENT_ID: "আগে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।",
  INVALID_STUDENT_ID: "জন্ম নিবন্ধন নম্বরটি সঠিক নয়। ঠিক ১৭ অঙ্কের নম্বর দিন।",
  INVALID_REQUEST: "অনুরোধটি পড়া যায়নি। তথ্যগুলো আবার দেখে চেষ্টা করুন।",
  SERVICE_UNAVAILABLE: "সার্ভার এখন ব্যস্ত। কিছুক্ষণ পরে আবার চেষ্টা করুন।",
  INTERNAL_ERROR: "সার্ভারে একটি সমস্যা হয়েছে। কিছুক্ষণ পরে আবার চেষ্টা করুন।",
};

const GENERIC = "কিছু একটা ভুল হয়েছে। আবার চেষ্টা করুন।";

/** A plain-language Bengali message for any error. Never returns an empty string. */
export function toUserMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const known = MESSAGES[error.code];
    if (known) return known;
    if (error.status === 0) return MESSAGES[NETWORK_ERROR]!;
    if (error.status === 404) return "যা খুঁজছেন তা পাওয়া যায়নি।";
    if (error.status >= 500) return MESSAGES.INTERNAL_ERROR!;
    if (error.status >= 400) return MESSAGES.INVALID_REQUEST!;
  }
  return GENERIC;
}
