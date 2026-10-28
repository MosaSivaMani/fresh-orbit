"use client";

import { formatDayShort, formatTime } from "@fresh/core";
import { FilterChip, OptionTile, Skeleton } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
import { browserHttp } from "@/api/browser";
import { getDeliverySlots } from "@/api/endpoints/catalog";
import { type DeliverySlot, type SlotStatus } from "@/api/types/locations";
import { EmptyPage, LoadError } from "@/components/states";
import { queryKeys } from "@/lib/query-keys";

const STATUS_TEXT: Record<SlotStatus, string> = {
  AVAILABLE: "",
  FULL: "Full",
  CUTOFF_PASSED: "Closed for orders",
  CLOSED: "Not delivering",
};

/**
 * Delivery slots for a pincode as food-app slot buttons (D88/D95): day chips, then one button per slot, the chosen
 * one filled with ink. Grouped by the server's `slotDate`; only AVAILABLE slots can be chosen. Used by the cart and
 * by checkout.
 */
export function SlotPicker({
  pincode,
  value,
  onChange,
}: {
  pincode: string;
  value: string | null;
  onChange: (slotId: string) => void;
}) {
  const name = useId();
  const slots = useQuery({
    queryKey: queryKeys.slots(pincode),
    queryFn: () => getDeliverySlots(browserHttp(), pincode),
    staleTime: 30_000,
  });
  const [day, setDay] = useState<string | null>(null);

  if (slots.isPending) return <Skeleton className="h-28 rounded-2xl" />;
  if (slots.isError) {
    return (
      <LoadError
        title="We could not load delivery slots"
        error={slots.error}
        headingLevel={3}
        onRetry={() => void slots.refetch()}
      />
    );
  }

  const byDay = new Map<string, DeliverySlot[]>();
  for (const slot of slots.data.items) {
    byDay.set(slot.slotDate, [...(byDay.get(slot.slotDate) ?? []), slot]);
  }
  const days = [...byDay.keys()];
  if (days.length === 0) {
    return (
      <EmptyPage
        slot="ORDER_PACKING"
        headingLevel={3}
        title="No delivery slots right now"
        description="Please check again later."
      />
    );
  }
  const selectedDay =
    day ??
    slots.data.items.find((s) => s.id === value)?.slotDate ??
    days.find((d) => byDay.get(d)?.some((s) => s.status === "AVAILABLE")) ??
    days[0]!;
  const daySlots = byDay.get(selectedDay) ?? [];
  // Slot times in the store's own zone (W10).
  const zone = slots.data.timeZone;

  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Delivery day" className="flex gap-2 overflow-x-auto pb-1">
        {days.map((d) => (
          <FilterChip key={d} pressed={d === selectedDay} onClick={() => setDay(d)}>
            {formatDayShort(byDay.get(d)![0]!.startAt, zone)}
          </FilterChip>
        ))}
      </div>
      <div
        role="radiogroup"
        aria-label="Delivery time"
        className="grid grid-cols-2 gap-2 sm:grid-cols-3"
      >
        {daySlots.map((slot) => {
          const available = slot.status === "AVAILABLE";
          return (
            <OptionTile
              key={slot.id}
              look="ink"
              name={name}
              value={slot.id}
              checked={slot.id === value}
              disabled={!available}
              onChange={() => onChange(slot.id)}
              label={`${formatTime(slot.startAt, zone)} – ${formatTime(slot.endAt, zone)}`}
              description={
                available ? `Order by ${formatTime(slot.cutoffAt, zone)}` : STATUS_TEXT[slot.status]
              }
            />
          );
        })}
      </div>
    </div>
  );
}
