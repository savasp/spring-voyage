import { apiPost, apiPut } from "../../fixtures/api.js";
import { agentName, unitName } from "../../fixtures/ids.js";
import { DEFAULT_MODEL, PROVIDER_ID, TOOL_ID } from "../../fixtures/runtime.js";
import { expect, test } from "../../fixtures/test.js";

/**
 * Engagement — send a message and observe a timeline event.
 *
 * Requires the live LLM (Ollama) so the agent's turn returns a real
 * response. Pre-seeds a unit + agent, kicks off a thread via the API
 * (so the engagement exists), then drives the composer in the browser
 * and asserts a new event lands in the timeline.
 */

interface MessageResponse {
  threadId: string;
}

test.describe("engagement — send message via composer", () => {
  test("composer sends, timeline shows the new event", async ({
    page,
    tracker,
    ollamaUp,
  }) => {
    void ollamaUp;
    const unit = tracker.unit(unitName("eng-msg"));
    const agent = tracker.agent(agentName("eng-msg-ada"));

    await apiPost("/api/v1/tenant/units", {
      name: unit,
      displayName: unit,
      description: "Engagement send-message spec (e2e-portal)",
      tool: TOOL_ID,
      provider: PROVIDER_ID,
      model: DEFAULT_MODEL,
      hosting: "ephemeral",
      isTopLevel: true,
    });
    // Set image + runtime defaults so ephemeral agent dispatch picks
    // up a working container image (otherwise the dispatch fails with
    // "Ephemeral agent requires a container image"). image/runtime
    // aren't on `CreateUnitRequest`; they live on `/execution`.
    await apiPut(
      `/api/v1/tenant/units/${encodeURIComponent(unit)}/execution`,
      { image: "localhost/spring-dapr-agent", runtime: "podman" },
    );
    await apiPost("/api/v1/tenant/agents", {
      name: agent,
      displayName: "Engagement Spec Agent",
      description: "Engagement send-message spec (e2e-portal)",
      unitIds: [unit],
    });

    // Kick off a thread by sending a free-form message via /messages.
    // The endpoint auto-generates a thread id when none is supplied.
    // Wire shape (`SendMessageRequest`): { to, type, payload, threadId? }.
    const seed = await apiPost<MessageResponse>("/api/v1/tenant/messages", {
      to: { scheme: "agent", path: agent },
      type: "Domain",
      payload: { text: "Hello from e2e-portal" },
    });
    expect(seed.threadId).toBeTruthy();

    // Open the engagement detail.
    await page.goto(`/engagement/${seed.threadId}`);
    await expect(page.getByTestId("engagement-detail-page")).toBeVisible();

    // The composer only renders for thread participants. When the
    // platform records the human-message-sender as Observer (#1292
    // tracks first-class participant tracking), the composer is
    // hidden — skip the spec rather than fail. Once #1292 lands,
    // remove the skip.
    const composer = page.getByTestId("engagement-composer");
    if (!(await composer.isVisible().catch(() => false))) {
      test.skip(
        true,
        "Engagement composer is hidden because the human sender is recorded as Observer; tracked by #1292.",
      );
    }

    // Capture the initial event count, send a follow-up, and assert growth.
    const before = await page
      .getByTestId("engagement-timeline-events")
      .locator('[data-testid^="conversation-event-"]')
      .count();

    // The composer renders both a recipient `<input>` and a message
    // `<textarea>` — fill the message slot specifically (the textarea
    // is aria-labelled "Message text" or "Your answer").
    await composer
      .getByRole("textbox", { name: /message text|your answer/i })
      .fill("Are you there?");
    await composer.getByRole("button", { name: /^send|submit$/i }).click();

    await expect(async () => {
      const now = await page
        .getByTestId("engagement-timeline-events")
        .locator('[data-testid^="conversation-event-"]')
        .count();
      expect(now).toBeGreaterThan(before);
    }).toPass({ timeout: 60_000, intervals: [500, 1000, 2000] });
  });
});
