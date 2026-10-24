import { ChevronRight } from "lucide-react";
import { cx } from "@/lib/cx";
import Link from "next/link";

export interface Crumb {
  href?: string;
  label: string;
}

/** `band`: ink text on the coral page band (D95). */
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
          band ? "text-on-block-coral" : "text-text-muted",
        )}
      >
        {items.map((item, index) => (
          <li key={`${item.label}-${index}`} className="flex items-center gap-1">
            {index > 0 && <ChevronRight aria-hidden="true" className="size-3.5" />}
            {item.href ? (
              <Link
                href={item.href}
                className={cx("underline-offset-4 hover:underline", !band && "hover:text-text")}
              >
                {item.label}
              </Link>
            ) : (
              <span aria-current="page" className={cx("font-semibold", !band && "text-text")}>
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
