// Admin orders, pick list, packing, substitutions and complaints (docs/api §4.15), generated from the backend
// OpenAPI document. Ordering writes most codes as plain strings; `Coded` narrows them to the documented codes.

import type { CalendarDate, Coded, Id, Quantity, Schema } from "./common";

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

export type PaymentMethod = Schema<"PaymentsPaymentMethod">;
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

/**
 * List row. `totalPaise` is the estimate; `finalTotalPaise` is set once packed. `version` is the order's row
 * version (G13), quoted by bulk actions.
 */
export type AdminOrderSummary = Coded<
  Schema<"AdminOrderListItem">,
  { status: OrderStatus; paymentMethod: PaymentMethod }
>;

/** `status` takes one or more codes (G14); `slotDate` narrows by the slot's local date. */
export interface AdminOrderListQuery {
  fulfillmentLocationId?: Id;
  status?: string | string[];
  slotDate?: CalendarDate;
  slotId?: Id;
  paymentMethod?: string;
  q?: string;
  page?: number;
  pageSize?: number;
}

export type OrderAddress = Schema<"OrderAddressDto">;

/** Order quantities: the spec types `unit` as a string; the server writes GRAM / PIECE / PACK. */
export type OrderItem = Coded<
  Schema<"OrderItemDto">,
  {
    status: OrderItemStatus;
    orderedQuantity: Quantity;
    maxChargeableQuantity: Quantity;
    packedQuantity: Quantity | null;
    chargedQuantity: Quantity | null;
  }
>;

export type OrderTotals = Schema<"AmountsDto">;

export type Substitution = Coded<
  Schema<"SubstitutionDto">,
  { status: "PROPOSED" | "ACCEPTED" | "REJECTED" | "EXPIRED"; proposedQuantity: Quantity }
>;

export type OrderStatusChange = Coded<
  Schema<"OrderHistoryDto">,
  { from: OrderStatus | null; to: OrderStatus; actorType: "CUSTOMER" | "STAFF" | "SYSTEM" }
>;

export type OrderAdjustment = Coded<
  Schema<"OrderAdjustmentDto">,
  { kind: "WEIGHT" | "SHORT_SUPPLY" | "SUBSTITUTION" | "GOODWILL" }
>;

/** One batch a line's stock was taken from when packed (G17). */
export type OrderLineBatch = Coded<Schema<"OrderLineBatchDto">, { quantity: Quantity }>;

/** Gross stock reserved per line (`grossQuantity` in the stock unit) and its batches, from Inventory. */
export type OrderLineStock = Coded<
  Schema<"OrderLineStockDto">,
  { grossQuantity: Quantity; batches: OrderLineBatch[] }
>;

export type CustomerOrderDetail = Coded<
  Schema<"OrderDetailResponse">,
  {
    status: OrderStatus;
    paymentMethod: PaymentMethod;
    substitutionPreference: SubstitutionPreference;
    items: OrderItem[];
    substitutions: Substitution[];
  }
>;

/** `GET /admin/orders/{id}` as sent: the customer order under `order`, staff extras beside it. */
export type AdminOrderDetailResponse = Omit<
  Schema<"AdminOrderDetailResponse">,
  "order" | "lines" | "history" | "adjustments"
> & {
  order: CustomerOrderDetail;
  lines: OrderLineStock[];
  history: OrderStatusChange[];
  adjustments: OrderAdjustment[];
};

/** The admin screens read one flat object: the customer order fields plus the staff extras. */
export type AdminOrderDetail = CustomerOrderDetail & Omit<AdminOrderDetailResponse, "order">;

export function flattenAdminOrder(response: AdminOrderDetailResponse): AdminOrderDetail {
  const { order, ...extras } = response;
  return { ...order, ...extras };
}

export type PackRequest = Omit<Schema<"PackOrderRequest">, "lines"> & {
  lines: { orderItemId: Id; packedQuantity: Quantity }[];
};

export type PackResponse = Coded<
  Schema<"PackResponse">,
  {
    status: OrderStatus;
    lines: Coded<
      Schema<"PackedLineDto">,
      { packed: Quantity; charged: Quantity; status: OrderItemStatus }
    >[];
  }
>;

export interface CancelOrderRequest {
  reasonCode: StaffCancelReason;
  note?: string | null;
}

/** ORD-R33: the substitute variant, its preparation method (id) and quantity. */
export type ProposeSubstitutionRequest = Coded<
  Schema<"ProposeSubstitutionRequest">,
  { preparationMethodId: Id | null; quantity: Quantity }
>;

/** Answer taken by phone. */
export interface RecordSubstitutionResponseRequest {
  answer: "ACCEPTED" | "REJECTED";
}

/** `amountPaise` is negative (ORD-R58). */
export type GoodwillRequest = Schema<"GoodwillRequest">;

/** `grossQuantity` is in `stockUnit` (GRAM or PIECE). */
export type PickListItem = Coded<Schema<"PickListLine">, { stockUnit: "GRAM" | "PIECE" }>;

/**
 * `GET /admin/orders/pick-list` (ORD-R60): gross quantities per variant × preparation for a slot, with the slot's
 * times (null for an unknown slot) and the number of orders (G18).
 */
export type PickList = Coded<Schema<"PickListResponse">, { lines: PickListItem[] }>;

export type ComplaintKind = "MISSING" | "DAMAGED" | "QUALITY" | "WRONG_ITEM" | "LATE";
export type ComplaintStatus = "OPEN" | "APPROVED" | "REJECTED" | "RESOLVED";
export type ComplaintResolution = "REFUND" | "REPLACEMENT" | "NONE";

/** List and detail are one shape (`ComplaintResponse`). */
export type ComplaintDetail = Coded<
  Schema<"ComplaintResponse">,
  { kind: ComplaintKind; status: ComplaintStatus; resolution: ComplaintResolution | null }
>;
export type ComplaintSummary = ComplaintDetail;

/**
 * The complained-about line as the server names it ("Seer fish · Steaks · Curry cut", G15), "One item" when the
 * server has no name for it, or "Whole order".
 */
export function complaintItemLabel(complaint: ComplaintDetail): string {
  if (!complaint.orderItemId) return "Whole order";
  const { productName, variantName, preparationName } = complaint;
  const named = [productName, variantName, preparationName].filter(Boolean).join(" · ");
  return named || "One item";
}

export interface DecideComplaintRequest {
  status: "APPROVED" | "REJECTED";
  resolution: ComplaintResolution;
  refundAmountPaise?: number | null;
  note?: string | null;
}

/** `GET /admin/orders/{id}/invoice`: the GST tax invoice issued at delivery (AD-5); amounts are the server's. */
export type Invoice = Schema<"InvoiceResponse">;
