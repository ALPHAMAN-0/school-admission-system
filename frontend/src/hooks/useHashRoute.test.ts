import { describe, expect, it } from "vitest";
import { parseRoute, routeToHash } from "./useHashRoute";

describe("hash routes", () => {
  it("parses every page", () => {
    expect(parseRoute("")).toEqual({ page: "classes" });
    expect(parseRoute("#/admin")).toEqual({ page: "admin" });
    expect(parseRoute("#/my")).toEqual({ page: "my" });
    expect(parseRoute("#/applications/h_abc")).toEqual({ page: "application", holdId: "h_abc" });
  });

  it("never throws on a broken link, it falls back to the class list", () => {
    expect(parseRoute("#/applications/%E0%A4")).toEqual({ page: "classes" });
    expect(parseRoute("#/nonsense")).toEqual({ page: "classes" });
  });

  it("round-trips", () => {
    const route = { page: "application" as const, holdId: "h_1/2" };
    expect(parseRoute(routeToHash(route))).toEqual(route);
  });
});
