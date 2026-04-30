/**
 * Recently-used image reference history (#622 / #968).
 *
 * Persists up to `MAX_IMAGE_HISTORY` distinct image reference strings in
 * `localStorage` so the unit-creation wizard and agent-execution surfaces
 * can offer autocomplete suggestions without a backend round-trip.
 *
 * Design choices:
 *   * localStorage (not sessionStorage) — image references are useful across
 *     sessions. They contain no secrets, just public container image tags.
 *   * FIFO eviction with dedup on insert: a reference already in the list
 *     moves to the front rather than accumulating duplicates.
 *   * Quota / SecurityError failures are swallowed — loss of history is
 *     graceful degradation; the operator can still type freely.
 *   * SSR-safe: every call guards `typeof window`. The module is imported
 *     by `"use client"` components that may be server-rendered; the
 *     guards prevent `ReferenceError: localStorage is not defined`.
 */

const STORAGE_KEY = "spring.image-history.v1";
export const MAX_IMAGE_HISTORY = 20;

/**
 * Read the persisted list. Returns an empty array on any failure.
 */
export function loadImageHistory(): string[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((v): v is string => typeof v === "string" && v.trim().length > 0);
  } catch {
    return [];
  }
}

/**
 * Add `reference` to the front of the history list, deduplicating and
 * capping at `MAX_IMAGE_HISTORY`. Silently ignores blank strings and
 * storage errors.
 */
export function recordImageReference(reference: string): void {
  if (typeof window === "undefined") return;
  const trimmed = reference.trim();
  if (!trimmed) return;
  try {
    const existing = loadImageHistory();
    const deduped = existing.filter((r) => r !== trimmed);
    const next = [trimmed, ...deduped].slice(0, MAX_IMAGE_HISTORY);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Quota exceeded or SecurityError — best-effort.
  }
}
