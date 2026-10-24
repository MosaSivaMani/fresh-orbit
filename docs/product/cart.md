# Cart module specification

Owner: E4. Schema `cart`. Sources: `docs/database/README.md` (cart, §4), `docs/api/README.md` §1.3, §1.4, §4.6, §5; `docs/decisions/log.md` (D3, D5, D10 win).
Cart stores lines only; every read is priced from current data (`CART-R1`).

## 1. Aggregates and boundaries

| Aggregate | Root / table | Children / table | Decides | Does not decide |
|---|---|---|---|---|
| Cart | `Cart` / `cart.carts` (xmin) | `CartItem` / `cart.cart_items` | Owner (customer or guest token hash), pincode + fulfillment location, line identity (variant × preparation), quantities vs variant rules, line limit, stored coupon code, status, expiry | Prices, availability, coupon validity, delivery fee (all computed on read by `CartPricer`) |

| Service | Kind | Uses (.Public) | Output |
|---|---|---|---|
| `CartPricer` | Application service, no writes | Locations (pincode → service area, location, city), Catalog (variant, product, preparation, yield), Pricing (price, prep charge, tax rule at location and time), Inventory (available per stock item), Settings (order policy `maxOverweightBp`), Promotions (coupon evaluation), Ordering (`HasQualifyingOrder` for first-order coupons) | Priced cart / quote (§5 of API) |
| `ICartApi` (Cart.Public) | Contract for Ordering and Identity | — | `GetCheckoutCart`, `Quote`, `MarkConverted` (caller's transaction), `MergeGuestCart`, `AddLines` (reorder) |

| Column | Rule |
|---|---|
| `carts.customer_id` | Null for guest carts |
| `carts.guest_token_hash` | SHA-256 of a 32-byte random token; null for customer carts |
| `carts.fulfillment_location_id`, `pincode` | Set on first add (`LOC`) or `PUT /cart/location` |
| `carts.coupon_code` | Customer carts only |
| `carts.expires_at` | Last write + cart lifetime (`CART-R20`) |
| `cart_items.quantity` | Variant stock unit: grams (KG), pieces (PIECE), pack count (PACK) |

## 2. States

### Cart

| From | To | Trigger |
|---|---|---|
| — | `ACTIVE` | First cart write (guest: token issued) or merge target creation |
| `ACTIVE` | `CONVERTED` | Order placed (`ICartApi.MarkConverted` in checkout transaction); guest cart merged on login |
| `ACTIVE` | `ABANDONED` | `AbandonCarts` job when `expires_at <= now` |
| `CONVERTED`, `ABANDONED` | — | Terminal; next write creates a new `ACTIVE` cart |

`DELETE /cart` removes lines and coupon; the cart stays `ACTIVE`.

### Line availability (computed, not stored)

| Status | Condition |
|---|---|
| `NOT_SOLD_HERE` | No current price, no stock item, product/variant inactive, or preparation not enabled at the location |
| `OUT_OF_STOCK` | `available < grossNeed` |
| `LOW_STOCK` | `available >= grossNeed` and `available <= low_stock_threshold` |
| `IN_STOCK` | Otherwise |

## 3. Business rules

### Pricing on read

| Key | Rule |
|---|---|
| CART-R1 | Every `GET /cart`, cart write response, checkout preview and place order re-reads price, preparation charge, tax rule, yield, stock and policy at `now`. Nothing priced is stored in `cart.*`. |
| CART-R2 | Location for pricing = cart's `fulfillment_location_id` (cart reads) or the delivery address's resolved location (quote for checkout). Headers are ignored after the cart has a pincode. |
| CART-R3 | `itemPaise` = round-half-up(`quantity × unitPricePaise / 1000`) for GRAM; `quantity × unitPricePaise` for PIECE/PACK. |
| CART-R4 | `preparationChargePaise` by basis: `PER_KG` = round-half-up(`chargePaise × quantityGrams / 1000`); `PER_ITEM` = `chargePaise × quantity` (PIECE/PACK only, see Q4); `FLAT` = `chargePaise` once per line. Missing charge row for an enabled preparation → line issue `PREPARATION_CHARGE_NOT_AVAILABLE`. |
| CART-R5 | `estimatedLineTotalPaise` = `itemPaise + preparationChargePaise`. Rounding is per line, never on totals. |
| CART-R6 | Prices are tax-inclusive. `taxPaise` = round-half-up(`lineTotal × taxRateBp / (10000 + taxRateBp)`), computed on the line total after its allocated discount (`PROMO-R14`). No tax rule → line issue `TAX_RULE_MISSING`. |
| CART-R7 | `maxChargeableQuantity`: weight-variable variant = floor(`quantity × (10000 + maxOverweightBp) / 10000`); otherwise = `quantity`. `maxLineTotalPaise` = CART-R3..R5 at that quantity. Policy = city row else default; neither → `CONFIGURATION_MISSING`. |
| CART-R8 | `estimatedNetQuantity` = `quantity` (net-weight model). |
| CART-R9 | Gross need via `Inventory.Public.GrossQuantity.From` (never re-implemented): GRAM stock unit = `ceil(quantity × 10000 / yieldBp)`; PIECE (incl. PACK variants, D10) = `quantity`. |
| CART-R10 | Totals exactly as API §5.2: `itemsPaise`, `preparationPaise` over sellable lines only; `subtotal = items + preparation`; `discount` from Promotions; delivery fee from service area, 0 when `subtotal − discount >= freeDeliveryAbovePaise` or FREE_DELIVERY coupon; `estimatedTotal = subtotal − discount + deliveryFee`; `maxTotal` same with max line totals; `minOrderShortfall` on `subtotal − discount`; `freeDeliveryShortfall` null when not configured. `taxPaise` = Σ line tax + contained tax on the delivery fee (tax category `DELIVERY_SERVICE`, D5). |
| CART-R11 | `canCheckout` = cart has ≥ 1 line, every line `isSellable`, no min-order shortfall, coupon not `INVALID`. `checkoutBlockers` list the §2 codes (`CART_EMPTY`, `OUT_OF_STOCK`, `PRICE_NOT_AVAILABLE`, `PRODUCT_NOT_AVAILABLE`, `QUANTITY_ABOVE_MAX`, `MIN_ORDER_NOT_MET`, `COUPON_*`). |
| CART-R12 | Price and stock reads are batched: one call per dependency for all lines (no call per line). |

### Availability messages

| Key | Rule |
|---|---|
| CART-R13 | `availableNet` = GRAM: floor(`available × yieldBp / 10000`) rounded down to `orderStep`; PIECE/PACK: `available`. |
| CART-R14 | `LOW_STOCK` message: "Only {availableNet formatted} left today." `OUT_OF_STOCK` with `availableNet >= minOrder`: "Only {availableNet} available; reduce the quantity." else "Out of stock today." `NOT_SOLD_HERE`: "Not sold at your location." Messages are display only; clients branch on `status`/`issues[].code`. |
| CART-R15 | The cart never reserves stock. Availability is advisory until checkout reserves (`ORD-R11`). |

### Writes

| Key | Rule |
|---|---|
| CART-R16 | Add: variant/product `ACTIVE` (`PRODUCT_NOT_AVAILABLE`), preparation enabled (`PREPARATION_NOT_AVAILABLE`; omitted → variant default), unit matches sell unit (`QUANTITY_UNIT_INVALID`), `>= minOrder`, `<= maxOrder`, multiple of `orderStep`, price at location (`PRICE_NOT_AVAILABLE`), gross need ≤ available (`OUT_OF_STOCK`). Same variant + preparation → quantities add; sum > `maxOrder` → `QUANTITY_ABOVE_MAX`. |
| CART-R17 | Max 30 lines → `CART_ITEM_LIMIT`. |
| CART-R18 | Patch preparation onto an existing variant + preparation line merges the two lines (capped at `maxOrder`). |
| CART-R19 | No If-Match on cart. Concurrent writes: `ConcurrentSaveException` → re-read and re-apply, up to 3 retries, then 409 `CONCURRENT_SAVE`. Two first writes racing on `unique (customer_id) where ACTIVE` → loser re-reads the winner's cart and applies its change there. |
| CART-R20 | Every write sets `expires_at = now + lifetime`: customer 30 days, guest 7 days (Q2). |
| CART-R21 | Owner: customer JWT wins; `X-Cart-Token` ignored when a JWT is present. Unknown token or non-`ACTIVE` cart → 404 `CART_NOT_FOUND` (except `GET /cart`, which returns an empty cart). |
| CART-R22 | Guest token is issued on the first write without token or JWT; returned once in `X-Cart-Token` and `cartToken`; only its hash is stored. |
| CART-R24 | **Price quote** (`GET /price-quote`, public, location required): one line priced before it is added, with the same loaders, `QuantityRules` and `LineCalculator.Price` as a cart line, so the quote equals the line it becomes (no coupon). Checks in order: parameters present (`VALIDATION_FAILED`), pincode (`LOCATION_REQUIRED` / `PINCODE_NOT_SERVICEABLE`), variant active on an ACTIVE product (`PRODUCT_NOT_AVAILABLE`), preparation enabled (`PREPARATION_NOT_AVAILABLE`; omitted → default), unit (`QUANTITY_UNIT_INVALID`), min / max / step (`QUANTITY_*` on `quantity`), price and charge (`PRICE_NOT_AVAILABLE`, `PREPARATION_CHARGE_NOT_AVAILABLE`). Returns item, preparation, contained tax, estimate, max chargeable quantity and amount (city order policy `maxOverweightBp`), `discountBp` (CAT-R22) and availability; stock is reported, not refused. Rate limit `public`. |
| CART-R23 | Coupon endpoints require a customer JWT (guest → 401 `LOGIN_REQUIRED`). `PUT /cart/coupon` validates now and refuses with the `COUPON_*` code; reads never throw for coupons (`PROMO-R12`). Rate limits (API §1.9, D62): every cart route is under the per-IP `public` limit (guests use the cart); `PUT /cart/coupon` instead has the `coupon` limit, 10 per 10 minutes per customer (IP when anonymous), against code guessing. |

### Location change

| Key | Rule |
|---|---|
| CART-R24 | First add without a cart pincode needs `LOC` → else 422 `LOCATION_REQUIRED`; pincode must resolve (`PINCODE_NOT_SERVICEABLE`, `CITY_NOT_ACTIVE`). |
| CART-R25 | `PUT /cart/location`: same fulfillment location → pincode updated, 200. Different location: lines not sellable at the new location are listed; any such line and `force` not true → 409 `CART_LOCATION_CHANGED` with `lines: [{ id, code }]`, nothing saved. `force=true` → those lines removed, location changed, removed lines named in `messages`. No dropped lines → change applied without `force`. |
| CART-R26 | Coupon with a different `city_id` after a location change is kept and shown `INVALID` (`COUPON_INVALID`) until removed. |

### Guest cart merge on login

| Key | Rule |
|---|---|
| CART-R27 | `MergeGuestCart(guestToken, customerId)` is called by Identity after `POST /auth/otp/verify` commits (separate transaction, Q1). Failure never fails login (`cart` in the response is then `null`). |
| CART-R28 | Target = customer's `ACTIVE` cart, created if absent. Matching variant + preparation → quantities added, capped at `maxOrder`; other lines appended until 30 lines; dropped lines counted. |
| CART-R29 | Pincode: guest cart pincode wins when both have one (latest intent); unsellable lines are kept and shown with issues (not removed). |
| CART-R30 | Guest cart → `CONVERTED` in the same transaction. Unknown or already `CONVERTED` token → no-op, `mergedLineCount = 0` (idempotent). |
| CART-R31 | Each cart line view carries the variant's order rules `minOrder`, `maxOrder` and `orderStep` (`{ value, unit }`, the same values the cart validates with), null when the variant is gone, so clients size the quantity stepper without their own rules (W4). |

## 4. Events

### Raised

| Event | Payload | Consumers |
|---|---|---|
| `CartAbandoned` | `cartId`, `customerId?`, `lineCount`, `pincode`, `fulfillmentLocationId`, `lastActivityAt`, `abandonedAt` | Notifications (optional reminder, `OFFERS` preference); Reporting reads tables |

Cart conversion is a write inside the checkout transaction; Ordering's `OrderPlaced` carries `cartId`.

### Consumed

None. (Prices, stock, catalog changes are seen on the next read.)

## 5. Commands

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| GetCart | `GET /cart` | Cart by owner; batched catalog, price, tax, stock, policy, service area, coupon | `CartPricer` | — | — |
| AddItem | `POST /cart/items` | Cart (or none), location (`LOC`), variant + preparation, price, stock | CART-R16, R17, R22, R24 | Cart (+ new) | Priced cart |
| ChangeItem | `PATCH /cart/items/{itemId}` | Cart, variant, price, stock | CART-R16, R18 | Cart | Priced cart |
| RemoveItem | `DELETE /cart/items/{itemId}` | Cart | Line exists (else 404) | Cart | Priced cart |
| ClearCart | `DELETE /cart` | Cart | — | Cart (lines, coupon removed) | Priced cart |
| ChangeLocation | `PUT /cart/location?force=` | Cart, new pincode resolution, sellability of lines at new location | CART-R25 | Cart | Priced cart |
| ApplyCoupon | `PUT /cart/coupon` | Cart, coupon evaluation (Promotions.Public), first-order flag | CART-R23 | Cart `coupon_code` | Priced cart |
| RemoveCoupon | `DELETE /cart/coupon` | Cart | — | Cart | Priced cart |
| MergeGuestCart | Called from `POST /auth/otp/verify` (Identity) | Both carts, variant limits | CART-R27..R30 | Both carts | `{ cartId, mergedLineCount }` |
| AddLines (reorder) | Called from `POST /orders/{id}/reorder` (Ordering) | Cart, variants, prices at cart location | Each line by CART-R16; unsellable lines skipped and named in `messages` | Cart | Priced cart |
| Quote | `ICartApi.Quote` (preview, place order) | As GetCart but location from address | `CartPricer` + blockers; `CART_LOCATION_CHANGED` when the address location differs from the cart location (ORD-R5) | — | Quote |
| MarkConverted | `ICartApi.MarkConverted` | Cart (already tracked) | `ACTIVE` only | Caller's transaction | — |

### API gaps

| Gap | Proposal |
|---|---|
| `CART_LOCATION_CHANGED` body member for dropped lines is not named | Member `lines: [{ id, code }]` |
| Cart lifetime / expiry not specified | CART-R20 (Q2) |
| Merge called from Identity: no `.Public` contract named | `ICartApi.MergeGuestCart(guestToken, customerId)`; E1 calls it after its commit |
| Cart needs Settings, Customers (user → customer id) and Ordering (`HasQualifyingOrder`) `.Public`; architecture table lists none | Add to Cart's "Uses" |
| `issues[].code` list in §5.1 omits `PREPARATION_CHARGE_NOT_AVAILABLE`, `TAX_RULE_MISSING`, `PREPARATION_NOT_AVAILABLE` | Allow them |

## 6. Jobs

| Job | Schedule | Work check | Action |
|---|---|---|---|
| `AbandonCarts` | Hourly | `exists carts where status = ACTIVE and expires_at <= now` (partial index `(expires_at) where status = 'ACTIVE'`); none → return | Load up to 500 due carts, mark `ABANDONED`, raise `CartAbandoned`, one `SaveChanges` per batch; repeat while work remains |

## 7. Test scenarios

### Unit

| # | Scenario | Expect |
|---|---|---|
| U1 | 500 g at 129900/kg, PER_KG 4000 | item 64950, prep 2000, line 66950 |
| U2 | 333 g at 89950/kg | item round-half-up(29953.35) = 29953 |
| U3 | Half-paise case (x.5) | rounds up |
| U4 | Weight-variable 500 g, 1000 bp | max 550; non-weight-variable max = quantity |
| U5 | Gross need 500 g at 6500 bp | 770 |
| U6 | Tax-inclusive 10500 at 500 bp | tax 500 |
| U7 | FLAT prep on 2 lines | charged once per line |
| U8 | Quantity below min / above max / bad step / wrong unit | each code on `quantity.value` |
| U9 | Add same variant + preparation | quantities add; sum > max → `QUANTITY_ABOVE_MAX` |
| U10 | 31st line | `CART_ITEM_LIMIT` |
| U11 | Merge: overlapping lines capped at max; > 30 lines dropped and counted | as rule |
| U12 | Availability boundaries (= need, = threshold) | `IN_STOCK`/`LOW_STOCK`/`OUT_OF_STOCK` as table |
| U13 | Free delivery exactly at threshold | fee 0 |
| U14 | Totals for API §4.6 example | match every field of the example |

### Integration (Postgres)

| # | Scenario | Expect |
|---|---|---|
| I1 | Price period changes between two reads | second read shows the new price; cart rows unchanged |
| I2 | No price at location | line `NOT_SOLD_HERE`, `isSellable=false`, blocker `PRICE_NOT_AVAILABLE`; add refused 422 |
| I3 | Guest add without `LOC` | 422 `LOCATION_REQUIRED` |
| I4 | Guest add issues token; DB holds only hash | token not in `cart.carts` |
| I5 | Location change dropping a line without `force` | 409 `CART_LOCATION_CHANGED`, nothing saved; with `force=true` line removed |
| I6 | Merge twice with the same token | second call `mergedLineCount=0`; guest cart `CONVERTED` |
| I7 | Two parallel first adds for one customer | one `ACTIVE` cart with both lines |
| I8 | Parallel writes on one cart | no lost line; no 500 |
| I9 | Coupon expires between reads | read returns `coupon.status = INVALID`, `code = COUPON_EXPIRED`, 200 |
| I10 | Guest `PUT /cart/coupon` | 401 `LOGIN_REQUIRED` |
| I11 | `AbandonCarts` with no due carts | no writes, no events |
| I12 | `AbandonCarts` with 2 due carts | both `ABANDONED`, 2 outbox rows |
| I13 | Reads per request | one query per dependency regardless of line count (query counter) |
| I14 | `GET /price-quote` (in `Catalog.Tests/Integration/StorefrontProductShapeTests`, which has the storefront fixtures) | amounts equal `LinePricing`; each refusal code with its field |

## 8. Open questions

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Who triggers guest cart merge, and in which transaction? | Identity calls `ICartApi.MergeGuestCart` after its own commit; failure logged, login still succeeds |
| Q2 | Cart lifetime? | Customer 30 days, guest 7 days after last write |
| Q3 | Refuse add when out of stock, or allow and flag? | Refuse add/patch with `OUT_OF_STOCK`; reads flag later changes |
| Q4 | `PER_ITEM` preparation charge on a GRAM line? | Refused by Pricing setup (E2); Cart treats it as configuration missing |
| Q5 | Location change with `force=true`: remove or keep unsellable lines? | Remove and name them in `messages` |
| Q6 | Tax rate on the delivery fee | Resolved by D5: category `DELIVERY_SERVICE`; missing → `TAX_RULE_MISSING` blocker |
| Q7 | Where does the line formula live so Cart and Ordering (pack) cannot diverge? | Pure `LineAmounts` in SharedKernel (manager change); both modules call it |
