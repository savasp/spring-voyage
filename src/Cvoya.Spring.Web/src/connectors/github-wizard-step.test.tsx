import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

// The wizard-step component calls the shared `api` client for
// installations / install-url. Mock the module before importing the
// component so the module graph sees the stub.
vi.mock("@/lib/api/client", async () => {
  // The wizard's disabled-with-reason branch checks `err instanceof
  // ApiError`, so the mock needs to expose the real class shape — a bare
  // object would fail the instanceof check and the panel would never
  // render. We construct a thin stand-in here so tests can throw it.
  class MockApiError extends Error {
    constructor(
      public readonly status: number,
      public readonly statusText: string,
      public readonly body: unknown,
    ) {
      super(`API error ${status}: ${statusText}`);
      this.name = "ApiError";
    }
  }
  return {
    ApiError: MockApiError,
    api: {
      listGitHubInstallations: vi.fn(),
      getGitHubInstallUrl: vi.fn(),
    },
  };
});

import { ApiError, api } from "@/lib/api/client";
import { expectNoAxeViolations } from "@/test/a11y";
import { GitHubConnectorWizardStep } from "@connector-github/connector-wizard-step";

const mocked = vi.mocked(api);

describe("GitHubConnectorWizardStep", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("bubbles null until both owner and repo are filled", async () => {
    mocked.listGitHubInstallations.mockResolvedValue([]);
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    // Initial render produces null (required fields empty).
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(null));

    const owner = screen.getByPlaceholderText("acme");
    await act(async () => {
      fireEvent.change(owner, { target: { value: "acme" } });
    });

    // Owner alone still isn't enough — repo is also required.
    await waitFor(() => {
      const lastCall = onChange.mock.calls.at(-1);
      expect(lastCall?.[0]).toBeNull();
    });
  });

  it("emits a typed config payload once owner + repo are filled", async () => {
    mocked.listGitHubInstallations.mockResolvedValue([]);
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await act(async () => {
      fireEvent.change(screen.getByPlaceholderText("acme"), {
        target: { value: "acme" },
      });
      fireEvent.change(screen.getByPlaceholderText("platform"), {
        target: { value: "platform" },
      });
    });

    await waitFor(() => {
      const last = onChange.mock.calls.at(-1)?.[0];
      expect(last).not.toBeNull();
      expect(last).toEqual(
        expect.objectContaining({
          owner: "acme",
          repo: "platform",
        }),
      );
    });
  });

  it("shows the install-app banner when no installations are visible", async () => {
    mocked.listGitHubInstallations.mockResolvedValue([]);
    mocked.getGitHubInstallUrl.mockResolvedValue({ url: "" });
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await waitFor(() =>
      expect(
        screen.getByText(/No GitHub App installations found\./),
      ).toBeInTheDocument(),
    );
  });

  it("renders the install-app link when the list comes back empty (#599)", async () => {
    // The previous implementation only fetched the install URL on the
    // catch branch, so a platform with the App configured but no
    // installations surfaced a banner with no call-to-action link.
    mocked.listGitHubInstallations.mockResolvedValue([]);
    mocked.getGitHubInstallUrl.mockResolvedValue({
      url: "https://github.com/apps/spring-voyage/installations/new",
    });
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await waitFor(() => {
      const link = screen.getByRole("link", { name: /install github app/i });
      expect(link).toHaveAttribute(
        "href",
        "https://github.com/apps/spring-voyage/installations/new",
      );
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("rel", "noopener noreferrer");
    });
    expect(mocked.getGitHubInstallUrl).toHaveBeenCalledTimes(1);
  });

  it("renders the install-app link when listing installations throws", async () => {
    mocked.listGitHubInstallations.mockRejectedValue(
      new Error("502 Bad Gateway"),
    );
    mocked.getGitHubInstallUrl.mockResolvedValue({
      url: "https://github.com/apps/spring-voyage/installations/new",
    });
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await waitFor(() => {
      expect(
        screen.getByRole("link", { name: /install github app/i }),
      ).toBeInTheDocument();
    });
    expect(
      screen.getByText(/502 Bad Gateway/, { exact: false }),
    ).toBeInTheDocument();
  });

  it("passes axe smoke with the install-app banner visible (#599)", async () => {
    mocked.listGitHubInstallations.mockResolvedValue([]);
    mocked.getGitHubInstallUrl.mockResolvedValue({
      url: "https://github.com/apps/spring-voyage/installations/new",
    });
    const onChange = vi.fn();

    let container!: HTMLElement;
    await act(async () => {
      const result = render(<GitHubConnectorWizardStep onChange={onChange} />);
      container = result.container;
    });

    await waitFor(() =>
      expect(
        screen.getByRole("link", { name: /install github app/i }),
      ).toBeInTheDocument(),
    );
    await expectNoAxeViolations(container);
  });

  it("renders the friendly disabled panel when the connector is not configured (#1186)", async () => {
    // Server returns the structured Problem+JSON `{ disabled: true,
    // reason: "GitHub App not configured on this deployment." }`. The
    // wizard must NOT leak the raw RFC 9110 envelope into the UI; it
    // should render the deployment-guide panel and skip the
    // install-url fetch (which would 404 with the same payload).
    mocked.listGitHubInstallations.mockRejectedValue(
      new ApiError(404, "Not Found", {
        type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        title: "GitHub connector is not configured",
        status: 404,
        detail: "GitHub App not configured on this deployment.",
        disabled: true,
        reason: "GitHub App not configured on this deployment.",
      }),
    );
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await waitFor(() =>
      expect(
        screen.getByText(/GitHub connector not configured on this deployment\./),
      ).toBeInTheDocument(),
    );
    expect(
      screen.getByRole("link", { name: /view deployment guide/i }),
    ).toBeInTheDocument();
    // The raw API error envelope must not appear anywhere in the UI.
    expect(screen.queryByText(/API error 404/)).not.toBeInTheDocument();
    // No install-url fetch attempted — the endpoint would 404 with the
    // same disabled payload and there is nothing to render.
    expect(mocked.getGitHubInstallUrl).not.toHaveBeenCalled();
  });

  // #1132: clicking the Recheck button while the panel says
  // "No installations" must re-run the same list-installations fetch
  // and re-render the panel with the new result. The previous code
  // fetched once on mount and offered no way to re-check, so operators
  // returning from the github.com install flow saw a permanently-stuck
  // empty banner.
  it("re-fetches installations when the Recheck button is clicked (#1132)", async () => {
    mocked.listGitHubInstallations.mockResolvedValueOnce([]);
    mocked.getGitHubInstallUrl.mockResolvedValue({
      url: "https://github.com/apps/spring-voyage/installations/new",
    });
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    // Initial mount: empty list + Recheck button visible.
    const recheck = await screen.findByTestId(
      "github-recheck-installations",
    );
    expect(mocked.listGitHubInstallations).toHaveBeenCalledTimes(1);
    expect(
      screen.getByText(/No GitHub App installations found\./),
    ).toBeInTheDocument();
    expect(recheck).toHaveAttribute("aria-label", "Recheck installations");

    // Operator returns from the GitHub install flow — the second
    // round-trip should now see one installation.
    mocked.listGitHubInstallations.mockResolvedValueOnce([
      {
        installationId: 42,
        account: "acme",
        accountType: "Organization",
        repoSelection: "all",
      } as never,
    ]);

    await act(async () => {
      fireEvent.click(recheck);
    });

    await waitFor(() => {
      expect(mocked.listGitHubInstallations).toHaveBeenCalledTimes(2);
    });
    // Empty banner should be gone and the installation picker should
    // now be present.
    await waitFor(() => {
      expect(
        screen.queryByText(/No GitHub App installations found\./),
      ).not.toBeInTheDocument();
    });
    expect(screen.getByText(/acme \(Organization, all\)/)).toBeInTheDocument();
  });

  // #1132: while in flight, the Recheck button is disabled and the
  // panel announces a busy state via aria-busy + a visually-hidden
  // status string. Without these the operator can fire double-clicks
  // and gets no SR feedback that the recheck is pending.
  it("disables the Recheck button and announces aria-busy while in flight (#1132)", async () => {
    let resolveSecond: ((value: never[]) => void) | null = null;
    mocked.listGitHubInstallations.mockResolvedValueOnce([]);
    mocked.getGitHubInstallUrl.mockResolvedValue({ url: "" });
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    const recheck = await screen.findByTestId(
      "github-recheck-installations",
    );
    // Stage a deferred response for the second fetch so we can observe
    // the busy state.
    mocked.listGitHubInstallations.mockReturnValueOnce(
      new Promise<never[]>((resolve) => {
        resolveSecond = resolve;
      }) as ReturnType<typeof api.listGitHubInstallations>,
    );

    await act(async () => {
      fireEvent.click(recheck);
    });

    await waitFor(() => {
      expect(recheck).toHaveAttribute("aria-busy", "true");
    });
    expect(recheck).toBeDisabled();
    expect(recheck.textContent).toMatch(/rechecking/i);

    // Resolve and verify we return to the idle state.
    await act(async () => {
      resolveSecond!([]);
    });
    await waitFor(() => {
      expect(recheck).toHaveAttribute("aria-busy", "false");
    });
    expect(recheck).not.toBeDisabled();
  });

  // #1132: when the connector is disabled at the deployment level,
  // recheck makes no sense — there are no credentials to check. The
  // friendly disabled panel from #1129 must remain the only thing on
  // screen; the Recheck button MUST NOT render.
  it("does not render the Recheck button when the connector is disabled (#1132)", async () => {
    mocked.listGitHubInstallations.mockRejectedValue(
      new ApiError(404, "Not Found", {
        disabled: true,
        reason: "GitHub App not configured on this deployment.",
      }),
    );
    const onChange = vi.fn();

    await act(async () => {
      render(<GitHubConnectorWizardStep onChange={onChange} />);
    });

    await waitFor(() =>
      expect(
        screen.getByText(/GitHub connector not configured on this deployment\./),
      ).toBeInTheDocument(),
    );
    expect(
      screen.queryByTestId("github-recheck-installations"),
    ).not.toBeInTheDocument();
  });

  it("hydrates from initialValue when provided", async () => {
    mocked.listGitHubInstallations.mockResolvedValue([]);
    const onChange = vi.fn();

    await act(async () => {
      render(
        <GitHubConnectorWizardStep
          onChange={onChange}
          initialValue={{
            owner: "prefilled-owner",
            repo: "prefilled-repo",
            appInstallationId: undefined,
            events: undefined,
          }}
        />,
      );
    });

    expect(
      screen.getByDisplayValue("prefilled-owner"),
    ).toBeInTheDocument();
    expect(screen.getByDisplayValue("prefilled-repo")).toBeInTheDocument();
  });
});
