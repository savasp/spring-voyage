import { cn } from "@/lib/utils";
import { ChevronRight } from "lucide-react";
import Link from "next/link";
import { Fragment } from "react";

export interface BreadcrumbItem {
  /** Visible label for the crumb. */
  label: string;
  /**
   * Optional link target. The final crumb (the current page) should omit
   * `href` so it renders as plain text — this matches the WAI-ARIA
   * breadcrumb pattern.
   */
  href?: string;
}

interface BreadcrumbsProps {
  items: BreadcrumbItem[];
  className?: string;
}

/**
 * Shared breadcrumb trail used on any page two levels or deeper.
 *
 * The final item represents the current page and should be passed without
 * an `href` so it is rendered as `aria-current="page"` text rather than a
 * link. See `docs/design/portal-exploration.md` § 3.3.
 */
export function Breadcrumbs({ items, className }: BreadcrumbsProps) {
  if (items.length === 0) return null;

  return (
    <nav
      aria-label="Breadcrumb"
      className={cn(
        "flex items-center text-sm text-muted-foreground",
        className,
      )}
    >
      <ol className="flex flex-wrap items-center gap-1">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;
          return (
            <Fragment key={`${item.label}-${index}`}>
              <li className="flex items-center">
                {item.href && !isLast ? (
                  <Link
                    href={item.href}
                    className="rounded-sm px-1 py-0.5 transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    {item.label}
                  </Link>
                ) : (
                  <span
                    aria-current={isLast ? "page" : undefined}
                    className={cn(
                      "px-1 py-0.5",
                      isLast && "font-medium text-foreground",
                    )}
                  >
                    {item.label}
                  </span>
                )}
              </li>
              {!isLast && (
                <li aria-hidden="true" className="flex items-center">
                  <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/60" />
                </li>
              )}
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
}
