// Reports and dashboard (docs/api §4.20), generated from the backend OpenAPI document. Report rows, totals and
// filters are dictionaries in the API (`object` in the spec), so their value type is written here.

import type { CalendarDate, Id, Schema } from "./common";

export type ReportKey =
  "daily-sales" | "product-performance" | "contribution-margin" | "waste" | "repeat-customers";

export interface ReportQuery {
  fromDate?: CalendarDate;
  toDate?: CalendarDate;
  cityId?: Id;
  fulfillmentLocationId?: Id;
  page?: number;
  pageSize?: number;
  sort?: string;
  /** Report-specific filters: variantId, categoryId, preparationCode, reasonCode, cohortMonths. */
  [extra: string]: string | number | undefined;
}

/** Report rows are server-shaped dictionaries; money columns end in `Paise`, rates in `Bp`. */
export type ReportRow = Readonly<Record<string, string | number | boolean | null>>;

/** `totals` is computed by the server over all matching rows; `asOf` is null before the first refresh. */
export type ReportResponse = Omit<Schema<"ReportResponse">, "filters" | "totals" | "items"> & {
  filters: Readonly<Record<string, unknown>>;
  totals: ReportRow;
  items: ReportRow[];
};

export type DashboardCount = Schema<"DashboardCount">;

export type DashboardSlot = Schema<"DashboardSlot">;

/** `available` and `lowStockThreshold` are stock-item units (grams or pieces); the unit is not sent (gap G4). */
export type DashboardLowStock = Schema<"DashboardLowStock">;

export type DashboardResponse = Omit<Schema<"DashboardResponse">, "filters"> & {
  filters: Readonly<Record<string, unknown>>;
};
