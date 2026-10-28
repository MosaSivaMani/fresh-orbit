# Customer website flow audit (2026-10-28)

Branch `web/complete-flows`. Goal: every customer flow on the website (`apps/web`) works end to end against the API,
in the shared theme (`@fresh/ui`). Method: every route under `apps/web/src/app` was walked by reading the code, and
each API call was compared with the backend endpoint and its rules (`backend/src/Modules/*/…/Endpoints`, services and
domain rules) and with `docs/product/*.md`. Response shapes were checked against the generated client
(`packages/api-client/src/schema.d.ts`) and, for public reads, against the live demo API
(`https://fresh-orbit-demo-api.onrender.com/api/v1`, pincode 530017) with `curl`. Nothing was run on the
development Mac (CLAUDE.md rule 18), so the fixes are proved by the tests in the same change, which only CI runs
(see "What is not verified").

Decisions D255–D264 (`decisions/log.md`). Front-end gap status: `design/web.md` §6 and §6a.

## Findings and fixes

| # | Flow | Finding | Fix |
|---|---|---|---|
| 1 | Browse: product card ADD (W3) | ADD read the whole product (`GET /products/{slug}`) before adding, and opened the product page for every product with more than one size, although the list item now carries `defaultVariant` with its order rules and default cut (CAT-R23) | ADD sends the default size, its `defaultPreparationCode` and `minOrder` with no extra read (D255). No `fromPrice` (not sold here) opens the product page. A list item without a default size keeps the old read-then-decide path |
| 2 | Cart: quantity bounds (W4) | Each cart line and each card stepper read its product for min / max / step: one request per line, and the stepper stayed disabled until it answered. The cart line has carried `minOrder` / `maxOrder` / `orderStep` since PR #6 (confirmed on the live API) | Steppers use the cart line's bounds (`src/cart/line-bounds.ts`, D256). A line without them keeps the stepper disabled rather than guessing; the server still validates every change |
| 3 | Browse: filters (W8) | No preparation ("cleaned and cut") filter, although `GET /preparation-methods` (CAT-R27) and `GET /products?preparation=` exist | "More filters" lists the server's methods (with the visitor's pincode); URL key `prep` (UPPER_SNAKE codes only), sent as `preparation`; removable chips (D257) |
| 4 | Network: client IP (W9) | Doc still said open | Closed in the backend (`ForwardedHeaders:KnownNetworks`, one hop); the website already forwards `X-Forwarded-For`. Doc only |
| 5 | Slots and orders: time zone (W10) | Every time was shown in IST, although slots, serviceability (`city.timeZone`), order detail, list and tracking carry the store's IANA `timeZone` (ORD-R65) | `@fresh/core` date formatters take an optional zone, IST when missing or unknown (D258). Used in the slot picker, serviceability lines (shop, product page, pincode form), order success, order list, order detail, tracking timeline and substitution deadline |
| 6 | Offers: banner pictures (W11) | The offer strip showed text only; `/promotions` items carry a resolved `imageUrl` and an optional `badge` (CAT-R28) | The strip shows the server's picture and badge; nothing is built from `imageKey` (D259) |
| 7 | Orders: complaint | The complaint sheet kept one `Idempotency-Key` for the life of the sheet, so a corrected complaint after a refusal (for example `description` too short) went out with the old key and a new body. Today's `IdempotencyFilter` releases the key when the command is refused by an exception, so this works now, but it breaks the website's own rule (D224: one key per action, replaced after a definitive answer) and would turn into 422 `IDEMPOTENCY_KEY_REUSED` for any refusal the server ever stores | The key is replaced after any definitive answer; only a retryable failure (network, 429, 503, `IDEMPOTENCY_IN_PROGRESS`) resends it (D260) |
| 8 | Orders: cancel | Same key rule as #7, also when the customer changes the reason. A 409 (the order moved past the cancel point) left the stale page with a Cancel button that could never work | New key after a definitive answer and when the reason changes. A 409/422 closes the dialog with the server's message and reads the order and tracking again, so Cancel disappears when the server no longer allows it (D260) |
| 9 | Orders: substitutions | A refused accept/reject (offer expired or already answered) showed a toast but kept the stale Accept/Reject buttons | The order is read again after any refusal except a network failure (D260) |
| 10 | Account: profile | 409 `CONCURRENT_SAVE` (lost race) was shown as a plain error; only 412 reloaded. After a 412 the "changed elsewhere" message vanished, because the form is keyed by version and restarts when the new version arrives | Both codes reload the profile; the notice is held by the page, above the form, so it survives the reload (D261) |
| 11 | Account: delete account | A stale version (412) or lost race (409) on `DELETE /me` showed an error, and every later try quoted the same stale version | The profile is read again, so confirming again quotes the new version (D261) |
| 12 | Account: addresses | Edit handled only 412 | `CONCURRENT_SAVE` is handled the same way (D261) |
| 13 | Checkout: address | Checkout could choose and add addresses but not edit one; a wrong flat number meant leaving checkout | "Edit this address" edits the chosen address in place with `PUT /me/addresses/{id}` and `If-Match`; a changed pincode clears the slot; the preview is read again; 412/409 reload the list with a notice (D262) |
| 14 | Cart: coupon offers | "View offers" asked `GET /coupons` with the pincode being browsed, not the one the cart is priced for, so a customer browsing another city saw the wrong city's coupons | The cart's pincode is sent, else the browsed one (D263) |
| 15 | Support: policies | No privacy policy or terms page; the legal drafts in `docs/legal/` are unapproved | Outline pages `/support/privacy-policy` and `/support/terms` in the same OWNER TEXT pattern as the other policies, stating only what the site does; linked from the footer, the policy side card and the sitemap (D264). The approved text replaces them later |
| 16 | Account: notifications | "Mark read" failed silently on a refusal | Shows the server's message; the button shows progress |
| 17 | Image URLs (W15) | Doc said open | The live API sends absolute `https://…/media/…` URLs (CAT-R18); doc only |

