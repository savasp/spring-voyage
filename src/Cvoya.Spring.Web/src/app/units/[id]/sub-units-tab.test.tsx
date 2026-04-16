import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { UnitDetailResponse, UnitResponse } from "@/lib/api/types";

// Mock the API module: only the calls the tab actually makes need to be
// defined. Anything else left undefined would throw if accidentally called.
const getUnitDetail =
  vi.fn<(unitId: string) => Promise<UnitDetailResponse>>();
const listUnits = vi.fn<() => Promise<UnitResponse[]>>();
const addMember = vi.fn();
const removeMember = vi.fn();

vi.mock("@/lib/api/client", () => ({
  api: {
    getUnitDetail: (u: string) => getUnitDetail(u),
    listUnits: () => listUnits(),
    addMember: (...args: unknown[]) => addMember(...args),
    removeMember: (...args: unknown[]) => removeMember(...args),
  },
}));

const toastMock = vi.fn();
vi.mock("@/components/ui/toast", () => ({
  useToast: () => ({ toast: toastMock }),
}));

// next/link is a server component shim under Next 16; stub it out for tests
// so the anchor renders with its `href` as a plain DOM attribute.
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

import { SubUnitsTab } from "./sub-units-tab";

function makeUnit(overrides: Partial<UnitResponse> = {}): UnitResponse {
  return {
    id: "actor-id",
    name: "engineering",
    displayName: "Engineering",
    description: "",
    registeredAt: new Date().toISOString(),
    status: "Draft",
    model: null,
    color: null,
    ...overrides,
  } as UnitResponse;
}

/**
 * Builds the `UnitDetailResponse.details` payload shape emitted by
 * `UnitActor.HandleStatusQueryAsync` — PascalCase `Members[]` with each
 * entry `{ Scheme, Path }`. Tests exercise the exact wire shape so the
 * tab stays honest about where the parsing contract lives.
 */
function makeDetail(
  unit: UnitResponse,
  members: Array<{ scheme: string; path: string }>,
): UnitDetailResponse {
  return {
    unit,
    details: {
      Status: "Draft",
      MemberCount: members.length,
      Members: members.map((m) => ({ Scheme: m.scheme, Path: m.path })),
    } as unknown as UnitDetailResponse["details"],
  };
}

