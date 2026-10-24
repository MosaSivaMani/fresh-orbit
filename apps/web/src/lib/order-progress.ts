// Display mapping from the server's order status and timeline to tracking steps and a character. The status is the
// server's; this file only decides how to draw it (which step is lit), never what the order's state is.
import { formatDateTime } from "@fresh/core";
import { type IllustrationSlot, type StatusStep } from "@fresh/ui";
import { type OrderStatus, type TrackingEvent } from "@/api/types/orders";
import { orderStatusMap } from "./status-maps";

/** The usual path of an order, in order. */
const HAPPY_PATH: readonly OrderStatus[] = [
  "CONFIRMED",
  "PREPARING",
  "PACKED",
  "OUT_FOR_DELIVERY",
  "DELIVERED",
];

function label(status: string): string {
  return orderStatusMap[status]?.label ?? status;
}

/**
 * Steps for `StatusSteps`. On the usual path every step is shown, those before the server's status done, the
 * status itself current (done once delivered), with the time from the timeline where the server recorded one.
 * Off the path (cancelled, returned, failed, awaiting payment) the steps are the timeline as recorded.
 */
export function orderSteps(status: OrderStatus, timeline: readonly TrackingEvent[]): StatusStep[] {
  const at = new Map<string, string>();
  for (const event of timeline) at.set(event.status, event.at);
  const reached = HAPPY_PATH.indexOf(status);
  if (reached >= 0) {
    return HAPPY_PATH.map((s, i) => {
      const time = at.get(s);
      return {
        key: s,
        label: label(s),
        ...(time ? { time: formatDateTime(time) } : {}),
        state:
          i < reached || (i === reached && status === "DELIVERED")
            ? "done"
            : i === reached
              ? "current"
              : "upcoming",
      };
    });
  }
  return timeline.map((event, i) => ({
    key: `${event.status}-${event.at}`,
    label: label(event.status),
    time: formatDateTime(event.at),
    state: i === timeline.length - 1 ? "current" : "done",
  }));
}

/** The character for an order's status (@fresh/illustrations slots). */
export function orderSlot(status: OrderStatus): IllustrationSlot {
  switch (status) {
    case "OUT_FOR_DELIVERY":
    case "DELIVERY_FAILED":
      return "ORDER_ON_THE_WAY";
    case "DELIVERED":
      return "ORDER_DELIVERED";
    case "CANCELLED":
    case "RETURNED":
    case "PAYMENT_FAILED":
      return "ORDER_CANCELLED";
    case "PENDING_PAYMENT":
      return "ORDER_PLACED";
    default:
      return "ORDER_PACKING";
  }
}
