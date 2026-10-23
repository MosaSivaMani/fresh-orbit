import { type SelectOption, type StatusMap } from "@fresh/ui";

// Status labels and tones, defined once per domain (docs/design §6.8). Codes come from the API; labels are UI
// copy. Unknown codes fall back to a humanised label in StatusBadge.

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

export const ORDER_ITEM_STATUS: StatusMap = {
  ACTIVE: { label: "Active", tone: "neutral" },
  SHORT_SUPPLIED: { label: "Short supplied", tone: "warning" },
  SUBSTITUTED: { label: "Substituted", tone: "info" },
  CANCELLED: { label: "Cancelled", tone: "neutral" },
};

export const SUBSTITUTION_STATUS: StatusMap = {
  PROPOSED: { label: "Waiting for customer", tone: "warning" },
  ACCEPTED: { label: "Accepted", tone: "success" },
  REJECTED: { label: "Rejected", tone: "danger" },
  EXPIRED: { label: "Expired", tone: "neutral" },
};

export const COMPLAINT_STATUS: StatusMap = {
  OPEN: { label: "Open", tone: "warning" },
  APPROVED: { label: "Approved", tone: "info" },
  REJECTED: { label: "Rejected", tone: "neutral" },
  RESOLVED: { label: "Resolved", tone: "success" },
};

export const SHIPMENT_STATUS: StatusMap = {
  PENDING: { label: "Waiting for rider", tone: "neutral" },
  ASSIGNED: { label: "Rider assigned", tone: "accent" },
  OUT_FOR_DELIVERY: { label: "Out for delivery", tone: "highlight" },
  DELIVERED: { label: "Delivered", tone: "success" },
  FAILED: { label: "Attempt failed", tone: "danger" },
  RETURNED: { label: "Returned", tone: "neutral" },
  CANCELLED: { label: "Cancelled", tone: "neutral" },
};

export const SLOT_STATUS: StatusMap = {
  OPEN: { label: "Open", tone: "success" },
  CLOSED: { label: "Closed", tone: "neutral" },
  AVAILABLE: { label: "Available", tone: "success" },
  FULL: { label: "Full", tone: "warning" },
  CUTOFF_PASSED: { label: "Cut-off passed", tone: "neutral" },
};

export const PAYMENT_STATUS: StatusMap = {
  PENDING: { label: "Pending", tone: "warning" },
  AUTHORIZED: { label: "Authorised", tone: "info" },
  CAPTURED: { label: "Captured", tone: "success" },
  COLLECTED: { label: "Collected", tone: "success" },
  FAILED: { label: "Failed", tone: "danger" },
  CANCELLED: { label: "Cancelled", tone: "neutral" },
};

export const REFUND_STATUS: StatusMap = {
  REQUESTED: { label: "Requested", tone: "warning" },
  APPROVED: { label: "Approved", tone: "info" },
  PROCESSING: { label: "Processing", tone: "info" },
  SUCCEEDED: { label: "Refunded", tone: "success" },
  FAILED: { label: "Failed", tone: "danger" },
};

export const SETTLEMENT_STATUS: StatusMap = {
  OPEN: { label: "Open", tone: "warning" },
  DISPUTED: { label: "Disputed", tone: "danger" },
  SETTLED: { label: "Settled", tone: "success" },
};

export const ISSUE_STATUS: StatusMap = {
  OPEN: { label: "Open", tone: "warning" },
  AUTO_RESOLVED: { label: "Auto-resolved", tone: "neutral" },
  RESOLVED: { label: "Resolved", tone: "success" },
};

export const WEBHOOK_STATUS: StatusMap = {
  RECEIVED: { label: "Received", tone: "info" },
  PROCESSED: { label: "Processed", tone: "success" },
  IGNORED: { label: "Ignored", tone: "neutral" },
  ERROR: { label: "Error", tone: "danger" },
  REJECTED: { label: "Rejected", tone: "danger" },
};

export const CITY_STATUS: StatusMap = {
  DRAFT: { label: "Draft", tone: "neutral" },
  ACTIVE: { label: "Active", tone: "success" },
  PAUSED: { label: "Paused", tone: "warning" },
  RETIRED: { label: "Retired", tone: "neutral" },
};

export const LOCATION_STATUS: StatusMap = {
  DRAFT: { label: "Draft", tone: "neutral" },
  ACTIVE: { label: "Active", tone: "success" },
  PAUSED: { label: "Paused", tone: "warning" },
};

export const PRODUCT_STATUS: StatusMap = {
  DRAFT: { label: "Draft", tone: "neutral" },
  ACTIVE: { label: "Published", tone: "success" },
  ARCHIVED: { label: "Archived", tone: "neutral" },
};

export const BATCH_STATUS: StatusMap = {
  ACTIVE: { label: "Active", tone: "success" },
  DEPLETED: { label: "Depleted", tone: "neutral" },
  EXPIRED: { label: "Expired", tone: "danger" },
  DISCARDED: { label: "Discarded", tone: "neutral" },
};

export const CUSTOMER_STATUS: StatusMap = {
  ACTIVE: { label: "Active", tone: "success" },
  DISABLED: { label: "Blocked", tone: "danger" },
  DELETED: { label: "Deleted", tone: "neutral" },
  LOCKED: { label: "Locked", tone: "warning" },
};

export const COUPON_STATUS: StatusMap = {
  ACTIVE: { label: "Active", tone: "success" },
  PAUSED: { label: "Paused", tone: "warning" },
  EXPIRED: { label: "Expired", tone: "neutral" },
};

export const ACTIVE_FLAG: StatusMap = {
  true: { label: "Active", tone: "success" },
  false: { label: "Inactive", tone: "neutral" },
};

/** Select options from a status map, in the map's order. */
export function statusOptions(map: StatusMap): SelectOption[] {
  return Object.entries(map).map(([value, style]) => ({ value, label: style.label }));
}
