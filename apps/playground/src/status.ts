import { type StatusMap } from "@fresh/ui";

/** Order status labels and tones (codes from docs/api/README.md §6). Apps define these maps once per domain. */
export const ORDER_STATUS: StatusMap = {
  PENDING_PAYMENT: { label: "Awaiting payment", tone: "warning" },
  PAYMENT_FAILED: { label: "Payment failed", tone: "danger" },
  CONFIRMED: { label: "Confirmed", tone: "info" },
  PREPARING: { label: "Preparing", tone: "accent" },
  PACKED: { label: "Packed", tone: "accent" },
  OUT_FOR_DELIVERY: { label: "Out for delivery", tone: "highlight" },
  DELIVERED: { label: "Delivered", tone: "success" },
  DELIVERY_FAILED: { label: "Delivery failed", tone: "danger" },
  RETURNED: { label: "Returned", tone: "neutral" },
  CANCELLED: { label: "Cancelled", tone: "neutral" },
};
