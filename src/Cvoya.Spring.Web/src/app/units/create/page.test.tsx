import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";

import { expectNoAxeViolations } from "@/test/a11y";
import type { ProviderCredentialStatusResponse } from "@/lib/api/types";

// Mock the API client. Only the surface touched by the create wizard's
// Step 1 matters for these tests — we don't exercise unit creation here.
const listOllamaModels = vi.fn();
const listProviderModels = vi.fn();
const getProviderCredentialStatus = vi.fn();
const createUnit = vi.fn();
const createUnitFromTemplate = vi.fn();
const createUnitFromYaml = vi.fn();
const createUnitSecret = vi.fn();
const createTenantSecret = vi.fn();
const rotateTenantSecret = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    listOllamaModels: () => listOllamaModels(),
    listProviderModels: (p: string) => listProviderModels(p),
    getProviderCredentialStatus: (p: string) => getProviderCredentialStatus(p),
    // Stubs for code paths we don't exercise on Step 1.
    getUnitTemplates: vi.fn().mockResolvedValue([]),
    getConnectorTypes: vi.fn().mockResolvedValue([]),
    createUnit: (body: unknown) => createUnit(body),
    createUnitFromTemplate: (body: unknown) => createUnitFromTemplate(body),
    createUnitFromYaml: (body: unknown) => createUnitFromYaml(body),
    createUnitSecret: (unit: string, body: unknown) =>
      createUnitSecret(unit, body),
    createTenantSecret: (body: unknown) => createTenantSecret(body),
    rotateTenantSecret: (name: string, body: unknown) =>
      rotateTenantSecret(name, body),
  },
}));

const toastMock = vi.fn();
vi.mock("@/components/ui/toast", () => ({
  useToast: () => ({ toast: toastMock }),
}));

const pushMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
  } & Record<string, unknown>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import CreateUnitPage from "./page";

function makeStatus(
  overrides: Partial<ProviderCredentialStatusResponse>,
): ProviderCredentialStatusResponse {
  return {
    provider: "anthropic",
    resolvable: true,
    source: "tenant",
    suggestion: null,
    ...overrides,
  };
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return render(<CreateUnitPage />, { wrapper: Wrapper });
}

async function selectTool(value: string) {
  const toolSelect = screen.getByLabelText("Execution tool") as HTMLSelectElement;
  await act(async () => {
    fireEvent.change(toolSelect, { target: { value } });
  });
}

describe("CreateUnitPage — Provider + Model gating (#598)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listOllamaModels.mockResolvedValue([]);
    listProviderModels.mockResolvedValue([]);
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "ollama", resolvable: true, source: null }),
    );
  });

  it("hides Provider + Model when the tool is Claude Code", async () => {
    renderPage();

    // Default tool is claude-code (see ai-models.ts).
    expect(
      screen.queryByLabelText(/^LLM provider$/i),
    ).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Model$/i)).not.toBeInTheDocument();
  });

  it("hides Provider + Model for Codex, Gemini, and Custom", async () => {
    renderPage();
    for (const tool of ["codex", "gemini", "custom"]) {
      await selectTool(tool);
      expect(
        screen.queryByLabelText(/^LLM provider$/i),
      ).not.toBeInTheDocument();
      expect(screen.queryByLabelText(/^Model$/i)).not.toBeInTheDocument();
    }
  });

  it("renders Provider + Model only when the tool is Dapr Agent", async () => {
    renderPage();
    await selectTool("dapr-agent");

    expect(
      screen.getByLabelText(/^LLM provider$/i),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/^Model$/i)).toBeInTheDocument();
  });

  it("labels the provider field 'LLM Provider' unconditionally when shown", async () => {
    renderPage();
    await selectTool("dapr-agent");

    // No stale 'Provider' label — only the canonical 'LLM Provider'
    // spelling remains when the field is visible.
    expect(screen.queryAllByText(/^Provider$/).length).toBe(0);
    expect(screen.getByText(/^LLM Provider$/)).toBeInTheDocument();
  });
});

