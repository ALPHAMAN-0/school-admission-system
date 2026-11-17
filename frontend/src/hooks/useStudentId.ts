import { useCallback, useEffect, useState } from "react";
import { isValidStudentId } from "../lib/format";

export const STUDENT_ID_STORAGE_KEY = "school-admission.studentId";

/** Only a valid 17-digit number is trusted from storage (it may have been edited by hand). */
function validOrNull(value: string | null): string | null {
  return value !== null && isValidStudentId(value) ? value : null;
}

function readStored(): string | null {
  try {
    return validOrNull(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY));
  } catch {
    return null; // storage blocked (private mode, policy)
  }
}

/**
 * The active student's birth registration number. Kept in localStorage so that after a
 * refresh, or closing and reopening the browser, the student still sees their applications.
 */
export function useStudentId(): [string | null, (value: string | null) => void] {
  const [studentId, setStudentIdState] = useState<string | null>(readStored);

  const setStudentId = useCallback((value: string | null) => {
    setStudentIdState(value);
    try {
      if (value) window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, value);
      else window.localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
    } catch {
      // Not fatal: the ID still works for this tab.
    }
  }, []);

  // Keep several open tabs on the same student.
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key === STUDENT_ID_STORAGE_KEY) setStudentIdState(validOrNull(event.newValue));
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  return [studentId, setStudentId];
}
