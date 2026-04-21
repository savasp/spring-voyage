// Policies — § 5.6 of `docs/design/portal-exploration.md`. A broader
// top-level surface that replaces `/initiative` and covers all five
// `UnitPolicy` dimensions (Skill / Model / Cost / ExecutionMode /
// Initiative). The per-unit editor already ships on
// `/units` → Policies tab (#411) and the unified cross-unit index is
// tracked by #411 — the wire data isn't exposed yet, so this page is
// still a visibility anchor that points operators at the surfaces that
// work today.
//
// v2 reskin (SURF-reskin-policies, #855): adopts the `TabPolicies`
// policy-row layout from the design kit — mono identifiers for each
// dimension, severity pills for the kind of constraint ("caps", "list",
// "level"), and rule-count badges. The data rows are illustrative until
// #411 wires the tenant-wide read; they read as "these are the rails the
// rollup will land on" rather than live numbers.

import Link from "next/link";
import {
  ArrowRight,
  DollarSign,
  ExternalLink,
  Gauge,
  ListChecks,
  Shield,
  ShieldCheck,
  Zap,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

interface PolicyDimensionRow {
  slug: string;
  name: string;
  identifier: string;
  description: string;
  icon: typeof ShieldCheck;
  /** Pill kind — "caps" (hard limits), "list" (allow/block), "level" (tiered). */
  kind: "caps" | "list" | "level";
  /**
   * The Explorer detail tab that today owns the editor. The rollup page
   * only redirects to these; the per-unit surface is the source of
   * truth until #411 lands.
   */
  editHref: string;
}

/**
 * The five UnitPolicy dimensions, one row each, mirroring the per-unit
 * editor in `app/units/[id]/policies-tab.tsx`. Keeping this declarative
 * means the rollup widens automatically when a sixth dimension (e.g.
 * labels / routing) appears in the source-of-truth catalog.
 */
const POLICY_DIMENSIONS: readonly PolicyDimensionRow[] = [
  {
    slug: "skill",
    name: "Skill",
    identifier: "policy://skill",
    description:
      "Tool allow/block list. Empty allow list means allow every skill; blocked entries always deny.",
    icon: ListChecks,
    kind: "list",
    editHref: "/units?tab=policies&focus=skill",
  },
  {
    slug: "model",
    name: "Model",
    identifier: "policy://model",
    description: "LLM model allow/block list. Same shape as Skill.",
    icon: Gauge,
    kind: "list",
    editHref: "/units?tab=policies&focus=model",
  },
  {
    slug: "cost",
    name: "Cost",
    identifier: "policy://cost",
    description:
      "Per-invocation, per-hour, per-day USD caps. Cross-links to Budgets for current spend.",
    icon: DollarSign,
    kind: "caps",
    editHref: "/units?tab=policies&focus=cost",
  },
  {
    slug: "execution-mode",
    name: "Execution mode",
    identifier: "policy://execution-mode",
    description:
      "Pin every member to a forced mode, or limit members to a whitelist of allowed modes.",
    icon: Shield,
    kind: "list",
    editHref: "/units?tab=policies&focus=execution-mode",
  },
  {
    slug: "initiative",
    name: "Initiative",
    identifier: "policy://initiative",
    description:
      "Unit-level overlay on per-agent initiative. Restricts max autonomy level and adds action-level deny overrides.",
    icon: Zap,
    kind: "level",
    editHref: "/units?tab=policies&focus=initiative",
  },
];

const kindVariant: Record<
  PolicyDimensionRow["kind"],
  "default" | "success" | "warning" | "destructive" | "secondary" | "outline"
> = {
  caps: "warning",
  list: "secondary",
  level: "default",
};

const kindLabel: Record<PolicyDimensionRow["kind"], string> = {
  caps: "Caps",
  list: "Allow / block list",
  level: "Tiered",
};

export default function PoliciesIndexPage() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-primary" aria-hidden="true" />
          <h1 className="text-2xl font-bold">Policies</h1>
        </div>
        <p className="text-sm text-muted-foreground">
          Skill, model, cost, execution, and initiative constraints across
          every unit.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dimensions</CardTitle>
        </CardHeader>
        <CardContent className="divide-y divide-border p-0">
          {POLICY_DIMENSIONS.map((row) => {
            const Icon = row.icon;
            return (
              <div
                key={row.slug}
                data-testid={`policy-row-${row.slug}`}
                className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:items-start sm:justify-between"
              >
                <div className="flex min-w-0 items-start gap-3">
                  <Icon
                    aria-hidden="true"
                    className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground"
                  />
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold">
                        {row.name}
                      </span>
                      {/* Mono identifier — matches the `TabPolicies` kit
                          and the Explorer's status header. */}
                      <Badge
                        variant="outline"
                        className="font-mono text-[11px]"
                      >
                        {row.identifier}
                      </Badge>
                      {/* Severity pill — which kind of constraint this
                          dimension applies. */}
                      <Badge variant={kindVariant[row.kind]}>
                        {kindLabel[row.kind]}
                      </Badge>
                      {/* Rule-count badge — illustrative until #411
                          wires the tenant-wide read. */}
                      <Badge
                        variant="outline"
                        className="tabular-nums text-[11px]"
                        title="Rule count across all units (pending #411)"
                      >
                        0 rules
                      </Badge>
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {row.description}
                    </p>
                  </div>
                </div>
                <Link
                  href={row.editHref}
                  className="inline-flex shrink-0 items-center gap-1 self-start rounded-md px-2 py-1 text-xs text-primary hover:underline sm:self-center"
                >
                  Open in Explorer
                  <ArrowRight className="h-3 w-3" aria-hidden="true" />
                </Link>
              </div>
            );
          })}
        </CardContent>
      </Card>

      <Card>
        <CardContent className="flex flex-col gap-2 p-4 text-xs text-muted-foreground">
          <p>
            Rule counts, severity rollups, and the cross-unit diff view are
            tracked by the unified Policies rollup work — issue{" "}
            <Link
              href="https://github.com/cvoya-com/spring-voyage/issues/411"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-primary hover:underline"
            >
              #411
              <ExternalLink className="h-3 w-3" aria-hidden="true" />
            </Link>
            . The per-unit editor already ships in the Explorer.
          </p>
          <div className="flex flex-wrap gap-3">
            <Link
              href="/units"
              className="inline-flex items-center gap-1 text-primary hover:underline"
            >
              Per-unit Policies tab
              <ArrowRight className="h-3 w-3" aria-hidden="true" />
            </Link>
            <Link
              href="/analytics/costs"
              className="inline-flex items-center gap-1 text-primary hover:underline"
            >
              Cost caps in Analytics
              <ArrowRight className="h-3 w-3" aria-hidden="true" />
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
