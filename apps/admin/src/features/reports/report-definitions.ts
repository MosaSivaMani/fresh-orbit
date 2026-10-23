import { type ReportKey } from "../../api/types/reports";

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
  | "quantity";

export interface ReportColumn {
  name: string;
  label: string;
  kind: ColumnKind;
  sortable?: boolean;
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
}

const col = (name: string, label: string, kind: ColumnKind, sortable = true): ReportColumn => ({
  name,
  label,
  kind,
  sortable,
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
    description: "Revenue after cost of goods, refunds and delivery cost.",
    paged: true,
    columns: [
      DATE,
      LOCATION,
      col("revenueExTaxPaise", "Revenue (ex. tax)", "money"),
      col("saleCostPaise", "Cost of goods", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("deliveryCostPaise", "Delivery cost", "money"),
      col("contributionPaise", "Contribution", "money"),
      col("contributionMarginBp", "Margin", "bp"),
    ],
    totals: [
      col("revenueExTaxPaise", "Revenue (ex. tax)", "money"),
      col("saleCostPaise", "Cost of goods", "money"),
      col("refundsPaise", "Refunds", "money"),
      col("deliveryCostPaise", "Delivery cost", "money"),
      col("contributionPaise", "Contribution", "money"),
      col("contributionMarginBp", "Margin", "bp"),
    ],
    filters: [],
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
