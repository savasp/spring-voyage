"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useToast } from "@/components/ui/toast";
import { api } from "@/lib/api/client";
import type {
  AgentDetailResponse,
  CloneResponse,
  CostSummaryResponse,
} from "@/lib/api/types";
import { formatCost, timeAgo } from "@/lib/utils";
import { ArrowLeft, Copy, DollarSign, Trash2 } from "lucide-react";
import Link from "next/link";

function AgentDetailContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { toast } = useToast();
  const id = searchParams.get("id") ?? "";
  const [data, setData] = useState<AgentDetailResponse | null>(null);
  const [cost, setCost] = useState<CostSummaryResponse | null>(null);
  const [clones, setClones] = useState<CloneResponse[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;

    async function load() {
      try {
        const [agentData, costData, clonesData] = await Promise.allSettled([
          api.getAgent(id),
          api.getAgentCost(id),
          api.getClones(id),
        ]);
        if (!cancelled) {
          if (agentData.status === "fulfilled") setData(agentData.value);
          if (costData.status === "fulfilled") setCost(costData.value);
          if (clonesData.status === "fulfilled") setClones(clonesData.value);
          setLoading(false);
        }
      } catch {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => { cancelled = true; };
  }, [id]);

  const handleDelete = async () => {
    try {
      await api.deleteAgent(id);
      toast({ title: "Agent deleted" });
      router.push("/");
    } catch (err) {
      toast({
        title: "Failed to delete agent",
        description: err instanceof Error ? err.message : String(err),
        variant: "destructive",
      });
    }
  };

  if (!id) {
    return <p className="text-muted-foreground">No agent ID specified.</p>;
  }

  if (loading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40" />
        <Skeleton className="h-32" />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="space-y-4">
        <Link href="/" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
          <ArrowLeft className="h-4 w-4" /> Back to Dashboard
        </Link>
        <p className="text-muted-foreground">Agent not found.</p>
      </div>
    );
  }

  const { agent } = data;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <Link href="/" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-2">
            <ArrowLeft className="h-4 w-4" /> Dashboard
          </Link>
          <h1 className="text-2xl font-bold">{agent.displayName}</h1>
          <p className="text-sm text-muted-foreground">{agent.name}</p>
        </div>
        <Button variant="destructive" size="sm" onClick={handleDelete}>
          <Trash2 className="h-4 w-4 mr-1" /> Delete
        </Button>
      </div>

      {/* Agent info */}
      <Card>
        <CardHeader>
          <CardTitle>Agent Info</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Description</span>
            <span>{agent.description || "—"}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Role</span>
            <span>{agent.role ? <Badge variant="outline">{String(agent.role)}</Badge> : "—"}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Registered</span>
            <span>{timeAgo(agent.registeredAt)}</span>
          </div>
        </CardContent>
      </Card>

      {/* Cost */}
      {cost !== null && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <DollarSign className="h-4 w-4" /> Cost Breakdown
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Total Cost</span>
              <span className="font-medium">{formatCost(cost.totalCost)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Input Tokens</span>
              <span>{cost.totalInputTokens.toLocaleString()}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Output Tokens</span>
              <span>{cost.totalOutputTokens.toLocaleString()}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Records</span>
              <span>{cost.recordCount}</span>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Status payload */}
      {data.status != null && (
        <Card>
          <CardHeader>
            <CardTitle>Status</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="overflow-x-auto rounded bg-muted p-3 text-xs">
              {JSON.stringify(data.status, null, 2)}
            </pre>
          </CardContent>
        </Card>
      )}

      {/* Clones */}
      {clones.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Copy className="h-4 w-4" /> Clones ({clones.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {clones.map((c) => (
              <div key={c.id} className="flex items-center justify-between rounded border border-border p-2 text-sm">
                <span className="font-mono text-xs">{c.id}</span>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{c.state}</Badge>
                  <span className="text-xs text-muted-foreground">{timeAgo(c.createdAt)}</span>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export default function AgentDetailPage() {
  return (
    <Suspense fallback={<Skeleton className="h-40" />}>
      <AgentDetailContent />
    </Suspense>
  );
}
