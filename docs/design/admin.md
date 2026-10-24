# Admin dashboard (`apps/admin`, Phase 6)

Staff web app: React 19 + Vite, React Router 7 data router (lazy routes), TanStack Query through `@fresh/core`,
`@fresh/ui` components, Tailwind via `@fresh/config`, react-hook-form + zod. Wireframes: `docs/design/README.md`
§11.2. API: `docs/api/README.md` §4.9–4.17. Owner: E3.

## 1. How it works

| Concern | Implementation |
|---|---|
| Sign-in | `POST /admin/auth/login` (email + password). A refusal shows only `INVALID_CREDENTIALS` (D33: no locked/disabled hint; other codes show the server sentence). 429 `RATE_LIMITED` shows "Try again in …" counted down from the server's `retryAfterSeconds` (body, else `Retry-After`) and keeps Sign in disabled until then. Access and refresh tokens in `sessionStorage` (`fresh-orbit.admin.session`), cleared when the tab closes. `GET /admin/auth/me` loads the profile |
| Refresh | `@fresh/core` `createAuthFetch`: a 401 `TOKEN_EXPIRED` refreshes once (`POST /admin/auth/refresh`) and retries. A failed refresh or any other 401 marks the session expired → `/login?reason=expired&next=…` |
| Sign-out | `POST /admin/auth/logout` with the refresh token, then the session and query cache are cleared |
| Permissions | JWT `perm` claim (repeated) decides menus, routes and buttons (`useCan`). A requirement is one code, a list (all of them) or `{ anyOf: [...] }`. A route without the permission shows "You do not have access to this page"; a 403 from the API shows the same. The API enforces everything again |
| Location scope | JWT `loc` / `all_loc`. Top-bar switcher (`Fulfillment location`): "All locations" only for `all_loc`; remembered in `localStorage`. Lists send `fulfillmentLocationId` when one is chosen |
| Lists | URL holds `page`, `pageSize`, `sort`, `q` and filters (`useListParams`); server paging/filtering only; `DataTable` + `FilterBar`; loading, empty and error (with trace id) states |
| Writes | `If-Match` from the body `version` or `ETag` on every update; `If-None-Match: *` for first-time creates where the API says so; `Idempotency-Key` (kept across retries, `useIdempotencyKey`) where the API marks IK |
| Errors | Problem Details field errors land on the form fields (`applyFieldErrors`, API paths such as `lines[0].packedQuantity.value` mapped to form paths); 412 `CONCURRENCY_CONFLICT` → "This changed since you opened it" with Reload; other refusals → toast with the server sentence and trace id |
| Business values | Never computed: totals, amounts due, discounts, availability, readiness and report totals are shown as the server returns them. The client only converts what staff type (₹ → paise, % → bp, IST → UTC) in `src/lib/units.ts` |

## 2. Technical choices (proposed for the decision log)

| Choice | Why |
|---|---|
| React Router 7.18.4 (not 8.x) | 8.x was under two weeks old at adoption |
| msw 2.15.0 (not 3.x) | Same reason |
| Tokens in `sessionStorage` | Staff sessions end with the tab; nothing survives a shared-PC browser restart. No cookies, so no CSRF surface |
| One app-wide conflict prompt | Same 412 handling on every screen |
| Lazy route per screen | Initial bundle holds only the shell, login and shared UI |
| Types are aliases over `@fresh/api-client` in `src/api/types/*.ts` | Generated from the backend OpenAPI document (D63); see §4 |

## 3. Screen inventory

Status: **done** = built and tested against MSW mocks shaped like `docs/api`; **partial** = built with a gap
listed in §5; **left** = not built.

