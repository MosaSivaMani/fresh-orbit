// Inventory (docs/api §4.14), generated from the backend OpenAPI document. The reason and kind lists below are
// codes the server checks; the spec types those request fields as plain strings.

import type { Coded, Schema } from "./common";

export type StockUnit = Schema<"InventoryStockUnit">;
export type BatchStatus = Schema<"BatchStatus">;
export type MovementKind = Schema<"MovementKind">;
export type MovementReferenceType = Schema<"MovementReferenceType">;
export type ReservationStatus = Schema<"ReservationStatus">;

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

export type StockQuantity = Schema<"InventoryQuantityDto">;

/** `stockItemVersion` is the If-Match value for discard / adjust / count; `costPerUnitPaise` is display only. */
export type Batch = Schema<"BatchResponse">;

/** `batches`: ACTIVE batches on the detail; null in lists. */
export type StockItem = Schema<"StockItemResponse">;

export type Movement = Schema<"MovementResponse">;

export type Reservation = Schema<"ReservationResponse">;

export type ColdChainLog = Schema<"ColdChainLogResponse">;

export type CreateStockItemRequest = Schema<"CreateStockItemRequest">;

export type SetThresholdRequest = Schema<"SetThresholdRequest">;

export type ReceiveStockRequest = Coded<
  Schema<"ReceiveStockRequest">,
  { receivedQuantity: StockQuantity }
>;

/** `temperatureWarning`: receipt above 4.0 °C, accepted, out-of-range cold-chain log written. */
export type ReceiptResponse = Schema<"ReceiptResponse">;

export type StockChangeResponse = Schema<"StockChangeResponse">;

export type DiscardBatchRequest = Schema<"DiscardBatchRequest">;

/** `batchId: null` = FEFO across active batches. */
export type RecordWasteRequest = Coded<
  Schema<"RecordWasteRequest">,
  { quantity: StockQuantity; reasonCode: WasteReason }
>;

/** `batchId` is required for RETURN, `notes` for OTHER. */
export type AdjustStockRequest = Coded<
  Schema<"AdjustStockRequest">,
  { kind: AdjustmentKind; quantity: StockQuantity; reasonCode: AdjustmentReason }
>;

export type CountStockRequest = Coded<
  Schema<"CountStockRequest">,
  { countedQuantity: StockQuantity }
>;

/** `recordedAt`: optional, within the last 24 hours, default now. */
export type RecordTemperatureRequest = Schema<"RecordTemperatureRequest">;
