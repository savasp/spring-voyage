import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi, beforeEach } from "vitest";
import type { ReactNode } from "react";

import { ActivityTab } from "./activity-tab";

const mockQueryActivity = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    queryActivity: (...args: unknown[]) => mockQueryActivity(...args),
  },
}));

// The SSE hook would try to open a real EventSource during tests. Stub
// it out — the tests cover the REST-backed query layer here.
vi.mock("@/lib/stream/use-activity-stream", () => ({
  useActivityStream: () => ({ events: [], connected: false }),
}));

const mockResult = {
  items: [
    {
      id: "evt-1",
      source: "unit://eng-team",
      eventType: "StateChanged",
      severity: "Info",
      summary: "Unit started",
      correlationId: null,
      cost: null,
      timestamp: new Date().toISOString(),
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 20,
};

function Wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
    },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("ActivityTab", () => {
  beforeEach(() => {
    mockQueryActivity.mockReset();
    mockQueryActivity.mockResolvedValue(mockResult);
  });

  it("calls API with unit source filter", async () => {
    render(
      <Wrapper>
        <ActivityTab unitId="eng-team" />
      </Wrapper>,
    );
    await waitFor(() => {
      expect(mockQueryActivity).toHaveBeenCalledWith({
        source: "unit:eng-team",
        pageSize: "20",
      });
    });
  });

  it("renders activity events for the unit", async () => {
    render(
      <Wrapper>
        <ActivityTab unitId="eng-team" />
      </Wrapper>,
    );
    await waitFor(() => {
      expect(screen.getByText("Unit started")).toBeInTheDocument();
    });
  });

  it("shows empty message when no events", async () => {
    mockQueryActivity.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    });
    render(
      <Wrapper>
        <ActivityTab unitId="eng-team" />
      </Wrapper>,
    );
    await waitFor(() => {
      expect(
        screen.getByText("No activity events for this unit."),
      ).toBeInTheDocument();
    });
  });

  // #1665: rows that carry a non-empty `details` payload render an
  // expand/collapse toggle that reveals the full structured payload
  // inline. Rows without details render no toggle so the gutter stays
  // tidy. The validation-failure StateChanged row is the canonical
  // example — promoted from Debug to Warning and carrying the
  // validation code/message in details.
  it("renders an expand toggle for events with details and reveals payload on click", async () => {
    mockQueryActivity.mockResolvedValue({
      items: [
        {
          id: "evt-with-details",
          source: "unit://eng-team",
          eventType: "StateChanged",
          severity: "Warning",
          summary:
            "Unit transitioned from Validating to Error: ConfigurationIncomplete \u2014 No execution defaults are configured",
          correlationId: null,
          cost: null,
          timestamp: new Date().toISOString(),
          details: {
            action: "StatusTransition",
            from: "Validating",
            to: "Error",
            validationCode: "ConfigurationIncomplete",
            validationMessage: "No execution defaults are configured",
            error: {
              Step: "SchedulingWorkflow",
              Code: "ConfigurationIncomplete",
              Message: "No execution defaults are configured",
              Details: { missing: "image,runtime" },
            },
          },
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });

    render(
      <Wrapper>
        <ActivityTab unitId="eng-team" />
      </Wrapper>,
    );

    const toggle = await screen.findByTestId("activity-row-toggle");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByTestId("activity-row-details")).not.toBeInTheDocument();

    fireEvent.click(toggle);

    expect(toggle).toHaveAttribute("aria-expanded", "true");
    const details = await screen.findByTestId("activity-row-details");
    expect(details).toHaveTextContent('"validationCode": "ConfigurationIncomplete"');
    expect(details).toHaveTextContent('"missing": "image,runtime"');

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByTestId("activity-row-details")).not.toBeInTheDocument();
  });

  it("does not render a toggle for events without details", async () => {
    mockQueryActivity.mockResolvedValue({
      items: [
        {
          id: "evt-bare",
          source: "unit://eng-team",
          eventType: "MessageReceived",
          severity: "Info",
          summary: "Bare row",
          correlationId: null,
          cost: null,
          timestamp: new Date().toISOString(),
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });
    render(
      <Wrapper>
        <ActivityTab unitId="eng-team" />
      </Wrapper>,
    );
    await waitFor(() => {
      expect(screen.getByText("Bare row")).toBeInTheDocument();
    });
    expect(screen.queryByTestId("activity-row-toggle")).not.toBeInTheDocument();
  });
});
