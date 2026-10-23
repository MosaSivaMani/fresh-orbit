// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Shared shapes from docs/api/README.md §1 (conventions).

export type { Page } from "@fresh/core";

/** UUIDv7 string. */
export type Id = string;
/** ISO-8601 UTC instant with `Z`. */
export type Instant = string;
/** Calendar date `YYYY-MM-DD`. */
export type CalendarDate = string;
/** Time of day `HH:mm` in the location's time zone. */
export type TimeOfDay = string;
/** Row version used for `If-Match` (docs/api §1.7). */
export type Version = string;

export type QuantityUnit = "GRAM" | "PIECE" | "PACK";

/** `{ "value": 500, "unit": "GRAM" }` (docs/api §1.1). */
export interface Quantity {
  value: number;
  unit: QuantityUnit;
}

/** Paging + sorting query common to every list (docs/api §1.5). */
export interface ListQuery {
  page?: number;
  pageSize?: number;
  /** `-placedAt,orderNumber`. */
  sort?: string;
  q?: string;
}
