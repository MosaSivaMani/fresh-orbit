import { type ReportKey, type ReportRow } from "../../api/types/reports";

// How each report's server columns are shown. Names are the API's camelCase JSON names (docs/api §4.20,
// backend Reporting ReportDefinitions.cs). The admin only formats values; every number, including totals,
// comes from the server.

export type ColumnKind =
  | "date"
  | "month"
  | "location"
  | "text"
  | "code"
  | "count"
  | "money"
  | "bp"
  /** Stock quantity in the row's `stockUnit` (grams or pieces). */
  | "quantity"
  /** A server true/false flag, shown as Yes / No. */
  | "flag";

export interface ReportColumn {
  name: string;
  label: string;
  kind: ColumnKind;
  sortable?: boolean;
  /** Shown instead of "—" when the server sends null (e.g. a cost that is not configured, D367). */
  nullLabel?: string;
}

export interface ReportFilterDef {
  name: string;
  label: string;
  /** Fixed choices; free text when absent. */
  options?: readonly { value: string; label: string }[];
  /** Only reachable by a link (a picker for ids is not built yet). */
  linkOnly?: boolean;
}

export interface ReportDefinition {
  key: ReportKey;
  title: string;
  description: string;
  columns: readonly ReportColumn[];
  /** Totals the server returns, in display order. */
  totals: readonly ReportColumn[];
  filters: readonly ReportFilterDef[];
  /** False for reports without server paging / sorting (repeat customers). */
  paged: boolean;
  /** A warning to show above the totals, from server values only (never derived). */
  notice?: (totals: ReportRow) => string | null;
}

const col = (name: string, label: string, kind: ColumnKind, sortable = true): ReportColumn => ({
  name,
  label,
  kind,
  sortable,
});

/** D367: the server sends null for a cost (and the contribution after it) that is not configured. */
const notConfiguredWhenNull = (column: ReportColumn): ReportColumn => ({
  ...column,
  nullLabel: "Not configured",
});

const DATE = col("businessDate", "Date", "date");
const LOCATION = col("fulfillmentLocationId", "Location", "location", false);

const WASTE_REASONS = [
  { value: "SPOILED", label: "Spoiled" },
  { value: "TEMPERATURE_BREACH", label: "Temperature breach" },
  { value: "DAMAGED", label: "Damaged" },
  { value: "EXPIRED", label: "Expired" },
  { value: "QC_REJECTED", label: "QC rejected" },
];

