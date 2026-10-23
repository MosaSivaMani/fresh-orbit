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
| `catalog.json` | Catalog | 8 categories (6 top-level: Fish, Prawns & Shrimp, Crabs, Shellfish, Fillets & Steaks, Ready to Cook; Fish has Sea Fish and Freshwater Fish children); 26 products, 28 variants (22 KG, 2 PIECE, 4 PACK), 67 variant preparations with yields | `POST /admin/categories`, `/products`, `…/variants` (`ProductRequest`, `VariantRequest`) |
| `pricing.json` | Pricing | 4 tax rules (placeholder rates, "confirm with CA"); 28 prices and 67 preparation charges for `VSKP-MVP` | `POST /admin/pricing/tax-rules`; `POST /admin/pricing/prices/bulk` (the `bulk` object is that request body) |
| `inventory.json` | Inventory | 28 stock items with low-stock thresholds; 29 receipts (batches with total cost, best-before, receipt temperature); 2 cold-chain readings | `POST /admin/inventory/stock-items`, `/receipts`, `/cold-chain` |
| `delivery.json` | Delivery | 21 slot templates (3 per day × 7 days: 07–09, 11–13, 17–19); 3 riders | `POST /admin/delivery/slot-templates`, `/riders` |
| `promotions.json` | Promotions | Coupons `WELCOME100` (₹100 off first order ≥ ₹499) and `PRAWNFEST10` (10 % off prawns, cap ₹150, category scope); 2 home banners | Promotions spec §5 (coupon fields in camelCase + `scopes`, `isListed`); `POST /admin/promotions` |
| `customers.json` | Customers (+ Identity users) | 3 demo customers, 5 addresses (one deliberately outside the service areas) | OTP-verify user creation, `PATCH /me`, `POST /me/addresses` (`SaveAddressRequest`) |
| `identity.json` | Identity | 2 staff: `sample.admin@freshorbit.example` (ADMIN), `sample.finance@freshorbit.example` (FINANCE); no passwords | `POST /admin/staff` (`CreateStaffRequest` without `temporaryPassword`) |
| `images.json` | Catalog / Promotions | One placeholder image description per product, category and banner | `POST …/products/{id}/images` (rendered, not downloaded) |

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
| Phones (customers, riders, location) | `+910000000NNN` | Valid E.164 for our regex (`^\+[1-9][0-9]{7,14}$`), but an Indian national number never starts with 0, so it cannot reach a real subscriber. A stricter Indian-mobile check (`[6-9]…`) would reject it — see decision S-D3 |
| Emails | `…@customer.example`, `sample.…@freshorbit.example` | `.example` is reserved (RFC 2606); mail goes nowhere, and Mailpit catches it in development |
| FSSAI number | `SAMPLE-FSSAI-0001` | Free text ≤ 20 chars; obviously not a licence |
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
| 10 | `LocationsSampleDataLoader` | `locations.json` | reference data | `locations.cities`, `fulfillment_locations`, `service_areas`, `pincodes`, `location_closures` (operating hours cascade with the location). City and location stay **DRAFT** |
| 20 | `IdentitySampleDataLoader` | `identity.json`, phones from `customers.json` | roles, permissions; locations | `identity.users` (staff and customer users), `identity.staff_members` (roles and location access cascade). Customer users are created through the same path as the first OTP verify (user ACTIVE, `phone_verified_at = T0`, `ICustomerAccountDirectory` creates the customer row) |
| 30 | `CatalogSampleDataLoader` | `catalog.json`, `images.json` | preparation methods | `catalog.categories`, `products`, `product_variants`, `product_images` (variant preparations cascade). Renders each placeholder image (SkiaSharp, already the Catalog image library), uploads through `IFileStorage`, then publishes products with `targetStatus: ACTIVE` |
| 40 | `PricingSampleDataLoader` | `pricing.json` | locations, catalog, tax categories | `pricing.tax_rules` first (a price is refused without a tax rule, PRC-R9), then the `bulk` body through the bulk-price service: `variant_prices`, `preparation_charges` |
| 50 | `InventorySampleDataLoader` | `inventory.json` | locations, catalog, storage units | `inventory.stock_items`, `batches`, `stock_movements` (RECEIPT), `cold_chain_logs` (including the out-of-range log the warm sardine receipt writes) |
| 60 | `DeliverySampleDataLoader` | `delivery.json` | locations | `delivery.slot_templates`, `riders` |
| 70 | `CustomersSampleDataLoader` | `customers.json` | identity (customer rows exist) | Profile fields on `customers.customers`; `customers.addresses` |
| 80 | `PromotionsSampleDataLoader` | `promotions.json` | locations, catalog | `promotions.coupons` (scopes cascade), `promotions.promotions` |
| 90 | `LocationsActivationSampleDataLoader` | `locations.json` (`targetStatus`) | pricing, inventory, delivery | Location → ACTIVE, then city → ACTIVE through the normal commands, so the LOC-R6 readiness check proves the set is complete |

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
| Catalog | `targetStatus: ACTIVE` → publish after variants and the image exist (CAT-R3). Placeholder: background colour, product name and the words "Sample image", 1200×1200 PNG, re-encoded by the normal upload path |
| Pricing | Use the bulk service with the file's `bulk` object as-is (all-or-nothing, PRC-R13); `validFrom: null` = `T0` |
| Inventory | `receivedAtOffsetHours` is informational if the receipt service stamps `received_at = now`; `caughtOrHarvestedOn` and `bestBeforeAt` come from the offsets |
| Delivery | Slots are not in the file; the generation job creates them after activation (DEL-R9) |
| Promotions | Promotions has no Application code yet; its loader is written with the module. Banner `linkTarget` is a category slug or coupon code; the loader converts it to whatever `link_target` stores |

