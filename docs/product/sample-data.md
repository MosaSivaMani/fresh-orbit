# Sample data

Owner: E4. Files: `backend/data/sample/*.json`, validator `backend/data/sample/tools/validate.py`.
Rules: CLAUDE.md rule 16, `docs/database/README.md` §5, loader contracts in
`backend/src/SharedKernel/FreshOrbit.SharedKernel.EntityFramework/Seeding/`.

Sample data is a fictional demo store: Fresh Orbit in Visakhapatnam (one location, three service areas). It exists
for demos, manual testing and screenshots. It is not reference data, never loaded in Production, and removable with
`--purge-sample`.

## 1. What is in it

| File | Module | Contents | Shape follows |
|---|---|---|---|
| `sample-set.json` | — | Set name, time-anchor rule, key rule, load order | — |
| `locations.json` | Locations | 1 city `VISAKHAPATNAM` (AP, `Asia/Kolkata`); 1 location `VSKP-MVP` with cold storage and a placeholder FSSAI number; 7-day operating hours; 1 closure 10 days ahead; 3 service areas (Central, North, South-West) with 20 pincodes and different fee / free-delivery / minimum-order values | `POST /admin/cities`, `/fulfillment-locations`, `PUT …/operating-hours`, `POST …/closures`, `/service-areas`, `…/pincodes` |
| `catalog.json` | Catalog | 11 categories (9 top-level, as on the owner's website reference D188: Fish, Prawns & Shrimp, Crabs, Shellfish, Fillets & Steaks, Ready to Cook, Marinated, Fish Eggs, Ready to Eat; Fish has Sea Fish and Freshwater Fish children; Marinated, Fish Eggs and Ready to Eat have no products yet); 26 products, 28 variants (22 KG, 2 PIECE, 4 PACK), 67 variant preparations with yields ; every variant states `serves` and 21 a `piecesMin`/`piecesMax` range per serving basis (CAT-R21, demo values) | `POST /admin/categories`, `/products`, `…/variants` (`ProductRequest`, `VariantRequest`) |
| `pricing.json` | Pricing | 4 tax rules (placeholder rates, "confirm with CA"); 28 prices and 67 preparation charges for `VSKP-MVP` | `POST /admin/pricing/tax-rules`; `POST /admin/pricing/prices/bulk` (the `bulk` object is that request body) |
| `inventory.json` | Inventory | 28 stock items with low-stock thresholds; 29 receipts (batches with total cost, best-before, receipt temperature); 2 cold-chain readings | `POST /admin/inventory/stock-items`, `/receipts`, `/cold-chain` |
| `delivery.json` | Delivery | 21 slot templates (3 per day × 7 days: 07–09, 11–13, 17–19); 3 riders | `POST /admin/delivery/slot-templates`, `/riders` |
| `promotions.json` | Promotions | Coupons `WELCOME100` (₹100 off first order ≥ ₹499) and `PRAWNFEST10` (10 % off prawns, cap ₹150, category scope); 2 home banners | `POST /admin/coupons` (`CouponRequest`) and `POST /admin/promotions` (`PromotionRequest`); checked against the Phase 3 records at the `integration/sec-sample` merge |
| `customers.json` | Customers (+ Identity users) | 3 demo customers, 5 addresses (one deliberately outside the service areas) | OTP-verify user creation, `PATCH /me`, `POST /me/addresses` (`SaveAddressRequest`) |
| `settings.json` | Settings | Business profile (trade name Fresh Orbit, fake GSTIN, fake support phone and email, `Asia/Kolkata`), loaded only when no profile exists | `PUT /admin/settings/business-profile` (`SaveBusinessProfileRequest`) |
| `identity.json` | Identity | 2 staff: `sample.admin@freshorbit.example` (ADMIN), `sample.finance@freshorbit.example` (FINANCE); no passwords | `POST /admin/staff` (`CreateStaffRequest` without `temporaryPassword`) |
| `images.json` | Catalog / Promotions | Per product: the alt text of each real picture (`pictures[].number`, `altText`) and a placeholder with its own alt text; one placeholder per category and banner | `POST …/products/{id}/images` (committed picture, else rendered; never downloaded) |
| `images/` | Catalog / Promotions | Real pictures (WebP): `categories/fish`, `prawns-shrimp`, `crabs`, `shellfish`, `fillets-steaks`, `ready-to-cook`, `fish-eggs`, `ready-to-eat` (512², the last four D294; `marinated` is still drawn) and `banners/welcome`, `prawn-week` (1200×600), made from the owner's pictures in `docs/design/characters/generated` by `tools/make_images.py` on the CI runner (workflow `sample-images.yml`); product pictures `products/<slug>/<n>.webp` (n = 1–4, 1280 px, D249) made the same way from `docs/design/products/<slug>/<n>.png\|jpg` | Stored as the file of image key `sample/<path>` (`SampleImageFiles`); a key without a file is drawn. Product pictures are uploaded through `ProductImageService` in number order (`SampleProductPictures`) |

Product list (scientific names and local names are drafts for the owner to verify):

| Category | Products |
|---|---|
| Sea Fish | Seer Fish (Vanjaram), Silver Pomfret (KG and single large PIECE), Yellowfin Tuna, Indian Mackerel (Bangda), Oil Sardine (Mathi), Red Snapper, Barramundi (Bhetki), Anchovy (Nethili), Ribbon Fish, Indian Halibut, Tongue Sole |
| Freshwater Fish | Rohu, Katla |
| Prawns & Shrimp | Tiger Prawns (Large and Jumbo), White Prawns, Scampi |
| Crabs | Mud Crab, Blue Swimmer Crab |
| Shellfish | Squid, Green Mussels, Clams, Spiny Lobster (PIECE) |
| Fillets & Steaks | Basa Fillet (frozen PACK), Atlantic Salmon Fillet (frozen PACK), Seer Fish Steaks (chilled PACK) |
| Ready to Cook | Andhra Fry Marinated Prawns (chilled PACK) |

Every record carries `sampleNote` starting with `[SAMPLE]`. Customer-visible fields (names, descriptions, coupon
descriptions, banner text, alt text) never contain the marker; the validator enforces this. Fields that are not part
of any request body (`sampleNote`, `localNameLanguages`, `hsnNote`, `rateIsPlaceholder`, `targetStatus`, `stockUnit`,
`isWeightVariable`, `key`, `*Code` / `*Slug` references, `*Offset*`) are loader inputs or assertions and are never
sent to the API as such.

### Fake contact data

| Kind | Pattern | Why |
|---|---|---|
| Phones (customers, riders, location, support) | `+91 99999 00NNN` (`+919999900000`–`+919999900999`) | **Reserved fake block (S-D3).** Mobile-shaped, so it passes our E.164 regex and any later Indian-mobile check (`+91[6-9]…`). Allocation: `1NN` store and support, `2NN` riders, `3NN` customers. India has no officially reserved fictional mobile range, so this block *could* be assigned to a real subscriber; no environment may send real SMS or calls to it (dev and staging use the fake SMS sender). Never use numbers from this block for real people, and never use any other number in sample data |
| Emails | `…@customer.example`, `sample.…@freshorbit.example` | `.example` is reserved (RFC 2606); mail goes nowhere, and Mailpit catches it in development |
| FSSAI number | `SAMPLE-FSSAI-0001` on the location; null on the business profile | Location field is free text ≤ 20 chars; the profile accepts only 14 digits, so it is left empty rather than faked |
| GSTIN | `37AAAAA0000A1Z1` | State 37 (AP), placeholder PAN `AAAAA0000A`, entity 1, valid check character, so it passes `Gstins.IsValid`; not a registered GSTIN |
| Suppliers | `Sample Supplier A…D` | No real business named; the landing centre is the public harbour name only |
| Addresses | `Sample Residency`, `Sample Towers`, plot 0 | Real localities and pincodes, fictional buildings |

## 2. Keys: natural keys, ids generated at load (decided here, S-D1)

Records refer to each other by **natural keys**: city `code`, location `code`, service-area `key`, category and
product `slug`, variant `sku`, preparation `code`, tax category `code`, coupon `code`, banner/rider/customer/staff
`key`. Database ids are **not** in the files; each loader creates rows with new UUIDv7 ids (as the services do) and
records every id in `platform.sample_records`.

| Option | For | Against |
|---|---|---|
| **Natural keys (chosen)** | Works for both loader styles (direct insert or replay through services, which generate their own ids); files are readable and reviewable; every key is already unique in the schema (`code`, `slug`, `sku`, coupon `code`) or scoped (`batch_code` per location); idempotent re-load = "skip when the natural key exists" | A loader needs a key → id map while it runs; a few entities (service areas, riders, banners, customers, staff) need a file-only `key`, matched on reload by a unique column (rider phone, customer phone, staff email) or by name within its parent |
| Fixed UUIDv7 strings | Same ids in every environment; tests and docs could quote them | Cannot be used when replaying through services (ids are generated inside); hand-made "v7" ids carry fake timestamps; unreadable in review |

## 3. Time anchor

Sample data must be valid whenever it is loaded, so no file contains an absolute date or time.

| Field pattern | Meaning |
|---|---|
| `…OffsetHours` | Hours from the load instant `T0` (UTC), e.g. `bestBeforeOffsetHours: 42` → `T0 + 42 h` |
| `…OffsetDays` | Days from the city-local date of `T0` (`Asia/Kolkata`), e.g. `closedOnOffsetDays: 10` |
| `validFrom: null` (prices) | `T0`, as the API treats an omitted `validFrom` |

Every loader uses one `T0` from `IClock` for the whole run.

## 4. Load order

One `ISampleDataLoader` per module (`Order` values leave gaps for later modules). Each loader saves its own changes
(contract), runs only with `--sample`, and the host refuses `--sample` in Production before any loader runs.

| Order | Loader (module) | Reads | Needs | Writes and tracks (`platform.sample_records`) |
|---|---|---|---|---|
| 5 | `SettingsSampleDataLoader` | `settings.json` | — | `settings.business_profile` only when no row exists (never replaces a real profile); invoices need its GSTIN (ORD-R52) |
| 10 | `LocationsSampleDataLoader` | `locations.json` | reference data | `locations.cities`, `fulfillment_locations`, `service_areas`, `pincodes`, `location_closures` (operating hours cascade with the location). City and location stay **DRAFT** |
| 20 | `IdentitySampleDataLoader` | `identity.json`, phones from `customers.json` | roles, permissions; locations | `identity.users` (staff and customer users), `identity.staff_members` (roles and location access cascade). Customer users are created through the same path as the first OTP verify (user ACTIVE, `phone_verified_at = T0`, `ICustomerAccountDirectory` creates the customer row) |
| 30 | `CatalogSampleDataLoader` | `catalog.json`, `images.json` | preparation methods | `catalog.categories`, `products`, `product_variants`, `product_images` (variant preparations cascade). Stores the committed picture of a category or banner key (`images/<path>` for `sample/<path>`) or else renders the placeholder (SkiaSharp, already the Catalog image library), uploads through `IFileStorage`, then publishes products with `targetStatus: ACTIVE` |
| 40 | `PricingSampleDataLoader` | `pricing.json` | locations, catalog, tax categories | `pricing.tax_rules` first (a price is refused without a tax rule, PRC-R9), then the `bulk` body through the bulk-price service: `variant_prices`, `preparation_charges` |
| 50 | `InventorySampleDataLoader` | `inventory.json` | locations, catalog, storage units | `inventory.stock_items`, `batches`, `stock_movements` (RECEIPT), `cold_chain_logs` (including the out-of-range log the warm sardine receipt writes) |
| 60 | `DeliverySampleDataLoader` | `delivery.json` | locations | `delivery.slot_templates`, `riders` |
| 70 | `CustomersSampleDataLoader` | `customers.json` | identity (customer rows exist) | Profile fields on `customers.customers`; `customers.addresses` |
| 80 | `PromotionsSampleDataLoader` | `promotions.json` | locations, catalog | `promotions.coupons` (scopes cascade), `promotions.promotions` |
| 90 | `LocationsActivationSampleDataLoader` | `locations.json` (`targetStatus`) | pricing, inventory, delivery | Location → ACTIVE, then city → ACTIVE through the normal commands, so the LOC-R6 readiness check proves the set is complete |
| 95 | `DeliverySlotsSampleDataLoader` | — | active location and city | Slots of the next 7 days through `SlotGenerationService` (as the daily job does), so the store is orderable at once. Slots are **not** tracked: they are generated rows, removed by the Delivery purge participant (§5) |

### Exact loader work for the backend team

Each module owner adds one class in `<Module>.Infrastructure/Seeding/` implementing `ISampleDataLoader` and registers it
in the module's DI setup (as the reference-data loaders are):

1. Read and deserialize its file from `SampleDataContext.DirectoryPath` into private `sealed record`s (camelCase JSON,
   enums as UPPER_SNAKE codes; `operatingHours.days[].dayOfWeek` is `MONDAY`…`SUNDAY`, slot templates use ISO
   1–7 as the API does). Ignore `sampleNote` and other annotation fields.
2. Resolve natural keys to ids in **one query per entity type** (rule 5: no database call in a loop), using the owning
   module's own tables or another module's `.Public` read members (`ILocationsApi`, `ICatalogApi.GetVariantsAsync`,
   …). A missing key fails the run with the key named; never skip silently (rule 7).
3. Idempotency: a record whose natural key already exists is skipped (not updated), so `--sample` can run twice.
   Exception: Pricing skips a tax category that already has a CURRENT rule, so sample rates never replace real ones.
4. Create rows through the module's Application services or domain factories with a SYSTEM actor, so every rule in
   `docs/product/<module>.md` runs. Writes that only exist as commands with `If-Match` (activation, publish) read the
   fresh version first.
5. Call `context.Tracker.Track("<schema>.<table>", id)` for **every** inserted row that has an `id` column, including
   children created inside a service (pincodes, RECEIPT movements, cold-chain logs, product images). When a service
   returns only the root id, read the children's ids in one query by parent after the save and track them.
6. Save once per loader (or once per aggregate when the service saves itself; then track in the same save).
7. Log inserted / skipped counts per entity type.
8. Integration test per loader (Testcontainers): load twice → second run inserts nothing; every inserted row has a
   `sample_records` row; `SampleDataPurger.PurgeAsync` afterwards leaves the module's tables as before.
9. One host test: `--sample` with `ASPNETCORE_ENVIRONMENT=Production` exits non-zero before touching the database.

Module notes:

| Module | Note |
|---|---|
| Identity | Staff password: from `FRESHORBIT_SAMPLE_STAFF_PASSWORD` (≥ 12 chars, IDN-R13); when absent, generate a random one and print it once to the console. Never written to a file. A sample ADMIN makes `--create-admin` refuse (`ADMIN_ALREADY_EXISTS`) until purged — acceptable for dev and staging |
| Catalog | `targetStatus: ACTIVE` → publish after variants and the image exist (CAT-R3). Placeholder (as built, replacing the `background`/`label` style of `images.json`): soft sand gradient, a pale teal disc and a simple silhouette in brand teal (`#0E9384`/`#0B7D70`) — a prawn for prawns, scampi, lobster and prawn banners, a crab for crabs, else a fish; **no text** in the image (the product name is shown by the UI). Product images are the committed pictures `images/products/<slug>/<n>.webp` with their `images.json` alt text, else one placeholder PNG, uploaded through `ProductImageService` (normal CAT-R16 renditions, D249); category and banner images are WebP files written to `IFileStorage` at the `imageKey` of `images.json` (banners only when the file is missing) |
| Pricing | Use the bulk service with the file's `bulk` object as-is (all-or-nothing, PRC-R13); `validFrom: null` = `T0` |
| Inventory | `receivedAtOffsetHours` is informational if the receipt service stamps `received_at = now`; `caughtOrHarvestedOn` and `bestBeforeAt` come from the offsets |
| Delivery | Slots are not in the file; the generation job creates them after activation (DEL-R9) |
| Promotions | Build `CouponRequest` / `PromotionRequest` and call `CouponAdminService` / `BannerService`. The file differs from the request bodies only by natural keys and offsets: `cityCode` → `cityId`; coupon `scopes[].scopeRef` (category or product slug) → `scopeId`; banner `linkTarget` for `CATEGORY`/`PRODUCT` is a slug in the file and must become the category or product **id** (the domain refuses anything but a Guid), while a `COUPON` target stays the code; `validFromOffsetDays`/`validToOffsetDays` → `validFrom`/`validTo` (city-local date of `T0`), `startsAtOffsetHours`/`endsAtOffsetHours` → `startsAt`/`endsAt`; `key` is file-only. Coupons first, so banner coupon links resolve. The validator also checks the domain length limits (description 200, title 120, subtitle 200) |

## 5. Purge (`--purge-sample`)

`SampleDataPurger` runs in **one transaction**. It first asks every `ISamplePurgeParticipant` for blockers and refuses
(`SamplePurgeRefusedException`, host exit code 4, nothing deleted) when any exist; then participants prepare (Delivery
deletes unbooked slots of sample templates); then it deletes every `platform.sample_records` row's target by `id`,
table by table under savepoints, retrying tables that hit a foreign key until nothing is left. If no table can make
progress it rolls everything back and refuses, naming the tables and the constraint (e.g. refresh tokens of a sample
staff user who signed in). Retried deletes appear as `ERR Failed executing DbCommand` lines in the log; they are
expected.

Participants (blockers): Delivery (booked slots of sample templates), Ordering (orders at a sample location or of a
sample customer), Payments (payments of such orders), Cart (carts of sample customers or at the sample location). Children with a
composite key (operating hours, variant preparations, staff roles and location access, coupon scopes, wishlist items)
have no `id` and are removed by their `ON DELETE CASCADE` from the tracked parent.

Rows created **after** loading that point at sample rows block the purge:

| Created later by | Rows | Proposed handling (S-D2) |
|---|---|---|
| Delivery slot generation (DEL-R9) | `delivery.delivery_slots` → `slot_templates` (restrict) | Built: `DeliverySamplePurgeParticipant` deletes slots of sample templates with `booked_count = 0`; a booked one blocks the purge |
| Demo activity | carts, orders, payments, shipments, reservations, SALE movements, complaints, notifications, refresh tokens, OTP challenges | Orders, payments and movements are never deleted (database §1). Purge refuses with the list of blocking tables; to start clean, drop the development database instead |
| Outbox / audit | `platform.outbox`, `audit.entries` | No foreign keys; left in place (append-only history) |
| Image files | `products/{productId}/…` objects; `sample/categories/*`, `sample/banners/*` | Rows are deleted by the purge; the daily orphan-image job (CAT-R17) removes product files. Category and banner files stay (a reload overwrites category files and reuses banner files) |

## 6. Validation

`python3 backend/data/sample/tools/validate.py` (standard library only; exit 1 with one line per problem). It checks:

- every file parses; `sample-set.json` load order names existing files;
- every natural-key reference resolves, including against `backend/data/reference` (preparation methods, tax
  categories, roles, storage units);
- unit rules (CAT-R4, D10): KG → `GRAM` and weight-variable; PIECE → `PIECE`, not weight-variable, `avgPieceGrams`;
  PACK → `PIECE`, not weight-variable, `packSizeGrams`; yields 1–10000, WHOLE and every PIECE/PACK preparation 10000
  (CAT-R6); one default preparation per active variant (CAT-R5); quantity rules (CAT-R7);
- prices 1–5,000,000 paise and `compareAtPaise` above the price (PRC-R5); an explicit charge row for every enabled
  preparation (PRC-R8) with a basis valid for the sell unit (PRC-R7); a tax rule for every product tax category and
  for `DELIVERY_SERVICE` (PRC-R9/R10); batch cost below selling value;
- fees (LOC-R5), unique pincodes (LOC-R3), ACTIVE-location fields (LOC-R11), future closure (LOC-R14);
- slot templates on 15-minute boundaries, capacity and cutoff ranges, no overlap (DEL-R1/R2);
- coupon code, type and limit rules (PROMO-R11/R17/R19/R20), scope and banner targets (PROMO-R22/R25);
- address fields (CUS-R5), one default address (CUS-R4), fake phone and reserved-domain email patterns, no passwords;
- `sampleNote` on every record and no `[SAMPLE]` marker in customer-visible text;
- city readiness (LOC-R6): service area with pincodes, slot template, a sellable priced variant with its default
  preparation charge and tax rule.

- business profile: GSTIN passes the same format and check-character rule as `Settings.Domain` `Gstins.IsValid`,
  FSSAI null or 14 digits, fake support phone and reserved-domain email, `onlyWhenMissing` true.

Last run: `OK: 11 files; 1 city, 1 location, 3 service areas, 20 pincodes; 11 categories, 26 products, 28 variants;
28 prices, 67 preparation charges, 4 tax rules; 28 stock items, 29 batches; 21 slot templates, 3 riders; 2 coupons,
2 banners; 3 customers, 2 staff; business profile yes.` A mutated copy (wrong unit flag, PIECE yield 6800, unknown
category, price above the cap, missing charge row, duplicate pincode, broken banner link) produced exactly those 7
errors; a GSTIN with a changed check character or state code 40 is refused.

CI: the backend job in `.github/workflows/ci.yml` runs the validator (step "Validate sample data").
The validator also checks every committed picture under `images/`: it must be the file of a category or banner key in
`images.json` and stay under 400 KB, or a product picture `products/<slug>/<1-4>.webp` (WebP, under 600 KB) of a
product whose `pictures` give that number an alt text (D249). Picture alt texts must not say "placeholder".

**Demo (D160).** The demo's disk is emptied by every restart. A missing `/media/sample/...` file is answered with the
committed picture from `Seeding:SampleDataPath` (`/app/data/sample` in the demo image) when one exists for the key,
else with the placeholder drawing (`UseFreshOrbitDemoMedia`).

## 7. Decisions (manager, 2026-10-24)

Logged in `docs/decisions/log.md` as D54 (S-D1) to D61 (S-D8); all accepted.

| # | Question | Decision |
|---|---|---|
| S-D1 | Ids in sample files | Accepted: natural keys, ids generated at load (§2) |
| S-D2 | Purge with rows created after loading | Accepted: Delivery purge hook deletes unbooked slots generated from sample templates; otherwise the purge refuses (§5) |
| S-D3 | Fake phone pattern | Accepted with change: mobile-shaped block `+91 99999 00NNN` (`+919999900000`–`+919999900999`), reserved as fake in this document (§1) |
| S-D4 | Staff passwords | Accepted: environment variable or a generated password printed once; never in a file |
| S-D5 | Tax rates in sample | Accepted: placeholders (fresh 0 bp, frozen and marinated 500 bp, delivery 1800 bp) marked "confirm with CA"; not advice |
| S-D6 | Business profile | Accepted: `settings.json` with a checksum-valid fake GSTIN `37AAAAA0000A1Z1`, loaded only when no profile exists |
| S-D7 | Preparation of a pre-cut PACK (seer steaks, marinated prawns, fillets) | Proposed: the preparation code describes what is in the pack (STEAK, PEELED_DEVEINED, FILLET…), yield 10000, charge 0; RD-G2 (`display_name`) would let it read "As packed" |
| S-D8 | Validator in CI | Accepted: done in the backend job |

## 8. Implementation (L1, branch `backend/sample-loaders`)

All loaders of §4 are built in `<Module>.Infrastructure/Seeding/` and registered with `TryAddEnumerable`. They call
the Application services (validators run first with `ValidateAndThrowAsync`) under the host's SYSTEM actor; the
Customers loader builds its profile and address services over a customer context of each sample customer. The
context carries `StartedAt` (`T0`); helpers in SharedKernel `Seeding/SampleFiles.cs` (file reading, city-local
dates) and `SampleTracking.cs` (track by entity type, read tracked ids).

Public read members added for key → id resolution (one query each): `IIdentityApi.FindUserIdsByPhonesAsync`,
`ICatalogApi.GetCategoryIdsBySlugsAsync`, `ICatalogApi.GetProductIdsBySlugsAsync`, `ILocationsApi.GetCitiesByCodesAsync`.

Not supported by the loaders: staff `locationCodes` (fails the run if non-empty; the sample staff are all-location).

Host: `--sample` and `--purge-sample` are refused in Production (exit 2) before any database work; a refused purge
exits 4. In the `Demo` environment (free public demo, `runbooks/render-demo.md`) the host first claims the database:
only a completely empty database is used and marked as the demo's (`COMMENT ON DATABASE … 'FRESH_ORBIT_DEMO'`); any
other is refused with exit 5 before anything changes. The demo runs `--sample` on every start (idempotent).

Tests: `backend/tests/FreshOrbit.SampleData.Tests` (Testcontainers): load (at a fixed clock, 00:00 UTC of today =
05:30 local, before the 11:00 slot's 09:00 cut-off; with the real clock today has no slot after 15:00 local) → counts
per table and every row tracked, 7 slot dates, banner link is the category id; second load inserts nothing; `GET /api/v1/products?pincode=530017`
returns 26 priced products in stock with images; a booked slot blocks the purge and nothing is deleted; purge
leaves every sample table empty; Production refusal of both flags.

Loaded counts (dev database, 2026-10-24): 1 business profile, 1 city, 1 location, 1 closure, 3 service areas,
20 pincodes, 5 users (2 staff, 3 customers), 5 addresses, 8 categories, 26 products, 28 variants, 26 images, 4 tax
rules, 28 prices, 67 preparation charges, 28 stock items, 29 batches and RECEIPT movements, 3 cold-chain logs,
21 slot templates, 19 slots over the next 7 days (today's slots past their cutoff are not generated), 3 riders, 2 coupons, 2 banners; 346 tracked rows.
