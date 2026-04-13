"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useToast } from "@/components/ui/toast";
import { api } from "@/lib/api/client";
import type {
  AgentExecutionMode,
  AgentResponse,
  UnitAgentSlot,
} from "@/lib/api/types";

const EXECUTION_MODES: AgentExecutionMode[] = ["Auto", "OnDemand"];

interface AgentsTabProps {
  unitId: string;
}

/**
 * Agents tab for the unit configuration page. Lists the agent slots
 * configured on the unit (see UnitAgentSlot on the server), lets an
 * operator assign a new agent, edit per-slot fields inline, and
 * unassign.
 *
 * Slots are independent from the unit's routing membership — see #124
 * for the rationale. An agent can be slotted here for configuration
 * without being wired into message dispatch, and vice versa.
 */
export function AgentsTab({ unitId }: AgentsTabProps) {
  const { toast } = useToast();
  const [slots, setSlots] = useState<UnitAgentSlot[]>([]);
  const [availableAgents, setAvailableAgents] = useState<AgentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [addAgentId, setAddAgentId] = useState("");
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [slotList, agentList] = await Promise.all([
        api.listUnitAgents(unitId),
        api.listAgents(),
      ]);
      setSlots(slotList);
      setAvailableAgents(agentList);
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [unitId]);

  useEffect(() => {
    load();
  }, [load]);

  const slottedIds = new Set(slots.map((s) => s.agentId));
  const assignable = availableAgents.filter((a) => !slottedIds.has(a.name));

  const handleAdd = async () => {
    if (!addAgentId) return;
    setAdding(true);
    setAddError(null);
    try {
      const created = await api.assignUnitAgent(unitId, addAgentId);
      setSlots((prev) => [...prev, created]);
      setAddAgentId("");
      toast({ title: "Agent assigned", description: addAgentId });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setAddError(message);
    } finally {
      setAdding(false);
    }
  };

  const patchSlot = async (
    agentId: string,
    patch: Partial<UnitAgentSlot>,
  ) => {
    try {
      const updated = await api.updateUnitAgent(unitId, agentId, patch);
      setSlots((prev) =>
        prev.map((s) => (s.agentId === agentId ? updated : s)),
      );
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      toast({
        title: "Update failed",
        description: message,
        variant: "destructive",
      });
    }
  };

  const handleRemove = async (agentId: string) => {
    try {
      await api.unassignUnitAgent(unitId, agentId);
      setSlots((prev) => prev.filter((s) => s.agentId !== agentId));
      toast({ title: "Agent unassigned", description: agentId });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      toast({
        title: "Unassign failed",
        description: message,
        variant: "destructive",
      });
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Agents</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {loadError && (
          <p className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {loadError}
          </p>
        )}

        {loading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : slots.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No agents assigned yet. Pick one below to configure it for this unit.
          </p>
        ) : (
          <ul className="space-y-2">
            {slots.map((slot) => (
              <li
                key={slot.agentId}
                className="rounded-md border bg-card p-3 space-y-3"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{slot.agentId}</span>
                    {!slot.enabled && (
                      <Badge variant="outline">Disabled</Badge>
                    )}
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleRemove(slot.agentId)}
                    aria-label={`Unassign ${slot.agentId}`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <label className="block space-y-1">
                    <span className="text-xs text-muted-foreground">
                      Model override
                    </span>
                    <Input
                      value={slot.model ?? ""}
                      placeholder="(inherit from agent)"
                      onChange={(e) =>
                        setSlots((prev) =>
                          prev.map((s) =>
                            s.agentId === slot.agentId
                              ? { ...s, model: e.target.value || null }
                              : s,
                          ),
                        )
                      }
                      onBlur={(e) =>
                        patchSlot(slot.agentId, {
                          model: e.target.value || null,
                        })
                      }
                    />
                  </label>

                  <label className="block space-y-1">
                    <span className="text-xs text-muted-foreground">
                      Specialty
                    </span>
                    <Input
                      value={slot.specialty ?? ""}
                      placeholder="e.g. reviewer"
                      onChange={(e) =>
                        setSlots((prev) =>
                          prev.map((s) =>
                            s.agentId === slot.agentId
                              ? { ...s, specialty: e.target.value || null }
                              : s,
                          ),
                        )
                      }
                      onBlur={(e) =>
                        patchSlot(slot.agentId, {
                          specialty: e.target.value || null,
                        })
                      }
                    />
                  </label>

                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={slot.enabled}
                      onChange={(e) =>
                        patchSlot(slot.agentId, { enabled: e.target.checked })
                      }
                    />
                    Enabled
                  </label>

                  <label className="block space-y-1">
                    <span className="text-xs text-muted-foreground">
                      Execution mode
                    </span>
                    <select
                      value={slot.executionMode}
                      onChange={(e) =>
                        patchSlot(slot.agentId, {
                          executionMode: e.target.value as AgentExecutionMode,
                        })
                      }
                      className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    >
                      {EXECUTION_MODES.map((m) => (
                        <option key={m} value={m}>
                          {m}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="border-t pt-4 space-y-2">
          <p className="text-sm font-medium">Assign an agent</p>
          {assignable.length === 0 ? (
            <p className="text-xs text-muted-foreground">
              All registered agents are already slotted in this unit.
            </p>
          ) : (
            <div className="flex items-end gap-2">
              <label className="flex-1 space-y-1">
                <span className="text-xs text-muted-foreground">Agent</span>
                <select
                  value={addAgentId}
                  onChange={(e) => setAddAgentId(e.target.value)}
                  className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option value="">Pick an agent…</option>
                  {assignable.map((a) => (
                    <option key={a.name} value={a.name}>
                      {a.displayName || a.name}
                    </option>
                  ))}
                </select>
              </label>
              <Button
                onClick={handleAdd}
                disabled={!addAgentId || adding}
                size="sm"
              >
                <Plus className="mr-1 h-4 w-4" />
                {adding ? "Assigning…" : "Assign"}
              </Button>
            </div>
          )}
          {addError && (
            <p className="text-xs text-destructive">{addError}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
