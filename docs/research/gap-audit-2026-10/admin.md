# Admin dashboard gap audit (2026-10-29)

Scope: `apps/admin`, every route in `src/features/*/routes.tsx`, its shared code (`src/app`, `src/auth`, `src/lib`,
`src/components`), and the `/api/v1/admin` endpoints behind it in `backend/src/Modules/*/…/Endpoints`. I read the code
and checked each claim against the file and line cited. Nothing was built, run or installed (CLAUDE.md rule 18), so
nothing was checked in a browser. Pure visual judgements (spacing, alignment at a given width) are left out because
they cannot be proven from the code.

## Summary

The admin covers almost every back-office flow. I found no money or stock arithmetic in the browser: rule 4 holds,
and the only conversions are what staff type (₹ → paise, % → bp, IST → UTC in `src/lib/units.ts`). Every update
sends `If-Match`, and every endpoint marked IK in the backend gets an `Idempotency-Key` that is kept across retries.
Each list has loading, empty and error states. 403, 404 and 412 have one shared treatment, and 401 refreshes once and
then sends the user to sign-in.

**Open items that are already done.** The three admin items in `docs/team/next-steps.md` §5 are finished in code but
still listed as open:

- Tax-rules gating by `tax.manage`: `auth/permissions.ts:53-54`, `features/pricing/TaxRulesPage.tsx:22`.
- 429 on login: `features/auth/LoginPage.tsx:39-46,142-153`.
- No locked or disabled hint on login: `LoginPage.tsx:34-36`.

`docs/design/admin.md` §5 G8 and G9 are closed too:

- G8: `docs/api/README.md:128,900,1081` now say "no If-Match".
- G9: the backend takes the category version, `PricingEndpoints.cs:69-75`.

**The gaps are in five places:**

1. **Delivery cannot be run from the board alone.**
   - A shipment row has no order number, customer, address, phone or amount to collect. The cash dialogs say the
     amount "must equal the amount due" but never show it.
   - A 412 on any shipment action leaves the board on the stale version.
2. **Evidence and documents.**
   - Complaint photos cannot load: they are `<img>` tags pointing at an endpoint that needs a bearer token.
   - No invoice can be opened or printed, and there is no packing slip or rider sheet.
   - The pick list prints with the whole app shell around it.
3. **Scale limits that hit a real catalogue.** Every staff variant picker searches one page of 100 variants in the
   browser. The server supports `q`, but the admin does not send it.
4. **Saves that wipe data.** These forms omit a field the API clears when it is left out:
   - Business profile drops `supportHours`.
   - City create and edit drop `stateName`.
5. **Missing screens.**
   - Home banners (the backend is on `main`).
   - Invoices and a GST register.
   - New orders do not appear without a manual refresh.

Nothing in the code stops the shop taking, packing and delivering orders on launch day, so there are no Blockers.
The High items are the ones staff will hit on the first busy day.

| Severity | Count |
|---|---|
| Blocker | 0 |
| High | 9 |
| Medium | 24 |
| Low | 52 |

Missing features and details are in the second table (Must 9, Should 17, Nice 9).

## Gaps

