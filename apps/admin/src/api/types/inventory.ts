// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.14; shapes match backend Inventory.Application/Stock/StockRequests.cs and
// StockResponses.cs, query names from Inventory.Infrastructure/Endpoints/InventoryEndpoints.cs.

import type { CalendarDate, Id, Instant, Version } from "./common";

export type StockUnit = "GRAM" | "PIECE";
export type BatchStatus = "ACTIVE" | "DEPLETED" | "EXPIRED" | "DISCARDED";
export type MovementKind =
  "RECEIPT" | "SALE" | "WASTE" | "ADJUSTMENT_IN" | "ADJUSTMENT_OUT" | "RETURN" | "COUNT_CORRECTION";
export type MovementReferenceType = "ORDER_ITEM" | "BATCH" | "MANUAL";
export type ReservationStatus = "ACTIVE" | "CONSUMED" | "RELEASED" | "EXPIRED";

export const WASTE_REASONS = [
  "SPOILED",
  "TEMPERATURE_BREACH",
  "DAMAGED",
  "EXPIRED",
  "QC_REJECTED",
] as const;
export type WasteReason = (typeof WASTE_REASONS)[number];

export const ADJUSTMENT_REASONS = [
  "DATA_ENTRY_ERROR",
  "FOUND",
  "LOST",
  "THEFT",
  "TRANSFER",
  "OTHER",
] as const;
export type AdjustmentReason = (typeof ADJUSTMENT_REASONS)[number];

export const ADJUSTMENT_KINDS = ["ADJUSTMENT_IN", "ADJUSTMENT_OUT", "RETURN"] as const;
export type AdjustmentKind = (typeof ADJUSTMENT_KINDS)[number];

/** Reference data `data/reference/inventory/storage-units.json`. */
export const STORAGE_UNITS = ["CHILLER", "FREEZER", "RECEIPT"] as const;

export interface StockQuantity {
  value: number;
  unit: StockUnit;
}

export interface Batch {
  id: Id;
  stockItemId: Id;
  /** If-Match value for discard / adjust / count. */
  stockItemVersion: Version | null;
  fulfillmentLocationId: Id;
  variantId: Id;
  batchCode: string;
  status: BatchStatus;
  supplierName: string | null;
  sourceLandingCenter: string | null;
  caughtOrHarvestedOn: CalendarDate | null;
  receivedAt: Instant;
  receivedQuantity: StockQuantity;
  remainingQuantity: StockQuantity;
  bestBeforeAt: Instant;
  receiptTemperatureC: number | null;
  totalCostPaise: number;
  /** Display only, server-rounded. */
  costPerUnitPaise: number;
}

export interface StockItem {
  id: Id;
  version: Version;
  fulfillmentLocationId: Id;
  variantId: Id;
  stockUnit: StockUnit;
  onHand: StockQuantity;
  reserved: StockQuantity;
  available: StockQuantity;
  lowStockThreshold: number;
  isLowStock: boolean;
  /** ACTIVE batches on the detail; null in lists. */
  batches: Batch[] | null;
}

export interface Movement {
  id: Id;
  stockItemId: Id;
  batchId: Id | null;
  kind: MovementKind;
  quantity: StockQuantity;
  costPaise: number;
  reasonCode: string | null;
  notes: string | null;
  referenceType: MovementReferenceType;
  referenceId: Id | null;
  occurredAt: Instant;
  recordedBy: Id | null;
}

export interface Reservation {
  id: Id;
  stockItemId: Id;
  fulfillmentLocationId: Id;
  variantId: Id;
  orderId: Id;
  orderItemId: Id;
  quantity: StockQuantity;
  status: ReservationStatus;
  expiresAt: Instant | null;
  createdAt: Instant;
}

export interface ColdChainLog {
  id: Id;
  fulfillmentLocationId: Id;
  storageUnit: string;
  temperatureC: number;
  isOutOfRange: boolean;
  recordedAt: Instant;
  recordedBy: Id | null;
}

export interface CreateStockItemRequest {
  fulfillmentLocationId: Id;
  variantId: Id;
  lowStockThreshold: number;
}

export interface SetThresholdRequest {
  lowStockThreshold: number;
}

export interface ReceiveStockRequest {
  fulfillmentLocationId: Id;
  variantId: Id;
  batchCode: string;
  supplierName: string | null;
  sourceLandingCenter: string | null;
  caughtOrHarvestedOn: CalendarDate | null;
  receivedQuantity: StockQuantity;
  totalCostPaise: number;
  bestBeforeAt: Instant;
  receiptTemperatureC: number | null;
}

export interface ReceiptResponse {
  batch: Batch;
  movement: Movement;
  stockItem: StockItem;
  /** Receipt above 4.0 °C: accepted, out-of-range cold-chain log written. */
  temperatureWarning: boolean;
}

export interface StockChangeResponse {
  movements: Movement[];
  stockItem: StockItem;
}

export interface DiscardBatchRequest {
  reasonCode: string;
  notes: string | null;
}

export interface RecordWasteRequest {
  stockItemId: Id;
  /** null = FEFO across active batches. */
  batchId: Id | null;
  quantity: StockQuantity;
  reasonCode: WasteReason;
  notes: string | null;
}

export interface AdjustStockRequest {
  stockItemId: Id;
  kind: AdjustmentKind;
  /** Required for RETURN. */
  batchId: Id | null;
  quantity: StockQuantity;
  reasonCode: AdjustmentReason;
  /** Required for OTHER. */
  notes: string | null;
}

export interface CountStockRequest {
  stockItemId: Id;
  countedQuantity: StockQuantity;
  batchId: Id | null;
  notes: string | null;
}

export interface RecordTemperatureRequest {
  fulfillmentLocationId: Id;
  storageUnit: string;
  temperatureC: number;
  /** Optional; within the last 24 hours, default now. */
  recordedAt: Instant | null;
}
