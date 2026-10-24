// Admin catalogue (docs/api §4.11), generated from the backend OpenAPI document.

import type { Schema } from "./common";

export type ProductStatus = Schema<"ProductStatus">;
export type SourceType = Schema<"SourceType">;
export type SellUnit = Schema<"SellUnit">;
export type StockUnit = Schema<"CatalogStockUnit">;

export type AdminCategory = Schema<"AdminCategoryResponse">;

export type CreateCategoryRequest = Schema<"CreateCategoryRequest">;

export type UpdateCategoryRequest = Schema<"UpdateCategoryRequest">;

export type ProductRequest = Schema<"ProductRequest">;

/** `yieldBp`: 1–10000 basis points of gross weight that remains after preparation. */
export type VariantPreparationRequest = Schema<"VariantPreparationRequest">;

/**
 * `minOrder`, `maxOrder`, `orderStep` are in the stock unit (grams for KG, else pieces/packs);
 * KG defaults 250 / 5000 / 250.
 */
export type VariantRequest = Schema<"VariantRequest">;

export type AdminPreparation = Schema<"AdminPreparationResponse">;

export type AdminVariant = Schema<"AdminVariantResponse">;

/** `url` is the 640 px rendition. */
export type AdminImage = Schema<"AdminImageResponse">;

export type AdminProduct = Schema<"AdminProductResponse">;

export type AdminProductListItem = Schema<"AdminProductListItem">;

export type PreparationMethod = Schema<"PreparationMethodResponse">;

/** `GET /admin/catalog/variants` item, readable by any staff member (G3): names, SKU and units only. */
export type VariantLookup = Schema<"VariantLookupItem">;
