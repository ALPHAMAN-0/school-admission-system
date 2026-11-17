import { useCallback, useEffect, useState } from "react";

export type Route =
  | { page: "classes" }
  | { page: "admin" }
  | { page: "my" }
  | { page: "application"; holdId: string };

function safeDecode(value: string): string | null {
  try {
    return decodeURIComponent(value);
  } catch {
    return null; // a truncated or mangled link, e.g. "%E0%A4"
  }
}

/** Never throws: an unknown or broken link falls back to the class list. */
export function parseRoute(hash: string): Route {
  const path = hash.replace(/^#/, "") || "/";
  const application = /^\/applications\/([^/]+)$/.exec(path);
  const holdId = application?.[1] ? safeDecode(application[1]) : null;
  if (holdId) return { page: "application", holdId };
  if (path === "/admin") return { page: "admin" };
  if (path === "/my") return { page: "my" };
  return { page: "classes" };
}

export function routeToHash(route: Route): string {
  switch (route.page) {
    case "admin":
      return "#/admin";
    case "my":
      return "#/my";
    case "application":
      return `#/applications/${encodeURIComponent(route.holdId)}`;
    default:
      return "#/";
  }
}

/** Tiny hash router: survives refresh, works from any static file server. */
export function useHashRoute(): [Route, (route: Route) => void] {
  const [route, setRoute] = useState<Route>(() => parseRoute(window.location.hash));

  useEffect(() => {
    const onHashChange = () => setRoute(parseRoute(window.location.hash));
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  const navigate = useCallback((next: Route) => {
    window.location.hash = routeToHash(next);
  }, []);

  return [route, navigate];
}
