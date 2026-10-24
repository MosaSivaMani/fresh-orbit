# Pricing module

Owner: E2. Schema `pricing`. Uses `Catalog.Public`, `Locations.Public`. Endpoints: `docs/api/README.md` §4.12;
pricing contract for cart/checkout: §5. Error codes: §2 (Pricing, Common).

## 1. Aggregates and boundaries

| Aggregate | Key | Tables | Version for If-Match |
|---|---|---|---|
| VariantPriceSchedule | (fulfillment location, variant) | `variant_prices` rows (periods) | `xmin` of the latest period row; none → `If-None-Match: *` |
| PreparationChargeSchedule | (location, variant, preparation method) | `preparation_charges` rows | same pattern |
| TaxRuleSchedule | tax category code | `tax_rules` rows | same pattern |

| Boundary rule |
|---|
| A schedule has no root table; the command loads all periods for its key, decides, and saves changed/new rows in one `SaveChangesAsync`. The `EXCLUDE` gist constraint (no overlapping periods) is the last line. |
| Pricing owns the line formula (PRC-R6–R8). Cart, Ordering and Catalog never re-implement it; they call `IPricingApi`. |
| Pricing does not store prices on carts or orders. Ordering snapshots the returned values at placement. |
| Delivery fees are Locations configuration; Pricing only computes the tax contained in them (PRC-R10). |
| Coupons and discounts belong to Promotions. |

### Public API (`Pricing.Public`)

| Member | Returns | Used by |
|---|---|---|
| `GetOffersAsync(locationId, variantIds, at)` | per variant: `unitPricePaise`, `compareAtPaise`, `priceUnit`, charges per preparation (`chargePaise`, `chargeBasis`), `taxRateBp`; or per-variant error (`PRICE_NOT_AVAILABLE`, `PREPARATION_CHARGE_NOT_AVAILABLE`, `TAX_RULE_MISSING`). One query per call, never per variant. | Cart, Ordering (checkout), Customers (wishlist) |
| `CalculateLine(LineQuoteInput)` | `itemPaise`, `preparationChargePaise`, `lineTotalPaise`, `taxPaise`, `maxLineTotalPaise` | Cart, Ordering (preview, place, weigh-pack with snapshot values) — pure, no I/O |
| `ContainedTax(amountPaise, rateBp)` | `taxPaise` | Ordering (invoice), delivery fee tax |
| `GetTaxRatesAsync(categoryCodes, at)` | rate per code | Ordering |
| Implements `ICityActivationCheck` (Locations) | `PRICE_MISSING` items | Locations |

## 2. States

Price, charge and tax periods have no status column. State is derived from time:

| Derived state | Condition (`t` = now from `IClock`) | Editable |
|---|---|---|
| PAST | `valid_to ≤ t` | never |
| CURRENT | `valid_from ≤ t < coalesce(valid_to, ∞)` | only `valid_to` (closing) |
| FUTURE | `valid_from > t` | replaceable (never effective) |

| Transition | Command / endpoint |
|---|---|
| none → CURRENT/FUTURE | SetPrice `PUT …/price` with `If-None-Match: *` |
| CURRENT → PAST + new CURRENT/FUTURE | SetPrice with `If-Match` |
| CURRENT → PAST (stop selling) | EndPrice `DELETE …/price` |
| FUTURE → removed | SetPrice with `replaceFuture=true` (PRC-R4) |
| FUTURE → CURRENT | time passes; no write, no event |

## 3. Business rules

