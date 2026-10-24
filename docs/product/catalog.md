# Catalog module

Owner: E2. Schema `catalog`. Uses no other module's `.Public`. Endpoints: `docs/api/README.md` §4.4, §4.11.
Error codes: `docs/api/README.md` §2 (Catalog, Common).

## 1. Aggregates and boundaries

| Aggregate root | Tables | Children | Row version |
|---|---|---|---|
| Category | `categories` | — | `categories.xmin` |
| Product | `products` | `product_variants`, `variant_preparations`, `product_images` | `products.xmin` (variant and image changes touch the product row) |
| PreparationMethod | `preparation_methods` | — | reference data, read-only via API |

| Boundary rule |
|---|
| Variants and their preparations change only through the Product aggregate; `If-Match` quotes the product version. |
| Prices (Pricing) and stock (Inventory) are not Catalog data; the customer browse query combines them (§3 CAT-R10, Q1). |
| Orders snapshot names, SKU, HSN, yield and image key at placement; Catalog edits never change placed orders. |
| Home banners (`promotions` table) belong to Promotions even though the admin screen uses `catalog.manage`. |
| Image bytes live in MinIO through `IFileStorage`; Catalog stores only `storage_key` and metadata. |

### Public API (`Catalog.Public`)

| Member | Returns | Used by |
|---|---|---|
| `GetVariantsAsync(variantIds)` | per variant: productId, productName, productStatus, slug, variant name, sku, sellUnit, stockUnit, packSizeGrams, avgPieceGrams, min/max/step, isWeightVariable, isActive, hsnCode, taxCategoryCode, primary image key, preparations (code, name, yieldBp, isDefault, isActive) | Pricing, Inventory, Cart, Ordering, Promotions |
| `GetSellableVariantIdsAsync()` | ids of active variants of ACTIVE products | Pricing (readiness), Inventory |
| Quantity rules (no member) | Callers apply CAT-R7 to the min/max/step returned by `GetVariantsAsync`, through a `QuantityRules` value in SharedKernel (Q5) | Cart, Ordering |
| `GetProductSummariesAsync(productIds)` | name, slug, image, status | Customers (wishlist), Promotions |

## 2. States and transitions

### Product (`products.status`)

| From | To | Command / endpoint | Guard |
|---|---|---|---|
| — | DRAFT | CreateProduct `POST /admin/products` | CAT-R1 |
| DRAFT | ACTIVE | PublishProduct `POST /admin/products/{id}/publish` | CAT-R3 |
| ACTIVE | ARCHIVED | ArchiveProduct `POST /admin/products/{id}/archive` | — |
| DRAFT | ARCHIVED | ArchiveProduct | — |
| ARCHIVED | DRAFT | RestoreProduct (Q4) | — |
| ACTIVE | DRAFT | refused | archive instead |

### Variant (`product_variants.is_active`)

| From | To | Command | Guard |
|---|---|---|---|
| — | active | AddVariant `POST /admin/products/{id}/variants` | CAT-R4–R7 |
| active | inactive | UpdateVariant `PUT …/variants/{variantId}` | product not ACTIVE, or another active variant remains (CAT-R3) |
| inactive | active | UpdateVariant | CAT-R4–R7 |

### Category (`categories.is_active`)

| From | To | Guard |
|---|---|---|
| active ↔ inactive | `PUT /admin/categories/{id}` | Deactivating hides the category, its children and their products (CAT-R10); no refusal |

## 3. Business rules

