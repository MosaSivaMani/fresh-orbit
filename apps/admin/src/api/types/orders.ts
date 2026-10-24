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
 * List row. `totalPaise` is the estimate; `finalTotalPaise` is set once packed. The row has no `version`
 * (gap G13): bulk actions read it from the order detail first.
 */
export type AdminOrderSummary = Coded<
  Schema<"AdminOrderListItem">,
  { status: OrderStatus; paymentMethod: PaymentMethod }
>;

/** `status` takes one code (the API refuses a list); `slotDate` narrows by the slot's local date. */
export interface AdminOrderListQuery {
  fulfillmentLocationId?: Id;
  status?: string;
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

/** Gross stock reserved per line (`grossQuantity` in the stock unit), from Inventory. */
export type OrderLineStock = Coded<Schema<"OrderLineStockDto">, { grossQuantity: Quantity }>;

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

/** `GET /admin/orders/pick-list` (ORD-R60): gross quantities per variant × preparation for a slot. */
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

export interface DecideComplaintRequest {
  status: "APPROVED" | "REJECTED";
  resolution: ComplaintResolution;
  refundAmountPaise?: number | null;
  note?: string | null;
}
