import type { ReactNode } from "react";

interface AlertProps {
  kind: "error" | "success" | "info" | "warning";
  children: ReactNode;
  onClose?: () => void;
}

/** Visible, screen-reader-announced message. Errors use role="alert", others role="status". */
export function Alert({ kind, children, onClose }: AlertProps) {
  return (
    <div className={`alert alert-${kind}`} role={kind === "error" ? "alert" : "status"}>
      <div className="alert-body">{children}</div>
      {onClose && (
        <button type="button" className="alert-close" onClick={onClose} aria-label="বার্তাটি বন্ধ করুন">
          ×
        </button>
      )}
    </div>
  );
}
