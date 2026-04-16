import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { DashboardSummary } from "@/lib/api/types";

const getDashboardSummary =
  vi.fn<() => Promise<DashboardSummary>>();

vi.mock("@/lib/api/client", () => ({
  api: {
    getDashboardSummary: () => getDashboardSummary(),
  },
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

import DashboardPage from "./page";

function makeSummary(
  overrides: Partial<DashboardSummary> = {},
): DashboardSummary {
  return {
    unitCount: 2,
    unitsByStatus: { Draft: 1, Running: 1 },
    agentCount: 3,
    recentActivity: [],
    totalCost: 42.5,
    units: [
      {
        name: "unit-alpha",
        displayName: "Unit Alpha",
        registeredAt: "2026-01-01T00:00:00Z",
        status: "Running",
      },
      {
        name: "unit-beta",
        displayName: "Unit Beta",
        registeredAt: "2026-01-02T00:00:00Z",
        status: "Draft",
      },
    ],
    agents: [
      {
        name: "agent-1",
        displayName: "Agent One",
        role: "backend",
        registeredAt: "2026-01-01T00:00:00Z",
      },
      {
        name: "agent-2",
        displayName: "Agent Two",
        role: null,
        registeredAt: "2026-01-01T00:00:00Z",
      },
      {
        name: "agent-3",
        displayName: "Agent Three",
        role: "frontend",
        registeredAt: "2026-01-01T00:00:00Z",
      },
    ],
    ...overrides,
  };
}

describe("DashboardPage", () => {
  beforeEach(() => {
    getDashboardSummary.mockReset();
  });

  it("renders unit count and agent count from summary", async () => {
    getDashboardSummary.mockResolvedValue(makeSummary());

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("2")).toBeInTheDocument();
    });
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("$42.50")).toBeInTheDocument();
  });

  it("renders unit rows with names and status badges", async () => {
    getDashboardSummary.mockResolvedValue(makeSummary());

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Unit Alpha")).toBeInTheDocument();
    });
    expect(screen.getByText("Unit Beta")).toBeInTheDocument();
    expect(screen.getByText("Running")).toBeInTheDocument();
    expect(screen.getByText("Draft")).toBeInTheDocument();
  });

  it("renders unit row links to unit detail pages", async () => {
    getDashboardSummary.mockResolvedValue(makeSummary());

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByTestId("unit-row-unit-alpha")).toBeInTheDocument();
    });
    const link = screen.getByTestId("unit-row-unit-alpha");
    expect(link).toHaveAttribute("href", "/units/unit-alpha");
  });

  it("renders agent rows with display names and roles", async () => {
    getDashboardSummary.mockResolvedValue(makeSummary());

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Agent One")).toBeInTheDocument();
    });
    expect(screen.getByText("Agent Two")).toBeInTheDocument();
    expect(screen.getByText("Agent Three")).toBeInTheDocument();
    expect(screen.getByText("backend")).toBeInTheDocument();
    expect(screen.getByText("frontend")).toBeInTheDocument();
  });

  it("renders recent activity items", async () => {
    getDashboardSummary.mockResolvedValue(
      makeSummary({
        recentActivity: [
          {
            id: "evt-1",
            source: "agent://agent-1",
            eventType: "MessageReceived",
            severity: "Info",
            summary: "Agent received a message",
            timestamp: "2026-04-13T10:00:00Z",
          },
          {
            id: "evt-2",
            source: "unit://unit-alpha",
            eventType: "StateChanged",
            severity: "Warning",
            summary: "Unit state changed",
            timestamp: "2026-04-13T09:00:00Z",
          },
        ],
      }),
    );

    render(<DashboardPage />);

    await waitFor(() => {
      expect(
        screen.getByText("Agent received a message"),
      ).toBeInTheDocument();
    });
    expect(screen.getByText("Unit state changed")).toBeInTheDocument();
    expect(screen.getByText("View all activity")).toBeInTheDocument();
  });

  it("shows empty-state messages when summary has no data", async () => {
    getDashboardSummary.mockResolvedValue(
      makeSummary({
        unitCount: 0,
        unitsByStatus: {},
        agentCount: 0,
        recentActivity: [],
        totalCost: 0,
        units: [],
        agents: [],
      }),
    );

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("No units created.")).toHaveTextContent(
        "No units created.",
      );
    });
    expect(screen.getByText("No agents registered.")).toBeInTheDocument();
    expect(screen.getByText("No recent activity.")).toBeInTheDocument();
  });

  it("shows 'Create one' link in empty units state", async () => {
    getDashboardSummary.mockResolvedValue(
      makeSummary({
        unitCount: 0,
        units: [],
      }),
    );

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Create one")).toBeInTheDocument();
    });
    expect(screen.getByText("Create one")).toHaveAttribute(
      "href",
      "/units/create",
    );
  });

  it("shows 'View all units' link when units exist", async () => {
    getDashboardSummary.mockResolvedValue(makeSummary());

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("View all units")).toBeInTheDocument();
    });
    expect(screen.getByText("View all units")).toHaveAttribute(
      "href",
      "/units",
    );
  });
});