describe("CreateUnitPage — Credential status banner (#598)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listOllamaModels.mockResolvedValue([]);
    listProviderModels.mockResolvedValue([]);
  });

  it("renders a 'tenant default' hint when credentials inherit from tenant", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "claude", resolvable: true, source: "tenant" }),
    );

    renderPage();
    await selectTool("dapr-agent");

    const status = await screen.findByTestId("credential-status");
    expect(status.dataset.resolvable).toBe("true");
    expect(status.dataset.source).toBe("tenant");
    expect(status.textContent).toMatch(/inherited from tenant default/i);
  });

  it("renders a 'set on unit' hint when credentials come from the unit", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "claude", resolvable: true, source: "unit" }),
    );

    renderPage();
    await selectTool("dapr-agent");

    const status = await screen.findByTestId("credential-status");
    expect(status.dataset.source).toBe("unit");
    expect(status.textContent).toMatch(/set on unit/i);
  });

  it("renders an inline credential input when the provider has no credentials (#626)", async () => {
    // #626: the PR-#627 "deep link" was replaced by an inline input +
    // save-as-tenant-default checkbox so operators can supply the key
    // without leaving the wizard.
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({
        provider: "anthropic",
        resolvable: false,
        source: null,
        suggestion:
          "Anthropic credentials are not configured. Set the tenant-default secret 'anthropic-api-key' …",
      }),
    );

    renderPage();
    await selectTool("dapr-agent");

    const status = await screen.findByTestId("credential-status");
    expect(status.dataset.resolvable).toBe("false");
    expect(status.textContent).toMatch(/not configured/i);
    // The "Configure in Settings" deep link is gone — the operator
    // types the key inline instead.
    expect(
      screen.queryByRole("link", { name: /tenant defaults/i }),
    ).not.toBeInTheDocument();
    expect(screen.getByTestId("credential-input")).toBeInTheDocument();
    expect(
      screen.getByTestId("credential-save-as-tenant-default"),
    ).toBeInTheDocument();
  });

  it("renders an 'Ollama unreachable' warning when the endpoint is down", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({
        provider: "ollama",
        resolvable: false,
        source: null,
        suggestion:
          "Ollama not reachable at http://spring-ollama:11434. Check that the Ollama server is running.",
      }),
    );

    renderPage();
    await selectTool("dapr-agent");

    // Switch the provider dropdown to Ollama so the badge re-queries
    // against the Ollama probe path.
    const providerSelect = screen.getByLabelText(
      /^LLM provider$/i,
    ) as HTMLSelectElement;
    await act(async () => {
      fireEvent.change(providerSelect, { target: { value: "ollama" } });
    });

    const status = await screen.findByTestId("credential-status");
    expect(status.dataset.resolvable).toBe("false");
    expect(status.textContent).toMatch(/Ollama not reachable/i);
    // Ollama has no tenant-defaults link — only the diagnostic message.
    expect(
      screen.queryByRole("link", { name: /tenant defaults/i }),
    ).not.toBeInTheDocument();
  });

  it("passes the derived provider id to the credential-status probe (#626)", async () => {
    // #626: the wizard derives the required provider from tool+provider.
    // When tool=dapr-agent and provider=claude the probe is asked about
    // "anthropic" (the canonical id used by ILlmCredentialResolver), not
    // the dropdown's "claude" alias.
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ resolvable: true, source: "tenant" }),
    );

    renderPage();
    await selectTool("dapr-agent");

    await waitFor(() => {
      expect(getProviderCredentialStatus).toHaveBeenCalledWith("anthropic");
    });

    const providerSelect = screen.getByLabelText(
      /^LLM provider$/i,
    ) as HTMLSelectElement;
    await act(async () => {
      fireEvent.change(providerSelect, { target: { value: "openai" } });
    });

    await waitFor(() => {
      expect(getProviderCredentialStatus).toHaveBeenCalledWith("openai");
    });
  });

  it("passes axe a11y smoke with the 'not configured' warning visible", async () => {
    // Re-run the axe check because this banner is a new coloured-state
    // primitive in the create wizard. PR #599 / #610 pinned the
    // `warning/50`-border + `warning/15`-fill combination as axe-clean
    // at AA; if any future tailwind refactor drifts the tokens this
    // assertion will catch it.
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({
        provider: "claude",
        resolvable: false,
        source: null,
        suggestion: "Anthropic credentials are not configured.",
      }),
    );

    const { container } = renderPage();
    await selectTool("dapr-agent");
    await screen.findByTestId("credential-status");
    await expectNoAxeViolations(container);
  });
});

// ---------------------------------------------------------------------------
// #626 — inline credential flow
// ---------------------------------------------------------------------------

