import { formatDateTime, formatRupees, formatTime, humanizeCode } from "@fresh/core";
import {
  type ActiveFilter,
  Button,
  type ColumnDef,
  DataTable,
  EmptyState,
  FilterBar,
  Input,
  type RowSelectionState,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { ClipboardList, PlayCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { Link } from "react-router";
import { BulkOrderError, useOrders, useStartPreparingMany } from "../../api/orders";
import { type AdminOrderSummary } from "../../api/types/orders";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { PageHeader, tableError } from "../../components/page";
import { reportError, notifySaved } from "../../lib/feedback";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { ORDER_STATUS, statusOptions } from "../../lib/status";

const FILTER_KEYS = ["status", "slotDate", "slotId", "paymentMethod"] as const;
const PAYMENT_METHODS = [
  { value: "COD", label: "Cash on delivery" },
  { value: "UPI_ON_DELIVERY", label: "UPI on delivery" },
  { value: "ONLINE", label: "Online" },
];
const ANY = "__any";

export function OrdersListPage() {
  const can = useCan();
  const list = useListParams({ defaultSort: "-placedAt" });
  const search = useDebouncedSearch(list);
  const { locationId, nameOf } = useLocationScope();
  const [selection, setSelection] = useState<RowSelectionState>({});

  const query = {
    ...list.query(FILTER_KEYS),
    ...(locationId ? { fulfillmentLocationId: locationId } : {}),
  };
  const orders = useOrders(query);
  const statuses = list.getAll("status");

  const columns = useMemo<ColumnDef<AdminOrderSummary>[]>(
    () => [
      {
        id: "orderNumber",
        header: "Order",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/orders/${row.original.id}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.orderNumber}
          </Link>
        ),
      },
      {
        id: "customer",
        header: "Customer",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span>{row.original.recipientName}</span>
            <span className="text-text-muted text-xs">{row.original.phone}</span>
          </span>
        ),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => <StatusBadge code={row.original.status} map={ORDER_STATUS} />,
      },
      {
        id: "placedAt",
        header: "Placed",
        cell: ({ row }) => formatDateTime(row.original.placedAt),
      },
      {
        id: "slot",
        header: "Slot",
        enableSorting: false,
        cell: ({ row }) => {
          const o = row.original;
          return (
            <span className="flex flex-col">
              <span>
                {formatTime(o.slotStartAt)} – {formatTime(o.slotEndAt)}
              </span>
              {can("orders.pack") && (
                <Link
                  to={`/orders/pick-list?slotId=${o.deliverySlotId}&fulfillmentLocationId=${o.fulfillmentLocationId}`}
                  className="text-accent-text text-xs hover:underline"
                >
                  Pick list
                </Link>
              )}
            </span>
          );
        },
      },
      ...(locationId
        ? []
        : [
            {
              id: "location",
              header: "Location",
              enableSorting: false,
              cell: ({ row }: { row: { original: AdminOrderSummary } }) =>
                nameOf(row.original.fulfillmentLocationId),
            } satisfies ColumnDef<AdminOrderSummary>,
          ]),
      {
        id: "itemCount",
        header: "Items",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => row.original.itemCount,
      },
      {
        id: "total",
        header: "Total",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.totalPaise),
      },
      {
        id: "paymentMethod",
        header: "Payment",
        enableSorting: false,
        cell: ({ row }) =>
          PAYMENT_METHODS.find((m) => m.value === row.original.paymentMethod)?.label ??
          humanizeCode(row.original.paymentMethod),
      },
    ],
    [can, locationId, nameOf],
  );

  const active: ActiveFilter[] = [
    ...statuses.map((s) => ({
      id: `status-${s}`,
      label: `Status: ${ORDER_STATUS[s]?.label ?? humanizeCode(s)}`,
      onRemove: () => list.set({ status: statuses.filter((x) => x !== s) }),
    })),
    ...(list.get("slotDate")
      ? [
          {
            id: "slotDate",
            label: `Slot date: ${list.get("slotDate")}`,
            onRemove: () => list.set({ slotDate: null }),
          },
        ]
      : []),
    ...(list.get("slotId")
      ? [{ id: "slotId", label: "One slot", onRemove: () => list.set({ slotId: null }) }]
      : []),
    ...(list.get("paymentMethod")
      ? [
          {
            id: "paymentMethod",
            label: `Payment: ${PAYMENT_METHODS.find((m) => m.value === list.get("paymentMethod"))?.label ?? list.get("paymentMethod")}`,
            onRemove: () => list.set({ paymentMethod: null }),
          },
        ]
      : []),
  ];

  const selectedRows = (ids: string[]) =>
    (orders.data?.items ?? []).filter((o) => ids.includes(o.id));

  return (
    <>
      <PageHeader
        title="Orders"
        description={locationId ? nameOf(locationId) : "All your locations"}
        actions={
          <>
            {can("complaints.manage") && (
              <Button asChild variant="outline" size="sm">
                <Link to="/complaints">Complaints</Link>
              </Button>
            )}
          </>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          search={{ ...search, placeholder: "Order number or phone", label: "Search orders" }}
          activeFilters={active}
          onClearAll={() => list.clear([...FILTER_KEYS, "q"])}
        >
          <div className="w-48">
            <Select
              aria-label="Add status filter"
              placeholder="Status"
              value=""
              options={statusOptions(ORDER_STATUS).filter((o) => !statuses.includes(o.value))}
              // `GET /admin/orders` takes one status code (gap G14): choosing a status replaces the filter.
              onValueChange={(v) => list.set({ status: [v] })}
            />
          </div>
          <div className="w-44">
            <Input
              type="date"
              aria-label="Slot date"
              value={list.get("slotDate") ?? ""}
              onChange={(e) => list.set({ slotDate: e.target.value || null })}
            />
          </div>
          <div className="w-48">
            <Select
              aria-label="Payment method"
              placeholder="Payment method"
              value={list.get("paymentMethod") ?? ANY}
              options={[{ value: ANY, label: "Any payment method" }, ...PAYMENT_METHODS]}
              onValueChange={(v) => list.set({ paymentMethod: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>

        <DataTable
          caption="Orders"
          columns={columns}
          data={orders.data?.items ?? []}
          getRowId={(o) => o.id}
          getRowLabel={(o) => o.orderNumber}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={orders.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          sorting={list.sorting}
          onSortingChange={list.setSorting}
          isLoading={orders.isFetching}
          error={tableError(orders.error)}
          onRetry={() => void orders.refetch()}
          enableRowSelection={can("orders.pack")}
          rowSelection={selection}
          onRowSelectionChange={setSelection}
          selectionActions={(ids) => (
            <BulkActions rows={selectedRows(ids)} onDone={() => setSelection({})} />
          )}
          emptyState={
            <EmptyState
              icon={<ClipboardList aria-hidden="true" />}
              title="No orders match these filters"
              description={
                active.length > 0 || list.q ? "Clear the filters to see all orders." : undefined
              }
            />
          }
        />
      </div>
    </>
  );
}

/** Bulk actions after selection: start preparing every selected CONFIRMED order, open the pick list of a slot. */
function BulkActions({ rows, onDone }: { rows: AdminOrderSummary[]; onDone: () => void }) {
  const confirmed = rows.filter((r) => r.status === "CONFIRMED");
  const slots = new Set(rows.map((r) => r.deliverySlotId));
  const first = rows[0];
  return (
    <>
      <StartPreparingMany orders={confirmed} onDone={onDone} />
      {slots.size === 1 && first && (
        <Button asChild size="sm" variant="outline">
          <Link
            to={`/orders/pick-list?slotId=${first.deliverySlotId}&fulfillmentLocationId=${first.fulfillmentLocationId}`}
          >
            Pick list for this slot
          </Link>
        </Button>
      )}
    </>
  );
}

function StartPreparingMany({
  orders,
  onDone,
}: {
  orders: AdminOrderSummary[];
  onDone: () => void;
}) {
  const start = useStartPreparingMany();
  if (orders.length === 0) return null;
  return (
    <Button
      size="sm"
      leadingIcon={<PlayCircle aria-hidden="true" />}
      loading={start.isPending}
      onClick={() =>
        start.mutate(
          orders.map((o) => o.id),
          {
            onSuccess: (count) => {
              notifySaved(`Started preparing ${count} ${count === 1 ? "order" : "orders"}`);
              onDone();
            },
            onError: (error) => {
              const bulk = error instanceof BulkOrderError ? error : null;
              const failed = orders.find((o) => o.id === bulk?.orderId);
              onDone();
              reportError(bulk?.cause ?? error, {
                title: failed
                  ? `Could not start ${failed.orderNumber}`
                  : "Could not start preparing",
              });
            },
          },
        )
      }
    >
      Start preparing ({orders.length})
    </Button>
  );
}
