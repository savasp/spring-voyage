/**
 * Tree-node shape used across the Explorer surface (`<UnitExplorer>`,
 * `<UnitTree>`, `<DetailPane>`, the per-tab content components, and the
 * dashboard `<UnitCard>` / `<AgentCard>` once they pick up tab chips).
 *
 * Three node kinds live in the same tree:
 *
 *   - `Tenant` — the synthesized root node. The plan §3 defines this as a
 *     UI-only construct: there is no "root unit" entity on the server. The
 *     Explorer presents it so multi-top-level-unit tenants still get a
 *     single navigation entry point.
 *   - `Unit` — a unit. Units may contain other units AND agents.
 *   - `Agent` — an agent. Every agent has ≥1 parent unit (per `SVR-membership`);
 *     agents that belong to multiple units appear as alias children under
 *     each parent. The `primaryParentId` field on the source payload picks
 *     which appearance owns the agent's "canonical" surface; the others
 *     render as deduplicated alias rows.
 *
 * The `status` set matches the design kit (`Explorer.jsx`) and is the same
 * vocabulary used by the dashboard cards. `aggregate()` ranks them when
 * rolling worst-status up the subtree.
 */
export type NodeKind = "Tenant" | "Unit" | "Agent";

export type NodeStatus =
  | "running"
  | "starting"
  | "paused"
  | "stopped"
  | "error";

export interface TreeNode {
  /**
   * Stable identifier — slug for units/tenants, address for agents. Used
   * as the React `key`, the URL `?node=` parameter, the Cmd-K teleport
   * target, and the `aggregate()` cache key.
   */
  id: string;
  /** Human-readable name shown in the tree row + detail pane title. */
  name: string;
  kind: NodeKind;
  status: NodeStatus;
  /**
   * Optional one-line description rendered above the stat tiles on the
   * Overview tab. Tenants and units typically supply one; most agents do
   * not — the UI silently omits the description when it's missing.
   */
  desc?: string;
  /** Self cost in USD over the last 24 h. Subtree totals come from {@link aggregate}. */
  cost24h?: number;
  /** Self message volume over the last 24 h. Subtree totals come from {@link aggregate}. */
  msgs24h?: number;
  /** Agent role (e.g. "tech-lead", "reviewer"). Only set on agent nodes. */
  role?: string;
  /** Number of skills equipped on an agent. Drives the Skills count tile. */
  skills?: number;
  /**
   * Direct children — units and/or agents under this node. Undefined
   * means "leaf"; an empty array means "branch with no children right now"
   * (which the tree renders without a twisty).
   */
  children?: TreeNode[];
}

/**
 * Subtree roll-up returned by {@link aggregate}.
 *
 * The roll-up includes the node it was called on. Both `units` and `agents`
 * count *every* unit/agent in the subtree, so the root node call returns
 * a tenant-wide total and a leaf-agent call returns `{ agents: 1, units: 0 }`.
 *
 * `worst` ranks statuses by severity (`error > starting > paused > running > stopped`)
 * and walks the subtree to surface the most concerning state. Used by
 * tree rows to colour the status dot of a *collapsed* branch — operators
 * can spot a failing agent buried four levels deep without expanding.
 */
export interface SubtreeAggregate {
  cost: number;
  msgs: number;
  agents: number;
  units: number;
  worst: NodeStatus;
}

const STATUS_RANK: Record<NodeStatus, number> = {
  error: 4,
  starting: 3,
  paused: 2,
  running: 1,
  stopped: 0,
};

/**
 * Recursively roll up cost, message volume, agent count, unit count, and
 * the worst-status-in-subtree for a node. Pure function — given the same
 * tree it returns the same result, so memoise around it freely.
 *
 * Direct port of `aggregate()` from `~/tmp/SpringVoyageDesign/project/ui_kits/portal/Explorer.jsx`,
 * tightened with TypeScript types.
 */
export function aggregate(node: TreeNode): SubtreeAggregate {
  let cost = node.cost24h ?? 0;
  let msgs = node.msgs24h ?? 0;
  let agents = node.kind === "Agent" ? 1 : 0;
  let units = node.kind === "Unit" ? 1 : 0;
  let worst: NodeStatus = node.status;

  for (const child of node.children ?? []) {
    const sub = aggregate(child);
    cost += sub.cost;
    msgs += sub.msgs;
    agents += sub.agents;
    units += sub.units;
    if (STATUS_RANK[sub.worst] > STATUS_RANK[worst]) {
      worst = sub.worst;
    }
  }

  return { cost, msgs, agents, units, worst };
}

/**
 * Flatten a tree into a depth-first list of `{ node, path }` records.
 *
 * `path` is the chain of ancestors from the root down to and including
 * `node`. Used by the detail pane's breadcrumb and by `findIndex()` for
 * O(1) selection lookup keyed on node id.
 */
export function flattenTree(
  node: TreeNode,
  path: TreeNode[] = [],
  out: Array<{ node: TreeNode; path: TreeNode[] }> = [],
): Array<{ node: TreeNode; path: TreeNode[] }> {
  const here = [...path, node];
  out.push({ node, path: here });
  for (const child of node.children ?? []) {
    flattenTree(child, here, out);
  }
  return out;
}

/**
 * Build an `id → { node, path }` index for fast selection lookup.
 *
 * The Explorer uses this every render to translate the URL-driven
 * `?node=…` parameter into the right tree node + breadcrumb path
 * without re-walking the tree on every selection change.
 */
export function findIndex(tree: TreeNode): {
  byId: Record<string, { node: TreeNode; path: TreeNode[] }>;
} {
  const all = flattenTree(tree);
  const byId: Record<string, { node: TreeNode; path: TreeNode[] }> = {};
  for (const entry of all) {
    byId[entry.node.id] = entry;
  }
  return { byId };
}

/**
 * Tab catalogs by node kind. Mirrors the locked v2.0 disposition table from
 * the plan (§3): Unit gets 8 visible tabs (Overview through Config); Agent
 * gets 8 visible tabs; Tenant gets 5. The order here is the order the tab
 * strip renders.
 */
export const UNIT_TABS = [
  "Overview",
  "Agents",
  "Orchestration",
  "Activity",
  "Messages",
  "Memory",
  "Policies",
  "Config",
] as const;

export const AGENT_TABS = [
  "Overview",
  "Activity",
  "Messages",
  "Memory",
  "Skills",
  "Traces",
  "Clones",
  "Config",
] as const;

export const TENANT_TABS = [
  "Overview",
  "Activity",
  "Policies",
  "Budgets",
  "Memory",
] as const;

export type UnitTabName = (typeof UNIT_TABS)[number];
export type AgentTabName = (typeof AGENT_TABS)[number];
export type TenantTabName = (typeof TENANT_TABS)[number];
export type TabName = UnitTabName | AgentTabName | TenantTabName;

export function tabsFor(kind: NodeKind): readonly TabName[] {
  switch (kind) {
    case "Agent":
      return AGENT_TABS;
    case "Tenant":
      return TENANT_TABS;
    case "Unit":
    default:
      return UNIT_TABS;
  }
}