## Checked and found correct (no change)

- **Sign-in and guest cart merge.** OTP request body (`phone`, `purpose: LOGIN`), BFF verify body
  (`challengeId`, `phone`, `code`, `deviceLabel`, `guestCartToken` from the httpOnly cart cookie) match
  `OtpVerifyRequest`; the cart is read again at once and after 2.5 s for the asynchronous merge; 429 / 503 handled.
- **Pincode and serviceability.** Always-200 `GET /serviceability` with `serviceable` / `reasonCode`;
  `CART_LOCATION_CHANGED` asks before `force=true`.
- **Product page.** Bounds and `maxOrderableNow` from the product (correct there: no cart line yet); estimate and
  maximum from `GET /price-quote`.
- **Checkout.** Preview and place bodies match `CheckoutRequest` (`addressId`, `deliverySlotId`, `paymentMethod`,
  `substitutionPreference`, `customerNotes`, `expectedTotalPaise`); one `Idempotency-Key` per action (D224),
  `PRICE_CHANGED` confirm with a new key and the new total, `CART_LOCATION_CHANGED` move; payment tiles follow the
  server's `paymentMethods`.
- **Orders.** Cancel reasons match `CustomerCancelReason` (CHANGED_MIND, WRONG_ADDRESS, SLOT_ISSUE, OTHER);
  complaint kinds match `ComplaintKind`; photos go one by one as multipart `file` after the complaint is created; only
  `allowedActions` decide which buttons show; tracking polls every 60 s until closed.
- **Rule 4.** No amount is added, subtracted or derived in the browser: bills, line totals, shortfalls and
  substitution price differences are the server's values, formatted only. The only arithmetic is rupees → paise on the
  shopper's own price-filter input.
- **Lists and states.** Every page has loading, empty (character) and error states with the server's message and the
  trace id; 404 and route errors have character states; internal links all resolve to existing routes.

## Left open (not in this change)

| Item | Why | Owner |
|---|---|---|
| Home hero "Next slot" still in IST | `apps/web/src/app/page.tsx` belongs to PR #30 (`web/home-reference-fixes`); one call: pass `serviceability.city?.timeZone` to the formatters | PR #30 or the next web change |
| Order list has no status filter | `GET /orders?status=` exists; not asked for | Product decision |
| Complaint photos are not shown back on the order page | The complaint list shows description and status only; photos are served at `GET …/complaints/{id}/photos/{photoId}` with the bearer token, so they need an authenticated image fetch | Next web change |
| Policy text | Outlines only until the owner and a lawyer approve `docs/legal/` | Owner |

## What is not verified

GitHub Actions is refusing jobs (billing), so on the day of this change **no check ran**: `pnpm typecheck`, `lint`,
`test`, `format:check` and `build` are unconfirmed. Prettier (print width 100, Tailwind class order) was applied by
hand. None of the fixes were seen in a browser against the live demo; only the public API shapes were confirmed with
`curl` (list item `defaultVariant`, cart line bounds, slots `timeZone`, banner `imageUrl`, preparation methods).
