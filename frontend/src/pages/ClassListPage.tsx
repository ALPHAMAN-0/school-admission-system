import { useState } from "react";
import { api } from "../api/client";
import { ApiError, toUserMessage } from "../api/errors";
import type { SchoolClass } from "../api/types";
import { Alert } from "../components/Alert";
import { usePolledQuery } from "../hooks/usePolledQuery";
import type { Route } from "../hooks/useHashRoute";
import { toBengaliNumber } from "../lib/format";

export const CLASS_POLL_MS = 3000;

interface ClassListPageProps {
  studentId: string | null;
  navigate: (route: Route) => void;
  onNeedStudentId: () => void;
}

export function ClassListPage({ studentId, navigate, onNeedStudentId }: ClassListPageProps) {
  const classes = usePolledQuery("classes", (signal) => api.listClasses(signal), CLASS_POLL_MS);
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; existingHoldId?: string } | null>(null);

  const apply = async (schoolClass: SchoolClass) => {
    if (!studentId) {
      setMessage({ text: "আবেদন করতে আগে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।" });
      onNeedStudentId();
      return;
    }

    setApplyingId(schoolClass.id);
    setMessage(null);
    try {
      const hold = await api.applyForSeat(schoolClass.id, studentId);
      navigate({ page: "application", holdId: hold.holdId });
    } catch (error) {
      const existingHoldId =
        error instanceof ApiError && error.code === "ALREADY_APPLIED" ? error.problem.holdId : undefined;
      setMessage({ text: toUserMessage(error), existingHoldId });
      classes.refetch();
    } finally {
      setApplyingId(null);
    }
  };

  return (
    <section aria-labelledby="class-list-title">
      <div className="page-heading">
        <h2 id="class-list-title">ক্লাসের তালিকা</h2>
        <p className="hint">আসনের হিসাব প্রতি কয়েক সেকেন্ডে নিজে থেকেই হালনাগাদ হয়। আগে আবেদন করলে আগে পাবে।</p>
      </div>

      {message && (
        <Alert kind="error" onClose={() => setMessage(null)}>
          <p>{message.text}</p>
          {message.existingHoldId && (
            <button
              type="button"
              className="link-button"
              onClick={() => navigate({ page: "application", holdId: message.existingHoldId! })}
            >
              আগের আবেদনটি দেখুন
            </button>
          )}
        </Alert>
      )}

      {classes.error !== undefined && (
        <Alert kind="warning">
          {classes.data
            ? `সর্বশেষ তথ্য আনা যায়নি; আগের তথ্য দেখানো হচ্ছে। ${toUserMessage(classes.error)}`
            : toUserMessage(classes.error)}
        </Alert>
      )}

      {classes.loading && <p className="hint">ক্লাসের তালিকা আনা হচ্ছে…</p>}

      {classes.data && classes.data.length === 0 && (
        <Alert kind="info">
          এখনো কোনো ক্লাস তৈরি করা হয়নি।{" "}
          <button type="button" className="link-button" onClick={() => navigate({ page: "admin" })}>
            ক্লাস তৈরি করুন
          </button>
        </Alert>
      )}

      {classes.data && classes.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th scope="col">ক্লাস ও শিফট</th>
                <th scope="col">মোট আসন</th>
                <th scope="col">হোল্ডে আছে</th>
                <th scope="col">নিশ্চিত</th>
                <th scope="col">খালি</th>
                <th scope="col">
                  <span className="visually-hidden">আবেদন</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {classes.data.map((schoolClass) => (
                <tr key={schoolClass.id} data-testid={`class-row-${schoolClass.id}`}>
                  <th scope="row">{schoolClass.name}</th>
                  <td data-label="মোট আসন">{toBengaliNumber(schoolClass.totalSeats)}</td>
                  <td data-label="হোল্ডে আছে">{toBengaliNumber(schoolClass.heldSeats)}</td>
                  <td data-label="নিশ্চিত">{toBengaliNumber(schoolClass.confirmedSeats)}</td>
                  <td data-label="খালি">
                    <strong className={schoolClass.availableSeats === 0 ? "text-danger" : "text-ok"}>
                      {toBengaliNumber(schoolClass.availableSeats)}
                    </strong>
                  </td>
                  <td>
                    <button
                      type="button"
                      onClick={() => void apply(schoolClass)}
                      disabled={applyingId !== null || schoolClass.availableSeats === 0}
                    >
                      {applyingId === schoolClass.id
                        ? "আবেদন হচ্ছে…"
                        : schoolClass.availableSeats === 0
                          ? "আসন নেই"
                          : "আবেদন করুন"}
                      {/* The visible text stays the accessible name; the class is added for context. */}
                      <span className="visually-hidden"> ({schoolClass.name})</span>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