export const REPORTS: readonly ReportDefinition[] = [
  {
    key: "daily-sales",
    title: "Daily sales",
    description: "Orders and revenue per day and location.",
    paged: true,
    columns: [
      DATE,
      LOCATION,
      col("ordersPlaced", "Placed", "count"),
      col("ordersDelivered", "Delivered", "count"),
      col("ordersCancelled", "Cancelled", "count"),
      col("grossPaise", "Gross", "money"),
      col("discountPaise", "Discounts", "money"),
      col("deliveryFeePaise", "Delivery fees", "money"),
      col("taxPaise", "Tax", "money"),
      col("netRevenuePaise", "Net revenue", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("postDeliveryRefundsPaise", "Post-delivery refunds", "money"),
      col("avgOrderValuePaise", "Avg. order value", "money"),
    ],
    totals: [
      col("ordersPlaced", "Orders placed", "count"),
      col("ordersDelivered", "Delivered", "count"),
      col("ordersCancelled", "Cancelled", "count"),
      col("grossPaise", "Gross", "money"),
      col("discountPaise", "Discounts", "money"),
      col("netRevenuePaise", "Net revenue", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("avgOrderValuePaise", "Avg. order value", "money"),
    ],
    filters: [],
  },
  {
    key: "product-performance",
    title: "Product performance",
    description: "Quantities, revenue and margin per product variant and preparation.",
    paged: true,
    columns: [
      DATE,
      LOCATION,
      col("productName", "Product", "text"),
      col("variantName", "Variant", "text"),
      col("sku", "SKU", "text"),
      col("preparationCode", "Preparation", "code"),
      col("ordersCount", "Orders", "count"),
      col("linesShortSupplied", "Short supplied", "count"),
      col("orderedQuantity", "Ordered", "quantity"),
      col("packedQuantity", "Packed", "quantity"),
      col("chargedQuantity", "Charged", "quantity"),
      col("revenuePaise", "Revenue", "money"),
      col("saleCostPaise", "Cost", "money"),
      col("grossMarginPaise", "Gross margin", "money"),
    ],
    totals: [
      col("ordersCount", "Orders", "count"),
      col("linesShortSupplied", "Lines short supplied", "count"),
      col("revenuePaise", "Revenue", "money"),
      col("saleCostPaise", "Cost", "money"),
      col("grossMarginPaise", "Gross margin", "money"),
    ],
    filters: [
      { name: "preparationCode", label: "Preparation code" },
      { name: "variantId", label: "Variant", linkOnly: true },
      { name: "categoryId", label: "Category", linkOnly: true },
    ],
  },
  {
    key: "contribution-margin",
    title: "Contribution margin",
    description: "Revenue after cost of goods, refunds, delivery cost and packaging cost.",
    paged: true,
    columns: [
      DATE,
      LOCATION,
      col("ordersDelivered", "Delivered", "count"),
      col("revenueExTaxPaise", "Revenue (ex. tax)", "money"),
      col("saleCostPaise", "Cost of goods", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("grossContributionPaise", "Before delivery and packaging", "money"),
      notConfiguredWhenNull(col("deliveryCostPaise", "Delivery cost", "money")),
      notConfiguredWhenNull(col("packagingCostPaise", "Packaging cost", "money")),
      notConfiguredWhenNull(col("contributionPaise", "Contribution", "money")),
      notConfiguredWhenNull(col("contributionMarginBp", "Margin", "bp")),
      col("costsNotConfigured", "Costs not configured", "flag"),
    ],
    totals: [
      col("ordersDelivered", "Delivered orders", "count"),
      col("revenueExTaxPaise", "Revenue (ex. tax)", "money"),
      col("saleCostPaise", "Cost of goods", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("grossContributionPaise", "Before delivery and packaging", "money"),
      notConfiguredWhenNull(col("deliveryCostPaise", "Delivery cost", "money")),
      notConfiguredWhenNull(col("packagingCostPaise", "Packaging cost", "money")),
      notConfiguredWhenNull(col("contributionPaise", "Contribution", "money")),
      notConfiguredWhenNull(col("contributionMarginBp", "Margin", "bp")),
      col("ordersWithoutCosts", "Orders without costs", "count"),
    ],
    filters: [],
    notice: (totals) =>
      totals.costsNotConfigured === true
        ? "Costs not configured: some delivered orders have no recorded delivery or packaging cost, so those costs and the contribution after them are not shown. Set the costs on each location's page; orders delivered before that stay not configured."
        : null,
  },
  {
    key: "waste",
    title: "Waste",
    description: "Stock written off as waste, by reason, against what was sold.",
    paged: true,
    columns: [
      DATE,
      LOCATION,
      col("productName", "Product", "text"),
      col("variantName", "Variant", "text"),
      col("reasonCode", "Reason", "code"),
      col("movementsCount", "Entries", "count"),
      col("wasteQuantity", "Wasted", "quantity"),
      col("soldQuantity", "Sold", "quantity"),
      col("wasteCostPaise", "Waste cost", "money"),
      col("wasteShareBp", "Waste share", "bp"),
    ],
    totals: [
      col("movementsCount", "Entries", "count"),
      col("wasteCostPaise", "Waste cost", "money"),
      col("wasteShareBp", "Waste share", "bp"),
    ],
    filters: [
      { name: "reasonCode", label: "Reason", options: WASTE_REASONS },
      { name: "variantId", label: "Variant", linkOnly: true },
    ],
  },
  {
    key: "repeat-customers",
    title: "Repeat customers",
    description: "Customers ordering again, and monthly cohorts of first orders.",
    paged: false,
    columns: [
      col("cohortMonth", "Cohort", "month", false),
      col("monthOffset", "Month", "count", false),
      col("cohortSize", "Cohort size", "count", false),
      col("activeCustomers", "Active", "count", false),
      col("retentionBp", "Retention", "bp", false),
    ],
    totals: [
      col("activeCustomers", "Active customers", "count"),
      col("newCustomers", "New customers", "count"),
      col("repeatCustomers", "Repeat customers", "count"),
      col("repeatRateBp", "Repeat rate", "bp"),
    ],
    filters: [
      {
        name: "cohortMonths",
        label: "Cohort months",
        options: Array.from({ length: 12 }, (_, i) => ({
          value: String(i + 1),
          label: `${i + 1} ${i === 0 ? "month" : "months"}`,
        })),
      },
    ],
  },
];

export function reportDefinition(key: string | undefined): ReportDefinition {
  return REPORTS.find((r) => r.key === key) ?? REPORTS[0]!;
}
