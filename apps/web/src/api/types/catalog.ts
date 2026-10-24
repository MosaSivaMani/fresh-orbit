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

export type VariantAvailability = Schema<"VariantAvailability">;

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
 * `GET /home`. The OpenAPI document types `banners` as `unknown[]` (Catalog sends an empty list until home banners
 * come from Promotions); the website reads banners from `GET /promotions` instead.
 */
export type HomeResponse = Schema<"HomeResponse">;
