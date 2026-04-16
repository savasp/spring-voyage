"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Bot, DollarSign, Network } from "lucide-react";
import { api } from "@/lib/api/client";
import type { DashboardSummary } from "@/lib/api/types";
import { formatCost, timeAgo } from "@/lib/utils";
import { StatCard } from "@/components/stat-card";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

const statusVariant: Record<
  string,
  "default" | "success" | "warning" | "destructive" | "secondary" | "outline"
> = {
  Draft: "outline",
  Stopped: "secondary",
  Starting: "default",
  Running: "success",
  Stopping: "warning",
  Error: "destructive",
};

const severityDot: Record<string, string> = {
  Debug: "bg-muted-foreground",
  Info: "bg-blue-500",
  Warning: "bg-warning",
  Error: "bg-destructive",
};

export default function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const s = await api.getDashboardSummary();
        if (!cancelled) {
          setSummary(s);
          setLoading(false);
        }
      } catch {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    const interval = setInterval(load, 10_000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Dashboard</h1>

      {/* Stat cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {loading ? (
          <>
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
          </>
        ) : (
          <>
            <StatCard
              label="Agents"
              value={summary?.agentCount ?? 0}
              icon={<Bot className="h-5 w-5" />}
            />
            <StatCard
              label="Units"
              value={summary?.unitCount ?? 0}
              icon={<Network className="h-5 w-5" />}
            />
            <StatCard
              label="Total Cost"
              value={formatCost(summary?.totalCost ?? 0)}
              icon={<DollarSign className="h-5 w-5" />}
            />
          </>
        )}
      </div>

      {/* Units section */}
      {!loading && (
        <section>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">Units</h2>
            {summary && summary.unitCount > 0 && (
              <Link
                href="/units"
                className="text-sm text-primary hover:underline"
              >
                View all units
              </Link>
            )}
          </div>
          {summary && summary.units.length > 0 ? (
            <Card>
              <CardContent className="divide-y p-0">
                {summary.units.map((unit) => (
                  <Link
                    key={unit.name}
                    href={`/units/${encodeURIComponent(unit.name)}`}
                    className="flex items-center justify-between px-4 py-3 hover:bg-muted/50 transition-colors"
                    data-testid={`unit-row-${unit.name}`}
                  >
                    <div>
                      <p className="font-medium">{unit.displayName}</p>
                      <p className="text-xs text-muted-foreground">
                        {unit.name}
                      </p>
                    </div>
                    <Badge variant={statusVariant[unit.status] ?? "outline"}>
                      {unit.status}
                    </Badge>
                  </Link>
                ))}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">
                  No units created.{" "}
                  <Link
                    href="/units/create"
                    className="text-primary hover:underline"
                  >
                    Create one
                  </Link>
                </p>
              </CardContent>
            </Card>
          )}
        </section>
      )}

      {/* Agents section */}
      {!loading && (
        <section>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">Agents</h2>
          </div>
          {summary && summary.agents.length > 0 ? (
            <Card>
              <CardContent className="divide-y p-0">
                {summary.agents.map((agent) => (
                  <div
                    key={agent.name}
                    className="flex items-center justify-between px-4 py-3"
                    data-testid={`agent-row-${agent.name}`}
                  >
                    <div>
                      <p className="font-medium">{agent.displayName}</p>
                      <p className="text-xs text-muted-foreground">
                        {agent.name}
                      </p>
                    </div>
                    {agent.role && (
                      <Badge variant="secondary">{agent.role}</Badge>
                    )}
                  </div>
                ))}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">
                  No agents registered.
                </p>
              </CardContent>
            </Card>
          )}
        </section>
      )}

      {/* Activity section */}
      {!loading && (
        <section>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">Recent Activity</h2>
            {summary &&
              summary.recentActivity.length > 0 && (
                <Link
                  href="/activity"
                  className="text-sm text-primary hover:underline"
                >
                  View all activity
                </Link>
              )}
          </div>
          {summary && summary.recentActivity.length > 0 ? (
            <Card>
              <CardContent className="space-y-2 p-4">
                {summary.recentActivity.map((item) => (
                  <div
                    key={item.id}
                    className="flex items-start gap-2 text-sm"
                  >
                    <span className="mt-1.5 shrink-0">
                      <span
                        className={`inline-block h-2 w-2 rounded-full ${severityDot[item.severity] ?? "bg-muted-foreground"}`}
                      />
                    </span>
                    <div className="min-w-0 flex-1">
                      <p>{item.summary}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.source} &middot; {item.eventType} &middot;{" "}
                        {timeAgo(item.timestamp)}
                      </p>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">
                  No recent activity.
                </p>
              </CardContent>
            </Card>
          )}
        </section>
      )}
    </div>
  );
}
