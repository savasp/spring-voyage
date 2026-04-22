// #1053 pin: `useAnalyticsFilters` writes a `/path?query` URL through
// `router.replace`, never the bare `?query` form. Next.js 16 silently
// drops the canonical-URL update for query-only relative URLs, so the
// controlled `windowValue` / `scope` derived from `useSearchParams()`
// snap back to the prior value on the next render. Mirrors the unit
// Explorer fix landed in PR #1052 / issue #1039.

import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const routerReplaceMock = vi.fn();
const searchParamsStateMock = { value: "" };
const pathnameMock = { value: "/analytics/costs" };

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: routerReplaceMock, push: vi.fn() }),
  usePathname: () => pathnameMock.value,
  useSearchParams: () => new URLSearchParams(searchParamsStateMock.value),
}));

import { useAnalyticsFilters } from "./analytics-filters";

describe("useAnalyticsFilters — Next.js 16 query-only navigation (#1053)", () => {
  beforeEach(() => {
    routerReplaceMock.mockReset();
    searchParamsStateMock.value = "";
    pathnameMock.value = "/analytics/costs";
  });
  afterEach(() => {
    routerReplaceMock.mockReset();
  });

  it("setWindow writes `/<pathname>?window=…` to router.replace, not bare `?…`", () => {
    const { result } = renderHook(() => useAnalyticsFilters());

    act(() => {
      result.current.setWindow("24h");
    });

    expect(routerReplaceMock).toHaveBeenCalledTimes(1);
    const [url, opts] = routerReplaceMock.mock.calls[0];
    // The fix: URL must start with the pathname so Next.js 16 honours
    // the navigation. A bare `?window=24h` would be dropped.
    expect(url).toMatch(/^\/analytics\/costs\?/);
    expect(url).toContain("window=24h");
    expect(opts).toEqual({ scroll: false });
  });

  it("setScope writes `/<pathname>?scope=…&name=…` with the active pathname", () => {
    pathnameMock.value = "/analytics/throughput";
    const { result } = renderHook(() => useAnalyticsFilters());

    act(() => {
      result.current.setScope({ kind: "unit", name: "engineering" });
    });

    expect(routerReplaceMock).toHaveBeenCalledTimes(1);
    const [url] = routerReplaceMock.mock.calls[0];
    expect(url).toMatch(/^\/analytics\/throughput\?/);
    expect(url).toContain("scope=unit");
    expect(url).toContain("name=engineering");
  });

  it("collapses to the bare pathname when every filter is at its default (no `?`)", () => {
    // Land on a non-default state first so `setScope({ kind: "all" })`
    // produces an empty query string and the hook falls into the
    // pathname-only branch.
    searchParamsStateMock.value = "scope=unit&name=engineering";
    const { result } = renderHook(() => useAnalyticsFilters());

    act(() => {
      result.current.setScope({ kind: "all" });
    });

    expect(routerReplaceMock).toHaveBeenCalledTimes(1);
    const [url] = routerReplaceMock.mock.calls[0];
    // Pathname only — never the bare `"?"` Next.js 16 drops.
    expect(url).toBe("/analytics/costs");
  });
});
