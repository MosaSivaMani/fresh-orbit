# Client API gaps (web and mobile), October 2026

Gaps the website and the mobile app reported (progress log, PR #16) and what the code on `main` showed.

| # | Gap | Finding | Outcome |
|---|---|---|---|
| 1 | Cart totals for the cart bar | `CartTotalsView` already carries `subtotalPaise`, `discountPaise`, `deliveryFeePaise` + `deliveryFeeWaived`, `estimatedTotalPaise` (payable estimate). The mobile cart bar reads `totals.subtotalPaise` (`apps/mobile/lib/router.dart`). A cart without a pincode is not priced (empty `lines`, zero totals). | No API change; documented as CART-R32 |
| 2 | State name for the address form | Cities stored only `state_code`; two codes exist for one state in data (TG and TS), so a code → name table in code would guess. | Optional `cities.state_name`, `city.stateCode` + `city.stateName` in `/serviceability` (LOC-R19) |
| 3 | Banner badge | No field. | Optional `promotions.badge`, at most 20 characters (PROMO-R29) |
| 4 | Catch time / kept cold | `freshness { caughtOn, receivedAt }` on product list items and detail (CAT-R25, from the batch receipt); `fulfillmentLocation.hasColdStorage` in `/serviceability` (required true for an ACTIVE location, LOC-R11). | No API change; documented in CAT-R25 |
| 5 | Checkout preview `address.label` null | `CheckoutService` passed `null`; `DeliveryAddress` had no label. | `DeliveryAddress.Label` (Customers.Public), mapped in the preview |
| 6 | OTP verify `cart: null` after a merge | The merge ran only in the `CustomerSignedIn` handler (outbox, seconds later), contrary to CART-R27. | `IGuestCartMerger` (Identity.Public, implemented by Cart) runs after the login commits; the handler then no-ops |
| 7 | `GET /coupons` empty on the demo | Sample `WELCOME100` has `isListed: true`. Both sample coupons are city-scoped (VISAKHAPATNAM). The visible-coupon query matches `city_id IS NULL OR city_id = <request city>`; the request city comes only from `?pincode=` or `X-Pincode`. The web client's `listCoupons` (`apps/web/src/api/endpoints/account.ts`) sends neither, so only all-city coupons can appear, and there are none. Banners show because `/promotions` and `/home` are called with `X-Pincode`. | Not a sample-data fault; the client must send the pincode (PROMO-R30) |
