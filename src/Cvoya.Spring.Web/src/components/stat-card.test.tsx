import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatCard } from "./stat-card";

describe("StatCard", () => {
  it("renders label and numeric value", () => {
    render(<StatCard label="Agents" value={5} icon={<span>icon</span>} />);
    expect(screen.getByText("Agents")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
  });

  it("renders string value", () => {
    render(<StatCard label="Cost" value="$12.50" icon={<span>$</span>} />);
    expect(screen.getByText("$12.50")).toBeInTheDocument();
  });
});
