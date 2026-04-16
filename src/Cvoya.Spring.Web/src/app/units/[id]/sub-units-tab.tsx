"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ChevronRight, Plus, Trash2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog } from "@/components/ui/dialog";
import { useToast } from "@/components/ui/toast";
import { api } from "@/lib/api/client";
import type { UnitResponse } from "@/lib/api/types";

interface SubUnitsTabProps {
  unitId: string;
}

/**
 * Shape of a single entry in the status-query payload's `Members` array.
 *
 * The server's `UnitActor.HandleStatusQueryAsync` serialises the payload via
 * `JsonSerializer.SerializeToElement` with the default (non-web) options, so
 * property names stay PascalCase. `UnitDetailResponse.Details` is written
 * through as an opaque `JsonElement` — `ConfigureHttpJsonOptions` only
 * re-serialises top-level response DTOs, not the JsonElement payload — so
 * this is the exact shape on the wire. See
 * `tests/Cvoya.Spring.Host.Api.Tests/Endpoints/UnitDetailsEndpointTests.cs`
 * for the explicit contract anchor.
 */
interface RawMember {
  Scheme?: string;
  Path?: string;
}

function readMembers(details: unknown): RawMember[] {
  if (!details || typeof details !== "object") return [];
  const maybe = (details as Record<string, unknown>).Members;
  return Array.isArray(maybe) ? (maybe as RawMember[]) : [];
}

interface SubUnitRow {
  path: string;
  displayName: string;
}

/**
 * Sub-units tab for the unit-detail page. Lists every child unit that has
 * been added to this unit via
 *
 *   spring unit members add <parent> --unit <child>
 *   POST /api/v1/units/{id}/members { memberAddress: { scheme: "unit", path } }
 *
 * Unit-scheme members intentionally don't flow through the `unit_memberships`
 * table today (#217 tracks the polymorphic M:N follow-up); the canonical read
 * path is therefore the unit actor's status-query payload exposed via
 * `GET /api/v1/units/{id}` → `details.Members[]` (#339 / #344 landed the
 * Members expansion). This tab filters that list to `Scheme === "unit"`.
 *
 * Row click → `/units/{child}` so the user can drill into the sub-unit's
 * own detail page. Add → dialog with a picker of every OTHER unit not
 * already a member. Remove → confirm, then `DELETE /units/{id}/members/{path}`
 * (scheme-agnostic — the server tries both agent:// and unit:// spellings).
 */