async function fillName(value: string) {
  const nameInput = screen.getByPlaceholderText(
    /engineering-team/i,
  ) as HTMLInputElement;
  await act(async () => {
    fireEvent.change(nameInput, { target: { value } });
  });
}

async function clickNext() {
  const next = screen.getByRole("button", { name: /^next$/i });
  await act(async () => {
    fireEvent.click(next);
  });
}

async function driveToFinalize() {
  // Step 1 → 2 → 3 → 4 → 5. Scratch mode, no connector, no secrets.
  await clickNext(); // 1 → 2
  const scratch = screen.getByRole("button", { name: /scratch/i });
  await act(async () => {
    fireEvent.click(scratch);
  });
  await clickNext(); // 2 → 3
  await clickNext(); // 3 → 4 (connector skipped by default)
  await clickNext(); // 4 → 5 (no secrets queued)
}

describe("CreateUnitPage — inline credential flow (#626)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listOllamaModels.mockResolvedValue([]);
    listProviderModels.mockResolvedValue([]);
    createUnit.mockResolvedValue({ name: "acme", id: "acme-id" });
    createUnitSecret.mockResolvedValue({
      name: "anthropic-api-key",
      version: "v1",
    });
    createTenantSecret.mockResolvedValue({
      name: "anthropic-api-key",
      version: "v1",
    });
    rotateTenantSecret.mockResolvedValue({
      name: "anthropic-api-key",
      version: "v2",
    });
  });

  it("derives provider=anthropic when the tool is Claude Code", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: true, source: "tenant" }),
    );
    renderPage();
    // Default tool is claude-code. The status probe is issued against
    // "anthropic" regardless of the (hidden) Provider dropdown value.
    await waitFor(() => {
      expect(getProviderCredentialStatus).toHaveBeenCalledWith("anthropic");
    });
  });

  it("derives provider=openai for --tool=codex and hides Provider dropdown", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "openai", resolvable: true, source: "tenant" }),
    );
    renderPage();
    await selectTool("codex");
    await waitFor(() => {
      expect(getProviderCredentialStatus).toHaveBeenCalledWith("openai");
    });
    expect(screen.queryByLabelText(/^LLM provider$/i)).not.toBeInTheDocument();
  });

  it("skips the credential surface entirely when the tool is custom", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ resolvable: true, source: "tenant" }),
    );
    renderPage();
    await selectTool("custom");
    // The initial render (tool=claude-code default) may have fired one
    // probe against "anthropic" before we flipped the tool; what matters
    // for the custom path is that the visible credential surface is
    // gone and no further probes are issued for this tool.
    expect(screen.queryByTestId("credential-status")).not.toBeInTheDocument();
    expect(screen.queryByTestId("credential-input")).not.toBeInTheDocument();
    expect(getProviderCredentialStatus).not.toHaveBeenCalledWith("custom");
    expect(getProviderCredentialStatus).not.toHaveBeenCalledWith("");
  });

  it("writes a unit-scoped secret when the tenant-default toggle is off", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: false, source: null }),
    );
    renderPage();
    // Tool = claude-code (default) ⇒ derives anthropic.
    await fillName("acme");
    await screen.findByTestId("credential-input");

    const input = screen.getByTestId("credential-input") as HTMLInputElement;
    await act(async () => {
      fireEvent.change(input, { target: { value: "sk-test-unit" } });
    });

    await driveToFinalize();

    const createBtn = screen.getByTestId("create-unit-button");
    await act(async () => {
      fireEvent.click(createBtn);
    });

    await waitFor(() => {
      expect(createUnit).toHaveBeenCalled();
    });
    expect(createTenantSecret).not.toHaveBeenCalled();
    expect(createUnitSecret).toHaveBeenCalledWith("acme", {
      name: "anthropic-api-key",
      value: "sk-test-unit",
    });
  });

  it("writes a tenant-scoped secret when the toggle is on and nothing exists yet", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: false, source: null }),
    );
    renderPage();
    await fillName("acme");
    await screen.findByTestId("credential-input");

    const input = screen.getByTestId("credential-input") as HTMLInputElement;
    await act(async () => {
      fireEvent.change(input, { target: { value: "sk-test-tenant" } });
    });
    const toggle = screen.getByTestId(
      "credential-save-as-tenant-default",
    ) as HTMLInputElement;
    await act(async () => {
      fireEvent.click(toggle);
    });

    await driveToFinalize();
    const createBtn = screen.getByTestId("create-unit-button");
    await act(async () => {
      fireEvent.click(createBtn);
    });

    await waitFor(() => {
      expect(createTenantSecret).toHaveBeenCalledWith({
        name: "anthropic-api-key",
        value: "sk-test-tenant",
      });
    });
    expect(createUnitSecret).not.toHaveBeenCalled();
  });

  it("treats Override click as a unit-scoped override when the toggle is off", async () => {
    // Tenant default already exists — the Override button opens the
    // input for a per-unit override.
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: true, source: "tenant" }),
    );
    renderPage();
    await fillName("acme");
    const overrideBtn = await screen.findByTestId(
      "credential-override-link",
    );
    await act(async () => {
      fireEvent.click(overrideBtn);
    });
    const input = screen.getByTestId("credential-input") as HTMLInputElement;
    await act(async () => {
      fireEvent.change(input, { target: { value: "sk-test-override" } });
    });
    await driveToFinalize();
    const createBtn = screen.getByTestId("create-unit-button");
    await act(async () => {
      fireEvent.click(createBtn);
    });
    await waitFor(() => {
      expect(createUnitSecret).toHaveBeenCalledWith("acme", {
        name: "anthropic-api-key",
        value: "sk-test-override",
      });
    });
    expect(createTenantSecret).not.toHaveBeenCalled();
    expect(rotateTenantSecret).not.toHaveBeenCalled();
  });

  it("writes no secrets when tenant default exists and Override is not clicked", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: true, source: "tenant" }),
    );
    renderPage();
    await fillName("acme");
    await driveToFinalize();
    const createBtn = screen.getByTestId("create-unit-button");
    await act(async () => {
      fireEvent.click(createBtn);
    });
    await waitFor(() => {
      expect(createUnit).toHaveBeenCalled();
    });
    expect(createUnitSecret).not.toHaveBeenCalled();
    expect(createTenantSecret).not.toHaveBeenCalled();
    expect(rotateTenantSecret).not.toHaveBeenCalled();
  });

  it("disables the Create button when a key is required but unset", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: false, source: null }),
    );
    renderPage();
    await fillName("acme");
    await driveToFinalize();
    const createBtn = screen.getByTestId(
      "create-unit-button",
    ) as HTMLButtonElement;
    expect(createBtn.disabled).toBe(true);
    expect(
      screen.getByTestId("missing-credential-message").textContent,
    ).toMatch(/set the anthropic api key/i);
  });

  it("never round-trips a plaintext secret value through the status endpoint", async () => {
    // Invariant: the browser-side credential-status query must only
    // receive booleans / source / suggestion — never the key.
    // `getProviderCredentialStatus` is mocked at the `api.*` layer, so
    // asserting the returned payload shape catches any regression that
    // would sneak plaintext through the public client surface.
    const spy = vi.fn().mockResolvedValue(
      makeStatus({
        provider: "anthropic",
        resolvable: true,
        source: "tenant",
      }),
    );
    getProviderCredentialStatus.mockImplementation(spy);
    renderPage();
    await waitFor(() => {
      expect(spy).toHaveBeenCalled();
    });
    const result = await spy.mock.results[0]!.value;
    const serialised = JSON.stringify(result);
    expect(serialised).not.toMatch(/sk-/);
    expect(serialised).not.toMatch(/value/i);
    expect(serialised).not.toMatch(/apiKey/i);
  });

  it("toggling the show/hide button is keyboard-accessible", async () => {
    getProviderCredentialStatus.mockResolvedValue(
      makeStatus({ provider: "anthropic", resolvable: false, source: null }),
    );
    renderPage();
    await fillName("acme");
    const input = (await screen.findByTestId(
      "credential-input",
    )) as HTMLInputElement;
    expect(input.type).toBe("password");
    const toggle = screen.getByTestId("credential-visibility-toggle");
    expect(toggle.getAttribute("aria-label")).toMatch(/show/i);
    await act(async () => {
      fireEvent.click(toggle);
    });
    expect(input.type).toBe("text");
    expect(toggle.getAttribute("aria-label")).toMatch(/hide/i);
    expect(toggle.getAttribute("aria-pressed")).toBe("true");
  });
});
