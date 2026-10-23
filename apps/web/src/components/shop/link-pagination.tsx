import { ChevronLeft, ChevronRight } from "lucide-react";
import Link from "next/link";
import { type SearchParams } from "@/lib/shop-params";

function pageRange(page: number, pages: number): (number | "gap")[] {
  if (pages <= 7) return Array.from({ length: pages }, (_, i) => i + 1);
  const result: (number | "gap")[] = [1];
  const start = Math.max(2, page - 1);
  const end = Math.min(pages - 1, page + 1);
  if (start > 2) result.push("gap");
  for (let p = start; p <= end; p++) result.push(p);
  if (end < pages - 1) result.push("gap");
  result.push(pages);
  return result;
}

/** Crawlable pagination (real links) for server-rendered lists; page numbers come from the server envelope. */
export function LinkPagination({
  basePath,
  searchParams,
  page,
  pageSize,
  totalCount,
}: {
  basePath: string;
  searchParams: SearchParams;
  page: number;
  pageSize: number;
  totalCount: number;
}) {
  const pages = Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));
  if (pages <= 1) return null;
  const href = (target: number) => {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(searchParams)) {
      if (key === "page" || value === undefined) continue;
      for (const v of Array.isArray(value) ? value : [value]) params.append(key, v);
    }
    if (target > 1) params.set("page", String(target));
    const qs = params.toString();
    return qs ? `${basePath}?${qs}` : basePath;
  };
  const linkClass =
    "inline-flex h-10 min-w-10 items-center justify-center rounded-md px-3 text-sm font-medium";
  return (
    <nav aria-label="Pagination" className="flex justify-center">
      <ul className="flex items-center gap-1">
        <li>
          {page > 1 ? (
            <Link
              href={href(page - 1)}
              rel="prev"
              className={`${linkClass} text-text hover:bg-muted`}
            >
              <ChevronLeft aria-hidden="true" className="size-4" />
              <span className="sr-only">Previous page</span>
            </Link>
          ) : null}
        </li>
        {pageRange(page, pages).map((item, index) =>
          item === "gap" ? (
            <li key={`gap-${index}`} aria-hidden="true" className="text-text-muted px-1">
              …
            </li>
          ) : (
            <li key={item}>
              <Link
                href={href(item)}
                aria-label={`Page ${item}`}
                aria-current={item === page ? "page" : undefined}
                className={`${linkClass} ${item === page ? "bg-primary text-on-primary" : "text-text hover:bg-muted"}`}
              >
                {item}
              </Link>
            </li>
          ),
        )}
        <li>
          {page < pages ? (
            <Link
              href={href(page + 1)}
              rel="next"
              className={`${linkClass} text-text hover:bg-muted`}
            >
              <ChevronRight aria-hidden="true" className="size-4" />
              <span className="sr-only">Next page</span>
            </Link>
          ) : null}
        </li>
      </ul>
    </nav>
  );
}
