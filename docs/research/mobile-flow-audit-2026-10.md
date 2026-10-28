# Mobile flow audit, October 2026

Goal: every screen of the Flutter customer app (`apps/mobile`) works end to end against the live API, in the
shared theme. This audit follows the API contract audit (`mobile-api-audit-2026-10.md`, PR #29, D235–D241),
which checked each request; this one walks each **flow** a customer takes and what the screen does with every
answer. Branch `mobile/complete-flows` (built on PR #29).

## Method

- Walked every route in `apps/mobile/lib/router.dart` and every flow by reading the screens and state:
  onboarding → pincode → home → shop / category / search → product → cart → sign-in → checkout → order placed →
  orders → tracking → account (profile, addresses, wishlist, notifications, preferences, settings, help, sign out).
- Compared each call and each refusal the server can send with the backend (`backend/src/Modules/*`: endpoints,
  validators, domain errors) and `docs/product/*.md`; confirmed shapes with curl on the Render demo
  (`/business-profile`, `/products/{slug}` at pincode 530017).
- Nothing ran on the dev Mac (CLAUDE.md rule 18): no `flutter analyze` or `flutter test` here. CI is blocked by
  GitHub billing, so the new tests and the analyser have **not run yet**.

## Findings and fixes

| # | Flow | Finding | Fix | Decision |
|---|---|---|---|---|
| F1 | Product | The page kept only a bare `PageView` for several pictures: no dots, nothing said there were more, screen readers could not move between them | `FreshGallery` in `fresh_theme` (pages + dots on a paper pill, tappable since the merge with #31's gallery, D276; one picture alone, none shows the placeholder; increase / decrease for screen readers). The product page uses it for 1–4 (any number of) server pictures; the stock sticker moved to the top so it never covers the dots | D266 |
| F2 | Product | `offer { couponCode, text }` (CAT-R26) and `serving { piecesMin, piecesMax, serves }` (CAT-R21) were not read; the "Kept cold" fact was always claimed (D239 fixed only the home) | Offer line shown as sent; serving facts formatted per the server basis, never scaled; "Kept cold" only when the pincode's store has cold storage | D274 |
| F3 | Sign-in | After an OTP the login did `context.go(from)`: from the product page (wishlist, Notify me) the whole stack was replaced, so Back went Home instead of to the shop or search the customer came from | `leaveLogin`: a login a screen pushed itself pops back; a router redirect (checkout, wishlist) replaces the login with the page that needed sign-in, keeping the pages below; alone it goes to the page or Home | D267 |
| F4 | Checkout | No way to edit an address from checkout (only add); the add form was a route inside the tab shell pushed over the full-screen checkout (a second shell over the first) | Edit button per address; `/addresses/new` and `/addresses/:id` are full-screen routes outside the shell; an edited chosen address keeps its slot only for the same pincode and is priced again; a new address starts with the customer's name and number from the profile | D268 |
| F5 | Checkout | The Idempotency-Key was reset by hand on each change; a refreshed preview with a new `expectedTotalPaise` after a network failure reused the old key with a different body → 422 `IDEMPOTENCY_KEY_REUSED` | The key belongs to one request body: a different body (selection, notes, expected total) always gets a new key; the same body after an unknown outcome reuses it | D269 |
| F6 | Checkout | `SLOT_FULL`, `SLOT_CUTOFF_PASSED`, `SLOT_NOT_AVAILABLE` on Place order left the old slot list on screen | The slot list is read again | D269 |
| F7 | Checkout | `substitutionPreference` `NONE` exists on the server but was not offered | "Leave it out" option | D275 |
| F8 | Complaint | "Delivered late" sent no `orderItemId` (the server requires it → 422); a description under 10 characters was sent (server: 10–1000 → 422); when a photo upload failed, Send raised a **second** complaint (409 `COMPLAINT_ALREADY_OPEN`) | The item is always asked; the 10-character minimum is checked before sending; the complaint is raised once, then each photo is sent and leaves the list, so Send retries only the photos still waiting (or Finish without photos) | D270 |
| F9 | Order | Invoice before it is issued answers 404 `INVOICE_NOT_ISSUED`; the app showed an error | "The invoice is not ready yet" note | D271 |
| F10 | Profile | Delete account refused with 422 `ACCOUNT_HAS_OPEN_MATTERS` (field `account`) showed nothing (the banner hid any error with field errors); 412 on delete kept the stale version | The banner shows every error not shown at a field; 412 re-reads the profile | D271 |
| F11 | Any | A 429 without `detail` showed the bare title ("Too Many Requests") | The wait from `retryAfterSeconds` / `Retry-After` is said | D271 |
| F12 | Account | Inbox and notification preferences did not follow the session: after signing out and in as another customer, the old inbox (unread badge) and preferences stayed | Both read again on sign-in / sign-out; nothing is read signed out | D272 |
| F13 | Help | The business profile's legal name, address, GSTIN and FSSAI licence were not shown ("Privacy and terms" opens Help) | "About the store" section with the configured rows | D273 |
| F14 | Navigation | Settings opened Help and Profile with `go`, dropping Settings from the stack; a CATEGORY offer banner pushed the Shop tab's route over Home | Settings pushes; a category banner switches to the Shop tab like the category tiles, a product banner is pushed | D267 |

## Checked and fine

- Onboarding, splash routing, pincode check, unserviceable state, cart move with `CART_LOCATION_CHANGED` confirm
  and `force=true`.
- Home, shop filters and sort (PR #27), search limits (D235), pincode refusals (D236).
- Product sizes, cuts (charge from the server, null charge not orderable), quantity bounds from
  `minOrder` / `orderStep` / `maxOrderableNow`, server price quote, Notify me (`STOCK_AVAILABLE` re-reads).
- Cart quantity steps against the product's server limits, change cut, coupons only signed in with the pincode
  (`X-Pincode`), View offers, checkout blockers.
- OTP request / verify codes (`OTP_EXPIRED`, `OTP_TOO_MANY_ATTEMPTS`, 429 wait), guest cart merge (token sent with
  verify, cart re-read).
- Checkout `PRICE_CHANGED` dialog, transient vs definitive refusals, payment methods from the preview.
- Order tracking polling, timeline, delivery code, substitutions accept / reject, cancel reasons, reorder.
- Profile `If-Match` with 412 re-read; addresses CRUD with `If-Match` on PUT, 412 re-read, field errors at the
  fields, `ADDRESS_LIMIT_REACHED` as a message.
- No client-side business math: amounts, discounts, totals and serving facts are the server's, only formatted.
  The cart and product steppers propose `quantity ± orderStep` inside the server's bounds; the server validates.

## Not verified

- No run on a phone and no CI run (GitHub Actions billing). The analyser and all tests, old and new, are
  unconfirmed until CI runs.
- Large text scale on the new parts (gallery dots, checkout edit button, complaint sheet, store details) was
  reasoned from the layout (wrapping text, no fixed heights), not measured.
- The product gallery tests load network image URLs in the test binding (answered 400, shown as the
  placeholder through `errorBuilder`); if the binding reports them, the tests need an image stub.
- Pushing a shell route from a full-screen route (the old checkout add-address path) was not tried on a device;
  it is no longer used.
