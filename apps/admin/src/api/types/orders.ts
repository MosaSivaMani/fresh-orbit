// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.15 (admin orders, complaints), §4.8 order detail shape, §6 status machine;
// docs/product/ordering.md (ORD-R25…R60) and docs/database/README.md (ordering tables). The Ordering module was
// not on `main` when these were written, so fields marked "proposed" are the admin's reading of the spec and
// must be checked against the generated client after Phase 3.

import type { CalendarDate, Id, Instant, Quantity, Version } from "./common";

export type OrderStatus =
  | "PENDING_PAYMENT"
  | "PAYMENT_FAILED"
  | "CONFIRMED"
  | "PREPARING"
  | "PACKED"
  | "OUT_FOR_DELIVERY"
  | "DELIVERED"
  | "DELIVERY_FAILED"
  | "RETURNED"
  | "CANCELLED";

export type PaymentMethod = "COD" | "UPI_ON_DELIVERY" | "ONLINE";
export type SubstitutionPreference = "CALL_ME" | "AUTO_SIMILAR" | "NONE";
export type OrderItemStatus = "ACTIVE" | "SHORT_SUPPLIED" | "SUBSTITUTED" | "CANCELLED";

export const STAFF_CANCEL_REASONS = [
  "OUT_OF_STOCK",
  "CUSTOMER_REQUEST",
  "ADDRESS_ISSUE",
  "QUALITY_ISSUE",
  "OTHER",
] as const;
export type StaffCancelReason = (typeof STAFF_CANCEL_REASONS)[number];

/** List row (proposed: docs/api §4.15 names the filters, not the item fields). */
export interface AdminOrderSummary {
  id: Id;
  orderNumber: string;
  status: OrderStatus;
  placedAt: Instant;
  fulfillmentLocationId: Id;
  customerName: string | null;
  customerPhone: string | null;
  slot: { id: Id; startAt: Instant; endAt: Instant } | null;
  itemCount: number;
  paymentMethod: PaymentMethod;
  /** Estimated total until packed, final total after. */
  totalPaise: number;
  amountDuePaise: number;
  version: Version;
}

export interface AdminOrderListQuery {
  fulfillmentLocationId?: Id;
  status?: string | string[];
  slotDate?: CalendarDate;
  slotId?: Id;
  paymentMethod?: string;
  q?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
}

export interface OrderAddress {
  recipientName: string;
  phone: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  locality: string;
  cityName: string;
  stateCode: string;
  pincode: string;
}

export interface OrderItem {
  id: Id;
  lineNo: number;
  productName: string;
  variantName: string;
  preparationName: string;
  orderedQuantity: Quantity;
  maxChargeableQuantity: Quantity;
  packedQuantity: Quantity | null;
  chargedQuantity: Quantity | null;
  unitPricePaise: number;
  preparationChargePaise: number;
  estimatedTotalPaise: number;
  finalTotalPaise: number | null;
  status: OrderItemStatus;
  /** Admin detail: gross stock reserved for the line (`ceil(net × 10000 / yieldBp)`), from Inventory. */
  grossReservedQuantity?: Quantity | null;
  /** Admin detail: batches the line was packed from (after pack). */
  batches?: { batchId: Id; batchCode: string; quantity: Quantity }[];
}

export interface OrderTotals {
  itemsPaise: number;
  preparationPaise: number;
  discountPaise: number;
  deliveryFeePaise: number;
  taxPaise: number;
  totalPaise: number;
}

export interface Substitution {
  id: Id;
  orderItemId: Id;
  proposedVariantId: Id;
  proposedName: string;
  proposedQuantity: Quantity;
  priceDifferencePaise: number;
  status: "PROPOSED" | "ACCEPTED" | "REJECTED" | "EXPIRED";
  expiresAt: Instant | null;
  respondedAt: Instant | null;
}

export interface OrderStatusChange {
  fromStatus: OrderStatus | null;
  toStatus: OrderStatus;
  actorType: "CUSTOMER" | "STAFF" | "SYSTEM";
  actorName: string | null;
  reason: string | null;
  changedAt: Instant;
}

