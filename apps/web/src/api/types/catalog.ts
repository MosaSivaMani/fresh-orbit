// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §4.4 and docs/product/catalog.md; where the docs are silent the field names follow
// the Phase 2 storefront contracts (Catalog.Application/Storefront/StorefrontContracts.cs).

import { type Availability, type AvailabilityStatus, type Quantity, type SellUnit } from "./common";

export type SourceType = "WILD" | "FARMED";

/** `GET /products` `sort` values (CAT-R15). `relevance` needs `q`; price sorts need a location. */
export type ProductSort = "relevance" | "price" | "-price" | "name" | "-newest";

export interface FromPrice {
  unitPricePaise: number;
  compareAtPaise: number | null;
  perUnit: SellUnit;
}

export interface ProductListItem {
  id: string;
  slug: string;
  name: string;
  localNames: string[];
  sourceType: SourceType;
  imageUrl: string | null;
  /** Null without a location (`locationRequired: true`). */
  fromPrice: FromPrice | null;
  availability: Availability | null;
  locationRequired: boolean;
}

export interface ProductListParams {
  category?: string[];
  q?: string;
  sourceType?: SourceType[];
  preparation?: string[];
  inStock?: boolean;
  minPricePaise?: number;
  maxPricePaise?: number;
  sort?: ProductSort;
  page?: number;
  pageSize?: number;
}

export interface ImageSource {
  width: number;
  url: string;
}

export interface ProductImage {
  url: string;
  altText: string;
  width: number;
  height: number;
  /** Phase 2 addition: the 320 / 640 / 1280 px renditions. */
  sources?: ImageSource[];
}

export type PreparationChargeBasis = "PER_KG" | "PER_ITEM" | "FLAT";

export interface Preparation {
  code: string;
  name: string;
  /** Net yield after cleaning in basis points (display only: "about 70% after cleaning"). */
  yieldBp: number;
  isDefault: boolean;
  chargePaise: number | null;
  chargeBasis: PreparationChargeBasis | null;
}

export interface VariantAvailability {
  status: AvailabilityStatus;
  /** Largest valid quantity the stock covers for the chosen preparation; computed by the server. */
  maxOrderableNow: Quantity | null;
  message: string | null;
}

export interface Variant {
  id: string;
  sku: string;
  name: string;
  sellUnit: SellUnit;
  isWeightVariable: boolean;
  packSizeGrams?: number | null;
  avgPieceGrams?: number | null;
  minOrder: Quantity;
  maxOrder: Quantity;
  orderStep: Quantity;
  unitPricePaise: number | null;
  compareAtPaise: number | null;
  preparations: Preparation[];
  availability: VariantAvailability | null;
}

export interface CategoryRef {
  slug: string;
  name: string;
}

export interface ProductDetail {
  id: string;
  slug: string;
  name: string;
  localNames: string[];
  speciesScientificName: string | null;
  description: string | null;
  sourceType: SourceType;
  sourceRegion: string | null;
  storageInstructions: string | null;
  category: CategoryRef;
  images: ProductImage[];
  location: { fulfillmentLocationCode: string; pincode: string } | null;
  variants: Variant[];
  /** Product-level availability (`NOT_SOLD_HERE` when not sellable at the location). */
  availability?: Availability | null;
  netWeightNote: string;
  isWishlisted: boolean | null;
  locationRequired?: boolean;
}

export interface CategoryNode {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  sortOrder: number;
  children: CategoryNode[];
}

export interface CategoryDetail {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  parent: CategoryRef | null;
  children: CategoryNode[];
}

export interface SuggestItem {
  name: string;
  matchedLocalName: string | null;
  slug: string;
  imageUrl: string | null;
}

/** Home offer banner (Promotions). Shape not fixed in docs/api; see docs/design/web.md "API gaps". */
export interface HomeBanner {
  id: string;
  title: string;
  description: string | null;
  imageUrl: string | null;
  /** `COUPON`, `CATEGORY` or `PRODUCT` (PROMO-R25). */
  linkType: "COUPON" | "CATEGORY" | "PRODUCT" | null;
  linkTarget: string | null;
}

export interface HomeResponse {
  banners: HomeBanner[];
  featuredCategories: CategoryNode[];
  featuredProducts: ProductListItem[];
  locationRequired: boolean;
}
