// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.17 (bodies and responses table); shapes match backend
// Delivery.Application (Reads/DeliveryReads.cs, *Service.cs) and Payments CashCollectedRequest.

import type { CalendarDate, Id, Instant, TimeOfDay, Version } from "./common";

/** 1 = Monday … 7 = Sunday. */
export type IsoDayOfWeek = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export interface SlotTemplate {
  id: Id;
  fulfillmentLocationId: Id;
  dayOfWeek: IsoDayOfWeek;
  startTime: TimeOfDay;
  endTime: TimeOfDay;
  capacity: number;
  cutoffMinutes: number;
  isActive: boolean;
  version: Version;
}

export interface CreateSlotTemplateRequest {
  fulfillmentLocationId: Id;
  dayOfWeek: number;
  startTime: TimeOfDay;
  endTime: TimeOfDay;
  capacity: number;
  cutoffMinutes: number;
}

export interface UpdateSlotTemplateRequest {
  dayOfWeek: number;
  startTime: TimeOfDay;
  endTime: TimeOfDay;
  capacity: number;
  cutoffMinutes: number;
  isActive: boolean;
}

export type SlotStatus = "OPEN" | "CLOSED";
export type SlotClosedReason = "MANUAL" | "CLOSURE" | "TEMPLATE_INACTIVE";
export type SlotDisplayStatus = "AVAILABLE" | "FULL" | "CUTOFF_PASSED" | "CLOSED";

export interface DeliverySlot {
  id: Id;
  fulfillmentLocationId: Id;
  slotTemplateId: Id | null;
  slotDate: CalendarDate;
  startAt: Instant;
  endAt: Instant;
  cutoffAt: Instant;
  capacity: number;
  bookedCount: number;
  status: SlotStatus;
  closedReason: SlotClosedReason | null;
  displayStatus: SlotDisplayStatus;
  version: Version;
}

/** At least one field. */
export interface ChangeSlotRequest {
  capacity?: number;
  status?: SlotStatus;
}

export interface Rider {
  id: Id;
  fulfillmentLocationId: Id;
  fullName: string;
  phone: string;
  isActive: boolean;
  version: Version;
}

export interface CreateRiderRequest {
  fulfillmentLocationId: Id;
  fullName: string;
  /** E.164. */
  phone: string;
}

export interface UpdateRiderRequest {
  fullName: string;
  phone: string;
  isActive: boolean;
}

export type ShipmentStatus =
  "PENDING" | "ASSIGNED" | "OUT_FOR_DELIVERY" | "DELIVERED" | "FAILED" | "RETURNED" | "CANCELLED";

export type AttemptOutcome =
  "DELIVERED" | "CUSTOMER_UNAVAILABLE" | "ADDRESS_NOT_FOUND" | "REFUSED" | "OTHER";

export type CashMode = "CASH" | "UPI";

export interface DeliveryAttempt {
  attemptNo: number;
  outcome: AttemptOutcome;
  notes: string | null;
  attemptedAt: Instant;
}

/** The delivery code itself is never returned to staff. */
export interface Shipment {
  id: Id;
  orderId: Id;
  fulfillmentLocationId: Id;
  deliverySlotId: Id;
  riderId: Id | null;
  status: ShipmentStatus;
  placedAt: Instant;
  packedAt: Instant | null;
  coldBoxUsed: boolean;
  dispatchTemperatureC: number | null;
  temperatureWarning: boolean;
  dispatchedAt: Instant | null;
  deliveredAt: Instant | null;
  returnedAt: Instant | null;
  cancelledAt: Instant | null;
  deliveryCodeFailures: number;
  deliveryCodeLocked: boolean;
  cashCollectedPaise: number | null;
  cashMode: CashMode | null;
  upiReference: string | null;
  cashCollectedAt: Instant | null;
  attempts: DeliveryAttempt[];
  version: Version;
}

export interface AssignRiderRequest {
  riderId: Id;
}

export interface DispatchShipmentRequest {
  /** −40.0 … 40.0; above 4.0 °C is accepted with `temperatureWarning`. */
  dispatchTemperatureC: number | null;
}

export interface DeliverShipmentRequest {
  deliveryCode: string;
  cashCollectedPaise: number | null;
  cashMode: CashMode | null;
  upiReference: string | null;
}

export interface RecordAttemptRequest {
  outcome: Exclude<AttemptOutcome, "DELIVERED">;
  notes: string | null;
}

/** `POST /admin/delivery/shipments/{id}/cash-collected` (Payments, D14); Idempotency-Key required. */
export interface CashCollectedRequest {
  amountPaise: number;
  mode: CashMode;
  upiReference: string | null;
}
