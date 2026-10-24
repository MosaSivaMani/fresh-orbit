import { formatDayShort, formatTime } from "@fresh/core";
import { Button, CharacterState } from "@fresh/ui";
import Link from "next/link";
import { getPromotions, getServiceability, listProducts } from "@/api/endpoints/catalog";
import { type RequestApi, loadWithLocation } from "@/api/server";
import { type CategoryRef } from "@/api/types/catalog";
import { CategoryRail } from "@/components/catalog/category-rail";
import { ProductGrid, categoryOffer, offersBySlug } from "@/components/catalog/product-card";
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
  offerCategoryIds,
}: {
  api: RequestApi;
  filters: Filters;
  searchParams: SearchParams;
  basePath: string;
  /** Category checkboxes (omitted on a category page). */
  filterCategories?: CategoryRef[];
  categoryNames?: Record<string, string>;
  /** The round category row above the chips (D95). */
  navCategories?: (CategoryRef & { imageUrl?: string | null })[];
  currentCategory?: string;
  /** Category ids whose promotion banner puts its offer line on every card (a category page and its parent). */
  offerCategoryIds?: string[];
}) {
  const hasLocation = api.pincode !== null;
  const [result, promotions, serviceability] = await Promise.all([
    settle(loadWithLocation(api, (http) => listProducts(http, toListParams(filters, hasLocation)))),
    settle(loadWithLocation(api, getPromotions)),
    api.pincode ? settle(getServiceability(api.anonymous, api.pincode)) : null,
  ]);
  const offers = promotions.ok ? offersBySlug(promotions.value.data) : {};
  const sharedOffer =
    promotions.ok && offerCategoryIds
      ? categoryOffer(promotions.value.data, offerCategoryIds)
      : undefined;
  const nextSlot = serviceability?.ok ? serviceability.value.nextSlot : null;
  const freshness = nextSlot
    ? `Next slot ${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)} – ${formatTime(nextSlot.endAt)}`
    : undefined;
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
          <CharacterState
            slot="NO_SEARCH_RESULTS"
            title="No products match these filters"
            description={`Try removing a filter${filters.q ? " or searching for another name" : ""}.`}
            actions={
              <Button asChild>
                <Link href={basePath}>Clear all filters</Link>
              </Button>
            }
          />
        ) : (
          <>
            <p className="text-text-muted text-sm" aria-live="polite">
              {numberFormat.format(result.value.data.totalCount)}{" "}
              {result.value.data.totalCount === 1 ? "product" : "products"}
            </p>
            <ProductGrid
              products={result.value.data.items}
              priorityCount={2}
              offers={offers}
              {...(sharedOffer ? { offer: sharedOffer } : {})}
              {...(freshness ? { freshness } : {})}
            />
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
