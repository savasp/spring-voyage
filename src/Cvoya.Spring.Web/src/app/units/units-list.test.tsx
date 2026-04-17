import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";

import type { UnitDashboardSummary } from "@/lib/api/types";

// Mock the API module.
const getDashboardUnits =
  vi.fn<() => Promise<UnitDashboardSummary[]>>();
const deleteUnit = vi.fn<(id: string) => Promise<void>>();

vi.mock("@/lib/api/client", () => ({
  api: {
    getDashboardUnits: () => getDashboardUnits(),
    deleteUnit: (id: string) => deleteUnit(id),
    // Stubs for the detail view (not exercised in these tests).
    getUnitDetail: vi.fn(),
    getUnitCost: vi.fn(),
    addMember: vi.fn(),
    removeMember: vi.fn(),
  },
}));

const toastMock = vi.fn();
vi.mock("@/components/ui/toast", () => ({
  useToast: () => ({ toast: toastMock }),
}));

const pushMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => new URLSearchParams(),
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

import UnitDetailPage from "./page";

// Fresh QueryClient per render keeps TanStack caches scoped to a single
// test so mocks reset between cases.
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
  return render(<UnitDetailPage />, { wrapper: Wrapper });
}

function makeUnit(
  overrides: Partial<UnitDashboardSummary> = {},
): UnitDashboardSummary {
  return {
    name: "engineering",
    displayName: "Engineering",
    registeredAt: new Date().toISOString(),
    ...overrides,
  } as UnitDashboardSummary;
}

describe("Units list — delete unit", () => {
  beforeEach(() => {
    getDashboardUnits.mockReset();
    deleteUnit.mockReset();
    toastMock.mockReset();
    pushMock.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renders a delete button for each unit row", async () => {
    getDashboardUnits.mockResolvedValue([
      makeUnit({ name: "eng", displayName: "Engineering" }),
      makeUnit({ name: "mkt", displayName: "Marketing" }),
    ]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Engineering")).toBeInTheDocument();
    });

    expect(
      screen.getByRole("button", { name: /Delete Engineering/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Delete Marketing/i }),
    ).toBeInTheDocument();
  });

  it("opens confirm dialog on delete click and cancels without API call", async () => {
    getDashboardUnits.mockResolvedValue([
      makeUnit({ name: "eng", displayName: "Engineering" }),
    ]);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Engineering")).toBeInTheDocument();
    });

    fireEvent.click(
      screen.getByRole("button", { name: /Delete Engineering/i }),
    );

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Delete unit/i)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(deleteUnit).not.toHaveBeenCalled();
    // Unit still in the list.
    expect(screen.getByText("Engineering")).toBeInTheDocument();
  });

  it("deletes the unit on confirm and removes it from the list", async () => {
    getDashboardUnits.mockResolvedValue([
      makeUnit({ name: "eng", displayName: "Engineering" }),
      makeUnit({ name: "mkt", displayName: "Marketing" }),
    ]);
    deleteUnit.mockResolvedValue(undefined);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Engineering")).toBeInTheDocument();
    });

    fireEvent.click(
      screen.getByRole("button", { name: /Delete Engineering/i }),
    );

    const dialog = await screen.findByRole("dialog");
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^delete$/i }),
    );

    await waitFor(() => {
      expect(deleteUnit).toHaveBeenCalledWith("eng");
    });

    await waitFor(() => {
      expect(screen.queryByText("Engineering")).toBeNull();
    });
    // Other unit remains.
    expect(screen.getByText("Marketing")).toBeInTheDocument();

    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Unit deleted" }),
    );
  });

  it("shows error toast on API failure and keeps the dialog open", async () => {
    getDashboardUnits.mockResolvedValue([
      makeUnit({ name: "eng", displayName: "Engineering" }),
    ]);
    deleteUnit.mockRejectedValue(
      new Error("API error 500: Internal Server Error"),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Engineering")).toBeInTheDocument();
    });

    fireEvent.click(
      screen.getByRole("button", { name: /Delete Engineering/i }),
    );

    const dialog = await screen.findByRole("dialog");
    fireEvent.click(
      within(dialog).getByRole("button", { name: /^delete$/i }),
    );

    await waitFor(() => {
      expect(toastMock).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Delete failed",
          variant: "destructive",
        }),
      );
    });

    // Dialog stays open, unit remains in list.
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Engineering")).toBeInTheDocument();
  });
});
