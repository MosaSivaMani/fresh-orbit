# Customer website (`apps/web`, Phase 7)

Next.js 16 App Router, React 19, TypeScript, Tailwind v4 through `@fresh/config`, components from `@fresh/ui`,
HTTP and formatting from `@fresh/core`. Owner: E2. Wireframes: `docs/design/README.md` §11.1.

**Rule 4 holds everywhere:** the website never computes a price, discount, fee, tax, total or stock figure. Every
amount on screen is a server value formatted by `@fresh/core` (`formatRupees`, `formatQuantity`, …). The only
arithmetic is unit conversion of user input (shop price filter: rupees typed → paise sent) and display paging.

## 1. Run it

| Task | Command |
|---|---|
| Dev server (http://localhost:3000) | `pnpm --filter @fresh/web dev` |
| Build / start | `pnpm --filter @fresh/web build` · `pnpm --filter @fresh/web start` |
| Unit tests (Vitest + Testing Library + MSW) | `pnpm --filter @fresh/web test` |
| Typecheck (generates route types first) | `pnpm --filter @fresh/web typecheck` |
| E2E smoke (Playwright; run `pnpm exec playwright install chromium` once) | `pnpm --filter @fresh/web test:e2e` |

Environment (`apps/web/.env.example`): `FRESH_API_ORIGIN` (API origin, server-side only; missing → pages that need
the API show their error state, the proxy and BFF answer 503 `SERVICE_UNAVAILABLE`), `NEXT_PUBLIC_SITE_URL` (canonical
URLs, JSON-LD, sitemap; missing → no canonical host, empty sitemap, robots disallow all).

## 2. Page inventory

Mode: **SSR** = rendered per request on the server (reads the pincode cookie, so not cached); **Static/ISR** =
prerendered, regenerated hourly (root layout `revalidate = 3600`, for the footer's city list); **Client** = shell
prerendered, data fetched in the browser with TanStack Query through the same-origin proxy (per-customer data,
`noindex`).

| Route | Page | Status | Mode | API calls |
|---|---|---|---|---|
| `/` | Home: hero with next slot, pincode prompt, search, categories, offers, featured products, trust row | Done | SSR | `GET /home`, `GET /categories`, `GET /promotions`, `GET /serviceability` |
| `/shop` | All products, filters (category, source, in stock, price), sort, pagination; URL-synced | Done | SSR | `GET /products`, `GET /categories` |
| `/shop/[category]` | Category listing with sub-category chips | Done | SSR | `GET /categories/{slug}`, `GET /products?category=` |
| `/search?q=` | Search results; header box has suggestions | Done | SSR (noindex) | `GET /products?q=`; suggestions `GET /search/suggest` (client) |
| `/products/[slug]` | Gallery, variants, preparation with charge and yield note, quantity/weight stepper, availability, add to cart, in-cart estimate and maximum, delivery box, description/storage/sourcing tabs, related, JSON-LD | Done | SSR + client panel | `GET /products/{slug}` (`?preparation=` from the panel), `GET /serviceability`, `GET /products?category=`, `POST /cart/items`, wishlist |
| `/cart` | Server-priced lines, change quantity (bounds from the product), remove, coupon apply/remove, summary, blockers, messages, location note | Done | Client (noindex) | `GET /cart`, `PATCH/DELETE /cart/items/{id}`, `PUT/DELETE /cart/coupon`, `GET /products/{slug}` (editor) |
| `/login?next=` | Phone → OTP, resend timer, guest cart merge, safe redirect | Done | Client (noindex) | `POST /auth/otp/request`; `POST /bff/auth/verify` |
| `/checkout` | Address choose/add, slot by day, payment (COD / UPI on delivery from `paymentMethods`), substitution preference, notes, live preview with blockers, place order with `Idempotency-Key`, `PRICE_CHANGED` confirm, `CART_LOCATION_CHANGED` move | Done | Client, signed in | `GET /cart`, `GET/POST /me/addresses`, `GET /delivery-slots?pincode=`, `POST /checkout/preview`, `POST /orders`, `PUT /cart/location` |
| `/orders/[orderId]/success` | Confirmation | Done | Client, signed in | `GET /orders/{id}` |
| `/orders/[orderId]` | Status, timeline, delivery code (only when sent), rider, items ordered vs charged, estimate/final amounts, amount due, cancel (reason, IK), report a problem (JSON, no photos yet — W12, IK), invoice, order again — each only when `allowedActions` says so | Done | Client, signed in | `GET /orders/{id}`, `GET /orders/{id}/tracking` (polls every 60 s until closed), `GET/POST /orders/{id}/complaints`, `POST /orders/{id}/cancel`, `GET /orders/{id}/invoice`, `POST /orders/{id}/reorder` |
| `/account` | Profile (PATCH with `If-Match`), sign out, delete account (typed confirmation, `If-Match`) | Done | Client, signed in | `GET/PATCH/DELETE /me` |
| `/account/orders` | Orders, paginated | Done | Client, signed in | `GET /orders` (no `sort` parameter; server order) |
| `/account/addresses` | List, add, edit (`If-Match`), delete, make default | Done | Client, signed in | `/me/addresses…` |
| `/account/wishlist` | Saved items, remove | Done (ids only, see §6) | Client, signed in | `GET/DELETE /me/wishlist…` |
| `/account/notifications` | Inbox (mark read / all read, paged), preferences switches (ORDER_UPDATES SMS locked on) | Done | Client, signed in | `GET /me/notifications`, `POST …/read`, `POST …/read-all`, `GET/PUT /me/notification-preferences` |
| `/about` | About with owner placeholder | Done (placeholder text) | Static/ISR | — |
| `/support` | FAQ, policy links, contact placeholder | Done (contact placeholder) | Static/ISR | — |
| `/support/{delivery,cancellation,refund}-policy` | Policy outlines, visibly marked placeholders | Done (placeholder text) | Static/ISR | — |
| 404, error, global error | Not found, retry with trace id | Done | — | — |
| `/sitemap.xml`, `/robots.txt` | Catalogue sitemap (static pages, categories, up to 2,000 products); robots disallows customer pages, allows all only in production with a site URL | Done | Dynamic | `GET /categories`, `GET /products` (cached 1 h) |

Loading skeletons: root, `/shop`, `/products/[slug]`, and inside each client view. Empty states: cart, orders,
addresses, wishlist, inbox, slots, search, listing.

**Placeholder text for the owner.** `<OwnerPlaceholder>` (in `src/components/content-page.tsx`) renders a dashed
"Placeholder text: to be replaced by the store owner" box. It is on About, Support (contact) and all three policy
pages, whose sections are outlines prefixed `[Placeholder]`. Search the code for `OwnerPlaceholder` before launch.

## 3. Location (pincode)

- Cookie `fo_pincode` (readable, `SameSite=Lax`, 1 year) set only after `GET /serviceability` says the pincode is
  served. An unserviceable pincode shows "We do not deliver to … yet" and is not stored.
- Header chip opens the picker; it also opens on the first visit unless dismissed for the session.
- Server pages send it as `X-Pincode`. If the API refuses it (`PINCODE_NOT_SERVICEABLE`, `CITY_NOT_ACTIVE`) the page
  falls back to an anonymous read and shows a banner. No pincode → products without prices (`locationRequired`),
  price filters/sorts dropped, "Set your pincode to see the price".
- Changing the pincode with a priced cart calls `PUT /cart/location`; 409 `CART_LOCATION_CHANGED` lists the lines that
  will drop and asks before `force=true`. Checkout does the same when the chosen address is served by another store.

## 4. Sign-in and tokens (BFF)

The browser never sees the refresh token.

```
browser ──/bff/auth/verify──▶ Next route handler ──POST /auth/otp/verify──▶ API
        ◀── {accessToken, user} + Set-Cookie fo_rt (httpOnly, Secure, SameSite=Strict, Path=/bff/auth)
browser ──/api/v1/* (Authorization: Bearer <access token from memory>)──▶ Next proxy ──▶ API
```

| Piece | Where | Why |
|---|---|---|
| Access token (15 min) | JS module memory only (`src/auth/session-store.ts`) | Not readable after the tab closes; nothing in `localStorage`/`sessionStorage` for XSS to lift for later use |
| Refresh token | httpOnly cookie `fo_rt`, `SameSite=Strict`, `Path=/bff/auth`, `Secure` in production | Unreadable by scripts; sent only to the three BFF handlers |
| Session hint | cookie `fo_session=1` (readable, no secret) | Lets a reload decide whether to try `/bff/auth/refresh` without a wasted call |
| Guest cart token | httpOnly cookie `fo_cart`; the proxy adds `X-Cart-Token`, captures a newly issued one and blanks `cartToken` in cart JSON | Same reasoning as the refresh token; passed to OTP verify so the API merges the guest cart |

- **CSRF:** the BFF handlers require `X-Fresh-Client: web` (a custom header forces a CORS preflight that the site never
  allows cross-origin) and check `Origin` / `Sec-Fetch-Site` are same-origin. `SameSite=Strict` is the third layer.
  The proxy carries bearer tokens, not cookies, for customer auth.
- **Refresh:** single flight per tab plus a Web Lock across tabs, because refresh tokens rotate and two tabs racing
  would trip reuse detection. A failed refresh ends the session (`fo_rt` and `fo_session` cleared).
- **Proxy:** `/api/v1/[...path]` forwards to `FRESH_API_ORIGIN`, adds `X-Pincode` from the cookie and
  `X-Forwarded-For`, and refuses `admin/*`, `auth/otp/verify`, `auth/refresh`, `auth/logout`, `payments/webhooks/*`,
  `dev/*` (404). The API must trust `X-Forwarded-For` only from the website host for per-IP rate limits.
- After verify the cart is read again immediately and once more 2.5 s later, because the merge is asynchronous.
- `/login?next=` follows only same-site paths (`//host` and absolute URLs go to `/`).

## 5. Data fetching

- Server components: `requestApi()` (`src/api/server.ts`) builds a `@fresh/core` client with `X-Pincode`, no cache.
  Public reads that do not depend on the visitor use `publicHttp(revalidateSeconds)`.
- Client components: `browserHttp()` (`src/api/browser.ts`) → same-origin proxy, bearer from memory, refresh on 401.
- Typed endpoint functions live in `src/api/endpoints/*.ts`; components never build URLs.
- Writes that need `Idempotency-Key` (place order, cancel, complaint) keep **one key per user action**: reused when
  the same action is retried after a retryable failure (network, 429, 503, `IDEMPOTENCY_IN_PROGRESS`), replaced after
  a definitive answer. After `PRICE_CHANGED` the customer confirms the new total with a **new** key and the new
  `expectedTotalPaise`.
- Errors: logic branches on `code`; the text shown is the server's message (`errors[field][0].message` under the
  field, otherwise `detail`), with our own copy only for transport failures. Error states show the `traceId`.

## 6. API gaps found while building (for Phase 3 owners)

| # | Gap | What the website does now |
|---|---|---|
Re-checked against the generated OpenAPI document on 2026-10-24 (D63).

| # | Gap | Status | What the website does now / what closes it |
|---|---|---|---|
| W1 | No bestsellers endpoint | STILL OPEN | Home shows `GET /home` `featuredProducts` titled "Featured today". Closes with a Catalog/Reporting bestsellers read |
| W2 | No price quote for a chosen weight before it is in the cart | STILL OPEN | `GET /products/{id}/availability` gives the unit price only; estimate and maximum appear from the cart line. Closes with a quote endpoint (variant, preparation, quantity → estimate, max chargeable) |
| W3 | Product list items carry no variant ids | STILL OPEN | No quick-add on cards. Closes with the default variant id (and its order rules) on `ProductListItem` |
| W4 | Cart lines lack `minOrder` / `maxOrder` / `orderStep` | STILL OPEN | Quantity editor fetches the product when opened. Closes with the three fields on `CartLineView` |
| W5 | `GET /me/wishlist` returns ids only | STILL OPEN | Always a dated placeholder row linking nowhere. Closes with a product summary (slug, name, image, price) per item |
| W6 | Shapes not shown in docs/api | CLOSED | All in the spec now; `/promotions`, `/products`, `/orders` are paged, `/cities`, `/suggest`, `/me/addresses`, `/me/wishlist`, complaints are bare arrays. `GET /home` `banners` is still `object[]`, so banners come from `/promotions` |
| W7 | No public business profile (support phone, email, hours) | STILL OPEN | Static owner placeholder on `/support`. Closes with a public `GET /business-profile` |
| W8 | No public list of preparation methods for filtering | STILL OPEN | Preparation filter omitted on `/shop`. Closes with a public `GET /preparation-methods` |
| W9 | API must trust `X-Forwarded-For` from the web host | STILL OPEN | Proxy sends it. Closes with forwarded-headers config for the web host (security review SEC-2) |
| W10 | Time zone for display | STILL OPEN | `@fresh/core` formats in IST. Closes with the location's `timeZone` in slot and order responses |
| W11 | Banner `imageKey` is a storage key, not a URL | NEW, OPEN | Offer strip shows text only. Closes with a resolved image URL on `BannerInfo` |
| W12 | Complaint create is JSON with no attachments | NEW, OPEN | Report-a-problem form has no photo field. Closes with a photo upload (multipart or upload URL) on complaints |

## 7. API types (generated)

`src/api/types/*.ts` (`common`, `catalog`, `locations`, `cart`, `auth`, `checkout`, `orders`, `account`) are now
thin aliases over `@fresh/api-client` (`Schema<"Name">` from the generated `schema.d.ts`), not hand-written shapes
(D63). Two helpers in `common.ts`:

- `Coded<T, { field: Union }>` narrows a field the backend types as plain `string` (the C# record holds a string
  code, e.g. cart `status`, order `status`/`paymentMethod`, address `label`) to the code union the app switches on.
- `RequestBody<T>` makes nullable request fields optional; the spec marks every request field required.

Regenerate after a backend change: run the API, `pnpm --filter @fresh/api-client fetch-spec`, `pnpm generate:api`;
`pnpm typecheck` then shows every shape difference. HTTP still goes through `@fresh/core` `HttpClient`.

Differences from the old temporary types that changed app code: addresses use `addressId`; preparation on cart
lines and the purchase panel is nullable; order list uses `slotStartAt` and `finalTotalPaise ?? totalPaise`;
wishlist items are ids only; banners use `subtitle` and no image; complaints post JSON without photos;
`/suggest`, `/cities`, addresses, wishlist and complaints are bare arrays, `/promotions` and `/me/coupons` paged.

Spec issues still open (backend, not metadata-only, so not fixed in D63):

- No `operationId` on any of the 216 operations (needs `.WithName(...)` per endpoint).
- About 65 code fields are plain `string` because the C# records hold strings (order `status`/`paymentMethod`,
  cart `sellUnit`/`status`, complaint `kind`/`status`, …); the apps narrow them with `Coded`.
