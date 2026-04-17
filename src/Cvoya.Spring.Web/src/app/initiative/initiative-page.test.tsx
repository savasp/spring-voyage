import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";

import type {
  AgentDashboardSummary,
  InitiativeLevelResponse,
  InitiativePolicy,
} from "@/lib/api/types";

const getDashboardAgents = vi.fn<() => Promise<AgentDashboardSummary[]>>();
const getAgentInitiativeLevel =
  vi.fn<(id: string) => Promise<InitiativeLevelResponse>>();
const getAgentInitiativePolicy =
  vi.fn<(id: string) => Promise<InitiativePolicy | null>>();
const setAgentInitiativePolicy = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    getDashboardAgents: () => getDashboardAgents(),
    getAgentInitiativeLevel: (id: string) => getAgentInitiativeLevel(id),
    getAgentInitiativePolicy: (id: string) => getAgentInitiativePolicy(id),
    setAgentInitiativePolicy: (...args: unknown[]) =>
      setAgentInitiativePolicy(...args),
  },
}));

const toastMock = vi.fn();
vi.mock("@/components/ui/toast", () => ({
  useToast: () => ({ toast: toastMock }),
}));

// The page subscribes to the activity stream; stub it so no real
// EventSource is opened during tests.
vi.mock("@/hooks/use-activity-stream", () => ({
  useActivityStream: () => ({ events: [], connected: false }),
}));

import InitiativePage from "./page";

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
  return render(<InitiativePage />, { wrapper: Wrapper });
}

describe("InitiativePage", () => {
  beforeEach(() => {
    getDashboardAgents.mockReset();
    getAgentInitiativeLevel.mockReset();
    getAgentInitiativePolicy.mockReset();
    setAgentInitiativePolicy.mockReset();
    toastMock.mockReset();
  });

  it("lists agents with their initiative level and max level", async () => {
    getDashboardAgents.mockResolvedValue([
      {
        name: "ada",
        displayName: "Ada",
        role: null,
        registeredAt: new Date().toISOString(),
      } as AgentDashboardSummary,
    ]);
    getAgentInitiativeLevel.mockResolvedValue({
      level: "Proactive",
    } as InitiativeLevelResponse);
    getAgentInitiativePolicy.mockResolvedValue({
      maxLevel: "Autonomous",
      requireUnitApproval: false,
      tier1: null,
      tier2: { maxCallsPerHour: 5, maxCostPerDay: 3 },
      allowedActions: null,
      blockedActions: null,
    } as InitiativePolicy);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Ada")).toBeInTheDocument();
    });
    // Both the current level and max level badges appear.
    expect(screen.getByText("Proactive")).toBeInTheDocument();
    expect(screen.getByText("Autonomous")).toBeInTheDocument();
  });

  it("renders empty state when no agents are registered", async () => {
    getDashboardAgents.mockResolvedValue([]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("No agents registered.")).toBeInTheDocument();
    });
  });
});
