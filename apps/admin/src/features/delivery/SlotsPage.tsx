import { formatCalendarDate, formatTime, humanizeCode } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  FilterBar,
  Input,
  StatusBadge,
} from "@fresh/ui";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useChangeSlot, useSlots } from "../../api/delivery";
import { type DeliverySlot } from "../../api/types/delivery";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { SelectField, TextField } from "../../components/form";
import { notifySaved } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { SLOT_STATUS } from "../../lib/status";
import { parseWholeNumber, todayInIndia } from "../../lib/units";
import { DeliveryNav, reportWriteError } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["fromDate", "toDate"] as const;

/** Generated delivery slots: capacity and open/closed per date (docs/api §4.17). */
export function SlotsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const [editing, setEditing] = useState<DeliverySlot | null>(null);
  const fromDate = list.get("fromDate") ?? todayInIndia();
  const query = {
    ...list.query(FILTERS),
    fromDate,
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  };
  const slots = useSlots(query);

  const columns: ColumnDef<DeliverySlot>[] = [
    { id: "date", header: "Date", cell: ({ row }) => formatCalendarDate(row.original.slotDate) },
    {
      id: "window",
      header: "Window",
      cell: ({ row }) => `${formatTime(row.original.startAt)} – ${formatTime(row.original.endAt)}`,
    },
    ...(scope.locationId
      ? []
      : [
          {
            id: "location",
            header: "Location",
            cell: ({ row }) => scope.nameOf(row.original.fulfillmentLocationId),
          } satisfies ColumnDef<DeliverySlot>,
        ]),
    { id: "cutoff", header: "Order cut-off", cell: ({ row }) => formatTime(row.original.cutoffAt) },
    {
      id: "booked",
      header: "Booked / capacity",
      meta: { align: "right" },
      cell: ({ row }) => `${row.original.bookedCount} / ${row.original.capacity}`,
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => (
        <span className="flex flex-col gap-0.5">
          <StatusBadge code={row.original.displayStatus} map={SLOT_STATUS} />
          {row.original.closedReason && (
            <span className="text-text-muted text-xs">
              {humanizeCode(row.original.closedReason)}
            </span>
          )}
        </span>
      ),
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => (
        <Button size="sm" variant="outline" onClick={() => setEditing(row.original)}>
          Change
        </Button>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Delivery"
        description={`Delivery slots · ${scope.nameOf(scope.locationId)}`}
      />
      <DeliveryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={[
            ...(list.get("fromDate")
              ? [
                  {
                    id: "fromDate",
                    label: `From: ${list.get("fromDate")}`,
                    onRemove: () => list.set({ fromDate: null }),
                  },
                ]
              : []),
            ...(list.get("toDate")
              ? [
                  {
                    id: "toDate",
                    label: `To: ${list.get("toDate")}`,
                    onRemove: () => list.set({ toDate: null }),
                  },
                ]
              : []),
          ]}
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-40">
            <Input
              type="date"
              aria-label="From date"
              title="From date"
              value={fromDate}
              onChange={(e) => list.set({ fromDate: e.target.value || null })}
            />
          </div>
          <div className="w-40">
            <Input
              type="date"
              aria-label="To date"
              title="To date"
              value={list.get("toDate") ?? ""}
              onChange={(e) => list.set({ toDate: e.target.value || null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Delivery slots"
          columns={columns}
          data={slots.data?.items ?? []}
          getRowId={(s) => s.id}
          getRowLabel={(s) => `${formatCalendarDate(s.slotDate)} ${formatTime(s.startAt)}`}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={slots.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={slots.isFetching}
          error={tableError(slots.error)}
          onRetry={() => void slots.refetch()}
          emptyState={
            <ListEmpty
              title="No slots in these dates"
              description="Slots are generated from the active slot templates each night."
            />
          }
        />
      </div>
      {editing && <SlotDialog slot={editing} onClose={() => setEditing(null)} />}
    </>
  );
}

const slotSchema = z.object({
  capacity: z
    .string()
    .trim()
    .refine((v) => parseWholeNumber(v) !== null, "Enter capacity as a whole number of orders."),
  status: z.enum(["OPEN", "CLOSED"]),
});

const SLOT_STATES = [
  { value: "OPEN", label: "Open for booking" },
  { value: "CLOSED", label: "Closed" },
];

export function SlotDialog({ slot, onClose }: { slot: DeliverySlot; onClose: () => void }) {
  const change = useChangeSlot();
  const form = useForm<z.infer<typeof slotSchema>>({
    resolver: zodResolver(slotSchema),
    defaultValues: { capacity: String(slot.capacity), status: slot.status },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    const capacity = parseWholeNumber(values.capacity) ?? slot.capacity;
    const body = {
      ...(capacity !== slot.capacity ? { capacity } : {}),
      ...(values.status !== slot.status ? { status: values.status } : {}),
    };
    if (Object.keys(body).length === 0) return onClose();
    try {
      await change.mutateAsync({ id: slot.id, version: slot.version, body });
      notifySaved("Slot updated");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["capacity", "status"],
        codeFields: { SLOT_CAPACITY_BELOW_BOOKED: "capacity" },
        onConflict: onClose,
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="Change slot"
        description={`${formatCalendarDate(slot.slotDate)}, ${formatTime(slot.startAt)} – ${formatTime(slot.endAt)} · ${slot.bookedCount} booked`}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="change-slot" loading={change.isPending}>
              Save
            </Button>
          </>
        }
      >
        <form id="change-slot" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <TextField
            form={form}
            name="capacity"
            label="Capacity (orders)"
            hint="Cannot be lower than the orders already booked."
            inputMode="numeric"
            required
          />
          <SelectField form={form} name="status" label="Booking" required options={SLOT_STATES} />
        </form>
      </DialogContent>
    </Dialog>
  );
}