- Request records mark every field required, including nullable ones (hence `RequestBody`).
- `[AsParameters]` query names are PascalCase in the spec (`Q`, `Status`, `Page`, `PageSize` on
  `/admin/customers`; `UnreadOnly`, `Page`, `PageSize` on `/me/notifications`; `Page`, `PageSize` on `/products`).
- Headers `X-Pincode`, `If-Match`, `Idempotency-Key`, `ETag` are not documented.
- 4xx Problem responses are documented on 38 operations only; problem extensions (`CART_LOCATION_CHANGED` lines,
  `PRICE_CHANGED` preview) are untyped.
- Untyped objects: `HomeResponse.banners` (`object[]`), `OrderPaymentDto.nextAction`, report and dashboard
  `filters`/`totals`/`items`; the bulk price upload body is read by hand and has no schema.
- XML doc comments (code lists) are not surfaced; reporting endpoints are tagged `FreshOrbit.Api`.

## 8. SEO and accessibility

- Metadata per page; product pages have canonical, Open Graph image and schema.org `Product` JSON-LD whose offers use
  only server prices and availability (`src/lib/json-ld.ts`; `<` escaped). Customer pages are `noindex`.
- Skip link, landmarks, labelled form fields with field-level errors (`aria-invalid`, described by), keyboard-usable
  dialogs/sheets (Radix), visible focus rings from `@fresh/ui`, text colours from the WCAG-AA token set (D32).
