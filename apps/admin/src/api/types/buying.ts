// Buying list and today's prices (docs/api §4.20, REP-R14 and REP-R15). Written here from
// `Reporting.Infrastructure/Buying/BuyingContracts.cs`; switch to `Schema<"BuyingListResponse">` and
// `Schema<"TodaysPricesResponse">` once the committed API client is regenerated. Every number is server-computed.

import type { CalendarDate, Id, Instant } from "./common";

export type StockUnit = "GRAM" | "PIECE";
export type SellUnit = "KG" | "PIECE" | "PACK";

export interface BuyingListQuery {
  fulfillmentLocationId: Id;
  /** Delivery date at the location; omitted = today there. */
  deliveryDate?: CalendarDate;
  /** Safety margin typed on the screen, 0–10000 bp; never stored. */
  safetyMarginBp?: number;
}

export interface BuyingListPreparation {
  preparationCode: string;
  preparationName: string;
  yieldBp: number;
  ordersCount: number;
  orderedQuantity: number;
  orderedNetGrams: number | null;
  neededQuantity: number;
  estimatedRawGrams: number | null;
}

/** Quantities are in `stockUnit`: raw grams for GRAM, pieces or packs for PIECE. */
export interface BuyingListItem {
  variantId: Id;
  productId: Id;
  productName: string;
  variantName: string;
  sku: string;
  sellUnit: SellUnit;
  stockUnit: StockUnit;
  packSizeGrams: number | null;
  ordersCount: number;
  orderedQuantity: number;
  orderedNetGrams: number | null;
  neededQuantity: number;
  estimatedRawGrams: number | null;
  withMarginQuantity: number;
  withMarginRawGrams: number | null;
  onHand: number | null;
  reservedForOtherOrders: number;
  availableQuantity: number;
  shortfallQuantity: number;
  shortfallRawGrams: number | null;
  stockItemMissing: boolean;
  preparations: BuyingListPreparation[];
}

export interface BuyingListResponse {
  asOf: Instant;
  fulfillmentLocationId: Id;
  locationCode: string;
  locationName: string;
  timeZone: string;
  deliveryDate: CalendarDate;
  safetyMarginBp: number;
  ordersCount: number;
  pendingPaymentOrders: number;
  estimatedRawGrams: number;
  shortfallRawGrams: number;
  items: BuyingListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TodaysPricesQuery {
  fulfillmentLocationId: Id;
  categoryId?: Id;
  /** Screen-wide target margin, 0–9500 bp; never stored (D371). */
  targetMarginBp?: number;
  /** `categoryId:bp,…` — per-category targets typed on the screen. */
  categoryTargets?: string;
}

export type TodaysPriceIssue =
  | "PRICE_MISSING"
  | "BATCH_MISSING"
  | "YIELD_MISSING"
  | "TAX_RULE_MISSING";

export interface TodaysPriceItem {
  variantId: Id;
  productId: Id;
  productName: string;
  variantName: string;
  sku: string;
  categoryId: Id | null;
  categoryName: string | null;
  sellUnit: SellUnit;
  stockUnit: StockUnit;
  packSizeGrams: number | null;
  unitPricePaise: number | null;
  compareAtPaise: number | null;
  priceValidFrom: Instant | null;
  nextUnitPricePaise: number | null;
  nextValidFrom: Instant | null;
  taxRateBp: number | null;
  preparationCode: string | null;
  yieldBp: number | null;
  latestBatchId: Id | null;
  latestBatchReceivedAt: Instant | null;
  latestBatchCostPaise: number | null;
  latestBatchQuantity: number | null;
  /** Per net kg (KG), per piece or per pack. */
  costPerUnitPaise: number | null;
  marginBp: number | null;
  targetMarginBp: number | null;
  /** Shown only; staff decide the price (never applied automatically). */
  suggestedPricePaise: number | null;
  issues: TodaysPriceIssue[];
}

export interface TodaysPricesResponse {
  asOf: Instant;
  fulfillmentLocationId: Id;
  locationCode: string;
  locationName: string;
  timeZone: string;
  /** Next local midnight at the location (UTC): the "from tomorrow" start for saved prices. */
  tomorrowStartsAt: Instant;
  targetMarginBp: number | null;
  categoryTargets: Record<Id, number>;
  items: TodaysPriceItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
