// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.11; shapes match backend Catalog.Application/Admin/AdminContracts.cs.

import type { Id, Instant, Version } from "./common";

export type ProductStatus = "DRAFT" | "ACTIVE" | "ARCHIVED";
export type SourceType = "WILD" | "FARMED";
export type SellUnit = "KG" | "PIECE" | "PACK";
export type StockUnit = "GRAM" | "PIECE";

export interface AdminCategory {
  id: Id;
  parentId: Id | null;
  slug: string;
  name: string;
  description: string | null;
  imageKey: string | null;
  sortOrder: number;
  isActive: boolean;
  version: Version;
  children: AdminCategory[];
}

export interface CreateCategoryRequest {
  parentId: Id | null;
  slug: string;
  name: string;
  description: string | null;
  sortOrder: number;
}

export interface UpdateCategoryRequest extends CreateCategoryRequest {
  imageKey: string | null;
  isActive: boolean;
}

export interface ProductRequest {
  categoryId: Id;
  slug: string;
  name: string;
  localNames: string[];
  speciesScientificName: string | null;
  description: string | null;
  sourceType: SourceType;
  sourceRegion: string | null;
  storageInstructions: string | null;
  handlingNotes: string | null;
  hsnCode: string | null;
  taxCategoryCode: string | null;
  featuredRank: number | null;
}

export interface VariantPreparationRequest {
  code: string;
  /** 1–10000 basis points of gross weight that remains after preparation. */
  yieldBp: number;
  isDefault: boolean;
}

/**
 * `minOrder`, `maxOrder`, `orderStep` are in the stock unit (grams for KG, else pieces/packs);
 * KG defaults 250 / 5000 / 250.
 */
export interface VariantRequest {
  sku: string | null;
  name: string;
  sellUnit: SellUnit;
  packSizeGrams: number | null;
  avgPieceGrams: number | null;
  minOrder: number | null;
  maxOrder: number | null;
  orderStep: number | null;
  isActive: boolean | null;
  preparations: VariantPreparationRequest[];
}

export interface AdminPreparation {
  code: string;
  name: string;
  yieldBp: number;
  isDefault: boolean;
  isActive: boolean;
}

export interface AdminVariant {
  id: Id;
  sku: string;
  name: string;
  sellUnit: SellUnit;
  stockUnit: StockUnit;
  packSizeGrams: number | null;
  avgPieceGrams: number | null;
  minOrder: number;
  maxOrder: number;
  orderStep: number;
  isWeightVariable: boolean;
  isActive: boolean;
  preparations: AdminPreparation[];
}

export interface AdminImage {
  id: Id;
  storageKey: string;
  /** 640 px rendition. */
  url: string;
  altText: string;
  width: number;
  height: number;
  sortOrder: number;
}

export interface AdminProduct {
  id: Id;
  categoryId: Id;
  slug: string;
  name: string;
  localNames: string[];
  speciesScientificName: string | null;
  description: string | null;
  sourceType: SourceType;
  sourceRegion: string | null;
  storageInstructions: string | null;
  handlingNotes: string | null;
  hsnCode: string | null;
  taxCategoryCode: string | null;
  status: ProductStatus;
  featuredRank: number | null;
  publishedAt: Instant | null;
  version: Version;
  variants: AdminVariant[];
  images: AdminImage[];
}

export interface AdminProductListItem {
  id: Id;
  categoryId: Id;
  slug: string;
  name: string;
  status: ProductStatus;
  activeVariantCount: number;
  imageUrl: string | null;
  publishedAt: Instant | null;
  version: Version;
}

export interface PreparationMethod {
  id: Id;
  code: string;
  name: string;
  description: string | null;
  sortOrder: number;
  guidanceYieldBp: number;
}
