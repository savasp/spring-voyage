"use client";

import { Sidebar } from "@/components/sidebar";
import type { ReactNode } from "react";

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <>
      <Sidebar />
      <main className="flex-1 overflow-y-auto p-4 md:p-6 pt-14 md:pt-6">
        {children}
      </main>
    </>
  );
}
