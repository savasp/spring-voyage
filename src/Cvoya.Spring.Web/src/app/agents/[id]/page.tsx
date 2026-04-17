import AgentDetailClient from "./agent-detail-client";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function AgentDetailPage({ params }: PageProps) {
  const { id } = await params;
  return <AgentDetailClient id={id} />;
}