export function SubUnitsTab({ unitId }: SubUnitsTabProps) {
  const { toast } = useToast();

  const [rows, setRows] = useState<SubUnitRow[]>([]);
  const [allUnits, setAllUnits] = useState<UnitResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [addOpen, setAddOpen] = useState(false);
  const [addSelection, setAddSelection] = useState("");
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);

  const [confirmRemove, setConfirmRemove] = useState<SubUnitRow | null>(null);
  const [removing, setRemoving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [detail, units] = await Promise.all([
        api.getUnitDetail(unitId),
        api.listUnits(),
      ]);
      setAllUnits(units);
      const nameToDisplay: Record<string, string> = {};
      for (const u of units) {
        nameToDisplay[u.name] = u.displayName || u.name;
      }
      const subUnits = readMembers(detail.details)
        .filter((m) => m.Scheme === "unit" && typeof m.Path === "string")
        .map<SubUnitRow>((m) => ({
          path: m.Path as string,
          displayName: nameToDisplay[m.Path as string] ?? (m.Path as string),
        }));
      setRows(subUnits);
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [unitId]);

  useEffect(() => {
    load();
  }, [load]);

  // Candidate list for the Add dialog: every unit that isn't THIS unit and
  // isn't already a sub-unit. No cycle pre-check here — the server rejects
  // cyclic adds with a 409 (CyclicMembershipException), which we surface
  // verbatim in the dialog's error slot.
  const addCandidates = useMemo(() => {
    const already = new Set(rows.map((r) => r.path));
    return allUnits.filter((u) => u.name !== unitId && !already.has(u.name));
  }, [allUnits, rows, unitId]);

  const handleAdd = async () => {
    if (!addSelection) {
      setAddError("Pick a unit to add.");
      return;
    }
    setAddError(null);
    setAdding(true);
    try {
      await api.addMember(unitId, "unit", addSelection);
      toast({ title: "Sub-unit added", description: addSelection });
      setAddOpen(false);
      setAddSelection("");
      // Re-load rather than mutate: the server is the source of truth for
      // membership state, and a full refresh also catches display-name
      // updates if the child was renamed concurrently.
      await load();
    } catch (err) {
      setAddError(err instanceof Error ? err.message : String(err));
    } finally {
      setAdding(false);
    }
  };

  const handleRemove = async () => {
    const target = confirmRemove;
    if (!target) return;
    setRemoving(true);
    try {
      await api.removeMember(unitId, target.path);
      setRows((prev) => prev.filter((r) => r.path !== target.path));
      toast({ title: "Sub-unit removed", description: target.path });
      setConfirmRemove(null);
    } catch (err) {
      toast({
        title: "Remove failed",
        description: err instanceof Error ? err.message : String(err),
        variant: "destructive",
      });
    } finally {
      setRemoving(false);
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Sub-units</CardTitle>
        <Button
          size="sm"
          onClick={() => {
            setAddError(null);
            setAddSelection("");
            setAddOpen(true);
          }}
          disabled={loading || addCandidates.length === 0}
          aria-label="Add sub-unit"
        >
          <Plus className="mr-1 h-4 w-4" />
          Add sub-unit
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {loadError && (
          <p className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {loadError}
          </p>
        )}

        {loading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : rows.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No sub-units yet. Click{" "}
            <span className="font-medium">Add sub-unit</span> to nest another
            unit inside this one.
          </p>
        ) : (
          <ul
            className="divide-y divide-border rounded-md border border-border"
            aria-label="Sub-units"
          >
            {rows.map((r) => (
              <li
                key={r.path}
                className="flex items-center gap-2 px-3 py-2"
              >
                <Link
                  href={`/units/${r.path}`}
                  className="flex min-w-0 flex-1 items-center gap-2 text-left hover:underline"
                  aria-label={`Open ${r.displayName}`}
                >
                  <ChevronRight className="h-4 w-4 text-muted-foreground" />
                  <span className="truncate font-medium">{r.displayName}</span>
                  <span className="truncate font-mono text-xs text-muted-foreground">
                    unit://{r.path}
                  </span>
                </Link>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setConfirmRemove(r)}
                  aria-label={`Remove ${r.displayName}`}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      <Dialog
        open={addOpen}
        onClose={() => {
          if (!adding) setAddOpen(false);
        }}
        title="Add sub-unit"
        description={`Nest another unit inside ${unitId}. The server rejects cyclic additions with a 409.`}
        footer={
          <>
            <Button
              variant="outline"
              onClick={() => setAddOpen(false)}
              disabled={adding}
            >
              Cancel
            </Button>
            <Button
              onClick={() => {
                void handleAdd();
              }}
              disabled={adding || !addSelection}
            >
              {adding ? "Adding…" : "Add sub-unit"}
            </Button>
          </>
        }
      >
        <label className="block space-y-1">
          <span className="text-sm text-muted-foreground">Unit</span>
          <select
            value={addSelection}
            onChange={(e) => setAddSelection(e.target.value)}
            aria-label="Unit"
            disabled={adding || addCandidates.length === 0}
            className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
          >
            <option value="">
              {addCandidates.length === 0
                ? "No units available to add"
                : "Pick a unit…"}
            </option>
            {addCandidates.map((u) => (
              <option key={u.name} value={u.name}>
                {u.displayName || u.name}
              </option>
            ))}
          </select>
        </label>

        {addError && (
          <p
            role="alert"
            className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {addError}
          </p>
        )}
      </Dialog>

      <ConfirmDialog
        open={confirmRemove !== null}
        title="Remove sub-unit"
        description={
          confirmRemove
            ? `This removes ${confirmRemove.displayName} (unit://${confirmRemove.path}) as a member of ${unitId}. The sub-unit itself is not deleted.`
            : undefined
        }
        confirmLabel="Remove"
        confirmVariant="destructive"
        pending={removing}
        onConfirm={handleRemove}
        onCancel={() => setConfirmRemove(null)}
      />
    </Card>
  );
}
