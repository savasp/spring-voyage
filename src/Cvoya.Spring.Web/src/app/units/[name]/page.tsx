/**
 * `/units/[name]` — legacy scaffold redirect.
 *
 * The T-06 scaffold (issue #948) was retired by #1011 in favour of the
 * Explorer (`/units?node=<name>&tab=Overview`). This thin server
 * component preserves any bookmarks / external links that still point
 * at the old URL by issuing a `redirect()` to the canonical Explorer
 * pane. Mirrors the `app/analytics/page.tsx` pattern.
 */

import { redirect } from "next/navigation";

interface PageProps {
  params: Promise<{ name: string }>;
}

export default async function UnitDetailRedirect({
  params,
}: PageProps): Promise<never> {
  const { name } = await params;
  redirect(`/units?node=${encodeURIComponent(name)}&tab=Overview`);
}
