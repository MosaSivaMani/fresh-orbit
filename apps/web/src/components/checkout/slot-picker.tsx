"use client";

import { formatDayShort, formatTime } from "@fresh/core";
import { EmptyState, ErrorState, Skeleton } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { CalendarX } from "lucide-react";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { getDeliverySlots } from "@/api/endpoints/catalog";
import { type DeliverySlot, type SlotStatus } from "@/api/types/locations";
import { cx } from "@/lib/cx";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const STATUS_TEXT: Record<SlotStatus, string> = {
  AVAILABLE: "",
  FULL: "Full",
  CUTOFF_PASSED: "Closed for orders",
  CLOSED: "Not delivering",
};

/** Delivery slots for the address's pincode, grouped by the server's `slotDate`; only AVAILABLE slots can be chosen. */
export function SlotPicker({
  pincode,
  value,
  onChange,
}: {
  pincode: string;
  value: string | null;
  onChange: (slotId: string) => void;
}) {
  const slots = useQuery({
    queryKey: queryKeys.slots(pincode),
    queryFn: () => getDeliverySlots(browserHttp(), pincode),
    staleTime: 30_000,
  });
  const [day, setDay] = useState<string | null>(null);

  if (slots.isPending) return <Skeleton className="h-28 rounded-xl" />;
  if (slots.isError) {
    return (
      <ErrorState
        title="We could not load delivery slots"
        description={describeError(slots.error)}
        traceId={asApiError(slots.error)?.traceId}
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
      <EmptyState
        icon={<CalendarX aria-hidden="true" />}
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

  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Delivery day" className="flex gap-2 overflow-x-auto pb-1">
        {days.map((d) => {
          const first = byDay.get(d)![0]!;
          const selected = d === selectedDay;
          return (
            <button
              key={d}
              type="button"
              aria-pressed={selected}
              onClick={() => setDay(d)}
              className={cx(
                "shrink-0 rounded-lg border px-4 py-2 text-sm font-medium",
                selected
                  ? "border-accent bg-accent-subtle text-accent-text"
                  : "border-border text-text hover:bg-muted",
              )}
            >
              {formatDayShort(first.startAt)}
            </button>
          );
        })}
      </div>
      <div
        role="radiogroup"
        aria-label="Delivery time"
        className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3"
      >
        {daySlots.map((slot) => {
          const available = slot.status === "AVAILABLE";
          const selected = slot.id === value;
          return (
            <button
              key={slot.id}
              type="button"
              role="radio"
              aria-checked={selected}
              disabled={!available}
              onClick={() => onChange(slot.id)}
              className={cx(
                "flex flex-col items-start rounded-lg border px-4 py-3 text-left text-sm",
                selected ? "border-accent bg-accent-subtle" : "border-border",
                available ? "hover:bg-muted" : "cursor-not-allowed opacity-60",
              )}
            >
              <span className="text-text font-semibold">
                {formatTime(slot.startAt)} – {formatTime(slot.endAt)}
              </span>
              <span className="text-text-muted text-xs">
                {available ? `Order by ${formatTime(slot.cutoffAt)}` : STATUS_TEXT[slot.status]}
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
