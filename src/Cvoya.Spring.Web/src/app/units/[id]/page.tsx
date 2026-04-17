import UnitConfigClient from "./unit-config-client";

interface PageProps {
  // Next 16 app router: route params are delivered as a Promise.
  params: Promise<{ id: string }>;
}

export default async function UnitConfigPage({ params }: PageProps) {
  const { id } = await params;
  return <UnitConfigClient id={id} />;
}
