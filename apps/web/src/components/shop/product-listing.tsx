import { Button, ColourBlockScreen } from "@fresh/ui";
import Link from "next/link";
import { listProducts } from "@/api/endpoints/catalog";
import { type RequestApi, loadWithLocation } from "@/api/server";
import { type CategoryRef } from "@/api/types/catalog";
import { CategoryRail } from "@/components/catalog/category-rail";
import { ProductGrid } from "@/components/catalog/product-card";
import { LocationBanner } from "@/components/location-banner";
import { SectionError } from "@/components/section";
import {
  PAGE_SIZE,
  type SearchParams,
  type ShopFilters as Filters,
  toListParams,
} from "@/lib/shop-params";
import { settle } from "@/lib/settle";
import { sourceTypeLabels } from "@/lib/status-maps";
import { LinkPagination } from "./link-pagination";
import { type ActiveChip, ShopToolbar } from "./shop-filters";

const numberFormat = new Intl.NumberFormat("en-IN");

/** Filters + toolbar + grid + pagination for /shop, /shop/[category] and /search (server-rendered). */
export async function ProductListing({
  api,
  filters,
  searchParams,
  basePath,
  filterCategories,
  categoryNames,
  navCategories,
  currentCategory,
}: {
  api: RequestApi;
  filters: Filters;
  searchParams: SearchParams;
  basePath: string;
  /** Category checkboxes (omitted on a category page). */
  filterCategories?: CategoryRef[];
  categoryNames?: Record<string, string>;
  /** Round category bubbles above the chips (the app shop). */
  navCategories?: (CategoryRef & { imageUrl?: string | null })[];
  currentCategory?: string;
}) {
  const hasLocation = api.pincode !== null;
  const result = await settle(
    loadWithLocation(api, (http) => listProducts(http, toListParams(filters, hasLocation))),
  );
  const located = result.ok && !result.value.pincodeRefused && hasLocation;

  const chips: ActiveChip[] = [
    ...(filterCategories
      ? filters.categories.map((slug) => ({
          key: "category",
          value: slug,
          label: categoryNames?.[slug] ?? slug,
        }))
      : []),
    ...filters.sourceTypes.map((s) => ({
      key: "source",
      value: s,
      label: sourceTypeLabels[s] ?? s,
    })),
    ...(filters.inStock ? [{ key: "inStock", label: "In stock" }] : []),
    ...(located && filters.minRupees !== undefined
      ? [{ key: "min", label: `From ₹${numberFormat.format(filters.minRupees)}` }]
      : []),
    ...(located && filters.maxRupees !== undefined
      ? [{ key: "max", label: `Up to ₹${numberFormat.format(filters.maxRupees)}` }]
      : []),
  ];

  return (
    <div className="flex flex-col gap-6">
      <LocationBanner
        pincode={api.pincode}
        pincodeRefused={result.ok && result.value.pincodeRefused}
      />
      {navCategories && navCategories.length > 0 && (
        <CategoryRail categories={navCategories} current={currentCategory} />
      )}
      <div className="flex min-w-0 flex-col gap-5">
        <ShopToolbar
          chips={chips}
          filters={filters}
          hasLocation={located}
          hasQuery={filters.q !== undefined}
          {...(filterCategories ? { categories: filterCategories } : {})}
        />
        {!result.ok ? (
          <SectionError message={result.message} traceId={result.traceId} />
        ) : result.value.data.items.length === 0 ? (
          <ColourBlockScreen
            tone="ice"
            framed
            eyebrow={filters.q ? `Search “${filters.q}”` : "Shop"}
            title="Nothing"
            title2="found"
            titleScript="line1"
            titleBrush="line2"
            headingLevel={2}
            slot="NO_SEARCH_RESULTS"
            avatarSize="sm"
            sheetTitle="No products match these filters"
          >
            <p className="text-text-muted text-sm">
              Try removing a filter{filters.q ? " or searching for another name" : ""}.
            </p>
            <Button asChild>
              <Link href={basePath}>Clear all filters</Link>
            </Button>
          </ColourBlockScreen>
        ) : (
          <>
            <p className="text-text-muted text-sm" aria-live="polite">
              {numberFormat.format(result.value.data.totalCount)}{" "}
              {result.value.data.totalCount === 1 ? "product" : "products"}
            </p>
            <ProductGrid products={result.value.data.items} priorityCount={2} />
            <LinkPagination
              basePath={basePath}
              searchParams={searchParams}
              page={result.value.data.page}
              pageSize={result.value.data.pageSize || PAGE_SIZE}
              totalCount={result.value.data.totalCount}
            />
          </>
        )}
      </div>
    </div>
  );
}
