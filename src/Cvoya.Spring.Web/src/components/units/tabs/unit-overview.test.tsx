import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";

import type { UnitNode } from "../aggregate";

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    ...rest
  }: { href: string; children: ReactNode } & Record<string, unknown>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock("../unit-overview-expertise-card", () => ({
  UnitOverviewExpertiseCard: ({ unitId }: { unitId: string }) => (
    <div data-testid="expertise-card-stub" data-unit-id={unitId} />
  ),
}));

const useUnitCostTimeseriesMock = vi.fn();
const useUnitMock = vi.fn();
const useUnitExecutionMock = vi.fn();
vi.mock("@/lib/api/queries", () => ({
  useUnitCostTimeseries: (id: string, window: string, bucket: string) =>
    useUnitCostTimeseriesMock(id, window, bucket),
  useUnit: (id: string) => useUnitMock(id),
  useUnitExecution: (id: string) => useUnitExecutionMock(id),
}));

// `<ValidationPanel>` is rendered for `Error` units; stub it out so the
// Overview tests don't need to thread the panel's full mutation /
// query-client wiring through every assertion.
vi.mock("../detail/validation-panel", () => ({
  default: ({ unit }: { unit: { lastValidationError?: { code?: string } | null } }) => (
    <div
      data-testid="validation-panel-stub"
      data-validation-code={unit.lastValidationError?.code ?? ""}
    />
  ),
}));

import UnitOverviewTab from "./unit-overview";

const emptyTimeseries = { data: null, isLoading: false };
const noUnit = { data: null, isLoading: false };
const noExecution = { data: null, isLoading: false };

beforeEach(() => {
  useUnitCostTimeseriesMock.mockReturnValue(emptyTimeseries);
  useUnitMock.mockReturnValue(noUnit);
  useUnitExecutionMock.mockReturnValue(noExecution);
});

describe("UnitOverviewTab", () => {
  it("renders subtree stat tiles rolled up from the node", () => {
    useUnitCostTimeseriesMock.mockReturnValue(emptyTimeseries);
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
      cost24h: 2.5,
      msgs24h: 42,
      children: [
        {
          kind: "Agent",
          id: "ada",
          name: "Ada",
          status: "running",
          cost24h: 1.25,
          msgs24h: 10,
        },
      ],
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    expect(screen.getByTestId("tab-unit-overview")).toBeInTheDocument();
    // Messages tile shows the rolled-up total (42 + 10 = 52).
    expect(screen.getByText("52")).toBeInTheDocument();
    // The "Cost (24h)" stat tile renders the aggregated cost.
    expect(screen.getByText("Cost (24h)")).toBeInTheDocument();
  });

  it("mounts the expertise card with the unit id (issue #936)", () => {
    useUnitCostTimeseriesMock.mockReturnValue(emptyTimeseries);
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    expect(
      screen.getByTestId("expertise-card-stub").dataset.unitId,
    ).toBe("engineering");
  });

  it("shows the cost timeseries card with empty state when no data", () => {
    useUnitCostTimeseriesMock.mockReturnValue(emptyTimeseries);
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    expect(
      screen.getByTestId("unit-cost-timeseries-card"),
    ).toBeInTheDocument();
    expect(
      screen.getByTestId("unit-cost-timeseries-empty"),
    ).toBeInTheDocument();
  });

  it("renders the sparkline when timeseries data is present", () => {
    useUnitCostTimeseriesMock.mockReturnValue({
      data: {
        scope: "unit",
        id: "engineering",
        bucket: "1d",
        from: "2024-01-01T00:00:00Z",
        to: "2024-01-08T00:00:00Z",
        points: [
          { t: "2024-01-01T00:00:00Z", costUsd: 2.0 },
          { t: "2024-01-02T00:00:00Z", costUsd: 3.5 },
          { t: "2024-01-03T00:00:00Z", costUsd: 1.8 },
        ],
      },
      isLoading: false,
    });
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    expect(screen.getByTestId("unit-cost-sparkline")).toBeInTheDocument();
  });

  it("renders the cross-portal engagement link with the unit id (E2.3 #1415)", () => {
    useUnitCostTimeseriesMock.mockReturnValue(emptyTimeseries);
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    const link = screen.getByTestId("unit-overview-engagement-link");
    expect(link).toHaveAttribute(
      "href",
      "/engagement/mine?unit=engineering",
    );
    expect(link).toHaveTextContent("View engagements for this unit");
  });

  // #1665: when the live unit is in `Error`, the validation panel
  // surfaces the structured `lastValidationError` so the operator
  // sees *why* validation failed without leaving the Overview tab.
  it("renders the validation panel when the live unit is in Error", () => {
    useUnitMock.mockReturnValue({
      data: {
        id: "engineering",
        name: "engineering",
        displayName: "Engineering",
        status: "Error",
        lastValidationError: {
          step: "SchedulingWorkflow",
          code: "ConfigurationIncomplete",
          message:
            "No execution defaults are configured on this unit. Set a container image (and optionally a runtime) before validation can run.",
          details: { missing: "image,runtime" },
        },
      },
      isLoading: false,
    });
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "error",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    const panel = screen.getByTestId("validation-panel-stub");
    expect(panel.dataset.validationCode).toBe("ConfigurationIncomplete");
  });

  it("does not render the validation panel for healthy units", () => {
    useUnitMock.mockReturnValue({
      data: {
        id: "engineering",
        name: "engineering",
        displayName: "Engineering",
        status: "Running",
        lastValidationError: null,
      },
      isLoading: false,
    });
    const node: UnitNode = {
      kind: "Unit",
      id: "engineering",
      name: "Engineering",
      status: "running",
    };
    render(<UnitOverviewTab node={node} path={[node]} />);
    expect(screen.queryByTestId("validation-panel-stub")).not.toBeInTheDocument();
  });
});
