import { defineConfig, devices } from "@playwright/test";

/**
 * Smoke-only Playwright config for the dashboard. The intent is NOT to
 * mirror every vitest spec at the browser level — vitest already drives
 * the component tree under jsdom, including the route-level a11y suite
 * (`src/test/a11y-routes.test.tsx`). What vitest CAN'T see is whether
 * the production build actually boots in a real browser: client-only
 * `next/font` plumbing, Turbopack chunk graph regressions, runtime SSR
 * hydration mismatches, etc. A handful of smoke tests against `next
 * start` covers exactly that gap.
 *
 * `webServer` runs `npm start` against the artifact produced by
 * `npm run build`, with `SPRING_API_URL` pinned to a non-routable host.
 * The dashboard's network-bound surfaces gracefully degrade to error
 * states (matched by the smoke tests below), which keeps the suite
 * deterministic without standing up a backend.
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: process.env.CI ? [["github"], ["list"]] : "list",
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://127.0.0.1:3100",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: {
    // Start the production build (`npm run build`) — smoke tests
    // explicitly target the artifact CI ships, not the dev server.
    command: "PORT=3100 npm start",
    url: "http://127.0.0.1:3100",
    reuseExistingServer: !process.env.CI,
    timeout: 120 * 1000,
    env: {
      // Point at a non-routable target so the dashboard's API rewrites
      // resolve to a host that exists but never responds. Each page
      // renders shell + skeletons + an empty/error state, which is the
      // surface these smoke tests intentionally assert against.
      SPRING_API_URL: "http://127.0.0.1:65535",
      NEXT_PUBLIC_API_URL: "http://127.0.0.1:65535",
      NODE_ENV: "production",
    },
  },
});
