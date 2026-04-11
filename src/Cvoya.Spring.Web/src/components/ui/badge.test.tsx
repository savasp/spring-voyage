import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Badge } from "./badge";

describe("Badge", () => {
  it("renders children text", () => {
    render(<Badge>Test</Badge>);
    expect(screen.getByText("Test")).toBeInTheDocument();
  });

  it("applies default variant classes", () => {
    render(<Badge>Default</Badge>);
    const el = screen.getByText("Default");
    expect(el).toHaveClass("text-primary");
  });

  it("applies destructive variant classes", () => {
    render(<Badge variant="destructive">Error</Badge>);
    const el = screen.getByText("Error");
    expect(el).toHaveClass("text-destructive");
  });

  it("applies success variant classes", () => {
    render(<Badge variant="success">OK</Badge>);
    const el = screen.getByText("OK");
    expect(el).toHaveClass("text-success");
  });
});
