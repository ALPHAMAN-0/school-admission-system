import { useId, useState, type FormEvent, type Ref } from "react";
import { isValidStudentId, normalizeStudentId } from "../lib/format";

interface StudentIdBarProps {
  studentId: string | null;
  onChange: (studentId: string | null) => void;
  inputRef?: Ref<HTMLInputElement>;
}

/** The box at the top of the page: the student's 17-digit birth registration number is their identity. */
export function StudentIdBar({ studentId, onChange, inputRef }: StudentIdBarProps) {
  const inputId = useId();
  const hintId = useId();
  const errorId = useId();
  const [draft, setDraft] = useState(studentId ?? "");
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(studentId === null);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const normalized = normalizeStudentId(draft);
    if (!isValidStudentId(normalized)) {
      setError("জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে (শুধু সংখ্যা)।");
      return;
    }
    setError(null);
    setDraft(normalized);
    setEditing(false);
    onChange(normalized);
  };

  if (!editing && studentId) {
    return (
      <section className="student-bar" aria-label="শিক্ষার্থীর পরিচয়">
        <span>
          শিক্ষার্থীর জন্ম নিবন্ধন নম্বর: <strong data-testid="active-student-id">{studentId}</strong>
        </span>
        <button
          type="button"
          className="button-secondary"
          onClick={() => {
            setDraft(studentId);
            setEditing(true);
          }}
        >
          পরিবর্তন করুন
        </button>
      </section>
    );
  }

  return (
    <form className="student-bar" onSubmit={submit} noValidate aria-label="শিক্ষার্থীর পরিচয়">
      <label htmlFor={inputId}>শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)</label>
      <input
        id={inputId}
        ref={inputRef}
        inputMode="numeric"
        autoComplete="off"
        placeholder="যেমন: 20152691234567890"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        aria-invalid={error !== null}
        aria-describedby={error ? errorId : hintId}
      />
      <button type="submit">ব্যবহার করুন</button>
      {studentId && (
        <button type="button" className="button-secondary" onClick={() => setEditing(false)}>
          বাতিল
        </button>
      )}
      {/* Mounted fresh when it appears, so screen readers reliably announce it. */}
      {error ? (
        <p id={errorId} className="field-error" role="alert">
          {error}
        </p>
      ) : (
        <p id={hintId} className="hint">
          আবেদন করতে ও নিজের আবেদন দেখতে এই নম্বরটি লাগবে। বাংলা অঙ্কেও লিখতে পারেন।
        </p>
      )}
    </form>
  );
}
