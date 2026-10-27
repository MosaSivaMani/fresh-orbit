# Mobile API contract audit, October 2026

The owner's live APK (Render demo API) showed "Could not load this" on the Shop tab: `ProductQuery.toQuery()` always
sent `sort=relevance`, which CAT-R15 refuses without `q` (fixed separately in PR #27). This audit checks every other
request the app sends, so the owner does not meet the next refusal on the phone.

## Method

- Read every call in `apps/mobile/lib/api/repositories.dart` and `api_client.dart` (path, query, headers, body,
  defaults) and the screens that build them.
- Compared each with the server's validators and endpoints (`backend/src/Modules/*`) and `docs/product/*.md`.
- Confirmed with curl against `https://fresh-orbit-demo-api.onrender.com/api/v1` (public GETs; signed-in calls with
  the demo OTP from `/dev/sms` for two test numbers, pincode 530017). No local Flutter or .NET runs; CI verifies.

## Headers

`X-Pincode` comes from the stored pincode unless a `pincode` query is sent; `X-Cart-Token` only for guests;
`Idempotency-Key` on `POST /orders`, `/orders/{id}/cancel` and `/orders/{id}/complaints`; `If-Match: "<version>"` on
`PATCH /me`, `DELETE /me` and `PUT /me/addresses/{id}`. All match the server; curl accepted each.

## Findings

| # | Call | What the app sent | Server | Outcome |
|---|---|---|---|---|
| M1 | `GET /products` (Shop) | `sort=relevance` without `q` | 422 `VALIDATION_FAILED` (CAT-R15) | PR #27 (not this branch) |
| M2 | `GET /products?q=`, `GET /search/suggest?q=` | Any non-empty submit; suggestions from 2 characters | `q` must be 2–60 characters after trim with a letter or digit (CAT-R12): `q=a`, 61 characters or `%%` → 422 | Checked before sending, field capped at 60 (D235) |
| M3 | Every catalogue call | `X-Pincode` of a pincode that is no longer served | 422 `PINCODE_NOT_SERVICEABLE` on `/home`, `/products`, `/products/{slug}`, `/search/suggest`; the screen showed only Try again | Change pincode on the error state (D236). Server correct (rule 7) |
| M4 | `DELETE /cart` | Clear when the server cart is gone | 404 `CART_NOT_FOUND` | Read as the empty cart (D237) |
| M5 | `GET /orders` | `sort=-placedAt` | Not a parameter; ignored (newest first anyway) | Removed (D241). Not a refusal |
| A1 | `GET /serviceability` | `city.stateName` parsed, not shown | D209 | Home city line "City, State" (D238). The demo city has no state name yet, so it shows the city alone |
| A2 | `GET /serviceability` | `fulfillmentLocation.hasColdStorage` ignored; "Kept cold" always claimed | D212, LOC-R11 | Parsed; claim only when true (D239) |
| A3 | `GET /products`, `/products/{slug}` | `freshness` ignored | CAT-R25, D212 | Parsed on list items and sizes; "Caught <date>" on the product page (D240) |
| — | `GET /home` banners | `badge` | D210 | Already shown |
| — | Cart bar | `totals.estimatedTotalPaise` | D212, CART-R32 | Already used |

## Checked and fine

| Call | Checked |
|---|---|
| `POST /auth/otp/request`, `/auth/otp/verify`, `/auth/logout` | `purpose: LOGIN`; null `deviceLabel` and `guestCartToken` accepted |
| `GET /me`, `PATCH /me`, `DELETE /me` | If-Match with the quoted version; null fields accepted |
| Addresses | `label` HOME/WORK/OTHER, E.164 phone, 2-letter `stateCode`, 6-digit pincode, null lat/lng; PUT with If-Match; DELETE; set default |
| Wishlist, notifications, preferences, device tokens | Paths and bodies accepted (`provider: FCM`, `platform: ANDROID/IOS`; categories ORDER_UPDATES/OFFERS, channels SMS/EMAIL/PUSH/IN_APP) |
| `GET /coupons` | Signed in only (401 otherwise; the cart shows the sheet only when signed in); sends `X-Pincode` (D213) |
| `GET /price-quote`, `POST /checkout/preview` | Accepted (COD, CALL_ME) |
| `POST /cart/items` | Shape accepted (the demo answered `OUT_OF_STOCK`, a data refusal) |
| `POST /orders`, cancel, complaints, photos, substitutions, reorder, tracking, invoice | Idempotency keys where required; enum codes (cancel reasons, complaint kinds) match the server |
| Price sorts and `inStock` without a pincode | 422 `LOCATION_REQUIRED`, but unreachable: the app asks for a pincode before the shop, and D236 covers it if reached |
