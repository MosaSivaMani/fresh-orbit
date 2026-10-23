// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §4.8, §4.9, §6 and docs/product/ordering.md.

import { type Quantity } from "./common";
import { type PaymentMethod, type SubstitutionPreference } from "./checkout";

export type OrderStatus =
  | "PENDING_PAYMENT"
  | "CONFIRMED"
  | "PREPARING"
  | "PACKED"
  | "OUT_FOR_DELIVERY"
  | "DELIVERED"
  | "DELIVERY_FAILED"
  | "RETURNED"
  | "CANCELLED"
  | "PAYMENT_FAILED";

export interface OrderSlot {
  startAt: string;
  endAt: string;
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

export type OrderItemStatus = "ACTIVE" | "SHORT_SUPPLIED" | "SUBSTITUTED" | "CANCELLED";

export interface OrderItem {
  id: string;
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
}

export interface OrderAmounts {
  itemsPaise: number;
  preparationPaise: number;
  discountPaise: number;
  deliveryFeePaise: number;
  taxPaise: number;
  totalPaise: number;
}

export interface AllowedActions {
  cancel: boolean;
  raiseComplaint: boolean;
  downloadInvoice: boolean;
  reorder: boolean;
}

export interface OrderDetail {
  id: string;
  orderNumber: string;
  version: string;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  placedAt: string;
  slot: OrderSlot;
  address: OrderAddress;
  substitutionPreference: SubstitutionPreference;
  items: OrderItem[];
  estimate: OrderAmounts;
  /** Null until `PACKED`. */
  final: OrderAmounts | null;
  amountDuePaise: number;
  amountPaidPaise: number;
  amountRefundedPaise: number;
  couponCode: string | null;
  substitutions: unknown[];
  payment: { method: PaymentMethod; status: string; nextAction: unknown } | null;
  invoice: { invoiceNumber?: string } | null;
  allowedActions: AllowedActions;
}

/**
 * Item of `GET /orders`. docs/api does not show the list item; these are the order detail header fields
 * (see docs/design/web.md "API gaps").
 */
export interface OrderListItem {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  placedAt: string;
  slot: OrderSlot | null;
  paymentMethod: PaymentMethod;
  /** Final total once packed, otherwise the estimate; chosen by the server. */
  totalPaise: number;
  itemCount?: number;
}

export interface TrackingEvent {
  status: OrderStatus;
  at: string;
}

export interface OrderTracking {
  orderNumber: string;
  status: OrderStatus;
  timeline: TrackingEvent[];
  slot: OrderSlot;
  shipment: {
    status: string;
    riderName: string | null;
    riderPhone: string | null;
    dispatchedAt: string | null;
    coldBoxUsed: boolean | null;
    attempts: { attemptNo?: number; outcome: string; attemptedAt?: string }[];
  } | null;
  /** Shown only while `OUT_FOR_DELIVERY` (ORD-R24). */
  deliveryCode: string | null;
  amountDuePaise: number;
}

export type CancelReasonCode = "CHANGED_MIND" | "WRONG_ADDRESS" | "SLOT_ISSUE" | "OTHER";

export type ComplaintKind = "MISSING" | "DAMAGED" | "QUALITY" | "WRONG_ITEM" | "LATE";

export interface Complaint {
  id: string;
  orderItemId: string | null;
  kind: ComplaintKind;
  description: string;
  status: "OPEN" | "APPROVED" | "REJECTED" | "RESOLVED";
  resolution: "REFUND" | "REPLACEMENT" | "NONE" | null;
  createdAt?: string;
}

export interface InvoiceResponse {
  invoiceNumber: string;
  pdfUrl: string | null;
}
