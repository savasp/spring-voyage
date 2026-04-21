import path from "node:path";
import type { NextConfig } from "next";

// Connector packages ship their own web UI under
// `src/Cvoya.Spring.Connector.<Name>/web/` — physically outside this
// Next.js project root. Turbopack's default project-root heuristic
// (walks up from next.config.ts looking for a lockfile) picks this
// directory, and then refuses to resolve `node_modules` for files
// imported from above it. That used to force each connector component
// to live inside `src/Cvoya.Spring.Web/src/connectors/<slug>/` instead
// of its owning package — see #198 for history.
//
// Setting `turbopack.root` to the monorepo root teaches Turbopack that
// the whole repo is one logical project. The web project still hosts
// the `node_modules` that the connector components consume (react,
// lucide-react, @/components/ui/*, etc.); Turbopack walks up from an
// imported file until it finds a matching module, which now succeeds
// because the repo root is an ancestor of both the connector package
// and `node_modules`.
//
// The tsconfig path alias `@connector-github/*` (see
// `src/Cvoya.Spring.Web/tsconfig.json`) resolves to the GitHub connector
// package's `web/` directory, so the registry imports stay symbolic.
const monorepoRoot = path.resolve(__dirname, "..", "..");

// Server-side proxy target for `/api/v1/*` and the activity SSE
// stream when the dashboard is running standalone (e.g. `npm run dev`,
// or the OSS Docker image). The browser keeps issuing same-origin
// requests, and Next.js rewrites them to the platform API.
//
// Resolution order:
//   1. SPRING_API_URL — server-side override, the same env var the
//      activity SSE proxy already honours.
//   2. NEXT_PUBLIC_API_URL — when the operator already pinned the
//      browser-side base, use that as the rewrite target too so dev
//      and prod stay consistent.
//   3. http://localhost:5000 — the documented `dotnet run` default
//      from the platform API host.
//
// Production deployments that put both the dashboard and the API
// behind the same reverse proxy don't hit these rewrites — the proxy
// terminates `/api/v1/*` before Next.js sees it. The fallback exists
// so a fresh `npm run dev` against a locally running API "just works"
// without requiring the operator to remember `NEXT_PUBLIC_API_URL`.
const apiProxyTarget =
  process.env.SPRING_API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5000";

const nextConfig: NextConfig = {
  output: "standalone",
  outputFileTracingRoot: monorepoRoot,
  images: { unoptimized: true },
  turbopack: {
    root: monorepoRoot,
  },

  // Legacy path redirects. `/budgets` used to be the budgets editor;
  // PR-S1 Sub-PR A (#544) / PR-S2 (#448) promote it under the new
  // Analytics surface as `/analytics/costs`. A permanent 308 keeps
  // bookmarks and old docs honest.
  async redirects() {
    return [
      {
        source: "/budgets",
        destination: "/analytics/costs",
        permanent: true,
      },
    ];
  },

  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiProxyTarget}/api/v1/:path*`,
      },
    ];
  },
};

export default nextConfig;
