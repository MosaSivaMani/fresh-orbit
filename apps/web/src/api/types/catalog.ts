// Catalogue shapes (docs/api §4.4), generated from the backend OpenAPI document (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";

export type SourceType = Schema<"SourceType">;

/** `GET /products` `sort` values (CAT-R15). `relevance` needs `q`; price sorts need a location. */
export type ProductSort = "relevance" | "price" | "-price" | "name" | "-newest";

export type FromPrice = Schema<"FromPrice">;

export type ProductListItem = Schema<"ProductListItem">;

/** Query of `GET /products` (the OpenAPI document lists it as query parameters, not a schema). */
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

export type ImageSource = Schema<"ImageSource">;

export type ProductImage = Schema<"StorefrontImage">;

export type PreparationChargeBasis = Schema<"ChargeBasis">;

/** `chargeBasis` is PER_KG / PER_ITEM / FLAT; the spec types it as a string. */
export type Preparation = Omit<Schema<"StorefrontPreparation">, "chargeBasis"> & {
  chargeBasis: PreparationChargeBasis | null;
};

export type VariantAvailability = Schema<"CatalogVariantAvailability">;

export type Variant = Omit<Schema<"StorefrontVariant">, "preparations"> & {
  preparations: Preparation[];
};

export type CategoryRef = Schema<"CategoryRef">;

export type ProductDetail = Omit<Schema<"StorefrontProductDetail">, "variants"> & {
  variants: Variant[];
};

export type CategoryNode = Schema<"StorefrontCategory">;

export type CategoryDetail = Schema<"StorefrontCategoryDetail">;

export type SuggestItem = Schema<"SuggestItem">;

/** Home offer banner: an item of `GET /promotions` (Promotions `BannerInfo`). */
export type HomeBanner = Omit<Schema<"BannerInfo">, "linkType"> & {
  linkType: Schema<"PromotionLinkType">;
};

/**
 * `GET /home`: featured categories and products, the location's `bestsellers` (CAT-R24) and its banners. The website
 * reads offer banners from `GET /promotions`.
 */
export type HomeResponse = Schema<"HomeResponse">;

/** Serving facts of a variant per its basis (CAT-R21): 1 kg net, one pack or one piece. */
export type ServingInfo = Schema<"ServingInfo">;

/** The batch the next order is packed from (CAT-R25). */
export type FreshnessInfo = Schema<"FreshnessInfo">;

/** `GET /price-quote`: the server's estimate and maximum for a chosen size, cut and weight (gap W2 closed). */
export type PriceQuote = Schema<"PriceQuoteResponse">;
