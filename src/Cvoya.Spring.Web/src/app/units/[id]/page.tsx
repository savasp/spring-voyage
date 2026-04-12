import UnitConfigClient from "./unit-config-client";

// The dashboard is exported as a static site (next.config.ts: output: "export"),
// so every dynamic segment must be enumerated at build time. Unit names are
// created by users at runtime, so we emit a single `__placeholder__` entry to
// satisfy the build check. Actual navigation uses client-side Link transitions
// where the `[id]` route handler reads the param from the URL at runtime.
// Direct hard-loads of an arbitrary /units/<id> path are a follow-up tracked
// alongside SSR decisions for this dashboard.
export function generateStaticParams(): { id: string }[] {
  return [{ id: "__placeholder__" }];
}

interface PageProps {
  // Next 16 app router: route params are delivered as a Promise.
  params: Promise<{ id: string }>;
}

export default async function UnitConfigPage({ params }: PageProps) {
  const { id } = await params;
  return <UnitConfigClient id={id} />;
}
