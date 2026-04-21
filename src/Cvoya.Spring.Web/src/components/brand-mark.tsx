"use client";

import Image from "next/image";
import { useTheme } from "@/lib/theme";
import { cn } from "@/lib/utils";

interface BrandMarkProps {
  /**
   * Pixel size of the rendered mark. Both axes are equal — the asset is
   * square. Defaults to 24, which matches the sidebar header in the design
   * system.
   */
  size?: number;
  /** Optional accessible label. Defaults to "Spring Voyage". */
  label?: string;
  className?: string;
}

/**
 * Theme-aware Spring Voyage sailboat mark.
 *
 * Renders one of two PNGs from `public/brand/` so the mark always paints
 * with sufficient contrast against the active surface:
 *
 *   - `/brand/sailboat-dark.png` — white-on-transparent; used in dark mode.
 *   - `/brand/sailboat-light.png` — black-on-transparent; used in light mode.
 *
 * The component reads the live theme from `useTheme()` and re-renders the
 * matching asset. Both files are emitted from the design source bundle in
 * `~/tmp/SpringVoyageDesign/logos/`.
 *
 * Both assets ship to the client at build time (Next.js bundles `public/`)
 * so the swap is instant — no FOUC, no extra network round-trip.
 *
 * The mark is `aria-hidden` when the surrounding chrome already labels the
 * brand (the sidebar wordmark sits next to it). Pass an explicit `label`
 * when the mark stands alone.
 */
export function BrandMark({
  size = 24,
  label = "Spring Voyage",
  className,
}: BrandMarkProps) {
  const { theme } = useTheme();
  const src =
    theme === "light" ? "/brand/sailboat-light.png" : "/brand/sailboat-dark.png";
  return (
    <Image
      src={src}
      width={size}
      height={size}
      alt={label}
      data-testid="brand-mark"
      data-theme={theme}
      // The asset is always square; `priority` would over-fetch on routes
      // that don't actually display the mark above the fold, so leave it
      // off — the sidebar load happens early enough that lazy is fine.
      className={cn("inline-block shrink-0 select-none", className)}
    />
  );
}
