import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { resetServerClock } from "../lib/clock";

afterEach(() => {
  cleanup();
  resetServerClock();
  window.localStorage.clear();
  window.location.hash = "";
});
