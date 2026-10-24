// Orders, tracking, complaints and invoices (docs/api §4.8, §4.9), generated from the backend OpenAPI document
// (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";
import { type PaymentMethod, type SubstitutionPreference } from "./checkout";
import { type Coded } from "./common";

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

export type OrderSlot = Schema<"SlotDto">;

export type OrderAddress = Schema<"OrderAddressDto">;

export type OrderItemStatus = "ACTIVE" | "SHORT_SUPPLIED" | "SUBSTITUTED" | "CANCELLED";

export type OrderItem = Coded<Schema<"OrderItemDto">, { status: OrderItemStatus }>;

export type OrderAmounts = Schema<"AmountsDto">;

export type AllowedActions = Schema<"AllowedActionsDto">;

export type OrderDetail = Coded<
  Schema<"OrderDetailResponse">,
  {
    status: OrderStatus;
    paymentMethod: PaymentMethod;
    substitutionPreference: SubstitutionPreference;
    items: OrderItem[];
  }
>;

/** Item of `GET /orders`: `totalPaise` is the final total once packed, otherwise the estimate (server's choice). */
export type OrderListItem = Coded<
  Schema<"OrderListItem">,
  { status: OrderStatus; paymentMethod: PaymentMethod }
>;

export type TrackingEvent = Coded<Schema<"TimelineEntryDto">, { status: OrderStatus }>;

export type OrderTracking = Coded<
  Schema<"OrderTrackingResponse">,
  { status: OrderStatus; timeline: TrackingEvent[] }
>;

export type CancelReasonCode = "CHANGED_MIND" | "WRONG_ADDRESS" | "SLOT_ISSUE" | "OTHER";

export type ComplaintKind = "MISSING" | "DAMAGED" | "QUALITY" | "WRONG_ITEM" | "LATE";

export type Complaint = Coded<
  Schema<"ComplaintResponse">,
  {
    kind: ComplaintKind;
    status: "OPEN" | "APPROVED" | "REJECTED" | "RESOLVED";
    resolution: "REFUND" | "REPLACEMENT" | "NONE" | null;
  }
>;

export type RaiseComplaintRequest = Coded<Schema<"RaiseComplaintRequest">, { kind: ComplaintKind }>;

export type InvoiceResponse = Schema<"InvoiceResponse">;