| Key | Rule |
|---|---|
| PRC-R1 | **Price per fulfillment location.** No CURRENT price for (location, variant) = not for sale there (`PRICE_NOT_AVAILABLE`). No fallback to another location, city or default. |
| PRC-R2 | **Periods.** Half-open `[valid_from, valid_to)`, `valid_to` null = open-ended. No overlap per key (EXCLUDE gist). Lookup at instant `t`: `valid_from ≤ t < coalesce(valid_to, 'infinity')`. One `t` per command. |
| PRC-R3 | **Set price at `validFrom` (T).** T omitted = now. T < now − 60 s → `PRICE_IN_PAST`; T within the last 60 s → now. T ≤ now + 90 days. The period containing T gets `valid_to = T`; a new open-ended period starts at T. |
| PRC-R4 | FUTURE periods starting after T → 409 `PRICE_PERIOD_OVERLAP` unless `replaceFuture=true`, which deletes them (never effective; audited). |
| PRC-R5 | Values: `unitPricePaise` 1–5 000 000 (≤ ₹50 000 per unit, typo guard); `compareAtPaise` null or > `unitPricePaise`. Same values as the CURRENT period and T = now → no write, 200 unchanged. Unit = variant sell unit: per KG, per PIECE, per PACK. |
| PRC-R6 | **Item amount.** KG: `roundHalfUp(grams × unitPricePaise / 1000)`. PIECE / PACK: `count × unitPricePaise`. |
| PRC-R7 | **Preparation charge basis.** `PER_KG` (KG variants only): `roundHalfUp(grams × chargePaise / 1000)`. `PER_ITEM` (PIECE / PACK only): `count × chargePaise`. `FLAT`: `chargePaise` once per line. Invalid basis for the unit → `VALIDATION_FAILED`. Preparation must be enabled on the variant (`PREPARATION_NOT_AVAILABLE`). |
| PRC-R8 | **Every enabled preparation needs an explicit charge row** at the location, 0 allowed; missing → `PREPARATION_CHARGE_NOT_AVAILABLE` for that preparation (the variant stays sellable with other preparations). |
| PRC-R9 | **Tax lookup.** Rate = CURRENT `tax_rules` row for the product's `taxCategoryCode` at `t`; `rateBp` 0–2800. Missing → `TAX_RULE_MISSING`, not for sale. Setting a price is refused when no tax rule covers T. Rates are data confirmed by the CA. |
| PRC-R10 | **Tax-inclusive prices.** Line total includes GST. `taxPaise = roundHalfUp(total × rateBp / (10000 + rateBp))`, per line; delivery fee tax uses category `DELIVERY_SERVICE` (Q2). Totals sum line values; never recompute from the grand total. |
| PRC-R11 | **Max line total** = PRC-R6/R7 with `maxChargeableQuantity` (ordered × (10000 + `maxOverweightBp`) / 10000, floored, KG only; otherwise ordered). Weigh-pack uses the order's snapshot price, charge, basis and tax rate, never current prices. |
| PRC-R12 | **City readiness** (`ICityActivationCheck`): for each location, ≥1 variant that is active on an ACTIVE product and has a CURRENT price, a CURRENT charge for its default preparation and a CURRENT tax rule; else `PRICE_MISSING` for that location. |
| PRC-R13 | **Bulk upload** (`POST /admin/pricing/prices/bulk`, `Idempotency-Key`): JSON or CSV (`text/csv`), ≤ 5 000 rows. Row kinds `PRICE` (location_code, sku, unit_price_paise, compare_at_paise) and `PREP_CHARGE` (location_code, sku, preparation_code, charge_paise, charge_basis). One `validFrom` per file (optional per row), `replaceFuture` per file. Every row validated with PRC-R3–R9; duplicates (same key twice) refused. All-or-nothing: any error → 422 `VALIDATION_FAILED` with `errors` keyed `rows[i].field`, nothing written. `dryRun=true` returns the diff (current → new, change in bp) without writing. Unchanged rows reported, not written. |
| PRC-R14 | Price changes > 50% up or down vs CURRENT are allowed but flagged `largeChange: true` in responses and dry-run (Q3). |
| PRC-R15 | Variant may be priced while its product is DRAFT (prepare before publish); ARCHIVED products refused (`PRODUCT_NOT_AVAILABLE`). Location may be DRAFT. |
| PRC-R16 | Stale `If-Match` → 412; a concurrent writer on the same key → EXCLUDE violation or row version → 409 `CONCURRENT_SAVE`. |

### Worked example (Seer fish, steak, 500 g, CHN-TVM)

| Step | Value |
|---|---|
| unit price | 129 900 paise / KG |
| item | 500 × 129 900 / 1000 = 64 950 |
| STEAK charge PER_KG 4 000 | 500 × 4 000 / 1000 = 2 000 |
| line total | 66 950 |
| tax at 0 bp | 0 (at 500 bp it would be 66 950 × 500 / 10 500 = 3 188) |
| max (550 g) | 71 445 + 2 200 = 73 645 |

## 4. Events

| Event (raised) | Payload | Consumers |
|---|---|---|
| `VariantPriceScheduled` | locationId, variantId, previous {unitPricePaise, compareAtPaise}, new {…}, validFrom | Audit, Customers (wishlist price drop, later) |
| `VariantPriceEnded` | locationId, variantId, endedAt | Audit |
| `PreparationChargeScheduled` | locationId, variantId, preparationCode, chargePaise, chargeBasis, validFrom | Audit |
| `TaxRuleScheduled` | taxCategoryCode, rateBp, validFrom | Audit |
| `BulkPricesApplied` | uploadId, rowCount, changedKeys[] | Audit (one entry + row details) |

Consumed: none required. (If Catalog chooses the projection option, catalog.md Q1, Catalog consumes `VariantPriceScheduled`/`Ended`.)

