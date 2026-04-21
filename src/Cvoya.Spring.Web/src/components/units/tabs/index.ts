"use client";

import type { ComponentType } from "react";

import type { NodeKind, TabName, TreeNode } from "../aggregate";

/**
 * Standard prop shape every per-tab content component receives.
 *
 * Tabs that need richer wiring (selection callbacks, tab teleport for
 * deeplinked child cards) extend this type via intersection — see
 * `EXP-tab-unit-overview` once it lands. The base shape always carries
 * the active node + its breadcrumb path so every tab can render headers,
 * empty-states, and "see all" links without re-querying the tree.
 */
export interface TabContentProps {
  node: TreeNode;
  path: TreeNode[];
}

/**
 * Tab key shape used by the registry: `"<kind>.<tab>"`. Examples:
 *
 *   "Unit.Overview"   → renders the unit Overview tab
 *   "Agent.Skills"    → renders the agent Skills tab
 *   "Tenant.Budgets"  → renders the tenant Budgets tab
 *
 * The `(kind, tab)` pair, not the bare tab name, is the key because some
 * tab labels (Overview, Activity, Messages, Memory, Policies) are shared
 * across kinds with different content shapes. Keying on both lets every
 * EXP-tab-* issue register its own component in isolation without colliding
 * with sibling tabs.
 */
export type TabKey = `${NodeKind}.${TabName}`;

export function tabKey(kind: NodeKind, tab: TabName): TabKey {
  return `${kind}.${tab}` as TabKey;
}

/**
 * The tab registry — initialized empty by `FOUND-tabscaffold`; each
 * `EXP-tab-*` issue lands one new entry. A `TabPlaceholder` is rendered
 * by `<DetailPane>` when the lookup misses, so the foundation PR is
 * mergeable without any tab content yet.
 *
 * Why a `Map` instead of an object literal: writes from sibling files
 * (each EXP-tab-* PR registers its own slot) compose without re-export
 * gymnastics, and the Map's identity does not change so React memoization
 * around `<DetailPane>` stays valid.
 *
 * The map is exported `const` and module-scoped — adding entries goes
 * through {@link registerTab} so registrations are explicit + grep-able.
 */
const REGISTRY = new Map<TabKey, ComponentType<TabContentProps>>();

/**
 * Register a tab content component for a given `(kind, tab)` pair.
 *
 * EXP-tab-* issues call this from the module top-level so the registration
 * runs at import time. Re-registering a key is a no-op error in dev (we
 * `console.warn` once instead of throwing — duplicate registrations from
 * HMR shouldn't crash the app).
 */
export function registerTab(
  kind: NodeKind,
  tab: TabName,
  component: ComponentType<TabContentProps>,
): void {
  const key = tabKey(kind, tab);
  if (REGISTRY.has(key) && process.env.NODE_ENV !== "production") {
    console.warn(
      `[units/tabs] duplicate registration for ${key} — keeping the first one`,
    );
    return;
  }
  REGISTRY.set(key, component);
}

/**
 * Look up the component registered for a `(kind, tab)` pair, or `null`
 * when no component has been registered. The detail pane substitutes its
 * `<TabPlaceholder>` for the `null` case.
 */
export function lookupTab(
  kind: NodeKind,
  tab: TabName,
): ComponentType<TabContentProps> | null {
  return REGISTRY.get(tabKey(kind, tab)) ?? null;
}

/**
 * Snapshot of every currently-registered tab key. Used by the registry
 * unit test to assert the foundation registry ships empty and by the
 * coverage assertions that EXP-tab-* PRs will land later.
 */
export function registeredTabs(): TabKey[] {
  return Array.from(REGISTRY.keys());
}

/**
 * Test-only escape hatch — clears every registration. Tests that need a
 * known-empty registry call this in `beforeEach`. Production code never
 * imports this helper (it isn't re-exported from `units/`).
 */
export function __resetTabRegistryForTesting(): void {
  REGISTRY.clear();
}
