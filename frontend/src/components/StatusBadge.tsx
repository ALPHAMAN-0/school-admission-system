import type { HoldStatus } from "../api/types";
import { STATUS_LABELS } from "../lib/format";

/** Bengali label plus the API status code (e.g. "মেয়াদ শেষ · EXPIRED"). */
export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`} data-testid="hold-status">
      {STATUS_LABELS[status]} · <span lang="en">{status}</span>
    </span>
  );
}
