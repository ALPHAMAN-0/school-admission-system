import { useId, useRef, useState, type FormEvent } from "react";
import { api } from "../api/client";
import { ApiError, toUserMessage } from "../api/errors";
import type { SchoolClass } from "../api/types";
import { Alert } from "../components/Alert";
import type { Route } from "../hooks/useHashRoute";
import { normalizeDigits, toBengaliNumber } from "../lib/format";

const MAX_NAME_LENGTH = 100;
const MAX_SEATS = 2_147_483_647; // what the API can store (32-bit integer)

interface FieldErrors {
  name?: string;
  totalSeats?: string;
}

function validate(name: string, seats: string): FieldErrors {
  const errors: FieldErrors = {};
  const trimmed = name.trim();
  if (!trimmed) errors.name = "ক্লাসের নাম দিন, যেমন: Class 6 - Morning Shift";
  else if (trimmed.length > MAX_NAME_LENGTH) errors.name = `নাম সর্বোচ্চ ${toBengaliNumber(MAX_NAME_LENGTH)} অক্ষরের হতে পারে।`;

  const digits = normalizeDigits(seats.trim());
  if (!/^[0-9]+$/.test(digits)) errors.totalSeats = "আসন সংখ্যা একটি পূর্ণ সংখ্যা হতে হবে (যেমন ১২০)।";
  else if (Number(digits) < 1) errors.totalSeats = "অন্তত ১টি আসন থাকতে হবে।";
  else if (Number(digits) > MAX_SEATS) errors.totalSeats = "আসন সংখ্যা অনেক বড়।";
  return errors;
}

/** School admin: create a class (each shift is its own class with its own seats). */
export function CreateClassPage({ navigate }: { navigate: (route: Route) => void }) {
  const nameId = useId();
  const seatsId = useId();
  const nameRef = useRef<HTMLInputElement>(null);
  const seatsRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<SchoolClass | null>(null);
  const [saving, setSaving] = useState(false);

  const focusFirstInvalid = (errors: FieldErrors) => {
    if (errors.name) nameRef.current?.focus();
    else if (errors.totalSeats) seatsRef.current?.focus();
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setCreated(null);

    const errors = validate(name, seats);
    setFieldErrors(errors);
    if (errors.name || errors.totalSeats) {
      focusFirstInvalid(errors);
      return;
    }

    setSaving(true);
    try {
      const schoolClass = await api.createClass(name.trim(), Number(normalizeDigits(seats.trim())));
      setCreated(schoolClass);
      setName("");
      setSeats("");
      nameRef.current?.focus();
    } catch (caught) {
      setError(toUserMessage(caught));
      if (caught instanceof ApiError && caught.problem.errors) {
        const serverErrors: FieldErrors = {
          name: caught.problem.errors.name ? "ক্লাসের নামটি সঠিক নয়।" : undefined,
          totalSeats: caught.problem.errors.totalSeats ? "আসন সংখ্যাটি সঠিক নয়।" : undefined,
        };
        setFieldErrors(serverErrors);
        focusFirstInvalid(serverErrors);
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="create-class-title" className="narrow">
      <div className="page-heading">
        <h2 id="create-class-title">নতুন ক্লাস তৈরি (স্কুল অ্যাডমিন)</h2>
        <p className="hint">প্রতিটি শিফট আলাদা ক্লাস, যেমন “Class 6 - Morning Shift” ও “Class 6 - Day Shift”।</p>
      </div>

      {created && (
        <Alert kind="success" onClose={() => setCreated(null)}>
          <p>
            ক্লাস তৈরি হয়েছে: <strong>{created.name}</strong> ({toBengaliNumber(created.totalSeats)}টি আসন)।
          </p>
          <button type="button" className="link-button" onClick={() => navigate({ page: "classes" })}>
            ক্লাসের তালিকায় দেখুন
          </button>
        </Alert>
      )}
      {error && (
        <Alert kind="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form onSubmit={(event) => void submit(event)} noValidate className="card form">
        <div className="field">
          <label htmlFor={nameId}>ক্লাসের নাম</label>
          <input
            id={nameId}
            ref={nameRef}
            value={name}
            maxLength={MAX_NAME_LENGTH + 20}
            placeholder="Class 6 - Morning Shift"
            onChange={(event) => setName(event.target.value)}
            aria-invalid={fieldErrors.name !== undefined}
            aria-describedby={fieldErrors.name ? `${nameId}-error` : undefined}
          />
          {fieldErrors.name && (
            <p id={`${nameId}-error`} className="field-error">
              {fieldErrors.name}
            </p>
          )}
        </div>

        <div className="field">
          <label htmlFor={seatsId}>মোট আসন সংখ্যা</label>
          <input
            id={seatsId}
            ref={seatsRef}
            inputMode="numeric"
            value={seats}
            placeholder="120"
            onChange={(event) => setSeats(event.target.value)}
            aria-invalid={fieldErrors.totalSeats !== undefined}
            aria-describedby={fieldErrors.totalSeats ? `${seatsId}-error` : undefined}
          />
          {fieldErrors.totalSeats && (
            <p id={`${seatsId}-error`} className="field-error">
              {fieldErrors.totalSeats}
            </p>
          )}
        </div>

        <button type="submit" disabled={saving}>
          {saving ? "তৈরি হচ্ছে…" : "ক্লাস তৈরি করুন"}
        </button>
      </form>
    </section>
  );
}
