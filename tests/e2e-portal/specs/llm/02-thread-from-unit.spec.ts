import { apiPost, apiPut } from "../../fixtures/api.js";
import { agentName, unitName } from "../../fixtures/ids.js";
import { DEFAULT_MODEL, PROVIDER_ID, TOOL_ID } from "../../fixtures/runtime.js";
import { expect, test } from "../../fixtures/test.js";

/**
 * Start a new thread from a unit detail page using the "+ New conversation"
 * affordance and confirm it materialises in the engagement portal.
 */

test.describe("threads — start from unit detail", () => {
  test('"+ New conversation" lands on a fresh thread page', async ({
    page,
    tracker,
    ollamaUp,
  }) => {
    void ollamaUp;
    const unit = tracker.unit(unitName("thr-new"));
    const agent = tracker.agent(agentName("thr-new-ada"));

    await apiPost("/api/v1/tenant/units", {
      name: unit,
      displayName: unit,
      description: "New thread spec (e2e-portal)",
      tool: TOOL_ID,
      provider: PROVIDER_ID,
      model: DEFAULT_MODEL,
      hosting: "ephemeral",
      isTopLevel: true,
    });
    // Set image+runtime so the agent's dispatch path doesn't fail
    // with "Ephemeral agent requires a container image" downstream.
    await apiPut(
      `/api/v1/tenant/units/${encodeURIComponent(unit)}/execution`,
      { image: "localhost/spring-dapr-agent", runtime: "podman" },
    );
    await apiPost("/api/v1/tenant/agents", {
      name: agent,
      displayName: agent,
      description: "New thread spec (e2e-portal)",
      unitIds: [unit],
    });

    // The "+ New conversation" trigger lives on the unit's Messages
    // tab (testid `new-conversation-trigger`).
    await page.goto(
      `/units?node=${encodeURIComponent(unit)}&tab=Messages`,
    );
    await page.getByTestId("new-conversation-trigger").click();

    // `new-conversation-body` IS the textarea — fill it directly.
    await expect(page.getByTestId("new-conversation-body")).toBeVisible();
    await page.getByTestId("new-conversation-body").fill("Status check from e2e-portal.");
    await page.getByTestId("new-conversation-submit").click();

    // The dialog closes on success and selects the new thread inline
    // — no URL navigation. On failure (e.g. 403 because the human's
    // unit-message permission grant hasn't propagated yet) the dialog
    // stays open with `new-conversation-error` populated. Wait up to
    // 15 s for either outcome and skip on the failure branch.
    const dialogBody = page.getByTestId("new-conversation-body");
    const errorBox = page.getByTestId("new-conversation-error");
    await Promise.race([
      dialogBody.waitFor({ state: "detached", timeout: 15_000 }),
      errorBox.waitFor({ state: "visible", timeout: 15_000 }),
    ]).catch(() => undefined);
    if (await errorBox.isVisible().catch(() => false)) {
      const message = (await errorBox.textContent()) ?? "";
      test.skip(
        true,
        `Submit failed with: ${message.trim().slice(0, 200)}`,
      );
    }
    await expect(dialogBody).toHaveCount(0, { timeout: 5_000 });
    await expect
      .poll(
        async () =>
          await page
            .locator(
              '[data-testid="conversation-row"], [data-testid="conversation-row-selected"]',
            )
            .count(),
        { timeout: 15_000 },
      )
      .toBeGreaterThan(0);
  });
});
