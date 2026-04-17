import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AgentCard } from "./agent-card";

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

describe("AgentCard", () => {
  it("renders agent display name, role, and open link", () => {
    render(
      <AgentCard
        agent={{
          name: "ada",
          displayName: "Ada",
          role: "backend",
          registeredAt: "2026-04-01T00:00:00Z",
        }}
      />,
    );
    expect(screen.getByText("Ada")).toBeInTheDocument();
    expect(screen.getByTestId("agent-role-badge")).toHaveTextContent("backend");
    expect(screen.getByTestId("agent-open-ada")).toHaveAttribute(
      "href",
      "/agents/ada",
    );
  });

  it("links to parent unit, conversations, and cost detail", () => {
    render(
      <AgentCard
        agent={{
          name: "engineering/ada",
          displayName: "Ada",
          role: "backend",
          registeredAt: "2026-04-01T00:00:00Z",
          parentUnit: "engineering",
          status: "active",
          executionMode: "Auto",
        }}
      />,
    );

    expect(screen.getByTestId("agent-parent-unit")).toHaveAttribute(
      "href",
      "/units/engineering",
    );
    expect(
      screen.getByTestId("agent-link-conversations-engineering/ada"),
    ).toHaveAttribute("href", "/agents/engineering%2Fada?tab=conversations");
    expect(
      screen.getByTestId("agent-link-cost-engineering/ada"),
    ).toHaveAttribute("href", "/agents/engineering%2Fada?tab=costs");
    expect(screen.getByTestId("agent-execution-mode-badge")).toHaveTextContent(
      "Auto",
    );
    expect(screen.getByTestId("agent-status-badge")).toHaveTextContent(
      "active",
    );
  });

  it("renders without role, parent-unit, or last-activity when absent", () => {
    render(
      <AgentCard
        agent={{
          name: "ada",
          displayName: "Ada",
          role: null,
          registeredAt: "2026-04-01T00:00:00Z",
        }}
      />,
    );
    expect(screen.getByText("Ada")).toBeInTheDocument();
    expect(screen.queryByTestId("agent-role-badge")).toBeNull();
    expect(screen.queryByTestId("agent-parent-unit")).toBeNull();
    expect(screen.queryByTestId("agent-last-activity")).toBeNull();
  });

  it("accepts explicit parentUnit and lastActivity props as overrides", () => {
    render(
      <AgentCard
        agent={{
          name: "ada",
          displayName: "Ada",
          role: null,
          registeredAt: "2026-04-01T00:00:00Z",
        }}
        parentUnit="engineering"
        lastActivity="Replied to PR review"
      />,
    );
    expect(screen.getByTestId("agent-parent-unit")).toHaveTextContent(
      "engineering",
    );
    expect(screen.getByTestId("agent-last-activity")).toHaveTextContent(
      "Replied to PR review",
    );
  });
});
