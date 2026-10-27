import { ChevronRight } from "lucide-react";
import { cx } from "@/lib/cx";
import Link from "next/link";

export interface Crumb {
  href?: string;
  label: string;
}

/** `band`: on the harbour-sky page band (D188), the same muted ink as on the page. */
export function Breadcrumbs({
  items,
  tone = "default",
}: {
  items: Crumb[];
  tone?: "default" | "band";
}) {
  const band = tone === "band";
  return (
    <nav aria-label="Breadcrumb">
      <ol
        className={cx(
          "flex flex-wrap items-center gap-1 text-sm",
          band ? "text-text-muted font-medium" : "text-text-muted",
        )}
      >
        {items.map((item, index) => (
          <li key={`${item.label}-${index}`} className="flex items-center gap-1">
            {index > 0 && <ChevronRight aria-hidden="true" className="size-3.5" />}
            {item.href ? (
              <Link href={item.href} className="hover:text-text underline-offset-4 hover:underline">
                {item.label}
              </Link>
            ) : (
              <span aria-current="page" className="text-text font-semibold">
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
