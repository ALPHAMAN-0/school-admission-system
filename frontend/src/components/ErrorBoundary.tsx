import { Component, type ErrorInfo, type ReactNode } from "react";

interface State {
  hasError: boolean;
}

/** Last resort for render errors: show a friendly message instead of a blank page. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Unexpected UI error", error, info.componentStack);
  }

  render() {
    if (!this.state.hasError) return this.props.children;
    return (
      <div className="alert alert-error" role="alert">
        <div className="alert-body">
          <p>দুঃখিত, পাতাটি দেখাতে একটি অপ্রত্যাশিত সমস্যা হয়েছে।</p>
          <button type="button" onClick={() => window.location.reload()}>
            পাতাটি আবার লোড করুন
          </button>
        </div>
      </div>
    );
  }
}
