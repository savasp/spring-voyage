import { unitName } from "../../fixtures/ids.js";
import { expect, test } from "../../fixtures/test.js";
import { createScratchUnit } from "../../helpers/unit-wizard.js";

/**
 * Wizard: 6-step "from scratch" flow with the dapr-agent + ollama runtime
 * pin. The shell counterpart is `tests/e2e/scenarios/fast/02-create-unit-scratch.sh`.
 */

test.describe("units — create from scratch (wizard)", () => {
  test("creates a top-level unit; lands on /units/<name> with the unit visible", async ({
    page,
    tracker,
  }) => {
    const name = tracker.unit(unitName("wiz-scratch"));
    const displayName = "Wizard Scratch Unit";
    const { unitUrl } = await createScratchUnit(page, {
      name,
      displayName,
      description: "Created by 03-units-create-scratch.spec.ts",
    });
    expect(unitUrl).toContain(`node=${name}`);

    // The detail pane heading carries the unit's displayName (the
    // unit name appears in the breadcrumb / address copier).
    await expect(
      page.getByRole("heading", { name: displayName }),
    ).toBeVisible();

    // Cross-check via the units list — the unit name shows in the
    // tree's address copy button + breadcrumb pill.
    await page.goto("/units");
    await expect(page.getByTestId("unit-explorer-route")).toBeVisible();
    await expect(
      page.getByRole("treeitem", { name: new RegExp(displayName, "i") }).first(),
    ).toBeVisible();
  });

  test("rejects an invalid name with an inline error", async ({ page }) => {
    await page.goto("/units/create");
    // Names must match /^[a-z0-9-]+$/. Try an invalid one.
    await page.getByLabel("Name").or(page.getByRole("textbox", { name: /^name$/i })).first().fill("Has Spaces");
    await page.getByLabel("Display name").or(page.getByRole("textbox", { name: /display name/i })).first().fill("oops");
    await page.getByTestId("parent-choice-top-level").click();
    // Step 1's validation hint surfaces via `stepError` after Next is
    // pressed (the `next-disabled-reason` testid is Step-2 only — see
    // `nextDisabledReason` in app/units/create/page.tsx). Click Next and
    // assert the URL-safe error message appears inline.
    await page.getByRole("button", { name: /^next$/i }).click();
    // The wizard surfaces the URL-safe rule in two places — a static
    // helper hint at the top of step 1 and the post-Next `stepError`
    // banner. Match the banner specifically (it's a paragraph-level
    // alert; the helper is part of an aside / muted hint).
    await expect(
      page.getByText(
        /Name must be URL-safe \(lowercase letters, digits, and hyphens\)/i,
      ).first(),
    ).toBeVisible({ timeout: 5_000 });
  });
});
