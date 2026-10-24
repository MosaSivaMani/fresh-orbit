// Delivery (docs/api §4.17) and cash collection (Payments, D14), generated from the backend OpenAPI document.

import type { Coded, Schema } from "./common";

/** 1 = Monday … 7 = Sunday. */
export type IsoDayOfWeek = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export type SlotTemplate = Coded<Schema<"SlotTemplateResponse">, { dayOfWeek: IsoDayOfWeek }>;

export type CreateSlotTemplateRequest = Schema<"CreateSlotTemplateRequest">;

export type UpdateSlotTemplateRequest = Schema<"UpdateSlotTemplateRequest">;

export type SlotStatus = Schema<"SlotStatus">;
export type SlotClosedReason = Schema<"SlotClosedReason">;
export type SlotDisplayStatus = Schema<"SlotDisplayStatus">;

export type DeliverySlot = Schema<"DeliverySlotResponse">;

/** At least one field. */
export type ChangeSlotRequest = Partial<Schema<"ChangeSlotRequest">>;

export type Rider = Schema<"RiderResponse">;

/** `phone` in E.164. */
export type CreateRiderRequest = Schema<"CreateRiderRequest">;

export type UpdateRiderRequest = Schema<"UpdateRiderRequest">;

export type ShipmentStatus = Schema<"ShipmentStatus">;

export type AttemptOutcome = Schema<"AttemptOutcome">;

export type CashMode = Schema<"CashMode">;

export type DeliveryAttempt = Schema<"DeliveryAttemptResponse">;

/** The delivery code itself is never returned to staff. */
export type Shipment = Schema<"ShipmentResponse">;

export type AssignRiderRequest = Schema<"AssignRiderRequest">;

/** −40.0 … 40.0; above 4.0 °C is accepted with `temperatureWarning`. */
export type DispatchShipmentRequest = Schema<"DispatchShipmentRequest">;

export type DeliverShipmentRequest = Schema<"DeliverShipmentRequest">;

export type RecordAttemptRequest = Coded<
  Schema<"RecordAttemptRequest">,
  { outcome: Exclude<AttemptOutcome, "DELIVERED"> }
>;

/** `POST /admin/delivery/shipments/{id}/cash-collected` (Payments, D14); Idempotency-Key required. */
export type CashCollectedRequest = Schema<"CashCollectedRequest">;
