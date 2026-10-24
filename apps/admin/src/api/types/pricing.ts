// Pricing (docs/api §4.12), generated from the backend OpenAPI document. The bulk upload body is read by hand on
// the server (JSON or CSV), so it is not in the spec and is written here from `BulkPriceReader.cs`.

import type { Id, Instant, Schema } from "./common";

export type ChargeBasis = Schema<"ChargeBasis">;
export type BulkRowKind = Schema<"BulkRowKind">;

export type PricePeriod = Schema<"PricePeriodResponse">;

/** `GET /admin/pricing/prices` row. `missing` = no current price at the location (not for sale there). */
export type PriceListItem = Schema<"PriceListItem">;

export interface PriceListQuery {
  fulfillmentLocationId: Id;
  variantId?: Id;
  q?: string;
  missingOnly?: boolean;
  page?: number;
  pageSize?: number;
}

/**
 * Price schedule of one variant at one location. `version` null → send `If-None-Match: *`; `unchanged`: nothing
 * was written; `largeChange`: more than 50 % up or down.
 */
export type PriceSchedule = Schema<"PriceScheduleResponse">;

/** `validFrom` omitted = now. */
export type SetPriceRequest = Schema<"SetPriceRequest">;

export type ChargePeriod = Schema<"ChargePeriodResponse">;

export type ChargeSchedule = Schema<"ChargeScheduleResponse">;

export type SetChargeRequest = Schema<"SetChargeRequest">;

export type TaxRulePeriod = Schema<"TaxRulePeriodResponse">;

export type TaxCategoryRules = Schema<"TaxCategoryRulesResponse">;

/** `rateBp` 0–2800. */
export type ScheduleTaxRuleRequest = Schema<"ScheduleTaxRuleRequest">;

export interface BulkPriceRow {
  kind: BulkRowKind;
  locationCode: string;
  sku: string;
  unitPricePaise?: number | null;
  compareAtPaise?: number | null;
  preparationCode?: string | null;
  chargePaise?: number | null;
  chargeBasis?: ChargeBasis | null;
  validFrom?: Instant | null;
}

export interface BulkPriceRequest {
  validFrom?: Instant | null;
  replaceFuture?: boolean | null;
  rows: BulkPriceRow[];
}

/** CSV header accepted by `POST /admin/pricing/prices/bulk` with `Content-Type: text/csv`. */
export const BULK_CSV_COLUMNS = [
  "kind",
  "location_code",
  "sku",
  "unit_price_paise",
  "compare_at_paise",
  "preparation_code",
  "charge_paise",
  "charge_basis",
  "valid_from",
] as const;

export type BulkValues = Schema<"BulkValues">;

/** `changeBp` is server-computed against the current value; null without a current value. */
export type BulkRowResult = Schema<"BulkRowResult">;

/** All-or-nothing: an invalid row refuses the whole upload with 422 and `errors["rows[i].field"]`. */
export type BulkPriceResponse = Schema<"BulkPriceResponse">;
