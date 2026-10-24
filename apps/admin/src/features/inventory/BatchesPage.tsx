import { formatDateTime, humanizeCode } from "@fresh/core";
import {
  Button,
  type ColumnDef,
  DataTable,
  FilterBar,
  Input,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { useState } from "react";
import { Link } from "react-router";
import { useBatches } from "../../api/inventory";
import { type Batch } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { BATCH_STATUS, statusOptions } from "../../lib/status";
import { istLocalToUtcIso } from "../../lib/units";
import { qty } from "./maps";
import { InventoryNav, VariantRef } from "./shared";
import { DiscardBatchDialog } from "./StockActions";
import { ListEmpty } from "../../components/states";

const FILTERS = ["status", "expiringBefore", "stockItemId"];

export function BatchesPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const [discarding, setDiscarding] = useState<Batch | null>(null);
  const status = list.get("status");
  const expiringBefore = list.get("expiringBefore"); // YYYY-MM-DD, end of that day in IST
  const query = {
    ...list.query(["status", "stockItemId"]),
    ...(expiringBefore
      ? { expiringBefore: istLocalToUtcIso(`${expiringBefore}T23:59`) ?? undefined }
      : {}),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  };
  const batches = useBatches(query);

  const columns: ColumnDef<Batch>[] = [
    {
      id: "code",
      header: "Batch",
      cell: ({ row }) => <span className="font-medium">{row.original.batchCode}</span>,
    },
    {
      id: "variant",
      header: "Stock item",
      cell: ({ row }) => (
        <Link
          to={`/inventory/stock/${row.original.stockItemId}`}
          className="text-accent-text hover:underline"
          aria-label={`Open stock item for batch ${row.original.batchCode}`}
        >
          <VariantRef variantId={row.original.variantId} />
        </Link>
      ),
    },
    ...(scope.locationId
      ? []
      : [
          {
            id: "location",
            header: "Location",
            cell: ({ row }) => scope.nameOf(row.original.fulfillmentLocationId),
          } satisfies ColumnDef<Batch>,
        ]),
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={row.original.status} map={BATCH_STATUS} />,
    },
    { id: "supplier", header: "Supplier", cell: ({ row }) => row.original.supplierName ?? "—" },
    {
      id: "bestBefore",
      header: "Best before",
      cell: ({ row }) => formatDateTime(row.original.bestBeforeAt),
    },
    {
      id: "remaining",
      header: "Remaining",
      meta: { align: "right" },
      cell: ({ row }) => qty(row.original.remainingQuantity),
    },
    ...(can("inventory.adjust")
      ? [
          {
            id: "actions",
            header: "Actions",
            cell: ({ row }) =>
              row.original.status === "ACTIVE" ? (
                <Button size="sm" variant="ghost" onClick={() => setDiscarding(row.original)}>
                  Discard
                  <span className="sr-only"> batch {row.original.batchCode}</span>
                </Button>
              ) : null,
          } satisfies ColumnDef<Batch>,
        ]
      : []),
  ];

  return (
    <>
      <PageHeader
        title="Batches"
        description="Every received batch with what remains and when it expires."
      />
      <InventoryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={[
            ...(status
              ? [
                  {
                    id: "status",
                    label: `Status: ${BATCH_STATUS[status]?.label ?? humanizeCode(status)}`,
                    onRemove: () => list.set({ status: null }),
                  },
                ]
              : []),
            ...(expiringBefore
              ? [
                  {
                    id: "expiring",
                    label: `Expiring by ${expiringBefore}`,
                    onRemove: () => list.set({ expiringBefore: null }),
                  },
                ]
              : []),
            ...(list.get("stockItemId")
              ? [
                  {
                    id: "item",
                    label: "One stock item",
                    onRemove: () => list.set({ stockItemId: null }),
                  },
                ]
              : []),
          ]}
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-44">
            <Select
              aria-label="Batch status"
              options={[{ value: "all", label: "All statuses" }, ...statusOptions(BATCH_STATUS)]}
              value={status ?? "all"}
              onValueChange={(v) => list.set({ status: v === "all" ? null : v })}
            />
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span aria-hidden="true" className="text-text-muted whitespace-nowrap">
              Expiring by
            </span>
            <Input
              type="date"
              aria-label="Expiring by"
              className="w-40"
              value={expiringBefore ?? ""}
              onChange={(e) => list.set({ expiringBefore: e.target.value || null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Batches"
          columns={columns}
          data={batches.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={batches.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={batches.isFetching}
          error={tableError(batches.error)}
          onRetry={() => void batches.refetch()}
          emptyState={
            <ListEmpty
              title="No batches match"
              description="Change the filters or receive stock."
            />
          }
        />
      </div>
      {discarding && (
        <DiscardBatchDialog
          batch={discarding}
          open
          onOpenChange={(o) => !o && setDiscarding(null)}
          onConflict={() => void batches.refetch()}
        />
      )}
    </>
  );
}
