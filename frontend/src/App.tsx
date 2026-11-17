import { useCallback, useEffect, useRef } from "react";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { StudentIdBar } from "./components/StudentIdBar";
import { routeToHash, useHashRoute, type Route } from "./hooks/useHashRoute";
import { useStudentId } from "./hooks/useStudentId";
import { ApplicationPage } from "./pages/ApplicationPage";
import { ClassListPage } from "./pages/ClassListPage";
import { CreateClassPage } from "./pages/CreateClassPage";
import { MyApplicationsPage } from "./pages/MyApplicationsPage";

const NAV: { route: Route; label: string }[] = [
  { route: { page: "classes" }, label: "ক্লাসের তালিকা" },
  { route: { page: "my" }, label: "আমার আবেদন" },
  { route: { page: "admin" }, label: "ক্লাস তৈরি (অ্যাডমিন)" },
];

const PAGE_TITLES: Record<Route["page"], string> = {
  classes: "ক্লাসের তালিকা",
  admin: "ক্লাস তৈরি",
  my: "আমার আবেদন",
  application: "আবেদন",
};

export function App() {
  const [route, navigate] = useHashRoute();
  const [studentId, setStudentId] = useStudentId();
  const studentInputRef = useRef<HTMLInputElement>(null);
  const mainRef = useRef<HTMLElement>(null);

  const focusStudentId = useCallback(() => {
    const input = studentInputRef.current;
    input?.scrollIntoView?.({ block: "center", behavior: "smooth" });
    input?.focus();
  }, []);

  const pageKey = route.page === "application" ? `application:${route.holdId}` : route.page;

  useEffect(() => {
    document.title = `${PAGE_TITLES[route.page]} · স্কুল ভর্তি সিস্টেম`;
  }, [route.page]);

  // On navigation, move focus to the new page so keyboard and screen-reader users start there.
  const previousPageKey = useRef(pageKey);
  useEffect(() => {
    if (previousPageKey.current !== pageKey) {
      previousPageKey.current = pageKey;
      mainRef.current?.focus();
    }
  }, [pageKey]);

  return (
    <div className="app">
      <header className="app-header">
        <h1>স্কুল ভর্তি সিস্টেম</h1>
        <p className="tagline">অনলাইন ভর্তি · আগে আবেদন করলে আগে পাবে</p>
        {/* Remount when the ID changes elsewhere (e.g. another tab) so the box shows it. */}
        <StudentIdBar
          key={studentId ?? "none"}
          studentId={studentId}
          onChange={setStudentId}
          inputRef={studentInputRef}
        />
        <nav aria-label="প্রধান মেনু">
          {NAV.map((item) => (
            <a
              key={item.route.page}
              href={routeToHash(item.route)}
              aria-current={route.page === item.route.page ? "page" : undefined}
            >
              {item.label}
            </a>
          ))}
        </nav>
      </header>

      <main ref={mainRef} tabIndex={-1}>
        <ErrorBoundary key={pageKey}>
          {route.page === "classes" && (
            <ClassListPage studentId={studentId} navigate={navigate} onNeedStudentId={focusStudentId} />
          )}
          {route.page === "admin" && <CreateClassPage navigate={navigate} />}
          {route.page === "my" && (
            <MyApplicationsPage studentId={studentId} navigate={navigate} onNeedStudentId={focusStudentId} />
          )}
          {route.page === "application" && (
            <ApplicationPage
              holdId={route.holdId}
              studentId={studentId}
              navigate={navigate}
              onNeedStudentId={focusStudentId}
            />
          )}
        </ErrorBoundary>
      </main>
    </div>
  );
}
