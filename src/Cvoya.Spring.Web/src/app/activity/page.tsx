"use client";

import { useCallback, useEffect, useState } from "react";
import {
  Activity,
  ChevronDown,
  ChevronRight,
  ChevronLeft,
  RefreshCw,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api } from "@/lib/api/client";
import type {
  ActivityEvent,
  ActivityEventType,
  ActivityQueryResult,
  ActivitySeverity,
} from "@/lib/api/types";
import { timeAgo } from "@/lib/utils";

const severityVariant: Record<
  ActivitySeverity,
  "default" | "success" | "warning" | "destructive"
> = {
  Debug: "default",
  Info: "success",
  Warning: "warning",
  Error: "destructive",
};

const eventTypes: ActivityEventType[] = [
  "MessageReceived",
  "MessageSent",
  "ConversationStarted",
  "ConversationCompleted",
  "DecisionMade",
  "ErrorOccurred",
  "StateChanged",
  "InitiativeTriggered",
  "ReflectionCompleted",
  "WorkflowStepCompleted",
  "CostIncurred",
  "TokenDelta",
];

const severities: ActivitySeverity[] = ["Debug", "Info", "Warning", "Error"];

interface Filters {
  source: string;
  eventType: string;
  severity: string;
}

const PAGE_SIZE = 20;

function EventRow({
  event,
  expanded,
  onToggle,
}: {
  event: ActivityQueryResult["items"][number];
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <div className="border-b border-border last:border-0">
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-start gap-3 py-3 text-left hover:bg-accent/50 px-2 rounded-md transition-colors"
      >
        {expanded ? (
          <ChevronDown className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
        ) : (
          <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
        )}
        <span className="text-xs text-muted-foreground mt-0.5 shrink-0 w-16">
          {timeAgo(event.timestamp)}
        </span>
        <Badge variant="outline" className="shrink-0 text-xs">
          {event.source}
        </Badge>
        <span className="text-xs text-muted-foreground shrink-0">
          {event.eventType}
        </span>
        <Badge
          variant={
            severityVariant[event.severity as ActivitySeverity] ?? "default"
          }
          className="shrink-0"
        >
          {event.severity}
        </Badge>
        <span className="min-w-0 flex-1 truncate text-sm">{event.summary}</span>
      </button>
      {expanded && (
        <div className="ml-10 mb-3 rounded-md border border-border bg-muted/30 p-3 text-sm space-y-1">
          <div className="flex gap-2">
            <span className="text-muted-foreground">ID:</span>
            <span className="font-mono text-xs">{event.id}</span>
          </div>
          {event.correlationId && (
            <div className="flex gap-2">
              <span className="text-muted-foreground">Correlation ID:</span>
              <span className="font-mono text-xs">{event.correlationId}</span>
            </div>
          )}
          {event.cost != null && (
            <div className="flex gap-2">
              <span className="text-muted-foreground">Cost:</span>
              <span>${event.cost.toFixed(4)}</span>
            </div>
          )}
          <div className="flex gap-2">
            <span className="text-muted-foreground">Timestamp:</span>
            <span>{new Date(event.timestamp).toLocaleString()}</span>
          </div>
        </div>
      )}
    </div>
  );
}

export default function ActivityPage() {
  const [result, setResult] = useState<ActivityQueryResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Filters>({
    source: "",
    eventType: "",
    severity: "",
  });
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const fetchActivity = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string> = {
        page: String(page),
        pageSize: String(PAGE_SIZE),
      };
      if (filters.source) params.source = filters.source;
      if (filters.eventType) params.eventType = filters.eventType;
      if (filters.severity) params.severity = filters.severity;

      const data = await api.queryActivity(params);
      setResult(data as ActivityQueryResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [page, filters]);

  useEffect(() => {
    fetchActivity();
  }, [fetchActivity]);

  const totalPages = result
    ? Math.max(1, Math.ceil(Number(result.totalCount) / Number(result.pageSize)))
    : 1;

  const toggleExpanded = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleFilterChange = (key: keyof Filters, value: string) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setPage(1);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Activity</h1>
        <Button
          variant="outline"
          size="sm"
          onClick={fetchActivity}
          disabled={loading}
        >
          <RefreshCw
            className={`h-4 w-4 mr-1 ${loading ? "animate-spin" : ""}`}
          />
          Refresh
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="pt-4">
          <div className="flex flex-wrap gap-3">
            <label className="space-y-1">
              <span className="text-xs text-muted-foreground">Source</span>
              <Input
                placeholder="e.g. unit:my-unit"
                value={filters.source}
                onChange={(e) => handleFilterChange("source", e.target.value)}
                className="w-48"
              />
            </label>
            <label className="space-y-1">
              <span className="text-xs text-muted-foreground">Event Type</span>
              <select
                value={filters.eventType}
                onChange={(e) =>
                  handleFilterChange("eventType", e.target.value)
                }
                className="flex h-9 w-48 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">All types</option>
                {eventTypes.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>
            <label className="space-y-1">
              <span className="text-xs text-muted-foreground">Severity</span>
              <select
                value={filters.severity}
                onChange={(e) =>
                  handleFilterChange("severity", e.target.value)
                }
                className="flex h-9 w-36 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">All</option>
                {severities.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </CardContent>
      </Card>

      {/* Event list */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Activity className="h-4 w-4" />
            Events
            {result && (
              <span className="ml-auto text-sm font-normal text-muted-foreground">
                {result.totalCount} total
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {error && (
            <p className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive mb-3">
              {error}
            </p>
          )}
          {loading && !result ? (
            <p className="text-sm text-muted-foreground">Loading activity...</p>
          ) : result?.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No activity events found.
            </p>
          ) : (
            <div className="max-h-[calc(100vh-400px)] overflow-y-auto">
              {result?.items.map((e) => (
                <EventRow
                  key={e.id}
                  event={e}
                  expanded={expandedIds.has(e.id)}
                  onToggle={() => toggleExpanded(e.id)}
                />
              ))}
            </div>
          )}

          {/* Pagination */}
          {result && totalPages > 1 && (
            <div className="flex items-center justify-between mt-4 pt-3 border-t border-border">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                <ChevronLeft className="h-4 w-4 mr-1" /> Previous
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
