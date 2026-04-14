import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { AgentResponse } from "@/lib/api/types";

// Mock the API module: we only care about the three calls the tab makes.
const listUnitAgents = vi.fn<(unitId: string) => Promise<AgentResponse[]>>();
const listAgents = vi.fn<() => Promise<AgentResponse[]>>();
const assignUnitAgent = vi.fn();
const unassignUnitAgent = vi.fn();
const updateAgentMetadata = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    listUnitAgents: (u: string) => listUnitAgents(u),
    listAgents: () => listAgents(),
    assignUnitAgent: (...args: unknown[]) => assignUnitAgent(...args),
    unassignUnitAgent: (...args: unknown[]) => unassignUnitAgent(...args),
    updateAgentMetadata: (...args: unknown[]) => updateAgentMetadata(...args),
  },
}));

vi.mock("@/components/ui/toast", () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

import { AgentsTab } from "./agents-tab";

function makeAgent(overrides: Partial<AgentResponse> = {}): AgentResponse {
  return {
    id: "actor-id",
    name: "ada",
    displayName: "Ada",
    description: "",
    role: null,
    registeredAt: new Date().toISOString(),
    model: null,
    specialty: null,
    enabled: true,
    executionMode: "Auto",
    parentUnit: null,
    ...overrides,
  } as AgentResponse;
}

describe("AgentsTab assignable filter (C2b-1 M:N)", () => {
  beforeEach(() => {
    listUnitAgents.mockReset();
    listAgents.mockReset();
    assignUnitAgent.mockReset();
    unassignUnitAgent.mockReset();
    updateAgentMetadata.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("shows agents that are not yet members of THIS unit, regardless of parentUnit", async () => {
    // Two registered agents:
    //   - ada: belongs to this unit — must be filtered OUT of assignable.
    //   - hopper: belongs to a DIFFERENT unit — under the old 1:N filter this
    //     would have been filtered out via !parentUnit; under M:N it must
    //     remain eligible because we filter on "already in this unit" only.
    const ada = makeAgent({ name: "ada", displayName: "Ada", parentUnit: "engineering" });
    const hopper = makeAgent({ name: "hopper", displayName: "Hopper", parentUnit: "marketing" });

    listUnitAgents.mockResolvedValue([ada]);
    listAgents.mockResolvedValue([ada, hopper]);

    render(<AgentsTab unitId="engineering" />);

    // Wait for the assignable <select> to render hopper (the only eligible
    // agent). Under the pre-C2b-1 filter, this assertion would have failed.
    await waitFor(() => {
      expect(screen.getByRole("option", { name: /hopper/i })).toBeInTheDocument();
    });

    // ada is an existing member, so it must NOT appear as a selectable option
    // (the placeholder option "Pick an agent…" is fine, and ada still appears
    // in the members list).
    expect(screen.queryByRole("option", { name: /^Ada$/ })).toBeNull();
  });

  it("shows the fallback message when every registered agent is already a member", async () => {
    const ada = makeAgent({ name: "ada", parentUnit: "engineering" });
    listUnitAgents.mockResolvedValue([ada]);
    listAgents.mockResolvedValue([ada]);

    render(<AgentsTab unitId="engineering" />);

    await waitFor(() => {
      expect(
        screen.getByText(/already a member of this unit/i),
      ).toBeInTheDocument();
    });
  });
});
