"use client";

import { useCallback, useEffect, useState } from "react";
import { Activity, RefreshCw } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api/client";
import type {
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

export function ActivityTab({ unitId }: { unitId: string }) {
  const [result, setResult] = useState<ActivityQueryResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchActivity = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.queryActivity({
        source: `unit:${unitId}`,
        pageSize: "20",
      });
      setResult(data as ActivityQueryResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [unitId]);

  useEffect(() => {
    fetchActivity();
  }, [fetchActivity]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Activity className="h-4 w-4" />
          Recent Activity
          <Button
            variant="ghost"
            size="sm"
            className="ml-auto"
            onClick={fetchActivity}
            disabled={loading}
          >
            <RefreshCw
              className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
            />
          </Button>
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
            No activity events for this unit.
          </p>
        ) : (
          <div className="space-y-0">
            {result?.items.map((e) => (
              <div
                key={e.id}
                className="flex items-start gap-2 border-b border-border py-2 last:border-0 text-sm"
              >
                <Badge
                  variant={
                    severityVariant[e.severity as ActivitySeverity] ?? "default"
                  }
                  className="mt-0.5 shrink-0"
                >
                  {e.severity}
                </Badge>
                <div className="min-w-0 flex-1">
                  <p className="text-sm">{e.summary}</p>
                  <div className="mt-0.5 flex flex-wrap gap-2 text-xs text-muted-foreground">
                    <span>{e.eventType}</span>
                    {e.cost != null && <span>${e.cost.toFixed(4)}</span>}
                    <span>{timeAgo(e.timestamp)}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
