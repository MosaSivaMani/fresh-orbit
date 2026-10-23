import { ChevronRight } from "lucide-react";
import Link from "next/link";

export interface Crumb {
  href?: string;
  label: string;
}

export function Breadcrumbs({ items }: { items: Crumb[] }) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="text-text-muted flex flex-wrap items-center gap-1 text-sm">
        {items.map((item, index) => (
          <li key={`${item.label}-${index}`} className="flex items-center gap-1">
            {index > 0 && <ChevronRight aria-hidden="true" className="size-3.5" />}
            {item.href ? (
              <Link href={item.href} className="hover:text-text underline-offset-4 hover:underline">
                {item.label}
              </Link>
            ) : (
              <span aria-current="page" className="text-text font-medium">
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