export interface OrderAdjustment {
  id: Id;
  orderItemId: Id | null;
  kind: "WEIGHT" | "SHORT_SUPPLY" | "SUBSTITUTION" | "GOODWILL";
  amountPaise: number;
  reason: string | null;
  createdAt: Instant;
}

/** `GET /admin/orders/{id}` = customer order detail (§4.8) + admin extras (history, adjustments). */
export interface AdminOrderDetail {
  id: Id;
  orderNumber: string;
  version: Version;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  placedAt: Instant;
  fulfillmentLocationId?: Id;
  customerId?: Id;
  customerNotes?: string | null;
  slot: { id?: Id; startAt: Instant; endAt: Instant };
  address: OrderAddress;
  substitutionPreference: SubstitutionPreference;
  items: OrderItem[];
  estimate: OrderTotals;
  final: OrderTotals | null;
  amountDuePaise: number;
  amountPaidPaise: number;
  amountRefundedPaise: number;
  couponCode: string | null;
  substitutions: Substitution[];
  payment: { method: PaymentMethod; status: string; nextAction: unknown } | null;
  invoice: { invoiceNumber: string; issuedAt: Instant } | null;
  history?: OrderStatusChange[];
  adjustments?: OrderAdjustment[];
}

export interface PackRequest {
  lines: { orderItemId: Id; packedQuantity: Quantity }[];
  coldBoxUsed: boolean;
}

export interface PackResponse {
  orderId: Id;
  status: OrderStatus;
  version: Version;
  lines: {
    orderItemId: Id;
    packed: Quantity;
    charged: Quantity;
    capped: boolean;
    finalTotalPaise: number;
    adjustmentPaise: number;
    status: OrderItemStatus;
  }[];
  finalTotalPaise: number;
  amountDuePaise: number;
  refundDuePaise: number;
}

export interface CancelOrderRequest {
  reasonCode: StaffCancelReason;
  notes?: string | null;
}

/** Proposed body (ORD-R33): the substitute variant, its preparation and quantity. */
export interface ProposeSubstitutionRequest {
  variantId: Id;
  preparationCode: string;
  quantity: Quantity;
}

/** Proposed body for `record-response` (answer taken by phone). */
export interface RecordSubstitutionResponseRequest {
  accepted: boolean;
}

export interface GoodwillRequest {
  /** Negative paise (ORD-R58). */
  amountPaise: number;
  reason: string;
  orderItemId?: Id | null;
}

/** `GET /admin/orders/pick-list` (ORD-R60): gross quantities per variant × preparation for a slot. */
export interface PickList {
  fulfillmentLocationId: Id;
  slotId: Id;
  slot?: { startAt: Instant; endAt: Instant } | null;
  orderCount?: number;
  items: PickListItem[];
}

export interface PickListItem {
  variantId: Id;
  sku: string | null;
  productName: string;
  variantName: string;
  preparationCode: string;
  preparationName: string;
  grossQuantity: Quantity;
  orderCount: number;
}

export type ComplaintKind = "MISSING" | "DAMAGED" | "QUALITY" | "WRONG_ITEM" | "LATE";
export type ComplaintStatus = "OPEN" | "APPROVED" | "REJECTED" | "RESOLVED";
export type ComplaintResolution = "REFUND" | "REPLACEMENT" | "NONE";

export interface ComplaintSummary {
  id: Id;
  orderId: Id;
  orderNumber: string;
  orderItemId: Id | null;
  productName: string | null;
  kind: ComplaintKind;
  status: ComplaintStatus;
  description: string;
  raisedAt: Instant;
  fulfillmentLocationId: Id;
  version: Version;
}

export interface ComplaintDetail extends ComplaintSummary {
  resolution: ComplaintResolution | null;
  refundAmountPaise: number | null;
  /** Final line total (or order total for LATE) — the server's upper bound for a refund. */
  maxRefundPaise?: number | null;
  attachments: { id: Id; url: string; contentType: string }[];
  decidedBy: string | null;
  decidedAt: Instant | null;
  resolvedAt: Instant | null;
}

export interface DecideComplaintRequest {
  status: "APPROVED" | "REJECTED";
  resolution: ComplaintResolution;
  refundAmountPaise?: number | null;
  notes?: string | null;
}
