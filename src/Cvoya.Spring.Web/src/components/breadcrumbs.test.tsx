import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Breadcrumbs } from "./breadcrumbs";

// next/link is a thin wrapper in tests.
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

describe("Breadcrumbs", () => {
  it("renders nothing when items is empty", () => {
    const { container } = render(<Breadcrumbs items={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renders each item label", () => {
    render(
      <Breadcrumbs
        items={[
          { label: "Units", href: "/units" },
          { label: "engineering-team", href: "/units/engineering-team" },
          { label: "ada" },
        ]}
      />,
    );
    expect(screen.getByText("Units")).toBeInTheDocument();
    expect(screen.getByText("engineering-team")).toBeInTheDocument();
    expect(screen.getByText("ada")).toBeInTheDocument();
  });

  it("renders linked crumbs as anchors and the last crumb as text", () => {
    render(
      <Breadcrumbs
        items={[
          { label: "Units", href: "/units" },
          { label: "ada" },
        ]}
      />,
    );

    const link = screen.getByRole("link", { name: "Units" });
    expect(link).toHaveAttribute("href", "/units");

    // The final crumb has aria-current="page" and is not a link.
    const current = screen.getByText("ada");
    expect(current).toHaveAttribute("aria-current", "page");
    expect(screen.queryByRole("link", { name: "ada" })).toBeNull();
  });

  it("does not render the last crumb as a link even when href is provided", () => {
    render(
      <Breadcrumbs
        items={[
          { label: "Units", href: "/units" },
          { label: "ada", href: "/units/ada" },
        ]}
      />,
    );
    // The final crumb should be plain text with aria-current, not a link.
    const current = screen.getByText("ada");
    expect(current).toHaveAttribute("aria-current", "page");
    expect(screen.queryByRole("link", { name: "ada" })).toBeNull();
  });

  it("is labelled for assistive tech", () => {
    render(<Breadcrumbs items={[{ label: "Home" }]} />);
    expect(screen.getByRole("navigation", { name: "Breadcrumb" })).toBeInTheDocument();
  });
});
