// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §1 (conventions) and §5 (pricing contract).

export type { Page } from "@fresh/core";

/** `GRAM` for KG variants, `PIECE`, `PACK` (docs/api §1.1). */
export type QuantityUnit = "GRAM" | "PIECE" | "PACK";

export interface Quantity {
  value: number;
  unit: QuantityUnit;
}

/** Variant sell unit; prices are per this unit. */
export type SellUnit = "KG" | "PIECE" | "PACK";

/** docs/api §4.4: `availability.status`. */
export type AvailabilityStatus = "IN_STOCK" | "LOW_STOCK" | "OUT_OF_STOCK" | "NOT_SOLD_HERE";

export interface Availability {
  status: AvailabilityStatus;
  /** Display text from the server; clients branch on `status`, never on this text. */
  message: string | null;
}

/** A refusal reported inside a 200 response (cart line issues, checkout blockers). */
export interface Issue {
  code: string;
  message: string;
}
