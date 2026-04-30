import { unitName } from "../../fixtures/ids.js";
import { expect, test } from "../../fixtures/test.js";
import { pickWizardMode } from "../../helpers/unit-wizard.js";

/**
 * Wizard: create-from-YAML flow. The wizard accepts a pasted YAML
 * manifest and POSTs it to /api/v1/tenant/units/from-yaml. Mirrors the
 * shell scenario `04-create-unit-from-template.sh`'s YAML branch.
 */

test.describe("units — create from yaml (wizard)", () => {
  test("paste a minimal manifest, finalize, land on detail page", async ({
    page,
    tracker,
  }) => {
    const name = tracker.unit(unitName("yaml"));

    await page.goto("/units/create");

    // Step 1 — Identity (name still required at the wizard level for
    // routing; the YAML's `name` field has to match).
    await page.getByLabel("Name").or(page.getByRole("textbox", { name: /^name$/i })).first().fill(name);
    await page.getByLabel("Display name").or(page.getByRole("textbox", { name: /display name/i })).first().fill(name);
    await page.getByTestId("parent-choice-top-level").click();
    await page.getByRole("button", { name: /^next$/i }).click();

    // Step 2 — Execution
    await page.getByLabel("Execution tool").selectOption("dapr-agent");
    await page.getByLabel("LLM provider").selectOption("ollama");
    const modelSelect = page.getByLabel("Model");
    await modelSelect.waitFor({ state: "visible", timeout: 30_000 });
    const values = await modelSelect.evaluate((el) =>
      Array.from((el as HTMLSelectElement).options).map((o) => o.value),
    );
    if (values.length === 0) test.skip(true, "Ollama returned no models");
    const firstValue = values[0]!;
    await modelSelect.selectOption(firstValue);
    await page.getByRole("button", { name: /^next$/i }).click();

    // Step 3 — Mode = YAML. The manifest grammar is `unit:` rooted —
    // see `ManifestParser` and `UnitManifest`. The Kubernetes-style
    // `apiVersion/kind/metadata/spec` shape was retired in v0.1.
    await pickWizardMode(page, "yaml");
    const yamlBody = [
      `unit:`,
      `  name: ${name}`,
      `  description: Created by 05-units-create-from-yaml.spec.ts`,
      `  ai:`,
      `    tool: dapr-agent`,
      `    provider: ollama`,
      `    model: ${firstValue}`,
      ``,
    ].join("\n");
    await page.getByRole("textbox", { name: /yaml|manifest/i }).first().fill(yamlBody);
    await page.getByRole("button", { name: /^next$/i }).click();

    // Step 4 — Connector (skip)
    const skip = page.getByRole("button", { name: /skip connector|don.?t bind/i }).first();
    if (await skip.isVisible().catch(() => false)) {
      await skip.click();
    } else {
      await page.getByRole("button", { name: /^next$/i }).click();
    }

    // Step 5 — Secrets (none) → Step 6 — Finalize.
    // The wizard's auto-start path is broken for credential-free
    // runtimes (see `helpers/unit-wizard.ts` § `awaitValidation`);
    // navigate to the explorer ourselves after confirming the
    // validation view mounted (which proves the create POST landed).
    await page.getByRole("button", { name: /^next$/i }).click();
    await page.getByTestId("create-unit-button").click();
    await expect(page.getByTestId("wizard-validation-view")).toBeVisible({
      timeout: 30_000,
    });
    await page.goto(
      `/units?node=${encodeURIComponent(name)}&tab=Overview`,
    );
    await expect(page.getByRole("heading", { name })).toBeVisible();
  });
});
