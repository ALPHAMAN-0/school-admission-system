import { useEffect, useState } from "react";
import { api } from "../api/client";
import { ApiError, toUserMessage } from "../api/errors";
import { Alert } from "../components/Alert";
import { StatusBadge } from "../components/StatusBadge";
import { useNow } from "../hooks/useNow";
import { usePolledQuery } from "../hooks/usePolledQuery";
import type { Route } from "../hooks/useHashRoute";
import { effectiveStatus, formatRemaining, formatTime } from "../lib/format";

export const HOLD_POLL_MS = 3000;

interface ApplicationPageProps {
  holdId: string;
  studentId: string | null;
  navigate: (route: Route) => void;
  onNeedStudentId: () => void;
}

/** One application: status, live countdown, pay-fee and cancel buttons. */
export function ApplicationPage({ holdId, studentId, navigate, onNeedStudentId }: ApplicationPageProps) {
  const hold = usePolledQuery(`hold:${holdId}`, (signal) => api.getHold(holdId, signal), HOLD_POLL_MS);
  const now = useNow(1000);
  const [busy, setBusy] = useState<"confirm" | "cancel" | null>(null);
  const [askCancel, setAskCancel] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const data = hold.data;
  const status = data ? effectiveStatus(data, now) : undefined;
  const expiredLocally = data?.status === "HELD" && status === "EXPIRED";
  const { refetch } = hold;

  // The countdown reached zero: ask the server so the page shows its authoritative state.
  useEffect(() => {
    if (expiredLocally) refetch();
  }, [expiredLocally, refetch]);

  const run = async (action: "confirm" | "cancel") => {
    if (!studentId) {
      setActionError("আগে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।");
      onNeedStudentId();
      return;
    }
    setBusy(action);
    setActionError(null);
    setNotice(null);
    try {
      if (action === "confirm") {
        await api.confirmHold(holdId, studentId);
        setNotice("অভিনন্দন! ভর্তি ফি জমা হয়েছে এবং ভর্তি নিশ্চিত হয়েছে।");
      } else {
        await api.cancelHold(holdId, studentId);
        setNotice("আবেদনটি বাতিল করা হয়েছে এবং আসনটি ছেড়ে দেওয়া হয়েছে।");
      }
    } catch (error) {
      setActionError(toUserMessage(error));
    } finally {
      setBusy(null);
      setAskCancel(false);
      refetch();
    }
  };

  const notFound = hold.error instanceof ApiError && hold.error.status === 404;
  const canAct = status === "HELD" && busy === null;

  return (
    <section aria-labelledby="application-title" className="narrow">
      <div className="page-heading">
        <h2 id="application-title">আবেদন</h2>
        <button type="button" className="link-button" onClick={() => navigate({ page: "my" })}>
          ← আমার সব আবেদন
        </button>
      </div>

      {notice && (
        <Alert kind="success" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}
      {actionError && (
        <Alert kind="error" onClose={() => setActionError(null)}>
          {actionError}
        </Alert>
      )}
      {hold.error !== undefined && !notFound && (
        <Alert kind="warning">
          {data ? `সর্বশেষ অবস্থা আনা যায়নি। ${toUserMessage(hold.error)}` : toUserMessage(hold.error)}
        </Alert>
      )}
      {notFound && (
        <Alert kind="error">
          আবেদনটি খুঁজে পাওয়া যায়নি।{" "}
          <button type="button" className="link-button" onClick={() => navigate({ page: "classes" })}>
            ক্লাসের তালিকায় ফিরে যান
          </button>
        </Alert>
      )}
      {hold.loading && <p className="hint">আবেদনের তথ্য আনা হচ্ছে…</p>}

      {data && status && (
        <div className="card application">
          <dl>
            <dt>ক্লাস</dt>
            <dd>{data.className ?? data.classId}</dd>
            <dt>অবস্থা</dt>
            <dd>
              <StatusBadge status={status} />
            </dd>
            {status === "HELD" && (
              <>
                <dt>মেয়াদ শেষ হতে বাকি</dt>
                <dd>
                  <span className="countdown" data-testid="countdown" aria-live="off">
                    {formatRemaining(Date.parse(data.expiresAt) - now)}
                  </span>{" "}
                  <span className="hint">({formatTime(data.expiresAt)}-এর মধ্যে ফি দিন)</span>
                </dd>
              </>
            )}
            {status === "EXPIRED" && (
              <>
                <dt>মেয়াদ</dt>
                <dd>সময়মতো ফি জমা না হওয়ায় আসনটি ছেড়ে দেওয়া হয়েছে। চাইলে আবার আবেদন করতে পারেন।</dd>
              </>
            )}
            {status === "CONFIRMED" && data.confirmedAt && (
              <>
                <dt>ফি জমার সময়</dt>
                <dd>{formatTime(data.confirmedAt)}</dd>
              </>
            )}
          </dl>

          <div className="actions">
            <button type="button" onClick={() => void run("confirm")} disabled={!canAct}>
              {busy === "confirm" ? "জমা হচ্ছে…" : "ভর্তি ফি দিন"}
            </button>
            {!askCancel ? (
              <button
                type="button"
                className="button-danger"
                onClick={() => setAskCancel(true)}
                disabled={!canAct}
              >
                বাতিল
              </button>
            ) : (
              <span className="confirm-cancel" role="group" aria-label="বাতিল নিশ্চিত করুন">
                <span>আবেদনটি বাতিল করবেন?</span>
                <button type="button" className="button-danger" onClick={() => void run("cancel")} disabled={busy !== null}>
                  {busy === "cancel" ? "বাতিল হচ্ছে…" : "হ্যাঁ, বাতিল করুন"}
                </button>
                <button type="button" className="button-secondary" onClick={() => setAskCancel(false)}>
                  না
                </button>
              </span>
            )}
          </div>
          {status === "EXPIRED" && (
            <button type="button" className="link-button" onClick={() => navigate({ page: "classes" })}>
              আবার আবেদন করতে ক্লাসের তালিকায় যান
            </button>
          )}
        </div>
      )}
    </section>
  );
}