| # | Screen | Route | Permission | Status | API calls |
|---|---|---|---|---|---|
| 1 | Login | `/login` | — | done | `POST /admin/auth/login` |
| 1 | Shell, location switcher, user menu | (layout) | signed in | done | `GET /admin/auth/me`, `POST /admin/auth/logout`, `GET /admin/fulfillment-locations` (names, with `locations.manage`) |
| 1 | Dashboard | `/` | — (tiles need `reports.view`) | done | `GET /admin/reports/dashboard` (60 s refresh), `GET /admin/reports/daily-sales` (today's totals) |
| 2 | Orders list | `/orders` | `orders.view` | done | `GET /admin/orders` (one status, slotDate, slotId, paymentMethod, q, fulfillmentLocationId, sort `-placedAt`); bulk `POST /admin/orders/{id}/start-preparing` |
| 2 | Order detail | `/orders/:orderId` | `orders.view` | done | `GET /admin/orders/{id}`; `start-preparing` (`orders.pack`), `cancel` (`orders.cancel`), `goodwill` (`refunds.request`), substitutions `POST …/items/{itemId}/substitutions`, `POST …/substitutions/{id}/record-response` (`orders.pack`; proposing needs the preparation method list, which needs `catalog.manage` — G3); payments panel (below) |
| 2 | Weigh and pack (whole order) | tab on order detail | `orders.pack` | done | `POST /admin/orders/{id}/pack` (If-Match); result shows packed, charged, capped, final total, amount due, refund due from the server |
| 2 | Pick list | `/orders/pick-list?slotId&fulfillmentLocationId` | `orders.pack` | done | `GET /admin/orders/pick-list` |
| 2 | Complaints list / detail | `/complaints`, `/complaints/:id` | `complaints.manage` | done | `GET /admin/complaints`, `GET /admin/complaints/{id}`, `POST …/decide`, `POST …/resolve` |
| 3 | Stock list | `/inventory` | `inventory.view` | done | `GET /admin/inventory/stock` (lowStockOnly, variantId); create stock item `POST /admin/inventory/stock-items` (`inventory.adjust`) |
| 3 | Stock item | `/inventory/stock/:id` | `inventory.view` | done | item, batches; threshold, adjust, count (If-Match), waste (IK), discard batch (`inventory.adjust`) |
| 3 | Receive stock | `/inventory/receive` | `inventory.receive` | done | `POST /admin/inventory/receipts` (IK) |
| 3 | Batches / movements / cold chain / reservations | `/inventory/batches`, `/movements`, `/cold-chain`, `/reservations` | `inventory.view` | done | list endpoints; record temperature (`inventory.adjust`) |
| 4 | Products list / create / editor | `/products`, `/products/new`, `/products/:id` | `catalog.manage` | done | products CRUD (If-Match), publish/archive/restore, variants and preparations, images upload/reorder/delete |
| 4 | Categories | `/products/categories` | `catalog.manage` | done | categories tree, create, update (If-Match) |
| 5 | Prices | `/pricing` | `pricing.manage` | done | `GET /admin/pricing/prices` (location required), `PUT /admin/pricing/locations/{loc}/variants/{variant}/price` (If-Match or `If-None-Match: *`) |
| 5 | Variant prices and preparation charges | `/pricing/variants/:variantId` | `pricing.manage` | done | price schedule, stop selling (DELETE, If-Match), preparation charges |
| 5 | Tax rules | `/pricing/tax-rules` (own menu entry "Tax rules") | `pricing.manage` or `tax.manage` (list, `{ anyOf }` requirement); `tax.manage` for "New tax period" / "New rate" (D35) | done | list, schedule new period |
| 5 | Bulk upload (dry run, then apply) | `/pricing/bulk` | `pricing.manage` | done | `POST /admin/pricing/prices/bulk` with `dryRun: true`, then apply with its own IK |
| 5 | Coupons list / editor / redemptions | `/promotions`, `/promotions/new`, `/promotions/:id` | `promotions.manage` | done | `GET/POST /admin/coupons`, `GET/PUT /admin/coupons/{id}` (If-Match), `POST …/pause`, `…/resume`, `GET …/redemptions` |
| 5 | Home banners | — | `catalog.manage` | **left** | `GET/POST/PUT /admin/promotions` (Promotions module not on `main`) |
| 6 | Shipments board | `/delivery` | `delivery.manage` | done | shipments list; assign, dispatch, deliver (code), failed attempt, reattempt, return, record cash (IK) — all If-Match |
| 6 | Shipment detail | `/delivery/shipments/:id` | `delivery.manage` | done | shipment, attempts, timeline (delivery code never shown) |
| 6 | Slots / templates / riders | `/delivery/slots`, `/templates`, `/riders` | `delivery.manage` | done | slot capacity/open (If-Match), templates CRUD, riders CRUD |
| 7 | Cities | `/cities` | `locations.manage` | done | `GET/POST /admin/cities` |
| 7 | City detail and launch checklist | `/cities/:cityId` | `locations.manage` (activate/pause/retire `cities.activate`) | done | city, `GET …/readiness`, transitions (If-Match), locations and service areas of the city |
| 7 | Fulfillment location | `/locations/:locationId` | `locations.manage` | done | update (If-Match), operating hours (whole week), closures |
| 7 | Service area | `/service-areas/:id` | `locations.manage` | done | update fees (If-Match), add/remove pincodes |
| 8 | Customers list / detail | `/customers`, `/customers/:id` | `customers.view` (block `customers.manage`) | partial | list, detail, block/unblock (If-Match); order history tab waits for the endpoint |
| 9 | Reports + CSV | `/reports?report=…` | `reports.view` | done | five reports with URL filters, server totals, `format=csv` download |
| 10 | Staff and roles | `/settings/staff` | `staff.manage` | done | staff CRUD, reset password, roles and permissions |
| 10 | Business profile | `/settings/business-profile` | `settings.manage` | done | GET (404 → create with `If-None-Match: *`), PUT (If-Match) |
| 10 | Order policies | `/settings/order-policies` | `settings.manage` | done | default and per-city policies |
| 10 | Number series | `/settings/number-series` | `settings.manage` | done | list, update (If-Match) |
| 10 | Notification templates | `/settings/notification-templates` | `notifications.manage` | done | list, update, send test |
| 10 | Audit log | `/settings/audit` | `audit.view` | done | list with filters; before/after per row |
| 10 | My account | `/account` | — | done | profile, change password (signs out every device) |
| 11 | Payments / detail / webhook events | `/payments`, `/payments/:id`, `/payments/webhook-events` | `payments.view` (webhooks: `all_loc`; reprocess `payments.manage`) | done | payments list and detail, webhook events, reprocess |
| 11 | Refunds / detail | `/refunds`, `/refunds/:id` | `payments.view` (approve/reject/mark paid `refunds.approve`) | done | list, approve, reject, mark paid (If-Match) |
| 11 | Order payments panel | order detail → Payment tab | `payments.view` (request `refunds.request`) | done | order payments and refunds; request refund (IK) |
| 11 | Cash settlements | `/cash-settlements` | `cash.settle` | done | list, record (IK), dispute, settle (If-Match) |
| 11 | Payment issues | `/payment-issues` | `payments.view` (resolve `payments.manage`) | done | list, resolve (If-Match) |

Left: home banners (row above). Everything else in the brief is built.

## 4. API types (generated)

The 16 files in `apps/admin/src/api/types/` (`common`, `auth`, `locations`, `reports`, `catalog`, `orders`,
`inventory`, `pricing`, `promotions`, `delivery`, `customers`, `staff`, `settings`, `notifications`, `audit`,
`payments`) are now thin aliases over `@fresh/api-client` (`Schema<"Name">`), no longer TEMPORARY (D63). The hooks
in `src/api/*.ts` kept their names. `common.ts` has `Coded<T, {…}>` (narrows string code fields to unions) and
`RequestBody<T>` (nullable request fields optional). One shape is still hand-written: the bulk price upload body
(`pricing.ts`), because the endpoint reads it by hand and the spec has no request schema.

Regenerate after a backend change: run the API, `pnpm --filter @fresh/api-client fetch-spec`, `pnpm generate:api`,
then `pnpm typecheck`.

Differences from the old temporary types that changed screens:

- Order detail is nested: `GET /admin/orders/{id}` returns `{ order, …staff extras }`; `flattenAdminOrder` in
  `types/orders.ts` flattens it. Version from `order.version` (or the ETag).
- Order list row: `recipientName`, `phone`, `deliverySlotId`, `slotStartAt`/`slotEndAt`; no `version`, so bulk
  start-preparing reads each order's detail first (G13). One `status` filter, not a list (G14).
- History rows use `to`/`at`/`reasonCode` (no actor name); adjustments use `at`; gross reserved quantity comes
  from `lines`; item batches are gone (G17).
- Pick list rows are `lines` with `grossQuantity` + `stockUnit`; no SKU, slot times or order count (G18).
- Cancel takes `note`; substitution proposal takes `preparationMethodId` (mapped from the picker's code);
  recording an answer sends `{ answer: "ACCEPTED" | "REJECTED" }`; complaint decision takes `note`.
- Complaint detail = complaint summary: no product name or refund limit (G15), no decided-by; shows decision note.
- Customer detail has no order counts (G6). Coupon `description` and `validTo` are nullable; `isListed` is
  required in the request and the editor keeps the current value (no control yet); redemptions show order id
  only (G16).
- Payment issue kinds add `REFUND_METHOD_REQUIRED` and `WEBHOOK_ERROR`. Audit `before`/`after` are untyped.

## 5. API gaps found (for Phase 3)

Re-checked against the generated OpenAPI document on 2026-10-24 (D63).

| # | Gap | Status | Effect now | Backend change that closes it |
|---|---|---|---|---|
| G1 | Location names need `locations.manage` | STILL OPEN | Other staff see `Location 1a2b3c` | Staff-readable `GET /admin/me/locations` (id, code, name) |
| G2 | Cities list needs `locations.manage` | STILL OPEN | City pickers (reports, order policies, coupons) hidden for others | Read-only cities lookup |
| G3 | No staff-readable variant search | STILL OPEN | Staff without `catalog.manage` type variant ids; inventory rows show shortened ids; substitution proposals cannot load preparation methods | `GET /admin/catalog/variants?q=`, staff-readable preparation methods, variant name/SKU on stock items, batches, reservations, movements |
| G4 | Dashboard has no sales figures; low stock has no unit | STILL OPEN | Dashboard reads today's `daily-sales` report | Sales tiles and unit in `DashboardResponse` |
| G5 | Ordering admin shapes not on `main` | CLOSED | Real shapes in the spec; screens follow them (§4) | — |
| G6 | No `GET /admin/customers/{id}/orders`; no city filter; no order counts | STILL OPEN | Orders tab shows "not available yet" | Add the endpoint, `cityId` filter, order counts on the detail |
| G7 | Rider list needs `delivery.manage` | STILL OPEN | Cash settlement takes a typed rider id for `cash.settle`-only staff | Rider lookup readable with `cash.settle` |
| G8 | `docs/api` lists If-Match on image upload and refund create, the backend does not require it | STILL OPEN | Admin follows the backend | Align `docs/api` (doc change) |
| G9 | Tax rule scheduling has no If-Match | STILL OPEN | `POST /admin/pricing/tax-rules` returns a version but takes none | Version on the tax-rule collection, or accept |
| G10 | Shipments list has no date filter; most lists have no `sort` | STILL OPEN | Shipments filter by location, slot, rider, status only | `slotDate` and `sort` where useful |
| G11 | `CITY_RETIREMENT_BLOCKED` blocking item shape undocumented | STILL OPEN | Shown as text | Typed Problem extension in the spec |
| G12 | Payments have no created date | STILL OPEN | `PaymentResponse` has no `createdAt` | Add `createdAt` |
| G13 | Admin order list row has no `version` | NEW, OPEN | Bulk start-preparing GETs each order first | `version` on `AdminOrderSummary` |
| G14 | Admin orders filter takes one status | NEW, OPEN | One status at a time | Accept a status list |
| G15 | Complaint has no product name or max refund | NEW, OPEN | "One item" / "Whole order"; server checks the refund amount | Product/variant name and refundable limit on the complaint |
| G16 | Redemptions have no order number | NEW, OPEN | Order id only | `orderNumber` on `CouponRedemption` |
| G17 | Order item batches not in admin order detail | NEW, OPEN | Batch column removed | Batch allocations per line |
| G18 | Pick list has no slot times, order count or SKU | NEW, OPEN | Columns show what is there | Add them to the pick list response |

Front end, not backend: coupon `isListed` has no editor control yet.

## 6. Tests

Vitest + Testing Library + MSW (`src/test/`): real routes rendered with a fake signed-in staff session
(`renderApp`), API mocked at `http://api.test/api/v1`. Tests assert request headers (If-Match, Idempotency-Key),
bodies in API units, error codes landing on fields, and the 412 prompt — never message wording from the server.
Playwright (`e2e/smoke.spec.ts`): login page renders and signed-out visitors are redirected; run with
`pnpm --filter @fresh/admin e2e` after `pnpm exec playwright install --only-shell chromium`.

## 7. Commands

| Task | Command |
|---|---|
| Dev server (http://localhost:5173, proxies `/api` to `FRESH_API_PROXY` or http://localhost:5080) | `pnpm --filter @fresh/admin dev` |
| Build / typecheck / lint / test | root `pnpm build` · `pnpm typecheck` · `pnpm lint` · `pnpm test` |
| End-to-end smoke | `pnpm --filter @fresh/admin e2e` |
