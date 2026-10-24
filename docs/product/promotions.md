# Promotions module specification

Owner: E4. Schema `promotions`. Sources: `docs/database/README.md` (promotions, §4), `docs/api/README.md` §2 Promotions,
§4.4, §4.6, §4.7, §4.11, §4.13, §5; `docs/decisions/log.md` D6 (reversal by event handler).

## 1. Aggregates and boundaries

| Aggregate | Root / table | Children / table | Decides |
|---|---|---|---|
| Coupon | `Coupon` / `promotions.coupons` (xmin) | `CouponScope` / `promotions.coupon_scopes` | Validity (status, dates, city, scope, first order, limits, min order), discount amount and allocation, `used_count` |
| CouponRedemption | `CouponRedemption` / `promotions.coupon_redemptions` | — | One redemption per order (`unique order_id`), `APPLIED` / `REVERSED` |
| Promotion (home offer) | `Promotion` / `promotions.promotions` (xmin) | — | Banner window, city, link, sort |

| Boundary rule | Value |
|---|---|
| Redemption is its own aggregate (unbounded count), but every redeem (checkout transaction, D6) and reverse (own event handler) also changes `Coupon.used_count`, so the coupon's row version serializes total and per-customer limit checks (`PROMO-R9`). |
| Promotions never reads Ordering tables. "Has a qualifying order" is supplied by the caller (`PROMO-R7`). |
| Category scope resolution (category + descendants, product's category) uses Catalog.Public; city existence uses Locations.Public (gap: not in architecture "Uses"). |

| `IPromotionsApi` (Promotions.Public) | Use |
|---|---|
| `Evaluate(EvaluateCoupon)` | Read only: cart reads, preview. Input `code`, `customerId`, `cityId`, `hasQualifyingOrder`, `at`, `lines[{ lineRef, productId, amountPaise }]`, `subtotalPaise`. Output `status` (`APPLIED`/`NOT_APPLICABLE`/`INVALID`), `code?`, `discountPaise`, `waivesDeliveryFee`, `allocations[{ lineRef, discountPaise }]` |
| `Redeem(RedeemCoupon)` | Checkout transaction: same input + `orderId`; refuses with `COUPON_*`; tracks coupon + new redemption in the shared unit of work (no save) |
| `GetActiveBanners(cityId?, at)`, `ListVisibleCoupons(cityId?, at)` | Catalog home, `GET /promotions`, `GET /coupons` |

## 2. States

### Coupon (`status` stored; "not started" and "limit reached" are computed)

| From | To | Trigger |
|---|---|---|
| — | `ACTIVE` | `POST /admin/coupons` |
| `ACTIVE` | `PAUSED` | `POST /admin/coupons/{id}/pause` |
| `PAUSED` | `ACTIVE` | `POST /admin/coupons/{id}/resume` (refused 409 `INVALID_STATUS_TRANSITION` when `valid_to <= now`) |
| `ACTIVE`, `PAUSED` | `EXPIRED` | `ExpireCoupons` job when `valid_to <= now` |
| `EXPIRED` | — | Terminal; PUT, pause, resume → 409 `INVALID_STATUS_TRANSITION` |

Validity is always decided from dates at `at`; the job only tidies `status` for lists.

### Redemption

| From | To | Trigger |
|---|---|---|
| — | `APPLIED` | `Redeem` in place-order transaction (`used_count + 1`) |
| `APPLIED` | `REVERSED` | Handler of `OrderCancelled` / `OrderPaymentFailed` (`used_count − 1`) |
| `REVERSED` | — | Terminal; reverse again = no-op |

Order `DELIVERED`, `RETURNED`, `DELIVERY_FAILED` keep the redemption `APPLIED` (Q3).

### Promotion (banner)

`is_active` flag; shown when `is_active` and `starts_at <= at < ends_at` (`ends_at` null = open). No state machine.

## 3. Business rules

### Validity (evaluated in this order; first failure wins)

| Key | Rule | Refusal |
|---|---|---|
| PROMO-R1 | Code matched case-insensitively (`citext`), trimmed, stored upper case | — |
| PROMO-R2 | Exists and `status = ACTIVE`; `EXPIRED` status → `COUPON_EXPIRED` | `COUPON_INVALID` |
| PROMO-R3 | `at >= valid_from` | `COUPON_NOT_STARTED` |
| PROMO-R4 | `valid_to` null or `at < valid_to` (half-open, like price periods) | `COUPON_EXPIRED` |
| PROMO-R5 | `city_id` null or equals the order/cart city | `COUPON_INVALID` |
| PROMO-R6 | Scope: no scopes = all lines eligible; else a line is eligible when its product is a `PRODUCT` scope or its category (or an ancestor) is a `CATEGORY` scope. No eligible sellable line | `COUPON_INVALID` |
| PROMO-R7 | `first_order_only` and caller's `hasQualifyingOrder` (customer has an order not in `CANCELLED`/`PAYMENT_FAILED`) | `COUPON_FIRST_ORDER_ONLY` |
| PROMO-R8 | `total_limit` not null and `used_count >= total_limit` | `COUPON_LIMIT_REACHED` |
| PROMO-R9 | `per_customer_limit` not null and count of `APPLIED` redemptions for (coupon, customer) `>= per_customer_limit` | `COUPON_LIMIT_REACHED` |
| PROMO-R10 | Eligible subtotal (Σ `itemPaise + preparationChargePaise` of eligible sellable lines) `>= min_order_paise` | `COUPON_MIN_ORDER_NOT_MET` |

### Discount

| Key | Rule |
|---|---|
| PROMO-R11 | `PERCENT`: `discount_value` in bp (1–10000); discount = round-half-up(`eligible × value / 10000`), capped at `max_discount_paise` (required for PERCENT). `FLAT`: `min(discount_value paise, eligible)`. `FREE_DELIVERY`: discount 0, `waivesDeliveryFee = true`. |
| PROMO-R12 | `Evaluate` never throws for rule failures: `NOT_APPLICABLE` for `COUPON_MIN_ORDER_NOT_MET` (fixable by cart change), `INVALID` for every other code; discount 0. `PUT /cart/coupon` and `Redeem` refuse with 422 and the code. |
| PROMO-R13 | Discount never exceeds eligible subtotal; total never negative. |
| PROMO-R14 | Allocation: discount split over eligible lines pro rata to line amount, floor each, remainder paise to lines with the largest fractional parts (ties: lower line no). Σ allocations = discount. Used for line tax (CART-R6) and invoice lines (ORD-R49). |
| PROMO-R15 | At redeem, `discount_paise` is frozen on the redemption and the order (ORD-R30 for pack). |

### Limits and race

| Key | Rule |
|---|---|
| PROMO-R16 | `Redeem` re-checks R2–R10 inside the checkout transaction, increments `used_count`, inserts the redemption. The coupon row version makes concurrent redeems of one coupon collide: the loser gets `ConcurrentSaveException`, the checkout retries from Read (ORD-R13) and sees the new count. DB check `used_count <= total_limit` is the last line. |
| PROMO-R17 | `first_order_only` coupons must have `per_customer_limit = 1` (create/update validation), so R9 + R16 also stop two parallel "first" orders. |
| PROMO-R18 | Reverse runs in Promotions' own handler (D6): redemption `REVERSED` + `used_count − 1` in one save; lost race on the coupon row → handler retried; no redemption for the order or already `REVERSED` → no-op. |

### Admin

| Key | Rule |
|---|---|
| PROMO-R19 | Code: 3–20 chars `[A-Z0-9]`, unique (`DUPLICATE`, field `code`); immutable once a redemption exists. |
| PROMO-R20 | `valid_to > valid_from`; `per_customer_limit`, `total_limit` ≥ 1 or null (unlimited); `min_order_paise ≥ 0`; `FREE_DELIVERY` has `discount_value = 0`; `FLAT` value > 0. Field-keyed `VALIDATION_FAILED`. |
| PROMO-R21 | `total_limit` cannot be set below `used_count` (`VALIDATION_FAILED` on `totalLimit`). |
| PROMO-R22 | `city_id` must exist (Locations.Public); scope ids must exist (Catalog.Public). |
| PROMO-R23 | PUT, pause, resume need `If-Match`; every admin write audited. |
| PROMO-R24 | `GET /coupons` lists `ACTIVE`, started, not expired, city-matching coupons that are marked listed (gap: `is_listed`) and not exhausted for the customer. |
| PROMO-R26 | **Product offer line** (Catalog's `IProductOfferSource`, CAT-R26): for each product, among the listed coupons usable in the city now (as PROMO-R24, not customer-specific), those whose scope covers the product (no scope = all; CATEGORY covers sub-categories); the most specific wins (PRODUCT, then CATEGORY, then store-wide), then lowest `min_order_paise`, then code. Text composed by the server: "15% off up to ₹100" / "₹50 off" / "Free delivery", + " on orders above ₹499", + " on your first order", + " with code CODE". No amount is computed for display. |
| PROMO-R25 | Banners: `link_type COUPON` target must be an existing coupon code; `CATEGORY`/`PRODUCT` target must exist (Catalog.Public). Permission `catalog.manage` (API §4.11). |
| PROMO-R27 | Banner responses (`GET /promotions`, `/home` banners) carry `imageUrl`, the public URL of `imageKey` from file storage, or null when there is no valid key. Clients use the URL as given. Promotions supplies `/home` banners through `IHomeBannerSource` (Catalog.Public). |
| PROMO-R28 | Banner responses carry `linkSlug` (W14): the storefront slug of a CATEGORY or PRODUCT `linkTarget`, read from Catalog in two queries for the whole list (`ICatalogApi.GetCategorySlugsAsync`, product summaries); null for COUPON links and for a target that no longer exists. `linkTarget` stays the id. |

## 4. Events

### Raised

| Event | Payload | Consumers |
|---|---|---|
| `CouponRedeemed` | `couponId`, `code`, `redemptionId`, `orderId`, `customerId`, `discountPaise`, `waivesDeliveryFee`, `redeemedAt` | None at launch (Reporting reads tables); kept for audit/analytics |
| `CouponRedemptionReversed` | `couponId`, `redemptionId`, `orderId`, `customerId`, `reason` (`ORDER_CANCELLED`/`PAYMENT_FAILED`), `reversedAt` | None at launch |
| `CouponStatusChanged` | `couponId`, `code`, `from`, `to`, `actorId?`, `at` | Audit |

### Consumed

| Event (module) | Payload used | Effect | Idempotency |
|---|---|---|---|
| `OrderCancelled` (Ordering) | `orderId`, `couponCode?` | PROMO-R18, reason `ORDER_CANCELLED` | inbox + redemption status |
| `OrderPaymentFailed` (Ordering) | `orderId`, `couponCode?` | PROMO-R18, reason `PAYMENT_FAILED` | inbox + redemption status |

Redeem runs through `IPromotionsApi.Redeem` inside Ordering's checkout transaction (D6).

## 5. Commands

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| ListVisibleCoupons | `GET /coupons` | Coupons for city, customer's redemption counts | R24 | — | — |
| Evaluate | `IPromotionsApi.Evaluate` (cart, preview) | Coupon + scopes, category ancestry (batched), customer redemption count | R1–R14 | — | — |
| Redeem | `IPromotionsApi.Redeem` (place order) | Same | R1–R16 | Coupon + redemption in caller's unit of work | `CouponRedeemed` |
| ReverseRedemption | Handler of `OrderCancelled` / `OrderPaymentFailed` | Redemption by order, coupon | R18 | Redemption + coupon | `CouponRedemptionReversed` |
| ListCoupons | `GET /admin/coupons` | Filter `status`, `cityId`, `q` | — | — | — |
| CreateCoupon | `POST /admin/coupons` | Code uniqueness, city, scopes | R19–R22 | Coupon | Audit |
| GetCoupon | `GET /admin/coupons/{id}` | Coupon, scopes | — | — | ETag |
| UpdateCoupon | `PUT /admin/coupons/{id}` (IM) | Coupon, redemption existence | R19–R23 | Coupon | Audit |
| PauseCoupon / ResumeCoupon | `POST /admin/coupons/{id}/pause` \| `/resume` (IM) | Coupon | §2 table | Coupon | `CouponStatusChanged` |
| ListRedemptions | `GET /admin/coupons/{id}/redemptions` | Paged | — | — | — |
| ListActiveBanners | `GET /promotions`, `GetActiveBanners` for `GET /home` | Banners for city | §2 banner rule | — | — |
| Create/Update banner | `GET/POST/PUT /admin/promotions[/{id}]` (IM on PUT) | Banner, link target | R25 | Promotion | Audit |

### API gaps

| Gap | Proposal |
|---|---|
| `GET /coupons` "visible to the customer" has no column to hide private coupons | Add `coupons.is_listed boolean` + request field `isListed` |
| `discount_value` unit for PERCENT not stated | Basis points; FLAT paise |
| Coupon request/response shapes not in §4.13 | Fields = table columns in camelCase + `scopes[{ scopeType, scopeId }]`, `usedCount`, `version` |
| `/admin/promotions` banners belong to Promotions but use `catalog.manage` | Keep as written; note ownership here |
| Promotions needs Locations.Public (city check) | Add to architecture "Uses" |

## 6. Jobs

| Job | Schedule | Work check | Action |
|---|---|---|---|
| `ExpireCoupons` | Hourly | `exists coupons where status in (ACTIVE, PAUSED) and valid_to <= now`; none → return | Mark `EXPIRED`, raise `CouponStatusChanged`, one save per batch of 500 |

## 7. Test scenarios

### Unit

| # | Scenario | Expect |
|---|---|---|
| U1 | Each validity rule R2–R10 alone | its code |
| U2 | Several failures | first in R-order |
| U3 | PERCENT 10% of 244900, cap 15000 | 15000 |
| U4 | PERCENT 10% of 99995 | 10000 (half up from 9999.5) |
| U5 | FLAT 10000 on eligible 8000 | 8000 |
| U6 | FREE_DELIVERY | discount 0, waives fee |
| U7 | Scope by parent category | child-category product eligible |
| U8 | Allocation of 10000 over 3 lines | Σ = 10000, remainder to largest fractions |
| U9 | `Evaluate` with min order not met | `NOT_APPLICABLE`, no throw |
| U10 | `valid_from = at` / `valid_to = at` | valid / expired |
| U11 | first_order_only with per_customer_limit 2 | validation refused |
| U12 | Resume after `valid_to` | `INVALID_STATUS_TRANSITION` |

### Integration (Postgres)

| # | Scenario | Expect |
|---|---|---|
| I1 | Total limit race: `total_limit = 1`, two checkouts by different customers in parallel | exactly one order with the coupon; other gets `COUPON_LIMIT_REACHED` after retry; `used_count = 1` |
| I2 | Per-customer race: limit 1, same customer two parallel checkouts (different keys) | one redemption |
| I3 | `OrderCancelled` handled (twice, and once with inbox row removed) | `REVERSED` once, `used_count` back once |
| I4 | DB check: force `used_count > total_limit` via update | rejected by constraint |
| I5 | Code case-insensitive uniqueness (`fresh100` vs `FRESH100`) | `DUPLICATE` |
| I6 | Code change after a redemption | `VALIDATION_FAILED` on `code` |
| I7 | Stale `If-Match` on PUT | 412 `CONCURRENCY_CONFLICT` |
| I8 | `ExpireCoupons` with nothing due | no writes |
| I9 | `GET /promotions` city filter and window | only current banners for the city + all-city ones |

## 8. Open questions

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Eligible subtotal: items only, or items + preparation? | Items + preparation of eligible lines |
| Q2 | How does Promotions know "first order"? | Caller passes `hasQualifyingOrder` from `IOrderingApi.HasQualifyingOrder` (Cart and Ordering) |
| Q3 | Reverse redemption on `RETURNED`? | No; only `CANCELLED` and `PAYMENT_FAILED` |
| Q4 | Private coupons (not listed) | Add `is_listed` (API gap) |
| Q5 | Should category scope include descendants? | Yes |
| Q6 | One coupon per order only? | Yes (`unique order_id`), no stacking |

## Admin gap G16 (branch `backend/api-gaps-admin-orders`)

`GET /api/v1/admin/coupons/{id}/redemptions` rows carry `orderNumber`, read from Ordering through
`IOrderingApi.GetOrderNumbers(orderIds)` in one call per page (not stored in Promotions: no schema change, numbers never
drift, old redemptions get them too). `null` when Ordering has no such order.
