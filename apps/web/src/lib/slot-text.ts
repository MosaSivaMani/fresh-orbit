// Delivery slot text in the store's own time zone (gap W10 closed: slots, serviceability and orders carry the
// location's IANA `timeZone`, ORD-R65). Formatting only; IST when the server sends no zone.
import { formatDayShort, formatTime } from "@fresh/core";

/** "Fri, 23 Oct, 2:00 pm – 4:00 pm". */
export function slotText(
  slot: { startAt: string; endAt: string },
  timeZone?: string | null,
): string {
  const day = formatDayShort(slot.startAt, timeZone);
  return `${day}, ${formatTime(slot.startAt, timeZone)} – ${formatTime(slot.endAt, timeZone)}`;
}