| ID | Severity | Page | Gap | Evidence (`path:line`) | Suggested fix |
|---|---|---|---|---|---|
| AD-1 | High | Delivery board | A shipment row shows "Shipment ab12cd34" and "Order ab12cd34" (short ids). It has no order number, customer name, phone, address or pincode, and the API shape has none of them. A dispatcher cannot tell which parcel is which without opening each order | `apps/admin/src/features/delivery/ShipmentsPage.tsx:37-44`; `packages/api-client/src/schema.d.ts:11877-11901` (`ShipmentResponse`) | Add `orderNumber`, `recipientName`, `phone`, `addressLine`, `pincode` to the admin shipment read (Delivery reads Ordering through `.Public`) and show them as columns |
| AD-2 | High | Delivery board, Record cash / Deliver | The cash dialog says "The amount must equal the amount due on the order." but never shows the amount due. Deliver says "Leave empty for prepaid orders" without saying whether the order is COD. Staff guess, and the server answers `CASH_AMOUNT_MISMATCH` | `features/delivery/ShipmentActions.tsx:496`, `:351`; `ShipmentResponse` has no amount due (`schema.d.ts:11877`) | Return `paymentMethod` and `amountDuePaise` on the shipment; show "Collect ₹…" in the row and in both dialogs, and hide the cash field for prepaid orders |
| AD-3 | High | Complaint detail | Photos are `<img src>` tags pointing at `/api/v1/admin/complaints/{id}/photos/{photoId}`. That endpoint requires `complaints.manage` through a bearer token, and the admin sends tokens only in the `Authorization` header (sessionStorage), never as a cookie, so every photo returns 401 and shows broken | `features/orders/ComplaintDetailPage.tsx:103-117`; `backend/src/Modules/Ordering/Ordering.Application/Complaints/ComplaintService.cs:114`; `Ordering.Infrastructure/Endpoints/OrderingEndpoints.cs:170,183` | Fetch each photo with `http` as a blob and show an object URL, or have the server issue short-lived signed photo URLs |
| AD-4 | High | Receive stock, Stock item, Substitution picker | `useVariantLookup` loads one page of 100 active variants and searches it in the browser. Once the catalogue has more than 100 active variants, the rest cannot be picked: they cannot be received into stock, so they cannot be sold. The endpoint already accepts `q` | `apps/admin/src/api/catalog.ts:102-119`; `features/inventory/VariantField.tsx:19`; `components/VariantPicker.tsx:41`; `backend/.../Catalog.Infrastructure/Endpoints/CatalogEndpoints.cs:189-209` | Send the typed text as `q` (debounced) and page server-side; keep the selected variant by `id=` |
| AD-5 | High | Order detail | No way to open, download or print the GST invoice: the Payment tab shows only the invoice number. `GET /admin/orders/{id}/invoice` exists and is unused | `features/orders/OrderDetailPage.tsx:193-196`; `OrderingEndpoints.cs:166` | Add "View invoice" / "Download PDF" on the order (blob fetch with the bearer token) |
| AD-6 | High | Promotions | No home-banner screen. `GET/POST/PUT /admin/promotions` (`catalog.manage`) is on `main`, including `badge` (PROMO-R29), but the admin has no route, so banners can only be changed in the database | `features/promotions/routes.tsx:5-15` (coupons only); `backend/.../Promotions.Infrastructure/Endpoints/PromotionsEndpoints.cs:81-100`; `docs/design/admin.md:58` | Build banners list and editor (title, subtitle, image, link, city, dates, order, active, badge) with If-Match |
| AD-7 | High | Orders list, Delivery board | New orders never appear on their own: only the dashboard polls (`refetchInterval: 60_000`). Lists refetch only on window focus, so a packer looking at the orders list does not see a new order | `apps/admin/src/api/reports.ts:35`; `api/orders.ts:41-49`; `api/delivery.ts` (no interval); `packages/core/src/query/query-client.ts:32-36` | Poll the orders list and shipments board (30 to 60 s) while visible, and show "N new orders" |
| AD-8 | High | Business profile | Saving sends `{ ...form }` without `supportHours`. `SaveBusinessProfileRequest.SupportHours` defaults to null, so every save clears the support hours shown to customers, and the field cannot be edited | `features/settings/BusinessProfilePage.tsx:74`; `backend/.../Settings.Application/BusinessProfiles/BusinessProfileService.cs:20` | Add a "Support hours" field and send it on every save |
| AD-9 | High | Cities (create and edit) | `stateName` is never sent. LOC-R19 says a PUT that omits it clears it, so editing a city wipes the state name used to prefill customer addresses, and new cities never get one | `features/cities/CitiesPage.tsx:131-154`; `features/cities/CityDetailPage.tsx:346`; `docs/product/locations.md:87` | Add a "State name" field to both forms and send it |
| AD-10 | Medium | Delivery board | A 412 on assign, dispatch, deliver, attempt or cash passes no `onConflict`. "Reload" does nothing, and the board is refreshed only on success, so the next try fails the same way | `features/delivery/ShipmentActions.tsx:137,217,302`; `api/delivery.ts:94` | Pass an `onConflict` that invalidates `deliveryKeys.all`, and invalidate on error as well |
| AD-11 | Medium | Delivery board | "Dispatch" shows for every ASSIGNED shipment, but the server refuses dispatch until the order is packed (`ShipmentNotDispatchable`). The board shows no packed state | `features/delivery/ShipmentActions.tsx:70`; `backend/.../Delivery.Domain/Shipments/Shipment.cs:202-205` | Show "Packed" / "Not packed" from `packedAt`; enable Dispatch only when `packedAt` is set |
| AD-12 | Medium | Pick list | "Print" calls `window.print()`, but no print stylesheet exists anywhere in the admin or `@fresh/ui`, so the sidebar, top bar and buttons print with the list | `features/orders/PickListPage.tsx:59`; no `@media print` / `print:` in `apps/admin/src` or `packages/ui/src` | Add print styles: hide the shell, show the slot header, use black-on-white tables |
| AD-13 | Medium | Pick list | The only way in is a slot link on an order row, so a packer cannot open the pick list for a slot with no visible order. There is no slot picker and no menu entry | `features/orders/PickListPage.tsx:33-43`; `app/nav.tsx` (no entry) | Add a date and slot picker on the page (from `GET /admin/delivery/slots`) and a menu entry |
| AD-14 | Medium | Orders list | The "Placed" column header can be clicked to sort, but `GET /admin/orders` has no `sort` parameter, so the arrow changes and the order does not | `features/orders/OrdersListPage.tsx:80-84`; `OrderingEndpoints.cs:111-121` | Accept `sort` on the server, or set `enableSorting: false` |
| AD-15 | Medium | Orders list | No slot picker: slot filtering works only through `slotId` in the URL, shown as the chip "One slot" | `features/orders/OrdersListPage.tsx:160-162,208-215` | Add a slot select for the chosen slot date |
| AD-16 | Medium | Dashboard | Status tiles count orders whose **slot is today**, but their links open `/orders?status=…` with no `slotDate`, so the list shows another number. The `fulfillmentLocationId` added to the link is ignored by the list | `features/dashboard/DashboardPage.tsx:86-87`; `backend/.../Reporting.Infrastructure/Reports/ReportReader.cs:320`; `OrdersListPage.tsx:26` | Add `slotDate=<today>` to tile links and drop the unused location parameter |
| AD-17 | Medium | Service area | Removing a pincode (customers there can no longer order) runs on one click of an "×" with no confirmation | `features/cities/ServiceAreaPage.tsx:236-250` | Confirm "Stop delivering to 5000xx?" first |
| AD-18 | Medium | Order detail | No link from the order to its shipment, rider or delivery status; the order response has no shipment id | `features/orders/OrderDetailPage.tsx:213-261`; `schema.d.ts:9169-9188` | Return the shipment id and status on the admin order, and link to `/delivery/shipments/:id` |
| AD-19 | Medium | Session (all pages) | Tokens are in `sessionStorage`, which is per tab. Opening an order in a new tab (Ctrl-click, middle-click) asks staff to sign in again | `apps/admin/src/auth/session.ts:24-31` | Adopt the D40 follow-up (refresh token in an httpOnly cookie) so new tabs can refresh, or share through `BroadcastChannel` |
| AD-20 | Medium | Session | The refresh token (12 h) is readable by any script on the page; the httpOnly-cookie follow-up is still open | `auth/session.ts:95-96`; `docs/team/next-steps.md:98` | Move the refresh token to an httpOnly, SameSite=Strict cookie scoped to `/api/v1/admin/auth` |
| AD-21 | Medium | Product editor | "Tax category code" is free text with the hint "Must match a tax category". A typo sets the wrong GST basis | `features/products/ProductDetailsForm.tsx:186-191` | Use a select fed by the tax categories (`GET /admin/pricing/tax-rules`) |
| AD-22 | Medium | Orders list | Bulk "Start preparing" stops at the first refusal. The toast names only the failed order, not how many already started, and the 412 prompt's Reload does nothing (no `onConflict`) | `features/orders/OrdersListPage.tsx:306-314`; `api/orders.ts:121-131` | Run them all, then report "Started 4 of 6; 2 refused: …"; on 412 refetch the list |
| AD-23 | Medium | Order detail, Audit log | The actor is shown only by type ("Staff"); `actorName` is null from the server, so nobody can see who cancelled or packed an order | `features/orders/OrderDetailPage.tsx:442-445`; `features/settings/AuditLogPage.tsx:51`; `docs/team/next-steps.md:96` | Fill `actorName` on the server (history and audit) and show it |
| AD-24 | Medium | Order detail | The cancel reason (`cancelReasonCode`), cold box used and refund due are in the response but never shown after packing or cancelling | `features/orders/OrderDetailPage.tsx:180-260`; `schema.d.ts:9181-9184` | Show "Cancelled: Out of stock" in the header, and cold box and refund due under Amounts |
| AD-25 | Medium | Weigh and pack | The weighed field accepts any whole number with no sanity check. Typing 15000 g for a 1.5 kg line is accepted, and the server only caps the charge | `features/orders/PackForm.tsx:15-19,151-157` | Warn (not block) when the weight is more than 2× the ordered quantity or 0 |
| AD-26 | Medium | Complaints list | No search by order number or phone; only status and kind filters | `features/orders/ComplaintsListPage.tsx:20` (filters); no `search=` on its `FilterBar` | Add `q` (order number or phone) on the server and a search box |
| AD-27 | Medium | Audit log | Filters are raw text boxes for "Entity id" and "Actor id" (UUIDs); there is no staff picker or entity type list | `features/settings/AuditLogPage.tsx:10-15` | Staff select, entity type select, and "View audit" links from order, product and coupon pages |
| AD-28 | Medium | Delivery board | No bulk actions: each shipment needs its own assign and dispatch dialog, even when one rider takes a whole slot | `features/delivery/ShipmentsPage.tsx:213-232` (no `enableRowSelection`) | Row selection with "Assign rider" and "Dispatch" for the selection, each quoting its version |
| AD-29 | Medium | Tests | No test covers the dashboard, substitutions (propose or record answer), goodwill, bulk start-preparing or the pick list | `features/orders/orders.test.tsx` (pack and cancel only); no `dashboard` test file | Add MSW tests for each, asserting request bodies, If-Match and codes |
| AD-30 | Medium | Tests | No test for complaint decide or resolve, payment-issue resolve, webhook reprocess or shipment assign / dispatch / attempt / reattempt / return | `features/delivery/delivery.test.tsx:68-160` (deliver, cash, slots only); `features/payments/payments.test.tsx` | Add MSW tests per action |
| AD-31 | Medium | Tests | Location hours, closures, service-area fees and pincodes, categories, riders, templates, order policies, number series and change password have no tests | no test names cover them (`*.test.tsx` lists) | Add tests, starting with pincodes and closures (they change who can order) |
| AD-32 | Medium | Tests | The only browser test is a smoke test of the login page; no Playwright case walks a day of orders (receive → price → start → pack → assign → dispatch → deliver → settle) | `apps/admin/e2e/smoke.spec.ts` | Add one end-to-end case against the compose stack with sample data |
| AD-33 | Medium | Orders list | No placed-date range and no "placed today" filter; only the slot date filters by day | `features/orders/OrdersListPage.tsx:26`; `OrderingEndpoints.cs:111-121` | Add `placedFrom`/`placedTo` on the server and a date range control |
| AD-34 | Low | Remove closure | Removing a planned closure is one click on a bin icon with no confirmation | `features/cities/LocationDetailPage.tsx:350-362` | Confirm first |
| AD-35 | Low | Coupon editor | Pause runs immediately with no confirmation, which stops a live campaign | `features/promotions/CouponEditorPage.tsx:303-323` | Confirm "Pause WELCOME100? Customers can no longer apply it." |
| AD-36 | Low | Webhook events | Reprocess runs on one click with no confirmation | `features/payments/WebhookEventsPage.tsx:21-35` | Confirm first |
| AD-37 | Low | Cancel order dialog | The dialog stays mounted, so the reason, notes and last error are still there when it is reopened | `features/orders/OrderDetailPage.tsx:264-269`; `features/orders/OrderDialogs.tsx:50-52` | Reset state on open, or mount only while open (like Goodwill) |
| AD-38 | Low | Order detail, Timeline | Money adjustments are listed after all status rows instead of in time order | `features/orders/OrderDetailPage.tsx:429-458` | Merge and sort by `at` |
| AD-39 | Low | Order detail | Customer section has no link to the customer's profile, although `customerId` is in the response | `features/orders/OrderDetailPage.tsx:214-244`; `schema.d.ts:9172` | Link the recipient to `/customers/:customerId` (when `customers.view`) |
| AD-40 | Low | Order detail | The address is plain text: no map link and no copy button for the rider | `features/orders/OrderDetailPage.tsx:230-241` | Add "Open in maps" and "Copy address" |
| AD-41 | Low | Delivery board | Empty-state copy says "Shipments appear here when orders are packed.", but shipments are created when the order is placed (PENDING) | `features/delivery/ShipmentsPage.tsx:229`; `backend/.../Delivery.Application/Handlers/OrderEventHandlers.cs:13-21` | "Shipments appear here when orders are placed for this day." |
| AD-42 | Low | Shipment detail | Title is "Shipment ab12cd34" and the order link is "Order ab12cd34"; the order number is not shown | `features/delivery/ShipmentDetailPage.tsx:35,52` | Use the order number once the API returns it (AD-1) |
| AD-43 | Low | Navigation | The "Payments" group's first item is also "Payments" | `apps/admin/src/app/nav.tsx:150,154` | Rename the group "Money" or "Finance" |
| AD-44 | Low | Copy | Mixed spelling: "Catalog" and "Fulfillment" (US) next to "licence", "Authorised", "centre", "catalogue" (UK) | `app/nav.tsx:88`; `app/AdminLayout.tsx` ("Fulfillment location"); `features/settings/BusinessProfilePage.tsx:103`; `lib/status.ts` ("Authorised"); `features/orders/OrderDialogs.tsx:161` | Pick Indian English (UK spelling) and apply it everywhere |
| AD-45 | Low | Business profile | "Time zone" is free text ("e.g. Asia/Kolkata") | `features/settings/BusinessProfilePage.tsx:113` | Select from IANA zones (default Asia/Kolkata) |
| AD-46 | Low | Business profile | GSTIN has no client format check (only "15 characters." as a hint); the error appears only after saving | `features/settings/BusinessProfilePage.tsx:14,102` | Add the 15-character pattern to the zod schema (the server still decides) |
| AD-47 | Low | Complaint detail | Every photo has the same alt text, "Complaint attachment" | `features/orders/ComplaintDetailPage.tsx:111` | "Photo 1 of 3 from the customer" |
| AD-48 | Low | Service area | A pincode's added date is only in a `title` tooltip (not reachable by keyboard or touch) | `features/cities/ServiceAreaPage.tsx:228` | Show it in text or drop it |
| AD-49 | Low | All pages | The browser tab title is always "Fresh Orbitt Admin"; it never names the page (WCAG 2.4.2), so tabs cannot be told apart | `apps/admin/index.html:7`; no `document.title` in `src` | Set the title from `PageHeader` ("Orders · Fresh Orbitt Admin") |
| AD-50 | Low | Login | No "Forgot password?" route or hint; a staff member who forgets must find an admin, which the page does not say | `features/auth/LoginPage.tsx:138-191` | Add "Forgot your password? Ask an admin to reset it." (self-service later) |
| AD-51 | Low | Login | No show-password toggle on the password field | `features/auth/LoginPage.tsx:180-187` | Add a toggle (from `@fresh/ui`) |
| AD-52 | Low | Product photos | No client check of file size, and no friendly message for 413 `PAYLOAD_TOO_LARGE` (not in `CODE_MESSAGES`) | `features/products/ImagesPanel.tsx:174-181`; `lib/errors.ts:50-62` | Check size before upload; map 413 to "The photo is too large; use one under N MB." |
| AD-53 | Low | Errors | 428 `VERSION_REQUIRED` falls back to "Reload this page and try again." with no reload action | `lib/errors.ts:57`; `lib/feedback.ts:26-29` (only 412 opens the prompt) | Treat 428 like 412 (open the reload prompt) |
| AD-54 | Low | Customers list | No column sorting (name, last order) | `features/customers/CustomersListPage.tsx` (no `onSortingChange`) | Server `sort` plus sortable headers |
| AD-55 | Low | Products list | No column sorting (name, status, published) | `features/products/ProductsListPage.tsx` (no `onSortingChange`) | Server `sort` plus sortable headers |
| AD-56 | Low | Stock list | No column sorting (available, threshold), so the lowest stock cannot be put first | `features/inventory/StockListPage.tsx` (no `onSortingChange`) | Server `sort=available` plus sortable headers |
| AD-57 | Low | Complaints list | No column sorting (raised at) | `features/orders/ComplaintsListPage.tsx` (no `onSortingChange`) | Sortable "Raised" column |
| AD-58 | Low | Delivery board | No column sorting (slot, rider, status) | `features/delivery/ShipmentsPage.tsx` (no `onSortingChange`) | Sortable slot and status columns |
| AD-59 | Low | Payments list | No search by order number or provider reference, and no sorting | `features/payments/PaymentsPage.tsx` (no `search=`, no `onSortingChange`) | Add `q` and sorting |
| AD-60 | Low | Refunds list | No search by order number, and no sorting by amount or date | `features/payments/RefundsPage.tsx:20` (filters only) | Add `q` and sorting |
| AD-61 | Low | Cash settlements | No sorting and no search by rider name | `features/payments/CashSettlementsPage.tsx` (no `search=`) | Add sorting and search |
| AD-62 | Low | Coupons list | No column sorting (ends, used) | `features/promotions/CouponsListPage.tsx` (no `onSortingChange`) | Sortable headers |
| AD-63 | Low | Riders | No search by name or phone | `features/delivery/RidersPage.tsx` (no `search=`) | Add `q` |
| AD-64 | Low | Batches, Movements, Reservations, Cold chain | No search by batch code or SKU; only filters | `features/inventory/BatchesPage.tsx`, `MovementsPage.tsx`, `ReservationsPage.tsx`, `ColdChainPage.tsx` (no `search=`) | Add a batch-code / SKU search |
| AD-65 | Low | Cities | No search box on the cities list | `features/cities/CitiesPage.tsx` (no `search=`) | Add `q` once there are many cities |
| AD-66 | Low | Categories | No search box on the categories table | `features/products/CategoriesPage.tsx` (no `FilterBar`) | Add a filter box |
| AD-67 | Low | Customer detail | The customer's orders tab keeps paging in component state, not the URL, so Back loses the page | `features/customers/CustomerDetailPage.tsx:170` | Use `useListParams` like other lists |
| AD-68 | Low | Customer detail, Block dialog | On a failed block or unblock, the dialog closes before the error toast, so staff lose what they were doing; a 412 then reloads behind a closed dialog | `features/customers/CustomerDetailPage.tsx:276-279` | Keep the dialog open and show the refusal in it; close only on success |
| AD-69 | Low | Customer detail | The "No saved addresses" and "No orders yet" empty states have no description of what to do or why | `features/customers/CustomerDetailPage.tsx:138,232` | Add one line, e.g. "Addresses appear when the customer saves one in the app." |
| AD-70 | Low | Order detail | The Notes tab shows only customer notes; when empty it says "No notes from the customer." with no staff-notes alternative | `features/orders/OrderDetailPage.tsx:203-209` | See the internal-notes feature (MF-12) |
| AD-71 | Low | Goodwill dialog | No upper limit or "at most ₹…" hint, unlike the complaint refund field | `features/orders/OrderDialogs.tsx:361-368`; compare `features/orders/ComplaintDetailPage.tsx:161-162` | Return and show the server's goodwill limit |
| AD-72 | Low | Weigh and pack | Weights are typed in grams only; staff reading a kg scale must convert (1.25 kg → 1250) | `features/orders/PackForm.tsx:127,151-157` | Accept "1.25 kg" or show a kg preview of the grams typed (display only) |
| AD-73 | Low | Orders list | The search placeholder is "Order number or phone"; customer name is not searchable | `features/orders/OrdersListPage.tsx:194` | Add name to the server's `q` or say so in the hint |
| AD-74 | Low | Reports | The "Preparation code" filter is free text, not a select of preparation methods | `features/reports/report-definitions.ts:127` | Select from `GET /admin/preparation-methods` |
| AD-75 | Low | Cash settlement dialog | When `riders/lookup` returns 403, staff must type a rider UUID | `features/payments/RecordSettlementDialog.tsx:31`; `docs/design/admin.md:119` (G7) | Grant the lookup to `cash.settle` and drop the id fallback |
| AD-76 | Low | Inventory | Waste and adjustment reason codes are a hard-coded list in the client | `features/inventory/maps.ts:30-43` | Serve them from the API (configuration is data, rule 7) |
| AD-77 | Low | Delivery board | The temperature warning detail is in a `title` tooltip ("Dispatched above the cold-chain limit") | `features/delivery/ShipmentsPage.tsx:81-87` | Add visible or screen-reader text |
| AD-78 | Low | Docs | `next-steps.md` §5 still lists tax gating, login 429 and the account-message removal as open, but all three are in code | `docs/team/next-steps.md:91`; `features/auth/LoginPage.tsx:34-46`; `auth/permissions.ts:53-54` | Move the line to done |
| AD-79 | Low | Docs | `admin.md` §5 marks G8 and G9 "STILL OPEN", but both are closed | `docs/design/admin.md:125-126,145`; `docs/api/README.md:128`; `PricingEndpoints.cs:69-75` | Mark them closed |
| AD-80 | Low | Docs | `admin.md` §3 says the dashboard calls `daily-sales` (it does not, G4), Customers is "partial" waiting for an endpoint (built, G6), and banners wait for a module "not on main" (it is) | `docs/design/admin.md:41,58,66` | Update the rows |
| AD-81 | Low | Docs | `admin.md` §4 still says the order row has no `version` and bulk reads each detail (G13 closed) | `docs/design/admin.md:99-100`; `api/orders.ts:117-131` | Update the bullets |
| AD-82 | Low | Dashboard | "Orders today" links to `/orders` with no filter, so the list is not today's orders | `features/dashboard/DashboardPage.tsx:116` | Link with a placed-today filter (needs AD-33) |
| AD-83 | Low | Dashboard | Users without `reports.view` see only "Pick a section from the menu"; packers get no slot or work counts | `features/dashboard/DashboardPage.tsx:69-79` | Show their own queues (orders waiting to pack via `orders.view`) |
| AD-84 | Low | Product editor | There is no "View on website" link to check how a published product looks | `features/products/ProductEditorPage.tsx` | Link to `/products/{slug}` on the website origin |
| AD-85 | Low | Location switcher | The chosen location is remembered in `localStorage` across staff on a shared PC | `app/location-scope.tsx:18-31` (one fixed key, `fresh-orbit.admin.location`) | Key it by staff id, or clear it on sign-out |

