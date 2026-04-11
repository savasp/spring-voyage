"use client";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { ActivityEvent, ActivitySeverity } from "@/lib/api/types";
import { timeAgo } from "@/lib/utils";
import { useActivityStream } from "@/hooks/use-activity-stream";
import { Activity, Wifi, WifiOff } from "lucide-react";

const severityVariant: Record<ActivitySeverity, "default" | "success" | "warning" | "destructive"> = {
  Debug: "default",
  Info: "success",
  Warning: "warning",
  Error: "destructive",
};

function EventRow({ event }: { event: ActivityEvent }) {
  return (
    <div className="flex items-start gap-3 border-b border-border py-3 last:border-0">
      <Badge variant={severityVariant[event.severity]} className="mt-0.5 shrink-0">
        {event.severity}
      </Badge>
      <div className="min-w-0 flex-1">
        <p className="text-sm">{event.summary}</p>
        <div className="mt-1 flex flex-wrap gap-2 text-xs text-muted-foreground">
          <span>
            {event.source.scheme}://{event.source.path}
          </span>
          <span>{event.eventType}</span>
          {event.cost != null && <span>${event.cost.toFixed(4)}</span>}
          <span>{timeAgo(event.timestamp)}</span>
        </div>
      </div>
    </div>
  );
}

export default function ActivityPage() {
  const { events, connected } = useActivityStream();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Activity Feed</h1>
        <Badge variant={connected ? "success" : "outline"} className="gap-1">
          {connected ? (
            <Wifi className="h-3 w-3" />
          ) : (
            <WifiOff className="h-3 w-3" />
          )}
          {connected ? "Connected" : "Disconnected"}
        </Badge>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Activity className="h-4 w-4" /> Real-time Events
          </CardTitle>
        </CardHeader>
        <CardContent>
          {events.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {connected
                ? "Waiting for activity events..."
                : "Connecting to activity stream..."}
            </p>
          ) : (
            <div className="max-h-[calc(100vh-250px)] overflow-y-auto">
              {events.map((e) => (
                <EventRow key={e.id} event={e} />
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