| Key | Rule |
|---|---|
| CAT-R1 | `slug`: lowercase kebab `^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 80 chars, unique (`SLUG_TAKEN`); generated from name when absent; immutable once ACTIVE. |
| CAT-R2 | Categories: max depth 2 (parent → child); no cycles; `sortOrder` integer; slug rules as CAT-R1. |
| CAT-R3 | **Publish** requires: category active; `hsnCode` (4–8 digits); `taxCategoryCode`; ≥1 image with alt text; ≥1 active variant; every active variant passes CAT-R4–R7. Failures → 422 `VALIDATION_FAILED` with all field errors (`images`, `variants[0].preparations`). An ACTIVE product always keeps ≥1 active variant. |
| CAT-R4 | **Units.** `sellUnit KG` → `stockUnit GRAM`, `isWeightVariable = true`. `PIECE` → `PIECE`, `isWeightVariable = false`, `avgPieceGrams` required (display). `PACK` → `PIECE` (packs counted), `packSizeGrams` required (net content), `isWeightVariable = false`. Other combinations → `VALIDATION_FAILED`. |
| CAT-R5 | **Preparations.** Each active variant has ≥1 active preparation and exactly one active default (`DEFAULT_PREPARATION_REQUIRED`). Methods come from `preparation_methods` (reference data). |
| CAT-R6 | **Yield.** `yieldBp` 1–10000 (`YIELD_INVALID`); `WHOLE` = 10000; non-KG variants must use 10000 (pieces and packs are counted, not weighed). Stock to reserve = `ceil(net × 10000 / yieldBp)` grams (applied by Inventory). A yield change affects only new orders. |
| CAT-R7 | **Quantity rules** (in stock unit). `orderStep > 0`; `minOrder ≥ orderStep`; `minOrder % orderStep = 0`; `maxOrder ≥ minOrder`; `maxOrder % orderStep = 0`. A quantity is valid when unit matches (`QUANTITY_UNIT_INVALID`), `minOrder ≤ q ≤ maxOrder` (`QUANTITY_BELOW_MIN` / `QUANTITY_ABOVE_MAX`) and `q % orderStep = 0` (`QUANTITY_STEP_INVALID`). Defaults for KG: 250 / 5000 / 250. |
| CAT-R8 | `sku` UPPER_SNAKE / hyphen, unique (`SKU_TAKEN`), immutable after the variant was first active. |
| CAT-R9 | Archived products and inactive variants are never deleted; they disappear from customer reads and stay readable for orders and admin. |
| CAT-R10 | **Customer visibility** at a location: product ACTIVE + category (and parent) active + ≥1 active variant with ≥1 active preparation + current price at the location (Pricing) + stock item at the location (Inventory). Fails price or stock item → excluded from lists, detail shows `NOT_SOLD_HERE`. Stock item with zero available → listed as `OUT_OF_STOCK`, sorted after in-stock items. Without location: ACTIVE + active variant only, `price`/`availability` null. |
| CAT-R11 | `fromPrice` on a list item = lowest current `unitPricePaise` among the product's visible variants at the location, with its `perUnit`. |
| CAT-R12 | **Search.** `search_vector` = `setweight(to_tsvector('simple', name), 'A') ‖ setweight(local_names, 'A') ‖ setweight(species_scientific_name, 'B') ‖ setweight(description, 'C')`; `simple` config (Indian names, no English stemming). Query = `websearch_to_tsquery('simple', q)` with prefix match on the last token, OR trigram `word_similarity(q, name or local names) ≥ 0.3` (typos: "vanjiram" → Vanjaram). Rank = `ts_rank_cd × 2 + similarity`. `q` 2–60 chars after trim, else `VALIDATION_FAILED`. |
| CAT-R13 | **Suggest** (`/search/suggest`): top 8 by trigram similarity over name and local names; returns name, matched local name, slug, image; no prices; visibility CAT-R10 without price/stock checks when no location. |
| CAT-R14 | **Filters.** `category` (slug; includes child categories), `q`, `sourceType` (WILD/FARMED), `preparation` (code; product has a visible variant offering it), `inStock` (true = available > 0), `minPricePaise` / `maxPricePaise` on `fromPrice` (require location, else `LOCATION_REQUIRED`). Repeated params = OR. |
| CAT-R15 | **Sorting.** `relevance` (default with `q`; invalid without), `price`, `-price` (on `fromPrice`; require location), `name`, `-newest` (published time). Default without `q`: in-stock first, category `sortOrder`, then `name`. Tie-breaker always `id`. Unknown → `VALIDATION_FAILED`. |
| CAT-R16 | **Images.** JPEG, PNG or WebP by magic bytes (not file name), ≤ 5 MB, ≥ 800 px shortest side, header dimensions ≤ 8,000 px a side and ≤ 40 MP (checked before any pixel memory is allocated, SEC-5), ≤ 8 per product, `altText` required (`IMAGE_INVALID`). Server re-encodes to WebP 320 / 640 / 1280 widths (EXIF stripped); a codec that can scale while decoding (JPEG) decodes at the smallest supported size still ≥ 1280 px wide. Keys `products/{productId}/{imageId}/{width}.webp`; original kept at `…/original`. First by `sortOrder` is the primary image. |
| CAT-R17 | **Image storage flow.** Upload objects first, then commit the row; if the commit fails, delete the objects; a daily job deletes objects under `products/` with no row older than 24 h. Removing an image deletes the row; objects are deleted by the follow-on handler. |
| CAT-R18 | Image URLs are public and cacheable (`https://img.freshorbit.in/{key}`); no signed URLs for product images. |
| CAT-R19 | `localNames`: up to 10 names, each ≤ 40 chars, de-duplicated case-insensitively. |
| CAT-R20 | Admin product list includes all statuses; customer endpoints never return DRAFT or ARCHIVED (404). |

