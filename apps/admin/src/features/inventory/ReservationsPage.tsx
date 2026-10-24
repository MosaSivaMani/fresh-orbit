import { formatDateTime, humanizeCode } from "@fresh/core";
import { type ColumnDef, DataTable, FilterBar, Select, StatusBadge } from "@fresh/ui";
import { Link } from "react-router";
import { useReservations } from "../../api/inventory";
import { type Reservation } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { statusOptions } from "../../lib/status";
import { RESERVATION_STATUS, qty } from "./maps";
import { InventoryNav, VariantRef } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["status", "orderId"];

export function ReservationsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const status = list.get("status");
  const reservations = useReservations({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });

  const columns: ColumnDef<Reservation>[] = [
    {
      id: "created",
      header: "Reserved",
      cell: ({ row }) => formatDateTime(row.original.createdAt),
    },
    {
      id: "variant",
      header: "Stock item",
      cell: ({ row }) => (
        <Link
          to={`/inventory/stock/${row.original.stockItemId}`}
          className="text-accent-text hover:underline"
          aria-label={`Open stock item ${row.original.variantId}`}
        >
          <VariantRef variantId={row.original.variantId} />
        </Link>
      ),
    },
    {
      id: "order",
      header: "Order",
      cell: ({ row }) =>
        can("orders.view") ? (
          <Link
            to={`/orders/${row.original.orderId}`}
            className="text-accent-text font-mono text-xs hover:underline"
            title={row.original.orderId}
          >
            …{row.original.orderId.slice(-12)}
          </Link>
        ) : (
          <span className="font-mono text-xs" title={row.original.orderId}>
            …{row.original.orderId.slice(-12)}
          </span>
        ),
    },
    {
      id: "quantity",
      header: "Quantity",
      meta: { align: "right" },
      cell: ({ row }) => qty(row.original.quantity),
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={row.original.status} map={RESERVATION_STATUS} />,
    },
    {
      id: "expires",
      header: "Expires",
      cell: ({ row }) => (row.original.expiresAt ? formatDateTime(row.original.expiresAt) : "—"),
    },
  ];

  return (
    <>
      <PageHeader
        title="Reservations"
        description="Stock held for orders between checkout and packing."
      />
      <InventoryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={[
            ...(status
              ? [
                  {
                    id: "status",
                    label: `Status: ${RESERVATION_STATUS[status]?.label ?? humanizeCode(status)}`,
                    onRemove: () => list.set({ status: null }),
                  },
                ]
              : []),
            ...(list.get("orderId")
              ? [{ id: "order", label: "One order", onRemove: () => list.set({ orderId: null }) }]
              : []),
          ]}
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-44">
            <Select
              aria-label="Reservation status"
              options={[
                { value: "all", label: "All statuses" },
                ...statusOptions(RESERVATION_STATUS),
              ]}
              value={status ?? "all"}
              onValueChange={(v) => list.set({ status: v === "all" ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Reservations"
          columns={columns}
          data={reservations.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={reservations.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={reservations.isFetching}
          error={tableError(reservations.error)}
          onRetry={() => void reservations.refetch()}
          emptyState={<ListEmpty title="No reservations match" description="Change the filters." />}
        />
      </div>
    </>
  );
}
