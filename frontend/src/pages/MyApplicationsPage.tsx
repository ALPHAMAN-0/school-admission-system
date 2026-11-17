import { api } from "../api/client";
import { toUserMessage } from "../api/errors";
import { Alert } from "../components/Alert";
import { StatusBadge } from "../components/StatusBadge";
import { useNow } from "../hooks/useNow";
import { usePolledQuery } from "../hooks/usePolledQuery";
import type { Route } from "../hooks/useHashRoute";
import { effectiveStatus, formatRemaining, formatTime } from "../lib/format";

export const MY_HOLDS_POLL_MS = 5000;

interface MyApplicationsPageProps {
  studentId: string | null;
  navigate: (route: Route) => void;
  onNeedStudentId: () => void;
}

/** All applications of the active student, with live status. */
export function MyApplicationsPage({ studentId, navigate, onNeedStudentId }: MyApplicationsPageProps) {
  const holds = usePolledQuery(
    studentId ? `my-holds:${studentId}` : null,
    (signal) => api.myHolds(studentId!, signal),
    MY_HOLDS_POLL_MS,
  );
  const now = useNow(1000);

  return (
    <section aria-labelledby="my-applications-title">
      <div className="page-heading">
        <h2 id="my-applications-title">আমার আবেদন</h2>
      </div>

      {!studentId && (
        <Alert kind="info">
          নিজের আবেদন দেখতে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।{" "}
          <button type="button" className="link-button" onClick={onNeedStudentId}>
            নম্বর দিন
          </button>
        </Alert>
      )}

      {holds.error !== undefined && (
        <Alert kind="warning">
          {holds.data ? `সর্বশেষ তথ্য আনা যায়নি। ${toUserMessage(holds.error)}` : toUserMessage(holds.error)}
        </Alert>
      )}
      {holds.loading && <p className="hint">আবেদনগুলো আনা হচ্ছে…</p>}

      {holds.data && holds.data.length === 0 && (
        <Alert kind="info">
          এই শিক্ষার্থীর এখনো কোনো আবেদন নেই।{" "}
          <button type="button" className="link-button" onClick={() => navigate({ page: "classes" })}>
            ক্লাসের তালিকা থেকে আবেদন করুন
          </button>
        </Alert>
      )}

      {holds.data && holds.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th scope="col">ক্লাস</th>
                <th scope="col">অবস্থা</th>
                <th scope="col">সময়</th>
                <th scope="col">
                  <span className="visually-hidden">বিস্তারিত</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {holds.data.map((hold) => {
                const status = effectiveStatus(hold, now);
                return (
                  <tr key={hold.holdId}>
                    <th scope="row">{hold.className ?? hold.classId}</th>
                    <td data-label="অবস্থা">
                      <StatusBadge status={status} />
                    </td>
                    <td data-label="সময়">
                      {status === "HELD"
                        ? `বাকি ${formatRemaining(Date.parse(hold.expiresAt) - now)}`
                        : status === "CONFIRMED" && hold.confirmedAt
                          ? `ফি জমা ${formatTime(hold.confirmedAt)}`
                          : hold.createdAt
                            ? `আবেদন ${formatTime(hold.createdAt)}`
                            : ""}
                    </td>
                    <td>
                      <button
                        type="button"
                        className={status === "HELD" ? "" : "button-secondary"}
                        onClick={() => navigate({ page: "application", holdId: hold.holdId })}
                      >
                        {status === "HELD" ? "ফি দিন / দেখুন" : "দেখুন"}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
