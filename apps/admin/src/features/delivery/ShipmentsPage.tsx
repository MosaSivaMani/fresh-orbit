import { formatCalendarDate, formatRupees, formatTime } from "@fresh/core";
import { type ColumnDef, DataTable, FilterBar, Input, Select, StatusBadge, cn } from "@fresh/ui";
import { Thermometer } from "lucide-react";
import { Link } from "react-router";
import { useRiders, useShipments, useSlots } from "../../api/delivery";
import { type DeliverySlot, type Rider, type Shipment } from "../../api/types/delivery";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { SHIPMENT_STATUS, statusOptions } from "../../lib/status";
import { todayInIndia } from "../../lib/units";
import { CollectLine, ShipmentActions } from "./ShipmentActions";
import { DeliveryNav, shortId } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["slotId", "riderId", "status"] as const;
const ANY = "__any";

function slotLabel(slot: DeliverySlot): string {
  return `${formatTime(slot.startAt)} – ${formatTime(slot.endAt)}`;
}

function columns(
  slots: ReadonlyMap<string, DeliverySlot>,
  riders: ReadonlyMap<string, Rider>,
): ColumnDef<Shipment>[] {
  return [
    {
      id: "order",
      header: "Order",
      cell: ({ row }) => (
        <span className="flex flex-col">
          <Link
            to={`/delivery/shipments/${row.original.id}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.orderNumber ?? `Shipment ${shortId(row.original.id)}`}
          </Link>
          <Link
            to={`/orders/${row.original.orderId}`}
            className="text-text-muted text-xs hover:underline"
          >
            Open order
          </Link>
        </span>
      ),
    },
    {
      id: "customer",
      header: "Customer",
      cell: ({ row }) =>
        row.original.recipientName ? (
          <span className="flex flex-col">
            <span>{row.original.recipientName}</span>
            {row.original.phone && (
              <a
                href={`tel:${row.original.phone}`}
                className="text-text-muted text-xs hover:underline"
              >
                {row.original.phone}
              </a>
            )}
          </span>
        ) : (
          "—"
        ),
    },
    {
      id: "address",
      header: "Address",
      cell: ({ row }) =>
        row.original.addressLine ? (
          <span className="block max-w-64 text-sm">
            {row.original.addressLine}
            {row.original.pincode && (
              <span className="text-text-muted block text-xs">{row.original.pincode}</span>
            )}
          </span>
        ) : (
          "—"
        ),
    },
    {
      id: "slot",
      header: "Slot",
      cell: ({ row }) => {
        const slot = slots.get(row.original.deliverySlotId);
        return slot ? (
          <span>
            {slotLabel(slot)}
            <span className="text-text-muted block text-xs">
              {formatCalendarDate(slot.slotDate)}
            </span>
          </span>
        ) : (
          <span className="text-text-muted">Other date</span>
        );
      },
    },
    {
      id: "rider",
      header: "Rider",
      cell: ({ row }) => {
        const id = row.original.riderId;
        if (!id) return <span className="text-text-muted">Not assigned</span>;
        return riders.get(id)?.fullName ?? `Rider ${shortId(id)}`;
      },
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => (
        <span className="flex flex-wrap items-center gap-1.5">
          <StatusBadge code={row.original.status} map={SHIPMENT_STATUS} />
          {row.original.temperatureWarning && (
            <span
              className="text-warning-text inline-flex items-center gap-0.5 text-xs"
              title="Dispatched above the cold-chain limit"
            >
              <Thermometer aria-hidden="true" className="size-3.5" />
              {row.original.dispatchTemperatureC} °C
            </span>
          )}
        </span>
      ),
    },
    {
      id: "collect",
      header: "To collect",
      cell: ({ row }) => <CollectLine shipment={row.original} />,
    },
    {
      id: "cash",
      header: "Collected",
      meta: { align: "right" },
      cell: ({ row }) =>
        row.original.cashCollectedPaise == null
          ? "—"
          : formatRupees(row.original.cashCollectedPaise),
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => <ShipmentActions shipment={row.original} />,
    },
  ];
}

/** Shipments board: the day's deliveries with the actions for each status (docs/design/admin.md, Delivery). */
export function ShipmentsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const slotDate = list.get("slotDate") ?? todayInIndia();
  const location = scope.locationId ? { fulfillmentLocationId: scope.locationId } : {};

  // The board shows the chosen day's shipments (G10, the slot's local date).
  const shipments = useShipments({ ...list.query(FILTERS), slotDate, ...location });
  const slots = useSlots({ ...location, fromDate: slotDate, toDate: slotDate, pageSize: 100 });
  const riders = useRiders({ ...location, pageSize: 100 });

  const slotMap = new Map((slots.data?.items ?? []).map((s) => [s.id, s]));
  const riderMap = new Map((riders.data?.items ?? []).map((r) => [r.id, r]));
  const statuses = list.getAll("status");
  const toggleStatus = (code: string) =>
    list.set({
      status: statuses.includes(code) ? statuses.filter((s) => s !== code) : [...statuses, code],
    });

  const slotId = list.get("slotId");
  const riderId = list.get("riderId");
  const active = [
    ...statuses.map((s) => ({
      id: `status-${s}`,
      label: `Status: ${SHIPMENT_STATUS[s]?.label ?? s}`,
      onRemove: () => toggleStatus(s),
    })),
    ...(slotId
      ? [
          {
            id: "slotId",
            label: `Slot: ${slotMap.get(slotId) ? slotLabel(slotMap.get(slotId)!) : shortId(slotId)}`,
            onRemove: () => list.set({ slotId: null }),
          },
        ]
      : []),
    ...(riderId
      ? [
          {
            id: "riderId",
            label: `Rider: ${riderMap.get(riderId)?.fullName ?? shortId(riderId)}`,
            onRemove: () => list.set({ riderId: null }),
          },
        ]
      : []),
  ];

  return (
    <>
      <PageHeader title="Delivery" description={`Shipments · ${scope.nameOf(scope.locationId)}`} />
      <DeliveryNav />
      <div className="flex flex-col gap-4">
        <FilterBar activeFilters={active} onClearAll={() => list.clear([...FILTERS, "slotDate"])}>
          <div className="w-40">
            <Input
              type="date"
              aria-label="Slot date"
              title="Slot date"
              value={slotDate}
              onChange={(e) => list.set({ slotDate: e.target.value || null, slotId: null })}
            />
          </div>
          <div className="w-48">
            <Select
              aria-label="Slot"
              options={[
                { value: ANY, label: "Slot: any" },
                ...(slots.data?.items ?? []).map((s) => ({ value: s.id, label: slotLabel(s) })),
              ]}
              value={slotId ?? ANY}
              onValueChange={(v) => list.set({ slotId: v === ANY ? null : v })}
            />
          </div>
          <div className="w-48">
            <Select
              aria-label="Rider"
              options={[
                { value: ANY, label: "Rider: any" },
                ...(riders.data?.items ?? []).map((r) => ({ value: r.id, label: r.fullName })),
              ]}
              value={riderId ?? ANY}
              onValueChange={(v) => list.set({ riderId: v === ANY ? null : v })}
            />
          </div>
          <div role="group" aria-label="Status" className="flex flex-wrap gap-1">
            {statusOptions(SHIPMENT_STATUS).map((o) => (
              <button
                key={o.value}
                type="button"
                aria-pressed={statuses.includes(o.value)}
                onClick={() => toggleStatus(o.value)}
                className={cn(
                  "border-border h-8 rounded-full border px-3 text-xs font-medium",
                  statuses.includes(o.value)
                    ? "bg-accent text-on-accent border-transparent"
                    : "bg-surface text-text-muted hover:text-text",
                )}
              >
                {o.label}
              </button>
            ))}
          </div>
        </FilterBar>
        <DataTable
          caption="Shipments"
          columns={columns(slotMap, riderMap)}
          data={shipments.data?.items ?? []}
          getRowId={(s) => s.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={shipments.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={shipments.isFetching}
          error={tableError(shipments.error)}
          onRetry={() => void shipments.refetch()}
          emptyState={
            <ListEmpty
              title="No shipments match these filters"
              description="Shipments appear here when orders are placed for this day."
            />
          }
        />
      </div>
    </>
  );
}
