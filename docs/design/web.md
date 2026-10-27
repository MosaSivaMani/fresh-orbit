# Customer website (`apps/web`, Phase 7)

Next.js 16 App Router, React 19, TypeScript, Tailwind v4 through `@fresh/config`, components from `@fresh/ui`,
HTTP and formatting from `@fresh/core`. Owner: E2. Wireframes: `docs/design/README.md` §11.1.

**Rule 4 holds everywhere:** the website never computes a price, discount, fee, tax, total or stock figure. Every
amount on screen is a server value formatted by `@fresh/core` (`formatRupees`, `formatQuantity`, …). The only
arithmetic is unit conversion of user input (shop price filter: rupees typed → paise sent) and display paging.

## 0. Look: food-app style (D95)

The website follows `docs/design/mockups/web-food-app.html` **version 2** (the owner rejected a widened copy of
the app; the mobile app keeps D88 "Bold blocks"). Fonts per D89: Baloo 2 800 headings, Dancing Script 700 for
the handwritten word, Inter body, all self-hosted from `@fresh/ui/fonts.css`. Phone widths use the same
components; nothing is a separate mobile design.

| Part | What it is | Data |
|---|---|---|
| Header (`components/header/site-header.tsx`, `header-nav.tsx`) | Coral bar at every width: logo, "Delivering to 530017 ▾" (opens the pincode picker), Offers, Help, Orders (signed in), Sign in / Account, ink Cart pill with a coral count. Phones show the link icons; the words stay as the links' names | Cart count = lines in the server's cart |
| Home hero (`app/page.tsx`, `FoodHero`) | Coral block, headline "Fresh fish. *Cleaned your way.* At your door, in your slot." (the handwritten words on a paper brush swipe), a white pincode box (`PincodeForm variant="hero"`, asks `GET /serviceability`), the search box with suggestions, three white `PromiseCard`s, two tilted character photos at the sides from `lg` up (`ONBOARDING_CLEANED`, `ONBOARDING_DELIVERY`) | Third promise shows the server's next slot; the other two state store policy (cut choice, pay for net weight) only |
| "What's on your *mind?*" (`CategoryRail`, `CategoryCircle`) | Round 88/110 px circles with the category picture, muted label; scrolls sideways on phones | `GET /home` `featuredCategories` |
| "Top picks in *city*" | Picture-card grid, 1 column below 480 px, then 2/3/4 | `featuredProducts`; city from `GET /serviceability` `city.name` (rule 7: no city names in code); "Top picks *today*" without a pincode |
| Picture card (`ShopProductCard` → `FoodCard`) | 16:11 picture, offer line on a dark gradient, name, round teal mark + freshness line, local names, price with struck MRP, ADD → − n + stepper | Offer line only from `GET /promotions` (a banner linking to the product, or on a category page a banner linking to that category); freshness = the item's availability message, else the next slot; nothing invented |
| ADD | With one size: fetches the product, adds `minOrder` with the default cut through `POST /cart/items`; with several sizes: opens the product page. Becomes a stepper when the cart holds exactly one line of the product (bounds from the product endpoint) | Server cart |
| Offers, "Filter and *pick*" | Mango offer cards (`OfferStrip`); link chips (price sort and "Under ₹500" only with a pincode, wild, farmed, in stock, newest) over eight more cards | `GET /promotions`, `GET /products?pageSize=8` |
| Get the app (`GetTheApp`, `AppBand`) | Mango-soft band, store buttons, character circle | Links from `FRESH_PLAY_STORE_URL` / `FRESH_APP_STORE_URL`; unset → "coming soon" text, no invented links |
| Footer | Dark ink: logo, shop and help links, cities we serve, small print | `GET /cities` |
| Shop, category, search (`PageBand`) | Coral page band with breadcrumbs, title (handwritten word), description and the search box; then the category circles, the chip row (sort, wild, farmed, in stock, "More filters" sheet with categories and price), the card grid and pagination. No results → `CharacterState` (`NO_SEARCH_RESULTS`) | `GET /products` with the URL filters |
| Product | Ice-tinted picture panel with a source sticker; title with the first local name handwritten; facts row (source, region, next slot); size and cut on `OptionTile`s ("Choose your cut"); pack/piece weight; coral net-weight note; quantity stepper and a teal "Add to cart · ₹/unit" bar (sticky at the bottom on phones); delivery box; tabs; "More *category*"; get the app | `GET /products/{slug}`, serviceability |

