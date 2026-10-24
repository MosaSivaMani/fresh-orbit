import { formatDateTime, formatRupees, humanizeCode } from "@fresh/core";
import { type ColumnDef, DataTable, FilterBar, Input, Select, StatusBadge } from "@fresh/ui";
import { Link } from "react-router";
import { useMovements } from "../../api/inventory";
import { type Movement } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { statusOptions } from "../../lib/status";
import { MOVEMENT_KIND, qty } from "./maps";
import { InventoryNav } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["stockItemId", "kind", "fromDate", "toDate"];

export function MovementsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const kind = list.get("kind");
  const stockItemId = list.get("stockItemId");
  const movements = useMovements({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });

  const columns: ColumnDef<Movement>[] = [
    { id: "when", header: "When", cell: ({ row }) => formatDateTime(row.original.occurredAt) },
    {
      id: "kind",
      header: "Kind",
      cell: ({ row }) => <StatusBadge code={row.original.kind} map={MOVEMENT_KIND} />,
    },
    {
      id: "stockItem",
      header: "Stock item",
      cell: ({ row }) => (
        <Link
          to={`/inventory/stock/${row.original.stockItemId}`}
          className="text-accent-text font-mono text-xs hover:underline"
          title={row.original.stockItemId}
        >
          …{row.original.stockItemId.slice(-12)}
        </Link>
      ),
    },
    {
      id: "quantity",
      header: "Quantity",
      meta: { align: "right" },
      cell: ({ row }) => qty(row.original.quantity),
    },
    {
      id: "cost",
      header: "Cost",
      meta: { align: "right" },
      cell: ({ row }) => formatRupees(row.original.costPaise),
    },
    {
      id: "reason",
      header: "Reason",
      cell: ({ row }) => (row.original.reasonCode ? humanizeCode(row.original.reasonCode) : "—"),
    },
    {
      id: "reference",
      header: "Reference",
      cell: ({ row }) =>
        row.original.referenceType === "MANUAL"
          ? "Manual"
          : humanizeCode(row.original.referenceType),
    },
    { id: "notes", header: "Notes", cell: ({ row }) => row.original.notes ?? "—" },
  ];

  return (
    <>
      <PageHeader
        title="Stock movements"
        description="The ledger every stock figure is computed from. Movements are never edited or deleted."
      />
      <InventoryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={[
            ...(stockItemId
              ? [
                  {
                    id: "item",
                    label: `Stock item …${stockItemId.slice(-8)}`,
                    onRemove: () => list.set({ stockItemId: null }),
                  },
                ]
              : []),
            ...(kind
              ? [
                  {
                    id: "kind",
                    label: `Kind: ${MOVEMENT_KIND[kind]?.label ?? humanizeCode(kind)}`,
                    onRemove: () => list.set({ kind: null }),
                  },
                ]
              : []),
            ...(list.get("fromDate")
              ? [
                  {
                    id: "from",
                    label: `From ${list.get("fromDate")}`,
                    onRemove: () => list.set({ fromDate: null }),
                  },
                ]
              : []),
            ...(list.get("toDate")
              ? [
                  {
                    id: "to",
                    label: `To ${list.get("toDate")}`,
                    onRemove: () => list.set({ toDate: null }),
                  },
                ]
              : []),
          ]}
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-48">
            <Select
              aria-label="Movement kind"
              options={[{ value: "all", label: "All kinds" }, ...statusOptions(MOVEMENT_KIND)]}
              value={kind ?? "all"}
              onValueChange={(v) => list.set({ kind: v === "all" ? null : v })}
            />
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span aria-hidden="true" className="text-text-muted">
              From
            </span>
            <Input
              type="date"
              aria-label="From date"
              className="w-40"
              value={list.get("fromDate") ?? ""}
              onChange={(e) => list.set({ fromDate: e.target.value || null })}
            />
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span aria-hidden="true" className="text-text-muted">
              To
            </span>
            <Input
              type="date"
              aria-label="To date"
              className="w-40"
              value={list.get("toDate") ?? ""}
              onChange={(e) => list.set({ toDate: e.target.value || null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Stock movements"
          columns={columns}
          data={movements.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={movements.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={movements.isFetching}
          error={tableError(movements.error)}
          onRetry={() => void movements.refetch()}
          emptyState={
            <ListEmpty title="No movements match" description="Change the filters or dates." />
          }
        />
      </div>
    </>
  );
}