describe("SubUnitsTab", () => {
  beforeEach(() => {
    getUnitDetail.mockReset();
    listUnits.mockReset();
    addMember.mockReset();
    removeMember.mockReset();
    toastMock.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("shows the empty state when the unit has no unit-scheme members", async () => {
    const parent = makeUnit({ name: "parent" });
    const other = makeUnit({ name: "other", displayName: "Other" });
    listUnits.mockResolvedValue([parent, other]);
    getUnitDetail.mockResolvedValue(
      makeDetail(parent, [
        // Agent-scheme members must NOT show up on this tab — they belong
        // to the Agents tab.
        { scheme: "agent", path: "ada" },
      ]),
    );

    render(<SubUnitsTab unitId="parent" />);

    await waitFor(() => {
      expect(screen.getByText(/No sub-units yet/i)).toBeInTheDocument();
    });
    expect(screen.queryByText("ada")).toBeNull();
    expect(
      screen.getByRole("button", { name: /add sub-unit/i }),
    ).toBeEnabled();
  });

  it("lists unit-scheme members with their display name and a deep link", async () => {
    const parent = makeUnit({ name: "parent" });
    const child = makeUnit({ name: "child", displayName: "Child Team" });
    listUnits.mockResolvedValue([parent, child]);
    getUnitDetail.mockResolvedValue(
      makeDetail(parent, [
        { scheme: "unit", path: "child" },
        { scheme: "agent", path: "ada" },
      ]),
    );

    render(<SubUnitsTab unitId="parent" />);

    await waitFor(() => {
      expect(screen.getByText("Child Team")).toBeInTheDocument();
    });

    // Row is a link to the child's unit-detail page.
    const link = screen.getByRole("link", { name: /open child team/i });
    expect(link).toHaveAttribute("href", "/units/child");

    // The mono `unit://child` marker is visible so the raw address is
    // discoverable even when the display name matches the id.
    expect(screen.getByText(/unit:\/\/child/)).toBeInTheDocument();

    // Agent-scheme members are filtered out.
    expect(screen.queryByText("ada")).toBeNull();
  });

  it("opens the Add dialog, submits the POST payload, and refreshes the list", async () => {
    const parent = makeUnit({ name: "parent" });
    const child = makeUnit({ name: "child", displayName: "Child Team" });
    const other = makeUnit({ name: "other", displayName: "Other Team" });
    listUnits.mockResolvedValue([parent, child, other]);

    // First load: no sub-units yet. After the add completes, the tab
    // re-fetches — return the new state on the second call.
    getUnitDetail
      .mockResolvedValueOnce(makeDetail(parent, []))
      .mockResolvedValueOnce(
        makeDetail(parent, [{ scheme: "unit", path: "child" }]),
      );
    addMember.mockResolvedValue(undefined);

    render(<SubUnitsTab unitId="parent" />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /add sub-unit/i }),
      ).toBeEnabled();
    });

    fireEvent.click(screen.getByRole("button", { name: /add sub-unit/i }));

    const dialog = await screen.findByRole("dialog");
    // `getAllByText` because the copy "Add sub-unit" reappears on the
    // submit button; asserting the collection is non-empty is enough —
    // more specific selectors (role=heading, aria-label) cover the
    // structural assertions elsewhere in this file.
    expect(within(dialog).getAllByText(/Add sub-unit/i).length).toBeGreaterThan(0);

    // Candidates exclude the current unit itself.
    const select = within(dialog).getByLabelText(/^Unit$/i) as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.value);
    expect(options).toContain("child");
    expect(options).toContain("other");
    expect(options).not.toContain("parent");

    fireEvent.change(select, { target: { value: "child" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /add sub-unit/i }));

    await waitFor(() => {
      expect(addMember).toHaveBeenCalledWith("parent", "unit", "child");
    });

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });

    await waitFor(() => {
      expect(screen.getByText("Child Team")).toBeInTheDocument();
    });

    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Sub-unit added" }),
    );
  });

  it("shows the 409 cycle error in the dialog without closing it", async () => {
    const parent = makeUnit({ name: "parent" });
    const child = makeUnit({ name: "child", displayName: "Child" });
    listUnits.mockResolvedValue([parent, child]);
    getUnitDetail.mockResolvedValue(makeDetail(parent, []));
    addMember.mockRejectedValue(
      new Error(
        "API error 409: Conflict — Cyclic unit membership: parent → child → parent",
      ),
    );

    render(<SubUnitsTab unitId="parent" />);
    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /add sub-unit/i }),
      ).toBeEnabled();
    });

    fireEvent.click(screen.getByRole("button", { name: /add sub-unit/i }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText(/^Unit$/i), {
      target: { value: "child" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: /add sub-unit/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/Cyclic/);
    });
    // Dialog stays open so the caller can change their pick.
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("does not call DELETE when the user cancels the confirm dialog", async () => {
    const parent = makeUnit({ name: "parent" });
    const child = makeUnit({ name: "child", displayName: "Child" });
    listUnits.mockResolvedValue([parent, child]);
    getUnitDetail.mockResolvedValue(
      makeDetail(parent, [{ scheme: "unit", path: "child" }]),
    );

    render(<SubUnitsTab unitId="parent" />);
    await waitFor(() => {
      expect(screen.getByText("Child")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /Remove Child/i }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Remove sub-unit/i)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(removeMember).not.toHaveBeenCalled();
    expect(screen.getByText("Child")).toBeInTheDocument();
  });

  it("calls DELETE on confirm and removes the row", async () => {
    const parent = makeUnit({ name: "parent" });
    const child = makeUnit({ name: "child", displayName: "Child" });
    listUnits.mockResolvedValue([parent, child]);
    getUnitDetail.mockResolvedValue(
      makeDetail(parent, [{ scheme: "unit", path: "child" }]),
    );
    removeMember.mockResolvedValue(undefined);

    render(<SubUnitsTab unitId="parent" />);
    await waitFor(() => {
      expect(screen.getByText("Child")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /Remove Child/i }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^remove$/i }));

    await waitFor(() => {
      expect(removeMember).toHaveBeenCalledWith("parent", "child");
    });

    await waitFor(() => {
      expect(screen.queryByText("Child")).toBeNull();
    });

    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Sub-unit removed" }),
    );
  });
});