- Lighthouse (target ≥ 90 for performance, accessibility, best practices, SEO) is not measured yet: it needs a running
  API with catalogue data (Phase 4 sample data).

## 9. Tests

| File | Proves |
|---|---|
| `src/location/pincode-form.test.tsx` | Serviceable pincode stored + pages refreshed; unserviceable not stored; malformed refused locally; `CART_LOCATION_CHANGED` asks before `force=true` |
| `src/components/product/purchase-panel.test.tsx` | Add to cart sends variant, preparation, quantity and `X-Pincode`; estimate and maximum come from the server line; field error from the server; no pincode → picker |
| `src/components/cart/cart-view.test.tsx` | Server totals rendered as sent; coupon refusal under the field; empty state |
| `src/components/auth/login-form.test.tsx` | OTP request with +91, verify through BFF with `X-Fresh-Client`, token in memory only, redirect to `next`; `OTP_INVALID` under the field; malformed number; safe redirect |
| `src/components/checkout/checkout-view.test.tsx` | `PRICE_CHANGED` → dialog with old/new server totals → confirm with new key and new `expectedTotalPaise`; network failure retry reuses the key; FULL slot disabled |
| `src/components/orders/order-detail-view.test.tsx` | Delivery code only when sent; cancel only when allowed, with reason and key |
| `e2e/smoke.spec.ts` (Playwright) | Home shell renders, skip link first in tab order, 404 page |
