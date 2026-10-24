import { pageCount as countPages } from "@fresh/core";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn, focusRing } from "../lib/cn";
import { IconButton } from "./button";
import { Select } from "./select";

export type PaginationItem = number | "ellipsis-start" | "ellipsis-end";

/** Page numbers to show: first, last, current ± siblings, with ellipses. */
export function paginationRange(page: number, pages: number, siblings = 1): PaginationItem[] {
  const range = (from: number, to: number) =>
    Array.from({ length: to - from + 1 }, (_, i) => from + i);
  // Always the same number of slots so the control does not jump around.
  const slots = siblings * 2 + 5;
  if (pages <= slots) return range(1, pages);
  if (page <= siblings + 3) return [...range(1, siblings * 2 + 3), "ellipsis-end", pages];
  if (page >= pages - siblings - 2)
    return [1, "ellipsis-start", ...range(pages - siblings * 2 - 2, pages)];
  return [1, "ellipsis-start", ...range(page - siblings, page + siblings), "ellipsis-end", pages];
}

export interface PaginationProps {
  /** 1-based current page (server paging). */
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  pageSizeOptions?: readonly number[];
  onPageSizeChange?: (pageSize: number) => void;
  /** Hide the "Showing x–y of z" text (e.g. on small cards). */
  hideSummary?: boolean;
  className?: string;
}

const numberFormat = new Intl.NumberFormat("en-IN");

export function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
  pageSizeOptions = [20, 50, 100],
  onPageSizeChange,
  hideSummary = false,
  className,
}: PaginationProps) {
  const pages = countPages({ pageSize, totalCount });
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  return (
    <nav
      aria-label="Pagination"
      className={cn(
        "text-text-muted flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between",
        className,
      )}
    >
      {!hideSummary && (
        <p aria-live="polite">
          Showing <span className="text-text font-medium">{numberFormat.format(from)}</span>–
          <span className="text-text font-medium">{numberFormat.format(to)}</span> of{" "}
          <span className="text-text font-medium">{numberFormat.format(totalCount)}</span>
        </p>
      )}
      <div className="flex items-center gap-3">
        {onPageSizeChange && (
          <div className="flex items-center gap-2">
            <span aria-hidden="true">Rows</span>
            <Select
              aria-label="Rows per page"
              className="h-8 w-20"
              value={String(pageSize)}
              onValueChange={(v) => onPageSizeChange(Number(v))}
              options={pageSizeOptions.map((n) => ({ value: String(n), label: String(n) }))}
            />
          </div>
        )}
        <ul className="flex items-center gap-1">
          <li>
            <IconButton
              label="Previous page"
              size="icon-sm"
              icon={<ChevronLeft aria-hidden="true" />}
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
            />
          </li>
          {paginationRange(page, pages).map((item) =>
            typeof item === "number" ? (
              <li key={item}>
                <button
                  type="button"
                  onClick={() => onPageChange(item)}
                  aria-label={`Page ${item}`}
                  aria-current={item === page ? "page" : undefined}
                  className={cn(
                    "inline-flex h-8 min-w-8 cursor-pointer items-center justify-center rounded-lg px-2 text-sm font-medium tabular-nums",
                    item === page ? "bg-primary text-on-primary" : "text-text hover:bg-muted",
                    focusRing,
                  )}
                >
                  {item}
                </button>
              </li>
            ) : (
              <li key={item} aria-hidden="true" className="px-1">
                …
              </li>
            ),
          )}
          <li>
            <IconButton
              label="Next page"
              size="icon-sm"
              icon={<ChevronRight aria-hidden="true" />}
              disabled={page >= pages}
              onClick={() => onPageChange(page + 1)}
            />
          </li>
        </ul>
      </div>
    </nav>
  );
}
