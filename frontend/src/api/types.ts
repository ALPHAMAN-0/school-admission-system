export type HoldStatus = "HELD" | "CONFIRMED" | "EXPIRED" | "CANCELLED";

export interface SchoolClass {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface Hold {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  studentId?: string;
  createdAt?: string;
  confirmedAt?: string | null;
}

/** RFC 9457 problem details as returned by the API, plus its extension fields. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  holdId?: string;
  holdStatus?: HoldStatus;
}