## 5. Purge (`--purge-sample`)

`SampleDataPurger` deletes every `platform.sample_records` row's target by `id`, table by table, retrying tables that
hit a foreign key until nothing is left; if no table can make progress it throws and names the tables. Children with a
composite key (operating hours, variant preparations, staff roles and location access, coupon scopes, wishlist items)
have no `id` and are removed by their `ON DELETE CASCADE` from the tracked parent.

Rows created **after** loading that point at sample rows block the purge:

| Created later by | Rows | Proposed handling (S-D2) |
|---|---|---|
| Delivery slot generation (DEL-R9) | `delivery.delivery_slots` → `slot_templates` (restrict) | Delivery adds a purge hook that deletes slots of sample templates with `booked_count = 0` before the purger runs |
| Demo activity | carts, orders, payments, shipments, reservations, SALE movements, complaints, notifications, refresh tokens, OTP challenges | Orders, payments and movements are never deleted (database §1). Purge refuses with the list of blocking tables; to start clean, drop the development database instead |
| Outbox / audit | `platform.outbox`, `audit.entries` | No foreign keys; left in place (append-only history) |
| Image files | `products/{productId}/…` objects | Rows are deleted by the purge; the daily orphan-image job (CAT-R17) removes the files |

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

Last run: `OK: 10 files; 1 city, 1 location, 3 service areas, 20 pincodes; 8 categories, 26 products, 28 variants;
28 prices, 67 preparation charges, 4 tax rules; 28 stock items, 29 batches; 21 slot templates, 3 riders; 2 coupons,
2 banners; 3 customers, 2 staff.` A mutated copy (wrong unit flag, PIECE yield 6800, unknown category, price above
the cap, missing charge row, duplicate pincode, broken banner link) produced exactly those 7 errors.

Suggested CI step (manager): run the validator in the backend job.

## 7. Open decisions

| # | Question | Proposal |
|---|---|---|
| S-D1 | Ids in sample files | Natural keys, ids generated at load (§2) |
| S-D2 | Purge with rows created after loading | Delivery purge hook for unbooked slots; otherwise refuse and drop the dev database (§5) |
| S-D3 | Fake phone pattern | `+910000000NNN`; if Identity later restricts to Indian mobiles (`+91[6-9]…`), switch to `+9199999NNNNN` and accept that it is a valid-looking number |
| S-D4 | Staff passwords | Environment variable or a generated password printed once; never in a file |
| S-D5 | Tax rates in sample | Placeholders (fresh 0 bp, frozen and marinated 500 bp, delivery 1800 bp) marked "confirm with CA"; they are not advice |
| S-D6 | Business profile and order policy | Not in the sample set: invoices need a business profile (`CONFIGURATION_MISSING` without it). Add a `settings.json` with an obviously fake GSTIN, or have the tester enter one in admin |
| S-D7 | Preparation of a pre-cut PACK (seer steaks, marinated prawns, fillets) | The preparation code describes what is in the pack (STEAK, PEELED_DEVEINED, FILLET…), yield 10000, charge 0; RD-G2 (`display_name`) would let it read "As packed" |
| S-D8 | Validator in CI | Add `python3 backend/data/sample/tools/validate.py` to the backend CI job |