## 5. Commands (Read → Decide → Commit → Follow-on)

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| SetPrice | `PUT /api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price` (IM / If-None-Match) | periods for key; variant + product (Catalog.Public); location (Locations.Public); tax rules for category | PRC-R3–R5, R9, R15, version | close containing period, delete replaced futures, insert new | `VariantPriceScheduled` |
| EndPrice | `DELETE …/price` (IM) | periods for key | a CURRENT period exists | `valid_to = now` | `VariantPriceEnded` |
| SetPreparationCharge | `PUT …/preparations/{code}/charge` (IM / If-None-Match) | periods for key; variant preparations | PRC-R3, R4, R7 | as SetPrice | `PreparationChargeScheduled` |
| ScheduleTaxRule | `POST /api/v1/admin/pricing/tax-rules` | periods for category | `tax.manage` (D35: ADMIN, FINANCE; endpoint policy plus `PricingRules.EnsureCanManageTax` in the service, 403 `FORBIDDEN`), `rateBp` 0–2800, PRC-R3/R4 | close + insert | `TaxRuleScheduled` |
| BulkSetPrices | `POST /api/v1/admin/pricing/prices/bulk` (IK) | all locations by code, variants by sku (one query each), all periods for the keys (one query), tax rules | PRC-R13 per row | one save for all rows | `BulkPricesApplied` |
| ListPrices (query) | `GET /api/v1/admin/pricing/prices` (`missingOnly`) | active variants × locations left-joined to CURRENT/FUTURE periods | — | — | — |
| GetOffers / CalculateLine (Public) | used by cart, preview, place order, pack | one query per call | PRC-R1–R11 | — | — |

## 6. Test scenarios

### Unit (domain, pure)

| # | Scenario | Expect |
|---|---|---|
| U1 | Item amounts: 500 g × 129 900; 750 g × 89 900 (= 67 425); 333 g × 129 900 (43 256.7 → 43 257); 2 PIECE × 65 000 | exact paise, half-up |
| U2 | Charges PER_KG / PER_ITEM / FLAT; PER_KG on PIECE variant | values; `VALIDATION_FAILED` |
| U3 | Contained tax 66 950 at 500 bp; 0 bp; 1 paise at 500 bp | 3 188; 0; 0 |
| U4 | Max line total at +10% for KG, PIECE unchanged | 73 645; 130 000 |
| U5 | Set price at T inside current period | current closed at T, new open-ended |
| U6 | T in past (> 60 s); T 30 s ago; T beyond 90 days | `PRICE_IN_PAST`; treated as now; `VALIDATION_FAILED` |
| U7 | Future period exists after T, without / with `replaceFuture` | `PRICE_PERIOD_OVERLAP`; future removed |
| U8 | `compareAtPaise` ≤ unit price; unit price 0 | `VALIDATION_FAILED` |
| U9 | Same price again | unchanged, no new row, no event |

### Integration (Postgres, Testcontainers)

| # | Scenario | Expect |
|---|---|---|
| I1 | Two concurrent SetPrice on same key with same `If-Match` | one 200, other 409 `CONCURRENT_SAVE`; never overlapping rows |
| I2 | Direct insert of overlapping period | EXCLUDE violation |
| I3 | `GetOffersAsync` for 30 variants | 1 SQL statement; missing price, missing charge, missing tax each reported per variant |
| I4 | Lookup at exact boundary `t = valid_to` of old / `valid_from` of new | new price |
| I5 | Price for a location with no tax rule for category | `TAX_RULE_MISSING` |
| I6 | Bulk 3 rows, row 2 invalid | 422 `rows[1].unitPricePaise`, nothing written |
| I7 | Bulk replay with same `Idempotency-Key` | same response, `Idempotent-Replayed: true`, no new rows |
| I8 | Bulk `dryRun=true` | diff returned, no rows, no outbox |
| I9 | Readiness check with priced variant lacking default-preparation charge | `PRICE_MISSING` |
| I10 | Stale `If-Match` / missing `If-Match` with existing price | 412 / 428 |

## 7. Open questions (with proposed answers)

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Coupon discount and invoice tax: tax on pre- or post-discount value? | Post-discount: Ordering allocates the discount across eligible lines pro rata (largest remainder to the paisa) and recomputes contained tax per line with `ContainedTax`; CA to confirm. |
| Q2 | Delivery fee GST category and rate. | Tax category `DELIVERY_SERVICE` in `tax_rules` (rate from CA, e.g. 1800 bp); no rule → fee tax 0 is NOT assumed: checkout refused with `TAX_RULE_MISSING`. |
| Q3 | Should large price changes require confirmation? | Not in v1 (flag only, PRC-R14). Later: `confirmLargeChange=true` + new code `PRICE_CHANGE_UNCONFIRMED`. |
| Q4 | Default preparation charge per method to reduce setup rows? | No; explicit per location (configuration is data). Bulk upload covers setup. |
| Q5 | Should a price also be required per preparation (fillet priced differently)? | No; preparation differences go through the charge (PRC-R7). |
| Q6 | Price history visible to customers (strike-through)? | Only `compareAtPaise`; no history endpoint for customers. |

**Implementation notes (Phase 2).** `validFrom` within 60 s in the past is treated as now; > 90 days ahead is refused. Repeating the current values (or the identical already-scheduled future period) is a no-op. Bulk upload: JSON or CSV, max 5000 rows, all-or-nothing, `dryRun`. Tax rules are set without `If-Match`. `CalculateLine`/`ContainedTax` belong to the checkout pricing contract (D20, Cart/Ordering owner). Pricing registers an `ICityActivationCheck` that reports `PRICE_MISSING` per active fulfillment location of the city.
