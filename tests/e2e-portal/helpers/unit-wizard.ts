import type { Page } from "@playwright/test";

import { expect } from "@playwright/test";

import { DEFAULT_MODEL, PROVIDER_ID, TOOL_ID } from "../fixtures/runtime.js";

/**
 * Drives `/units/create` — the 6-step wizard at
 * `src/Cvoya.Spring.Web/src/app/units/create/page.tsx`.
 *
 * Steps (per STEP_LABELS in the wizard source):
 *   1. Identity         — name, displayName, parent choice
 *   2. Execution        — tool, provider, model, environment defaults
 *   3. Mode             — scratch / template / yaml
 *   4. Connector        — optional binding (we skip in fast specs)
 *   5. Secrets          — optional pending secrets
 *   6. Finalize         — review + Create
 *
 * The helper is opinionated: it pins tool=dapr-agent, provider=ollama
 * (see fixtures/runtime.ts for the rationale) and creates a top-level
 * unit in scratch mode. Specs that diverge call it with overrides or
 * drive the wizard manually.
 */

export interface ScratchUnitOptions {
  name: string;
  displayName?: string;
  description?: string;
  /** Override the pinned model. Falls back to DEFAULT_MODEL. */
  model?: string;
  /**
   * Set true to await the wizard's auto-validation phase reaching a
   * terminal state and redirecting into the explorer. Defaults to
   * false because the wizard's auto-start path is currently broken
   * for credential-free runtimes (Ollama) — it POSTs `/start` from
   * Draft and the actor rejects with `Draft → Starting` per #939.
   * Specs that need the unit to exist verify via API; specs that
   * exercise the validation UI itself should opt in explicitly once
   * that bug is resolved.
   */
  awaitValidation?: boolean;
  /** Per-step error tolerance — surfaces the wizard's stepError text on failure. */
  stepErrorTimeoutMs?: number;
}

export const WIZARD_DEFAULT_TIMEOUTS = {
  validationPanelMs: 90_000,
};

/**
 * Create a top-level unit from scratch via the wizard. Returns when the
 * wizard navigates away from `/units/create` (i.e. unit POSTed and the
 * page transitioned to `/units/<name>`).
 *
 * The caller is responsible for tracking the returned name in the
 * artifact tracker for cleanup.
 */
export async function createScratchUnit(
  page: Page,
  opts: ScratchUnitOptions,
): Promise<{ unitUrl: string }> {
  const displayName = opts.displayName ?? opts.name;
  const description = opts.description ?? `Created by e2e-portal: ${opts.name}`;

  await page.goto("/units/create");

  // ── Step 1 — Identity ──────────────────────────────────────────────────
  await page.getByLabel("Name").or(page.getByRole("textbox", { name: /^name$/i })).first().fill(opts.name);
  await page.getByLabel("Display name").or(page.getByRole("textbox", { name: /display name/i })).first().fill(displayName);
  // Description is optional — the textarea fallback is by aria-label.
  const descField = page.getByLabel("Description").first();
  if (await descField.isVisible().catch(() => false)) {
    await descField.fill(description);
  }
  // Top-level vs has-parents (#814). Click the explicit top-level chip.
  await page.getByTestId("parent-choice-top-level").click();

  await clickNext(page);

  // ── Step 2 — Execution ─────────────────────────────────────────────────
  await page.getByLabel("Execution tool").selectOption(TOOL_ID);
  // Provider dropdown only renders when tool === dapr-agent.
  await page.getByLabel("LLM provider").selectOption(PROVIDER_ID);
  // The model dropdown is hidden until the runtime catalog resolves.
  // Wait for it to appear, then pick.
  const modelSelect = page.getByLabel("Model");
  await modelSelect.waitFor({ state: "visible", timeout: 30_000 });
  // Picking by exact value lets us target whichever Ollama model the
  // operator has installed (the catalog is the union of seed models +
  // tenant overrides, so this avoids hard-coding a label).
  const desired = opts.model ?? DEFAULT_MODEL;
  const optionValues = await modelSelect.evaluate((el) =>
    Array.from((el as HTMLSelectElement).options).map((o) => o.value),
  );
  if (optionValues.includes(desired)) {
    await modelSelect.selectOption(desired);
  } else if (optionValues.length > 0) {
    // Fall back to whatever the runtime offered first — the spec just
    // needs a valid choice to proceed past the wizard's "model required"
    // gate.
    const firstValue = optionValues[0]!;
    await modelSelect.selectOption(firstValue);
  } else {
    throw new Error(
      `Wizard model dropdown is empty — Ollama returned no models. Pull one ` +
        `via 'ollama pull ${desired}' before running this spec.`,
    );
  }

  await clickNext(page);

  // ── Step 3 — Mode ──────────────────────────────────────────────────────
  // Card-based selection. The button's accessible name concatenates the
  // title ("Scratch") and the description, so we match on the leading
  // title plus a word-boundary so the regex doesn't drift into siblings.
  // See `ModeCard` in `src/Cvoya.Spring.Web/src/app/units/create/page.tsx`.
  await pickWizardMode(page, "scratch");
  await clickNext(page);

  // ── Step 4 — Connector ────────────────────────────────────────────────
  // Skip — explicit "Skip connector" affordance, falls back to Next when absent.
  const skipConnector = page.getByRole("button", { name: /skip connector|no connector|don.?t bind/i }).first();
  if (await skipConnector.isVisible().catch(() => false)) {
    await skipConnector.click();
  } else {
    await clickNext(page);
  }

  // ── Step 5 — Secrets ──────────────────────────────────────────────────
  // No pending secrets in the default flow — Next.
  await clickNext(page);

  // ── Step 6 — Finalize ─────────────────────────────────────────────────
  await page.getByTestId("create-unit-button").click();

  // The wizard transitions into the in-page Validation view after POST
  // succeeds; verify the panel mounts so we know the create succeeded.
  await expect(page.getByTestId("wizard-validation-view")).toBeVisible({
    timeout: 30_000,
  });

  if (opts.awaitValidation) {
    // Wizard navigates to the explorer-deep-link form on success
    // (`/units?node=<name>&tab=Overview`). The legacy `/units/<name>`
    // routes are gone (they now redirect to the same deep-link).
    await page.waitForURL(/\/units\?[^#]*\bnode=[^&]+/, {
      timeout: WIZARD_DEFAULT_TIMEOUTS.validationPanelMs,
    });
    return { unitUrl: page.url() };
  }

  // Skip the wizard's auto-validation hop — see `awaitValidation`
  // doc above. Navigate the test to the explorer's deep-link
  // ourselves so callers always land on the unit detail page.
  const target = `/units?node=${encodeURIComponent(opts.name)}&tab=Overview`;
  await page.goto(target);
  return { unitUrl: page.url() };
}

async function clickNext(page: Page): Promise<void> {
  await page.getByRole("button", { name: /^next$/i }).click();
}

/**
 * Wizard mode-card selector. The mode cards are buttons whose accessible
 * name starts with the title ("Scratch" / "Template" / "YAML") and is
 * followed by the descriptive blurb. Match on the title prefix so the
 * regex pins exactly one card per mode.
 */
export async function pickWizardMode(
  page: Page,
  mode: "scratch" | "template" | "yaml",
): Promise<void> {
  const titlePattern: Record<typeof mode, RegExp> = {
    scratch: /^Scratch\b/i,
    template: /^Template\b/i,
    yaml: /^YAML\b/i,
  };
  await page
    .getByRole("button", { name: titlePattern[mode] })
    .first()
    .click();
}
