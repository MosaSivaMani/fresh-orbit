import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const pricingRoutes: RouteObject[] = [
  screen("pricing", "pricing.manage", async () => (await import("./PricesPage")).PricesPage),
  screen(
    "pricing/bulk",
    "pricing.manage",
    async () => (await import("./BulkUploadPage")).BulkUploadPage,
  ),
  screen(
    "pricing/tax-rules",
    "pricing.manage",
    async () => (await import("./TaxRulesPage")).TaxRulesPage,
  ),
  screen(
    "pricing/variants/:variantId",
    "pricing.manage",
    async () => (await import("./VariantPricePage")).VariantPricePage,
  ),
];
