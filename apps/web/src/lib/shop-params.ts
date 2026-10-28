// URL ⇄ product list filters (docs/api §4.4, CAT-R14/R15). The URL is the source of truth so filtered pages can be
// shared, bookmarked and server-rendered.
import { type ProductListParams, type ProductSort, type SourceType } from "@/api/types/catalog";

export const PAGE_SIZE = 24;

export const SORT_OPTIONS: {
  value: ProductSort | "default";
  label: string;
  needsLocation?: boolean;
  needsQuery?: boolean;
}[] = [
  { value: "default", label: "Recommended" },
  { value: "relevance", label: "Best match", needsQuery: true },
  { value: "price", label: "Price: low to high", needsLocation: true },
  { value: "-price", label: "Price: high to low", needsLocation: true },
  { value: "name", label: "Name" },
  { value: "-newest", label: "Newest" },
];

const SORTS = new Set<string>(["relevance", "price", "-price", "name", "-newest"]);
const SOURCES = new Set<string>(["WILD", "FARMED"]);

export type SearchParams = Record<string, string | string[] | undefined>;

function all(value: string | string[] | undefined): string[] {
  if (value === undefined) return [];
  return Array.isArray(value) ? value : [value];
}

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

/** Whole rupees typed by the shopper, as an integer; anything else is ignored. */
function rupees(value: string | undefined): number | undefined {
  if (!value || !/^\d{1,6}$/.test(value)) return undefined;
  return Number(value);
}

/** A preparation code as the server writes it (UPPER_SNAKE, rule 8); anything else in the URL is ignored. */
const PREPARATION_CODE = /^[A-Z][A-Z0-9_]{0,39}$/;

export interface ShopFilters {
  categories: string[];
  q: string | undefined;
  sourceTypes: SourceType[];
  /** Preparation method codes (`?prep=`), from the server's `GET /preparation-methods` (W8, CAT-R27). */
  preparations: string[];
  inStock: boolean;
  minRupees: number | undefined;
  maxRupees: number | undefined;
  sort: ProductSort | undefined;
  page: number;
}

export function parseShopFilters(params: SearchParams, fixedCategory?: string): ShopFilters {
  const q = first(params.q)?.trim();
  const sort = first(params.sort);
  const page = Number(first(params.page));
  return {
    categories: fixedCategory ? [fixedCategory] : all(params.category).filter(Boolean),
    q: q && q.length > 0 ? q.slice(0, 60) : undefined,
    sourceTypes: all(params.source).filter((s): s is SourceType => SOURCES.has(s)),
    preparations: [...new Set(all(params.prep).filter((p) => PREPARATION_CODE.test(p)))],
    inStock: first(params.inStock) === "true",
    minRupees: rupees(first(params.min)),
    maxRupees: rupees(first(params.max)),
    sort: sort && SORTS.has(sort) ? (sort as ProductSort) : undefined,
    page: Number.isInteger(page) && page > 0 ? page : 1,
  };
}

/**
 * API query for the filters. Price filters and price sorts need a location (CAT-R14/R15), so they are dropped when
 * the visitor has no pincode; the UI disables them in that case. Rupees → paise is a unit conversion of the
 * shopper's own input, not a price calculation.
 */
export function toListParams(filters: ShopFilters, hasLocation: boolean): ProductListParams {
  const sortNeedsLocation = filters.sort === "price" || filters.sort === "-price";
  const sortNeedsQuery = filters.sort === "relevance";
  return {
    ...(filters.categories.length ? { category: filters.categories } : {}),
    ...(filters.q ? { q: filters.q } : {}),
    ...(filters.sourceTypes.length ? { sourceType: filters.sourceTypes } : {}),
    ...(filters.preparations.length ? { preparation: filters.preparations } : {}),
    ...(filters.inStock ? { inStock: true } : {}),
    ...(hasLocation && filters.minRupees !== undefined
      ? { minPricePaise: filters.minRupees * 100 }
      : {}),
    ...(hasLocation && filters.maxRupees !== undefined
      ? { maxPricePaise: filters.maxRupees * 100 }
      : {}),
    ...(filters.sort && !(sortNeedsLocation && !hasLocation) && !(sortNeedsQuery && !filters.q)
      ? { sort: filters.sort }
      : {}),
    page: filters.page,
    pageSize: PAGE_SIZE,
  };
}