Images: the API returns picture URLs as paths (`/media/...`). `app/media/[...path]/route.ts` serves them on the
website's origin from `FRESH_API_ORIGIN` (GET, images only, no cookies forwarded), so cards show the real product
photos in development and behind Caddy.

### 0.1 Inner pages in the app theme (D181, D188; PR #22)

Every page except the home (PR #20) uses the same look as the app and the owner's home reference. Code:
`src/components/theme/` (`marks.tsx`, `theme-band.tsx`).

| Part | What it is |
|---|---|
| `ThemeBand` (replaces `PageBand` on the web) | The home hero's peach-to-cream harbour sky (`.fo-sky` in `globals.css`), shorter: breadcrumbs, a Baloo 2 800 heading with the key word in a `Highlight` (the yellow `#F5D04A` brush), coral `Sparks` after it, an optional coral Dancing Script line with a `Swoosh`, a description, `Gulls`, an optional owner picture in a ringed circle from `sm` up, and `BandWaves` curving into the page. Shop, category, search, cart, checkout, sign-in, account and order pages |
| `Highlight` | `Brush` from `@fresh/ui` in the home's yellow; the word stays ink text |
| `SkyPanel` | The same sky behind a whole-page state: 404, route error (global error uses `.fo-sky` on the body) |
| `Section` heading | Coral sparks after the teal script word; the action is an outlined dark-teal "View all →" pill (as on the reference) |
| `ContentPage` | About, help and the three policies: band, then the text on a paper card (radius 24) with a side card from `lg` (contact, other policies, "Ready to order?") |
| Cards | Small cards radius 20 (D189): wishlist cards, FAQ items, product facts, delivery box, substitutions, offers |
| Pictures | Owner pictures (FINAL slots) in the band circle: shop and about `ONBOARDING_FRESH`, cart `ONBOARDING_CLEANED`, checkout and help `ONBOARDING_DELIVERY`; empty states keep their EMPTY_* pictures |

The marks are our own shapes and are decorative (`aria-hidden`, no motion). Server Components use `cx`, never `cn`
(client bundle). The home's `scenery.tsx` (PR #20) has its own copies; one shared set is a follow-up.

Behaviour added in the same PR (QA findings):

| Where | What | API |
|---|---|---|
| Order page | "Substitutions" card: each proposed item with the server's price difference and deadline, Accept / Reject; the answer's re-priced order replaces the cached one | `POST /orders/{id}/substitutions/{sid}/accept`, `/reject` |
| Report a problem | Optional photos, sent one by one after the complaint is created; a refused photo shows the server's message and never undoes the complaint | `POST /orders/{id}/complaints/{cid}/photos` (multipart `file`) |
| Checkout | The place-order `Idempotency-Key` is dropped when the address, slot, payment method, substitution choice or note changes, so a retry never reuses a key with a new body | `POST /orders` |
| Header | "Offers" shows only when the server has a banner for the pincode (`/#offers` exists only then) | `GET /promotions` |
| Address form | Pincode first; a full pincode fills an empty city (serviceability) and state code (cities list) | `GET /serviceability`, `GET /cities` |
| Product | Sold-out size: "Notify me when it is back" (guests sign in first), "Stop alert" when set; the quote line under the options | `/me/stock-alerts`, `GET /price-quote` |
| Cart | "View offers": the customer's coupons with the server's description, minimum order and end date, each with Apply | `GET /coupons`, `PUT /cart/coupon` |
| Wishlist, help | Product cards; contact card | `GET /me/wishlist`, `GET /business-profile` |

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
URLs, JSON-LD, sitemap; missing → no canonical host, empty sitemap, robots disallow all), `FRESH_PLAY_STORE_URL` and
`FRESH_APP_STORE_URL` (optional; the "get the app" band shows only the links that are set).

## 2. Page inventory

