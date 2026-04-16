import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { ActivityTab } from "./activity-tab";

const mockQueryActivity = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    queryActivity: (...args: unknown[]) => mockQueryActivity(...args),
  },
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

describe("ActivityTab", () => {
  beforeEach(() => {
    mockQueryActivity.mockReset();
    mockQueryActivity.mockResolvedValue(mockResult);
  });

  it("calls API with unit source filter", async () => {
    render(<ActivityTab unitId="eng-team" />);
    await waitFor(() => {
      expect(mockQueryActivity).toHaveBeenCalledWith({
        source: "unit:eng-team",
        pageSize: "20",
      });
    });
  });

  it("renders activity events for the unit", async () => {
    render(<ActivityTab unitId="eng-team" />);
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
    render(<ActivityTab unitId="eng-team" />);
    await waitFor(() => {
      expect(
        screen.getByText("No activity events for this unit."),
      ).toBeInTheDocument();
    });
  });
});
