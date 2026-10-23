// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.12; shapes match backend Pricing.Application/Contracts/PricingContracts.cs and
// Pricing.Infrastructure/Endpoints (PricingEndpoints.cs, BulkPriceReader.cs).

import type { Id, Instant, Version } from "./common";

export type ChargeBasis = "PER_KG" | "PER_ITEM" | "FLAT";
export type BulkRowKind = "PRICE" | "PREP_CHARGE";

export interface PricePeriod {
  id: Id;
  unitPricePaise: number;
  compareAtPaise: number | null;
  validFrom: Instant;
  validTo: Instant | null;
}

/** `GET /admin/pricing/prices` row. `missing` = no current price at the location (not for sale there). */
export interface PriceListItem {
  fulfillmentLocationId: Id;
  variantId: Id;
  productId: Id;
  sku: string;
  productName: string;
  variantName: string;
  /** KG / PIECE / PACK. */
  priceUnit: string;
  current: PricePeriod | null;
  next: PricePeriod | null;
  missing: boolean;
}

export interface PriceListQuery {
  fulfillmentLocationId: Id;
  variantId?: Id;
  q?: string;
  missingOnly?: boolean;
  page?: number;
  pageSize?: number;
}

/** Price schedule of one variant at one location. `version` null → send `If-None-Match: *`. */
export interface PriceSchedule {
  fulfillmentLocationId: Id;
  variantId: Id;
  version: Version | null;
  current: PricePeriod | null;
  next: PricePeriod | null;
  history: PricePeriod[];
  /** The request matched the current price; nothing was written. */
  unchanged: boolean;
  /** More than 50 % up or down against the previous price. */
  largeChange: boolean;
}

export interface SetPriceRequest {
  unitPricePaise: number;
  compareAtPaise: number | null;
  /** Omitted = now. */
  validFrom: Instant | null;
  reason: string | null;
  replaceFuture: boolean | null;
}

export interface ChargePeriod {
  id: Id;
  chargePaise: number;
  chargeBasis: ChargeBasis;
  validFrom: Instant;
  validTo: Instant | null;
}

export interface ChargeSchedule {
  fulfillmentLocationId: Id;
  variantId: Id;
  preparationCode: string;
  version: Version | null;
  current: ChargePeriod | null;
  next: ChargePeriod | null;
  history: ChargePeriod[];
  unchanged: boolean;
}

export interface SetChargeRequest {
  chargePaise: number;
  chargeBasis: ChargeBasis;
  validFrom: Instant | null;
  replaceFuture: boolean | null;
}

export interface TaxRulePeriod {
  id: Id;
  rateBp: number;
  validFrom: Instant;
  validTo: Instant | null;
}

export interface TaxCategoryRules {
  taxCategoryCode: string;
  name: string;
  version: Version | null;
  current: TaxRulePeriod | null;
  next: TaxRulePeriod | null;
  history: TaxRulePeriod[];
  unchanged: boolean;
}

export interface ScheduleTaxRuleRequest {
  taxCategoryCode: string;
  /** 0–2800. */
  rateBp: number;
  validFrom: Instant | null;
  replaceFuture: boolean | null;
}

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

export interface BulkValues {
  unitPricePaise: number | null;
  compareAtPaise: number | null;
  chargePaise: number | null;
  chargeBasis: ChargeBasis | null;
}

export interface BulkRowResult {
  index: number;
  kind: BulkRowKind;
  locationCode: string;
  sku: string;
  preparationCode: string | null;
  status: "CHANGED" | "UNCHANGED";
  current: BulkValues | null;
  new: BulkValues;
  validFrom: Instant;
  /** Change against the current value in basis points (server-computed); null without a current value. */
  changeBp: number | null;
  largeChange: boolean;
}

/** All-or-nothing: an invalid row refuses the whole upload with 422 and `errors["rows[i].field"]`. */
export interface BulkPriceResponse {
  uploadId: Id | null;
  dryRun: boolean;
  rowCount: number;
  changedCount: number;
  unchangedCount: number;
  rows: BulkRowResult[];
}
