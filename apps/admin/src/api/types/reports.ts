// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.20; shapes match backend Reporting (ReportRequests.cs, ReportReader.cs).

import type { CalendarDate, Id, Instant } from "./common";

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

export interface ReportResponse {
  /** Oldest refresh of the views used; null before the first refresh. */
  asOf: Instant | null;
  fromDate: CalendarDate;
  toDate: CalendarDate;
  filters: Readonly<Record<string, unknown>>;
  /** Totals over all matching rows (server-computed). */
  totals: ReportRow;
  items: ReportRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DashboardCount {
  status: string;
  count: number;
}

export interface DashboardSlot {
  slotId: Id;
  fulfillmentLocationId: Id;
  startAt: Instant;
  endAt: Instant;
  bookedCount: number;
  capacity: number;
  fillBp: number | null;
}

export interface DashboardLowStock {
  stockItemId: Id;
  fulfillmentLocationId: Id;
  variantId: Id;
  sku: string | null;
  variantName: string | null;
  /** Stock-item unit quantity (grams or pieces). */
  available: number;
  lowStockThreshold: number;
}

export interface DashboardResponse {
  asOf: Instant;
  date: CalendarDate;
  filters: Readonly<Record<string, unknown>>;
  ordersByStatus: DashboardCount[];
  slots: DashboardSlot[];
  lowStock: DashboardLowStock[];
  openComplaints: number;
  refundsRequested: number;
  refundsRequestedPaise: number;
  openPaymentIssues: number;
}