## 4. Events

| Event (raised) | Payload | Consumers |
|---|---|---|
| `ProductPublished` | productId, slug | Audit, Notifications (wishlist back-in-catalog, later) |
| `ProductArchived` | productId | Audit, Promotions (flag coupons scoped only to it) |
| `ProductUpdated` | productId, changed fields | Audit |
| `VariantActivated` / `VariantDeactivated` | variantId, productId | Audit, Pricing (admin `missingOnly` list) |
| `VariantPreparationsChanged` | variantId, preparations with yieldBp | Audit, Pricing (charges for new preparations show as missing) |
| `ProductImageRemoved` | productId, imageId, storageKey prefix | Catalog handler deletes MinIO objects |
| `CategoryChanged` | categoryId, isActive | Audit |

Consumed: `ProductImageRemoved` (own, follow-on). No other module's events.

## 5. Commands (Read → Decide → Commit → Follow-on)

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| CreateCategory | `POST /api/v1/admin/categories` | parent, slug exists? | CAT-R1, CAT-R2 | insert | `CategoryChanged` |
| UpdateCategory | `PUT /api/v1/admin/categories/{id}` (IM) | category, ancestors | CAT-R2 | update | `CategoryChanged` |
| CreateProduct | `POST /api/v1/admin/products` | category, slug exists? | CAT-R1, CAT-R19 | insert DRAFT | `ProductUpdated` |
| UpdateProduct | `PUT /api/v1/admin/products/{id}` (IM) | product aggregate | CAT-R1 (slug lock), CAT-R19 | update | `ProductUpdated` |
| PublishProduct | `POST …/products/{id}/publish` (IM) | product aggregate + category | CAT-R3 | status ACTIVE, `published_at` | `ProductPublished` |
| ArchiveProduct | `POST …/products/{id}/archive` (IM) | product | DRAFT/ACTIVE | status ARCHIVED | `ProductArchived` |
| AddVariant | `POST …/products/{id}/variants` (IM product) | product aggregate, sku exists? | CAT-R4–R8 | insert variant + preparations | `VariantActivated` |
| UpdateVariant | `PUT …/variants/{variantId}` (IM product) | product aggregate | CAT-R3–R8 | update variant, replace preparations | `VariantPreparationsChanged`, `VariantDeactivated` as applicable |
| UploadImage | `POST …/products/{id}/images` (multipart) | product, image count | CAT-R16 | (upload objects via `IFileStorage` first) insert image row | compensate on failure (CAT-R17) |
| ReorderImages | `PUT …/images/order` (IM) | product images | ids = exactly current set | update `sort_order` | `ProductUpdated` |
| RemoveImage | `DELETE …/images/{imageId}` (IM) | product images | ACTIVE product keeps ≥1 image | delete row | `ProductImageRemoved` |
| Browse (query) | `GET /api/v1/products`, `/products/{slug}`, `/categories`, `/home`, `/search/suggest` | one SQL per request (Q1) | CAT-R10–R15 | — | — |

## 6. Test scenarios

### Unit (domain)

