import {
  type ColumnDef,
  type OnChangeFn,
  type RowSelectionState,
  type SortingState,
  flexRender,
  functionalUpdate,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react";
import { type ReactNode, useMemo } from "react";
import { cn, focusRing } from "../lib/cn";
import { Checkbox } from "./choice";
import { EmptyState, ErrorState, Skeleton } from "./display";
import { Pagination } from "./pagination";

export type { ColumnDef, RowSelectionState, SortingState } from "@tanstack/react-table";

export interface DataTableError {
  traceId?: string | undefined;
  message?: string;
}

export interface DataTableProps<TData> {
  // TanStack column value types vary per column, so `any` is the library's own convention here.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  columns: ColumnDef<TData, any>[];
  /** Rows of the current page only (server paging). */
  data: readonly TData[];
  getRowId: (row: TData) => string;
  /** Human name of a row for the selection checkbox ("Select FO-260001"). Defaults to the row id. */
  getRowLabel?: (row: TData) => string;
  /** Accessible table name; shown visually only when `showCaption`. */
  caption: string;
  showCaption?: boolean;

  /** 1-based page from the server envelope. */
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;

  /** Server sorting; ids must match the API's sort field names. */
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;

  enableRowSelection?: boolean;
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: (selection: RowSelectionState) => void;

  isLoading?: boolean;
  error?: DataTableError | null;
  onRetry?: () => void;
  emptyState?: ReactNode;
  /** Bulk actions shown when rows are selected. */
  selectionActions?: (selectedIds: string[]) => ReactNode;
  density?: "compact" | "comfortable";
  className?: string;
}

const SKELETON_ROWS = 5;

/**
 * Admin data table. Sorting and paging are server-driven: the table reports changes, the caller refetches.
 * Columns can set `meta: { align: "right" }` for numbers and money.
 */
export function DataTable<TData>({
  columns,
  data,
  getRowId,
  getRowLabel,
  caption,
  showCaption = false,
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  sorting = [],
  onSortingChange,
  enableRowSelection = false,
  rowSelection = {},
  onRowSelectionChange,
  isLoading = false,
  error,
  onRetry,
  emptyState,
  selectionActions,
  density = "compact",
  className,
}: DataTableProps<TData>) {
  const allColumns = useMemo<ColumnDef<TData>[]>(() => {
    if (!enableRowSelection) return columns;
    const select: ColumnDef<TData> = {
      id: "__select",
      enableSorting: false,
      header: ({ table }) => (
        <Checkbox
          aria-label="Select all rows on this page"
          checked={
            table.getIsAllPageRowsSelected()
              ? true
              : table.getIsSomePageRowsSelected()
                ? "indeterminate"
                : false
          }
          onCheckedChange={(v) => table.toggleAllPageRowsSelected(v === true)}
        />
      ),
      cell: ({ row }) => (
        <Checkbox
          aria-label={`Select ${getRowLabel ? getRowLabel(row.original) : row.id}`}
          checked={row.getIsSelected()}
          disabled={!row.getCanSelect()}
          onCheckedChange={(v) => row.toggleSelected(v === true)}
        />
      ),
    };
    return [select, ...columns];
  }, [columns, enableRowSelection, getRowLabel]);

  const handleSorting: OnChangeFn<SortingState> = (updater) =>
    onSortingChange?.(functionalUpdate(updater, sorting));
  const handleSelection: OnChangeFn<RowSelectionState> = (updater) =>
    onRowSelectionChange?.(functionalUpdate(updater, rowSelection));

  // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table returns non-memoisable functions by design.
  const table = useReactTable<TData>({
    data: data as TData[],
    columns: allColumns,
    getRowId: (row) => getRowId(row),
    getCoreRowModel: getCoreRowModel(),
    manualSorting: true,
    manualPagination: true,
    enableSortingRemoval: true,
    enableMultiSort: false,
    rowCount: totalCount,
    state: { sorting, rowSelection, pagination: { pageIndex: page - 1, pageSize } },
    onSortingChange: handleSorting,
    onRowSelectionChange: handleSelection,
    enableRowSelection,
  });

  const colSpan = allColumns.length;
  const cellPad = density === "compact" ? "px-3 py-2" : "px-4 py-3";
  const selectedIds = Object.keys(rowSelection).filter((k) => rowSelection[k]);
  const showSkeleton = isLoading && data.length === 0;

  let body: ReactNode;
  if (error) {
    body = (
      <tr>
        <td colSpan={colSpan}>
          <ErrorState
            title="Could not load this list"
            {...(error.message ? { description: error.message } : {})}
            traceId={error.traceId}
            {...(onRetry ? { onRetry } : {})}
          />
        </td>
      </tr>
    );
  } else if (showSkeleton) {
    body = Array.from({ length: SKELETON_ROWS }, (_, i) => (
      <tr key={`skeleton-${i}`} className="border-border border-b">
        {allColumns.map((c, j) => (
          <td key={c.id ?? j} className={cellPad}>
            <Skeleton className="h-4 w-full max-w-40" />
          </td>
        ))}
      </tr>
    ));
  } else if (data.length === 0) {
    body = (
      <tr>
        <td colSpan={colSpan}>{emptyState ?? <EmptyState title="Nothing here yet" />}</td>
      </tr>
    );
  } else {
    body = table.getRowModel().rows.map((row) => (
      <tr
        key={row.id}
        data-state={row.getIsSelected() ? "selected" : undefined}
        className="border-border hover:bg-muted/60 data-[state=selected]:bg-accent-subtle border-b transition-colors"
      >
        {row.getVisibleCells().map((cell) => (
          <td
            key={cell.id}
            className={cn(cellPad, "align-middle", alignClass(cell.column.columnDef.meta))}
          >
            {flexRender(cell.column.columnDef.cell, cell.getContext())}
          </td>
        ))}
      </tr>
    ));
  }

  return (
    <div className={cn("flex flex-col gap-3", className)}>
      {enableRowSelection && selectedIds.length > 0 && (
        <div
          role="status"
          className="border-accent/30 bg-accent-subtle text-accent-text flex flex-wrap items-center gap-3 rounded-lg border px-3 py-2 text-sm"
        >
          <span className="font-medium">{selectedIds.length} selected</span>
          {selectionActions?.(selectedIds)}
        </div>
      )}
      <div className="border-border bg-surface shadow-soft-sm overflow-x-auto rounded-xl border">
        <table className="w-full border-collapse text-sm" aria-busy={isLoading || undefined}>
          <caption
            className={cn(showCaption ? "text-text p-3 text-left font-semibold" : "sr-only")}
          >
            {caption}
          </caption>
          <thead className="bg-muted/60">
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id} className="border-border border-b">
                {group.headers.map((header) => {
                  const sorted = header.column.getIsSorted();
                  const canSort = header.column.getCanSort() && onSortingChange !== undefined;
                  const content = header.isPlaceholder
                    ? null
                    : flexRender(header.column.columnDef.header, header.getContext());
                  return (
                    <th
                      key={header.id}
                      scope="col"
                      aria-sort={
                        canSort
                          ? sorted === "asc"
                            ? "ascending"
                            : sorted === "desc"
                              ? "descending"
                              : "none"
                          : undefined
                      }
                      className={cn(
                        cellPad,
                        "text-text-muted text-left text-xs font-semibold tracking-wide whitespace-nowrap uppercase",
                        alignClass(header.column.columnDef.meta),
                        header.column.id === "__select" && "w-10",
                      )}
                    >
                      {canSort ? (
                        <button
                          type="button"
                          onClick={header.column.getToggleSortingHandler()}
                          className={cn(
                            "hover:text-text -mx-1 inline-flex cursor-pointer items-center gap-1 rounded px-1 uppercase",
                            focusRing,
                          )}
                        >
                          {content}
                          {sorted === "asc" ? (
                            <ArrowUp aria-hidden="true" className="size-3.5" />
                          ) : sorted === "desc" ? (
                            <ArrowDown aria-hidden="true" className="size-3.5" />
                          ) : (
                            <ArrowUpDown aria-hidden="true" className="size-3.5 opacity-50" />
                          )}
                        </button>
                      ) : (
                        content
                      )}
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody className={cn(isLoading && !showSkeleton && "opacity-60")}>{body}</tbody>
        </table>
      </div>
      {!error && totalCount > 0 && (
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={totalCount}
          onPageChange={onPageChange}
          {...(onPageSizeChange ? { onPageSizeChange } : {})}
        />
      )}
    </div>
  );
}

function alignClass(meta: unknown): string | undefined {
  const align = (meta as { align?: "left" | "right" | "center" } | undefined)?.align;
  if (align === "right") return "text-right tabular-nums";
  if (align === "center") return "text-center";
  return undefined;
}