## Missing features and details

| ID | Priority | Page | What is missing | Why the shop needs it | Suggested approach |
|---|---|---|---|---|---|
| MF-1 | Must | Order detail | Packing slip / parcel label per order: order number, slot, recipient, phone, address, pincode, lines with packed weights, COD amount | The packer sticks it on the bag; the rider knows what to collect | A print route `/orders/:id/slip` with print CSS; bulk print for a slot |
| MF-2 | Must | Order detail | Invoice view and download (AD-5) | GST invoices must be producible for staff and customer queries | Blob-fetch `GET /admin/orders/{id}/invoice` |
| MF-3 | Must | Delivery | Rider manifest / route sheet per slot and rider: stops in order with address, phone, amount to collect, and a signature column | The rider's paper (or phone) list for the run; the shop's record of what left | Server read grouped by rider and slot; printable page and CSV |
| MF-4 | Must | Delivery board | Order number, customer, address and amount due on each shipment (AD-1, AD-2) | The dispatcher works from this board all day | Extend the shipment read |
| MF-5 | Must | Promotions | Home-banner management (AD-6) | Marketing changes without a developer | New screen over `/admin/promotions` |
| MF-6 | Must | Orders | New-order alert: auto-refresh plus a sound or badge for new CONFIRMED orders at the chosen location | Same-day seafood: a missed order is a lost slot | Poll every 30 s while visible; badge on the Orders menu entry |
| MF-7 | Must | Reports | GST sales register: invoices, credit notes, HSN-wise and rate-wise summary, CSV for GSTR-1 | Monthly GST filing by the CA | New report definitions over the invoice tables |
| MF-8 | Must | Invoices | An invoice list (number, date, order, taxable value, tax, status) with search and download | Finding an invoice for a customer or the CA without knowing the order | `GET /admin/invoices` plus a list screen |
| MF-9 | Must | Pickers | Server-side variant search (AD-4) | Receiving stock for every variant in a full catalogue | Send `q` |
| MF-10 | Should | Receive stock | Multi-line receipt: the morning catch of many varieties from one supplier in one form | Today each variety needs a separate submit and IK, which is slow at 6 am | Repeating rows with one supplier header; one request per row behind the scenes (keys per row), or a batch endpoint |
| MF-11 | Should | Receive stock | Supplier list (name, phone, landing centre) to pick from, instead of free text every morning | Clean supplier history and cost per supplier | Small Suppliers table in Inventory with a picker |
| MF-12 | Should | Order detail, Customer detail | Internal staff notes (not shown to the customer), with author and time | Hand-over between shifts ("call before arriving", "complained last week") | Notes child table per order and customer, audited |
| MF-13 | Should | Orders | CSV export of the filtered orders list | Daily reconciliation and offline review | `format=csv` on `GET /admin/orders` like reports |
| MF-14 | Should | Delivery board | Bulk assign rider / bulk dispatch (AD-28) | One rider usually takes a whole slot | Row selection with per-row versions |
| MF-15 | Should | Navigation | Count badges on menu entries (orders waiting to pack, open complaints, refunds to approve, payment issues) | Staff see work waiting without opening the dashboard | Reuse the dashboard counts |
| MF-16 | Should | Dashboard | Comparison with yesterday and with the same weekday last week (orders, sales, average order value) | Owner's daily health check | Server returns the comparison figures; client only formats |
| MF-17 | Should | Cash | End-of-day close per location: cash expected from delivered COD vs settled per rider, open differences | Daily cash control | Server summary on the cash-settlements page |
| MF-18 | Should | Customer detail | Block reason and note; block history | Explaining a block to the customer or another staff member later | Add `reason` to block/unblock (backend has none) |
| MF-19 | Should | Customer detail | Customer's complaints, refunds and goodwill totals on the profile | Judging a complaint fairly (repeat claimant or first issue) | Read through `.Public` of Ordering/Payments |
| MF-20 | Should | Login / Staff | Staff self-service password reset by email, and a break-glass for the last admin | Today a forgotten password needs another admin; the last admin needs SQL | Reset token by email (Mailpit in dev); runbook-backed CLI |
| MF-21 | Should | Inventory | Low-stock alert to staff by email or WhatsApp, not only on the dashboard | Reorder before the morning auction | Use the existing `inventory.low_stock_digests` job; add staff recipients in settings |
| MF-22 | Should | Inventory | Stock transfer between locations (out at one, in at another, linked) | Moving fish from the main store to a dark store | Paired movements with one transfer id |
| MF-23 | Should | Orders list | Saved quick filters: "Waiting to pack (today)", "Out for delivery", "Failed today" | One-tap queues for packers and dispatchers | Preset URL filters as chips |
| MF-24 | Should | Products | Duplicate a product (with variants and preparations, unpublished) | Adding similar fish quickly | Server "copy" command |
| MF-25 | Should | Pricing | Price change history per variant and location, with who changed it | Audit of price mistakes | Read from the price schedule and audit |
| MF-26 | Should | Audit | "View history" links from records (order, product, coupon, staff) | Finding changes without typing UUIDs | Prefilled audit links |
| MF-27 | Nice | Weigh and pack | Scale integration (USB or Bluetooth scale fills the weighed field) | Fewer typing mistakes at packing | Web Serial / HID adapter behind a feature flag (free) |
| MF-28 | Nice | Weigh and pack | Keyboard flow: Enter moves to the next line, Ctrl+Enter marks packed | Speed at the packing table | Key handlers in `PackForm` |
| MF-29 | Nice | Inventory | Batch label print (batch code, received, best before, barcode) | Shelf traceability and FIFO picking | Print page per batch |
| MF-30 | Nice | Order detail | Click-to-chat (WhatsApp) and call buttons for the customer | Substitution calls and delivery coordination | `wa.me` link from the phone |
| MF-31 | Nice | Delivery | Map view of today's stops per slot | Route planning | Free OpenStreetMap tiles once addresses carry coordinates |
| MF-32 | Nice | Dashboard | Hourly orders chart for today | Staffing the packing table | Server series, chart in `@fresh/ui` |
| MF-33 | Nice | Settings | Per-staff notification preferences (new order, complaint, low stock) | The right person is told | Settings table plus Notifications handlers |
| MF-34 | Nice | Products | Preview of the product card as the customer sees it | Catches bad photos or names before publishing | Render the website card component in the editor |
| MF-35 | Nice | Reports | Scheduled email of the daily sales report to the owner | Owner reads it without opening the admin | Worker job with Mailpit/SMTP adapter |