| # | Scenario | Expect |
|---|---|---|
| U1 | Publish without image / without active variant / variant without default prep | `VALIDATION_FAILED` listing all three keys |
| U2 | Two default preparations on one variant | `DEFAULT_PREPARATION_REQUIRED` |
| U3 | `yieldBp` 0, 10001; PIECE variant with 7000 | `YIELD_INVALID` / `VALIDATION_FAILED` |
| U4 | KG variant with stock unit PIECE; PACK without `packSizeGrams` | `VALIDATION_FAILED` |
| U5 | Quantity 600 g with step 250; 4 PIECE on KG variant; 100 g below min 250 | `QUANTITY_STEP_INVALID`, `QUANTITY_UNIT_INVALID`, `QUANTITY_BELOW_MIN` |
| U6 | Deactivate the only active variant of ACTIVE product | refused |
| U7 | Change slug after publish | refused (`VALIDATION_FAILED` on `slug`) |
| U8 | Category third level / cycle | `VALIDATION_FAILED` |
| U9 | Archive ACTIVE, restore ARCHIVED → DRAFT, ACTIVE → DRAFT | ok, ok, `INVALID_STATUS_TRANSITION` |

### Integration (Postgres, Testcontainers; `LocalDiskFileStorage` on a temp directory, D21)

| # | Scenario | Expect |
|---|---|---|
| I1 | Search "vanjaram" (local name), "seer", "vanjiram" (typo), "prawn" (prefix of prawns) | Seer Fish first for 1–3; Tiger Prawns for 4 |
| I2 | List at location: product without price / without stock item / out of stock / ARCHIVED | excluded / excluded / listed last as `OUT_OF_STOCK` / excluded |
| I3 | Same list without pincode | prices null, `locationRequired: true` |
| I4 | `minPricePaise` without location | 422 `LOCATION_REQUIRED` |
| I5 | Sort `price` with mixed variants | ordered by `fromPrice`, stable by `id` across pages |
| I6 | Duplicate slug / sku concurrently | one wins, other `SLUG_TAKEN` / `SKU_TAKEN` (unique index) |
| I7 | Upload 6 MB PNG; text file renamed `.jpg` | `IMAGE_INVALID` both, no objects stored |
| I8 | Upload succeeds then DB commit fails (forced) | objects deleted |
| I9 | Remove image → handler run | MinIO objects gone (Testcontainers MinIO) |
| I10 | Update variant with stale `If-Match` | 412; missing → 428 |
| I11 | Browse SQL count | 1 statement for list, 1 for detail |

## 7. Open questions (with proposed answers)

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Browse needs prices and stock, but Catalog may not use Pricing or Inventory (they use Catalog). | Catalog `Reads/` runs one read-only SQL joining `pricing.variant_prices` (current period), `pricing.preparation_charges` and `inventory.stock_items`; allowed by an architecture-test allowlist for `Reads` only (like Reporting). Alternative: a `catalog.variant_offers` projection fed by Pricing/Inventory events plus a job at price `validFrom` — more moving parts, risk of stale prices. |
| Q2 | `/home` "featured products" has no column. | Add `products.featured_rank int null` (null = not featured); needs a DB doc change. |
| Q3 | `local_names` type and trigram index. | `text[]`; trigram GIN on `immutable_array_to_string(local_names, ' ')` (wrapper function marked IMMUTABLE). |
| Q4 | RestoreProduct (ARCHIVED → DRAFT) is not in `docs/api`. | Add `POST /admin/products/{id}/restore` (catalog.manage, IM). |
| Q5 | Where does the quantity check (CAT-R7) live so Cart and Ordering use one rule? | A `QuantityRules` value in SharedKernel built from `GetVariantsAsync` data; `.Public` stays records only. |
| Q6 | Image processing library. | SkiaSharp (MIT); not ImageSharp (split licence). |
| Q7 | Tax category codes are validated where? | Catalog checks format only; Pricing refuses a price when no tax rule exists (PRC-R9), so an unknown code can never sell. |
| Q8 | `-newest` sort and PublishProduct need a publish time; no column. | Add `products.published_at timestamptz null` (set on first publish); needs a DB doc change. |

**Implementation notes (Phase 2).** Q1: done as SQL views (`catalog.storefront_offers`, `storefront_preparation_charges`, `storefront_locations`) read by `Reads/` only. Q2, Q3, Q4, Q6, Q8: done as proposed. Q5: `QuantityRules` lives in `Catalog.Domain` for now; moving it to SharedKernel is a manager decision. Images: EXIF orientation is applied before resizing; shortest side must be ≥ 800 px; an orphan-image cleanup job (`catalog.cleanup-orphan-images`, 03:30 daily) deletes storage prefixes with no `product_images` row. Banners on `/home` stay empty until Promotions ships; `isWishlisted` is null until Customers exposes wishlists.