Mode: **SSR** = rendered per request on the server (reads the pincode cookie, so not cached); **Static/ISR** =
prerendered, regenerated at most every 5 minutes (root layout `revalidate = 300`, for the footer's city list; E2E-6);
**Client** = shell
prerendered, data fetched in the browser with TanStack Query through the same-origin proxy (per-customer data,
`noindex`).

| Route | Page | Status | Mode | API calls |
|---|---|---|---|---|
| `/` | Home: hero with next slot, pincode prompt, search, categories, offers, featured products, trust row | Done | SSR | `GET /home`, `GET /categories`, `GET /promotions`, `GET /serviceability` |
| `/shop` | All products, filters (category, source, in stock, price), sort, pagination; URL-synced | Done | SSR | `GET /products`, `GET /categories` |
| `/shop/[category]` | Category listing with sub-category chips | Done | SSR | `GET /categories/{slug}`, `GET /products?category=` |
| `/search?q=` | Search results; header box has suggestions | Done | SSR (noindex) | `GET /products?q=`; suggestions `GET /search/suggest` (client) |
| `/products/[slug]` | Gallery, variants, preparation with charge and yield note, quantity/weight stepper, availability, add to cart, in-cart estimate and maximum, delivery box, description/storage/sourcing tabs, related, JSON-LD | Done | SSR + client panel | `GET /products/{slug}` (`?preparation=` from the panel), `GET /serviceability`, `GET /products?category=`, `POST /cart/items`, wishlist |
| `/cart` | Server-priced lines with a − n + stepper (bounds from the product), remove, coupon strip, slot buttons (carried to checkout as `?slot=`), bill box, blockers, messages, location note, sticky Place order bar | Done | Client (noindex) | `GET /cart`, `PATCH/DELETE /cart/items/{id}`, `PUT/DELETE /cart/coupon`, `GET /products/{slug}` (stepper bounds, one read per line), `GET /delivery-slots?pincode=` |
| `/login?next=` | Phone → OTP, resend timer, guest cart merge, safe redirect; 429 waits for `Retry-After` (default 30 s) with the buttons disabled; 503 `SMS_PROVIDER_UNAVAILABLE` shows the server's message and "Try sending again" to the same number | Done | SSR shell (reads `next`) + client form (noindex) | `POST /auth/otp/request`; `POST /bff/auth/verify` |
| `/checkout?slot=` | Address choose/add, slot by day (preselected from the cart's `?slot=`), pay on delivery (COD / UPI on delivery from `paymentMethods`), substitution preference, notes, live preview with blockers, sticky Place order bar with `Idempotency-Key`, `PRICE_CHANGED` confirm, `CART_LOCATION_CHANGED` move | Done | SSR shell (reads `slot`) + client, signed in | `GET /cart`, `GET/POST /me/addresses`, `GET /delivery-slots?pincode=`, `POST /checkout/preview`, `POST /orders`, `PUT /cart/location` |
| `/orders/[orderId]/success` | Order-placed screen: ORDER_PLACED character, number, slot, payment, bill from the server's estimate | Done | Client, signed in | `GET /orders/{id}` |
| `/orders/[orderId]` | Coral band with number and status character, status steps, delivery code (only when sent), slot, address, rider, items ordered vs charged, the bill (estimate or final), amount due, cancel (reason, IK), report a problem (JSON, no photos yet — W12, IK), invoice, order again — each only when `allowedActions` says so | Done | Client, signed in | `GET /orders/{id}`, `GET /orders/{id}/tracking` (polls every 60 s until closed), `GET/POST /orders/{id}/complaints`, `POST /orders/{id}/cancel`, `GET /orders/{id}/invoice`, `POST /orders/{id}/reorder` |
| `/account` | Profile (PATCH with `If-Match`) | Done | Client, signed in | `GET/PATCH /me` |
| `/account/settings` | Contact preferences (moved from notifications), sign out, delete account (typed confirmation, `If-Match`) | Done | Client, signed in | `GET/PUT /me/notification-preferences`, `GET/DELETE /me`, `POST /bff/auth/logout` |
| `/account/orders` | Order cards with the status character, paginated | Done | Client, signed in | `GET /orders` (no `sort` parameter; server order) |
| `/account/addresses` | List, add, edit (`If-Match`), delete, make default | Done | Client, signed in | `/me/addresses…` |
| `/account/wishlist` | Saved items, remove | Done (ids only, see §6) | Client, signed in | `GET/DELETE /me/wishlist…` |
| `/account/notifications` | Inbox (mark read / all read, paged); preferences switches (ORDER_UPDATES SMS locked on) are on `/account/settings` | Done | Client, signed in | `GET /me/notifications`, `POST …/read`, `POST …/read-all` |
| `/about` | About with owner placeholder | Done (placeholder text) | Static/ISR | — |
| `/support` | FAQ, policy links, contact placeholder | Done (contact placeholder) | Static/ISR | — |
| `/support/{delivery,cancellation,refund}-policy` | Policy outlines, visibly marked placeholders | Done (placeholder text) | Static/ISR | — |
| 404, error, global error | Character states (NOT_FOUND, ERROR_GENERIC), retry with trace id | Done | — | — |
| `/sitemap.xml`, `/robots.txt` | Catalogue sitemap (static pages, categories, up to 2,000 products); robots disallows customer pages, allows all only in production with a site URL | Done | Dynamic | `GET /categories`, `GET /products` (cached 1 h) |

Loading skeletons: `/shop` only (in the `shop/(all)` route group), and inside each client view. A loading boundary
above a page flushes a 200 shell before `notFound()` runs, so the root and product loading files were removed and
unknown product and category slugs answer a real 404 (QA 5). Empty states: cart, orders,
addresses, wishlist, inbox, slots, search, listing. The cart, checkout, sign-in, orders, account, empty, error and
not-found pages follow the food-app look (§10).

**Owner text.** About and the three policy pages show plain, neutral customer text with no percentages, timelines or
charges the server does not provide. A code comment `OWNER TEXT` marks where the owner's final wording goes. No
developer note is ever visible to customers (QA 1). The help page's contact card reads `GET /business-profile`.

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
  Public reads that do not depend on the visitor use `publicHttp(revalidateSeconds)`. Categories and the footer's
  city list use 300 s, so a city that staff activate shows in the footer within about 5 minutes (E2E-6,
  `research/deploy-gaps-ops-2026-10.md` §3). The sitemap uses 3600 s.
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
| W1 | No bestsellers endpoint | CLOSED (`web/api-adoption`) | Home keeps "Top picks in <city>" from `featuredProducts` and adds "Bestsellers in <city>" from `/home` `bestsellers` (CAT-R24); the section is left out when the list is empty, never filled |
| W2 | No price quote for a chosen weight before it is in the cart | ADOPTED (PR #22): the product panel shows `GET /price-quote` (estimate, and the maximum for weighed items) | Adopted in the website |
| W3 | Product list items carry no variant ids | STILL OPEN | ADD on a card first reads the product (one extra request), then adds when there is one size; with several it opens the product page. Closes with the default variant id (and its order rules) on `ProductListItem` |
| W4 | Cart lines lack `minOrder` / `maxOrder` / `orderStep` | STILL OPEN | Each line's − n + stepper reads its product (`GET /products/{slug}`, cached 60 s) for the bounds and stays disabled until they arrive: one read per line. Closes with the three fields on `CartLineView` |
| W5 | `GET /me/wishlist` returns ids only | ADOPTED (PR #22): saved items are cards with picture, name, server price and a link | Adopted in the website |
| W6 | Shapes not shown in docs/api | CLOSED | All in the spec now; `/promotions`, `/products`, `/orders` are paged, `/cities`, `/suggest`, `/me/addresses`, `/me/wishlist`, complaints are bare arrays. `GET /home` `banners` is still `object[]`, so banners come from `/promotions` |
| W7 | No public business profile (support phone, email, hours) | ADOPTED (PR #22): help page contact card | Adopted in the website |
| W8 | No public list of preparation methods for filtering | STILL OPEN | Preparation filter omitted on `/shop`. Closes with a public `GET /preparation-methods` |
| W9 | API must trust `X-Forwarded-For` from the web host | STILL OPEN | Proxy sends it. Closes with forwarded-headers config for the web host (security review SEC-2) |
| W10 | Time zone for display | STILL OPEN | `@fresh/core` formats in IST. Closes with the location's `timeZone` in slot and order responses |
| W11 | Banner `imageKey` is a storage key, not a URL | NEW, OPEN | Offer strip shows text only. Closes with a resolved image URL on `BannerInfo` |
| W12 | Complaint create is JSON with no attachments | ADOPTED (PR #22): photos go one by one to `POST …/complaints/{id}/photos` after the complaint is created | Adopted in the website |

| W13 | List items carry no weight, pieces or serves | CLOSED (`web/api-adoption`) | Picture cards show the default variant's `serving` as "1 kg · 10–14 pcs · serves 4" (`lib/product-facts.ts` `servingLine`, per the server's basis, never scaled), else the local names; the product page adds the chosen size's line under pack and piece weight |
| W14 | A CATEGORY banner's `linkTarget` is the category id, not its slug | CLOSED (`web/api-adoption`) | Offer strip links CATEGORY banners by `linkSlug` (featured-category id → slug map as fallback) and PRODUCT banners by `linkSlug` only (they linked to `/products/<id>` before, a broken page); `offersBySlug` keys product offers by `linkSlug` (it keyed by the id and never matched a card) |
| W15 | Image URLs are paths on the API origin (`/media/...`) | NEW, OPEN | The website serves them through `/media/*` (route handler). Closes with absolute public image URLs, or keeps the route |
| W16 | No product-specific offer or freshness ("caught 5:40 am") fields | CLOSED (`web/api-adoption`) | Card offer line = item `offer.text` (CAT-R26), else a banner title; freshness = availability message, else "Caught <date>" from `freshness.caughtOn` (CAT-R25), else the next slot; `PriceTag` shows "<n>% off" from `discountBp` (CAT-R22) on cards and on the product page. Nothing is computed in the browser |

**Backend Track E (2026-10-24).** These gaps are closed in the backend: on `main` through #5, or on draft PR #6 (`backend/api-gaps-2`, not merged). The website has not adopted them yet; the rows above stay as the front-end status until it does. The generated client is not committed yet, because the new `timeZone` fields break `apps/web/test/fixtures.ts` and that fix belongs to the web owner. The CI `openapi` job uploads the regenerated client as the `api-client` artifact.

| # | Backend now | Where |
|---|---|---|
| W1 | `GET /bestsellers` and `/home` `bestsellers` (CAT-R24) | main |
| W2 | `GET /price-quote` for a chosen weight | main |
| W3 | `ProductListItem.defaultVariant` (CAT-R23) | main |
| W4 | Cart lines carry `minOrder` / `maxOrder` / `orderStep` | PR #6 |
| W5 | Wishlist returns priced product cards (CAT-R29) | PR #6 |
| W7 | Public `GET /business-profile` with `supportHours` | PR #6 |
| W8 | Public `GET /preparation-methods` (CAT-R27) | main |
| W9 | Forwarded headers trusted from the web hop only (edge network) | PR #6 |
| W10 | `timeZone` on slots, order detail, list and tracking (ORD-R65); serviceability `nextSlot` date and `cutoffAt` for the ETA | PR #6 |
| W11 | Typed `/home` banners with a resolved `imageUrl` (CAT-R28) | PR #6 |
| W12 | Complaint photo upload, list and delete | main |
| W13 | Variant `piecesMin` / `piecesMax` / `serves` (CAT-R21) | main (migration in PR #6) |
| W16 | `offer` (CAT-R26), `freshness` (CAT-R25) and `discountBp` (CAT-R22) | main |
| W14 | Banner `linkSlug` for CATEGORY and PRODUCT links (PROMO-R28); `linkTarget` stays the id | main (PR #12) |
| W15 | Production refuses a relative or non-https `FileStorage:PublicBaseUrl`, so server image URLs are absolute (CAT-R18) | main (PR #12) |

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
| `src/components/cart/cart-view.test.tsx` | Server line totals, bill and sticky total rendered as sent; stepper steps by the product's `orderStep` and shows the server's new line; below minimum removes the line; chosen slot button carried to checkout (`?slot=`, through sign-in for a guest); coupon refusal under the field; empty-cart character; error state with trace id |
| `src/components/auth/login-form.test.tsx` | OTP request with +91, verify through BFF with `X-Fresh-Client`, token in memory only, redirect to `next`; `OTP_INVALID` under the field; malformed number; 429 waits for `Retry-After` with Send disabled; 503 `SMS_PROVIDER_UNAVAILABLE` shows the server's message and resends to the same number; safe redirect |
| `src/components/checkout/checkout-view.test.tsx` | `PRICE_CHANGED` → dialog with old/new server totals → confirm with new key and new `expectedTotalPaise`; network failure retry reuses the key; FULL slot disabled; cart's slot priced from `?slot=`; server-disabled ONLINE tile disabled; UPI on delivery re-prices |
| `src/components/orders/order-detail-view.test.tsx` | Delivery code only when sent; status steps up to the server's status; bill uses the server's final total; not-found character for an unknown order; cancel only when allowed, with reason and key |
| `src/components/orders/order-success.test.tsx` | ORDER_PLACED character, number, payment and the server's estimate bill |
| `src/components/account/account-pages.test.tsx` | Account chips mark the current section; settings: locked order SMS switch, sign out through the BFF; addresses empty character and list; order cards link to tracking with the server's total; empty-orders character |
| `src/lib/order-progress.test.ts` | Status → steps (usual path, delivered, off-path timeline) and status → character |
| `src/app/not-found.test.tsx` | 404 and route error character states, reference and retry |
| `e2e/smoke.spec.ts` (Playwright) | Home shell renders, skip link first in tab order, 404 page |

## 10. Food-app pages (D95): cart, checkout, sign-in, orders, account, states

Built by W2 on `web/food-app-pages` against `docs/design/mockups/web-food-app.html` version 2, inside B1's coral
header and dark footer. Same components at phone and desktop widths; the only width changes are grid columns.

| Page | Look |
|---|---|
| Every inner page | `PageBand` (coral band, Baloo 2 heading with one Dancing Script word via `Script`), then the page in `Container` |
| Cart | `StepCard` "n items" with rows: rounded picture, name, variant · cut, the server's line total (and "at most"), unit price, `QuantityStepper` (− n +, bounds from the product, below min removes), remove. `StepCard` "Delivery slot" with `SlotPicker` (day `FilterChip`s, `OptionTile look="ink"` slot buttons). Right column: `CouponStrip` / dashed mango coupon form, `BillBox`, blockers. `StickyActionBar` at the bottom: server's estimated total and Place order |
| Checkout | `StepCard` 1 address (`OptionTile` per address, unserviceable disabled), 2 slot (`SlotPicker`), 3 pay on delivery (`OptionTile` per server `paymentMethods`, disabled when the server says so) and substitution tiles, notes. Right column: `BillBox` from the preview. `StickyActionBar` "Place order · ₹…" |
| Order placed | `CharacterState` ORDER_PLACED (md circle), slot and payment, `BillBox` from the estimate, Track / Continue |
| Sign in | Coral band "Welcome back", white card with the LOGIN_OTP circle on its top edge; 429 / 503 notices above the form |
| Orders | Cards with the status character (`orderSlot`), number, `StatusBadge`, slot, items, server total |
| Tracking | Band with number, status and character; `StepCard` "Tracking" with `StatusSteps` (across from `sm`, down on phones) and the slot/address/rider box; actions; items; `BillBox` "Bill" (final when packed) and amount due |
| Account | Section `FilterChip` links (Profile, Orders, Addresses, Saved items, Notifications, Settings); pages in `StepCard`s; Settings holds contact preferences, sign out and delete account |
| Empty / error / 404 | `CharacterState` with the slot: EMPTY_CART, EMPTY_ORDERS, EMPTY_ADDRESSES, EMPTY_WISHLIST, EMPTY_NOTIFICATIONS, ORDER_PACKING (no slots), ERROR_GENERIC, OFFLINE (network / 503 shop), NOT_FOUND. Load errors show the server's message, the trace id and Try again (`src/components/states.tsx`) |

New `@fresh/ui` components (`packages/ui/src/components/food-app-checkout.tsx`, tests beside it): `StepCard`,
`BillBox` (rows and a total passed in, never summed), `StickyActionBar`, `StatusSteps` (`aria-current="step"`).
`src/lib/order-progress.ts` maps the server's status to which step is lit and which character shows; it never
decides an order's state.

Rule 4: every amount on these pages is a server value formatted by `@fresh/core`. The stepper proposes a requested
quantity; the server's returned cart replaces the cached one.
