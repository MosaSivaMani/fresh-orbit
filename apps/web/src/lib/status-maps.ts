// UI copy for server status codes (design README §6.8). Codes come from the API; this file only names them.
import { type StatusMap } from "@fresh/ui";

export const orderStatusMap: StatusMap = {
  PENDING_PAYMENT: { label: "Awaiting payment", tone: "warning" },
  CONFIRMED: { label: "Confirmed", tone: "info" },
  PREPARING: { label: "Being prepared", tone: "info" },
  PACKED: { label: "Packed", tone: "accent" },
  OUT_FOR_DELIVERY: { label: "Out for delivery", tone: "accent" },
  DELIVERED: { label: "Delivered", tone: "success" },
  DELIVERY_FAILED: { label: "Delivery attempt failed", tone: "warning" },
  RETURNED: { label: "Returned", tone: "neutral" },
  CANCELLED: { label: "Cancelled", tone: "neutral" },
  PAYMENT_FAILED: { label: "Payment failed", tone: "danger" },
};

export const availabilityMap: StatusMap = {
  IN_STOCK: { label: "In stock", tone: "success" },
  LOW_STOCK: { label: "Few left", tone: "highlight" },
  OUT_OF_STOCK: { label: "Sold out", tone: "danger" },
  NOT_SOLD_HERE: { label: "Not sold at your location", tone: "neutral" },
};

export const paymentMethodLabels: Record<string, string> = {
  COD: "Cash on delivery",
  UPI_ON_DELIVERY: "UPI on delivery",
  ONLINE: "Pay online",
};

export const complaintStatusMap: StatusMap = {
  OPEN: { label: "Open", tone: "info" },
  APPROVED: { label: "Approved", tone: "success" },
  REJECTED: { label: "Not approved", tone: "neutral" },
  RESOLVED: { label: "Resolved", tone: "success" },
};

export const sourceTypeLabels: Record<string, string> = {
  WILD: "Wild caught",
  FARMED: "Farmed",
};

export const sellUnitLabels: Record<string, string> = {
  KG: "/ kg",
  PIECE: "/ piece",
  PACK: "/ pack",
};
