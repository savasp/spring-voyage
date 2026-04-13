import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

// The wizard-step component calls the shared `api` client for
// installations / install-url. Mock the module before importing the
// component so the module graph sees the stub.
vi.mock("@/lib/api/client", () => ({
  api: {
    listGitHubInstallations: vi.fn(),
    getGitHubInstallUrl: vi.fn(),
  },
}));

import { api } from "@/lib/api/client";
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
