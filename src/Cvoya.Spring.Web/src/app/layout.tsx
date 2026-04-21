import type { Metadata, Viewport } from "next";
import { GeistMono } from "geist/font/mono";
import { GeistSans } from "geist/font/sans";
import { ThemeProvider } from "@/lib/theme";
import { ToastProvider } from "@/components/ui/toast";
import { AppShell } from "@/components/app-shell";
import { QueryProvider } from "@/lib/api/query-provider";
import "./globals.css";

export const metadata: Metadata = {
  title: "Spring Voyage",
  description: "AI agent orchestration dashboard",
};

export const viewport: Viewport = {
  themeColor: "#09090b",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // Geist + Geist Mono are loaded via `next/font` so the binary payload is
  // self-hosted, deduplicated, and FOUC-free. The two `.variable` strings
  // expose the fonts as CSS custom properties — `--font-geist-sans` and
  // `--font-geist-mono` — which `globals.css` then plugs into Tailwind 4's
  // `--font-sans` and `--font-mono` `@theme` tokens. Components keep using
  // the `font-sans` / `font-mono` Tailwind utilities and pick up Geist for
  // free.
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${GeistSans.variable} ${GeistMono.variable}`}
    >
      <body className="flex h-screen bg-background text-foreground font-sans antialiased dark">
        <ThemeProvider>
          <QueryProvider>
            <ToastProvider>
              <AppShell>{children}</AppShell>
            </ToastProvider>
          </QueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
