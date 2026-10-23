# Customer mobile app (Flutter, Phase 8)

Owner: E6. The customer app for Android and iOS at `apps/mobile` (decision D28). It follows `CLAUDE.md`
rule 4: **the server computes, the app renders.** The app never calculates a price, discount, tax, total,
stock or payment status. It only formats what the API returns (₹ with Indian grouping, kg/g, IST dates).

| Item | Value |
|---|---|
| Package id | `in.freshorbit.app` (Android `applicationId` and iOS bundle id) |
| App name | Fresh Orbit |
| Toolchain | Flutter 3.47.7 stable (Dart 3.13.5), null-safe, Material 3 |
| Theme | `fresh_theme` (`FreshTheme.light()` / `dark()`), generated from `packages/design-tokens` |
| State | Riverpod 3 (`flutter_riverpod`) |
| Navigation | go_router (`StatefulShellRoute.indexedStack` for the five tabs) |
| HTTP | dio, base URL from `--dart-define=API_BASE_URL` |

## 1. How to run

```sh
cd apps/mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000      # Android emulator → API on the host
flutter run --dart-define=API_BASE_URL=http://localhost:5000     # iOS simulator
flutter analyze        # zero issues expected
flutter test           # unit, provider and widget tests; no device needed
```

| Define | Required | Meaning |
|---|---|---|
| `API_BASE_URL` | Yes | API origin (no path). The app calls `<origin>/api/v1/…`. When it is missing, the app shows "API address not configured" and makes no calls (rule 7: missing configuration is refused, no fallback) |
| `SUPPORT_PHONE`, `SUPPORT_EMAIL` | No | Contact rows on Help and support. The customer API does not expose the business profile's support contacts yet (see §8); without them the screen says contact details are not set up |

No city, price, slot or policy value is written in the app.

## 2. Screens

Status: **Done** means the screen is built, analysed, and wired to the documented API contract. None of them
has been run against a live API or on a device yet (see §9).

| Screen | Route | Status | Tests |
|---|---|---|---|
| Splash | `/splash` | Done. Loads prefs and session, then goes to onboarding, location or home | — |
| Onboarding (3 pages) | `/onboarding` | Done | — |
| Location (pincode, serviceability, unserviceable state) | `/location` (`?change=1` from the pincode chip) | Done. Moving the cart asks first on `CART_LOCATION_CHANGED` | Widget (4) |
| Home | `/home` (tab) | Done. Banners, categories, featured products, pincode chip | — |
| Shop (categories, filters, sort, paging) | `/shop?category=` (tab) | Done | — |
| Search with suggestions | `/search` | Done. 250 ms debounce | — |
| Product detail | `/product/:slug` | Done. Variant, preparation with yield note, quantity/weight stepper, server unit price and preparation charge, add to cart; the estimate and maximum come from the cart line (§8) | Widget (6) |
| Wishlist | `/wishlist` | Done | — |
| Cart | `/cart` (tab) | Done. Server-priced lines and totals, coupon, availability and shortfall messages | Widget (4), provider (5) |
| Login (phone → OTP, guest cart merge) | `/login?from=` | Done | — |
| Checkout | `/checkout` | Done. Address select/create, slot picker, payment method (from the preview), preview blockers, place order with `Idempotency-Key`, `PRICE_CHANGED` confirm | Widget (4), provider (8) |
| Order success | `/order-success/:orderId` | Done | — |
| My orders | `/orders` (tab) | Done. Paged list | — |
| Order tracking | `/orders/:orderId` | Done. Timeline, delivery code while `OUT_FOR_DELIVERY`, rider call, items with weighed and charged quantities, cancel / complaint / reorder / invoice from `allowedActions`, reported problems; refresh every 30 s while active | Widget (2) |
| Account | `/account` (tab) | Done. Sign-in prompt when signed out; appearance (system/light/dark); sign out | — |
| Profile (edit, delete account) | `/account/profile` | Done. `If-Match`; 412 reloads; 409 refusal shown | — |
| Addresses (list, add, edit, default, delete) | `/account/addresses`, `/new`, `/:addressId` | Done | — |
| Notifications inbox | `/account/notifications` | Done. Mark read, mark all read | — |
| Notification preferences | `/account/preferences` | Done. `PREFERENCE_REQUIRED` shown as an error | — |
| Help and support | `/account/help` | Done. FAQ text, contact rows from defines | — |

Every data screen has a loading skeleton, an empty state and an error state (with the Problem Details
`traceId` as "Reference" and a retry button), via `AsyncValueView`, `EmptyState`, `ErrorState` in
`lib/widgets/states.dart`.

## 3. Navigation map

```
/splash ─► /onboarding ─► /location ─► /home
                                         │
 ┌─────────── bottom navigation (StatefulShellRoute, state kept per tab) ───────────┐
 │ /home        /shop          /cart          /orders            /account          │
 │   │            │              │              └─ /:orderId       ├─ profile  *   │
 │   │            │              │                                 ├─ addresses *  │
 │   │            │              │                                 │   ├─ new      │
 │   │            │              │                                 │   └─ :addressId│
 │   │            │              │                                 ├─ notifications│
 │   │            │              │                                 ├─ preferences *│
 │   │            │              │                                 └─ help         │
 └───┼────────────┼──────────────┼──────────────────────────────────────────────────┘
     ├─► /search ─┴─► /product/:slug ─► (add) ─► /cart ─► /checkout * ─► /order-success/:id ─► /orders/:id
     └─► /wishlist *              /location?change=1 (pincode chip)
```

`*` needs a session. The router redirect sends signed-out users to `/login?from=<path>` for `/checkout`,
`/orders/…`, `/account/…` (except `/account/help`) and `/wishlist`, and returns them to `from` after sign-in.
The `/orders` and `/account` tab roots show a sign-in prompt instead of redirecting. The redirect listens to
the session, so sign-out from any screen leaves protected routes.

## 4. State management

All state is Riverpod providers in `lib/state/`. Widgets read providers; providers call repositories; only
repositories know HTTP.

| File | Providers | Notes |
|---|---|---|
| `core_providers.dart` | `appPrefsProvider`, `tokenStoreProvider` (overridden in `main.dart`), `apiBaseUrlProvider`, `pushServiceProvider`, `dioProvider`, `apiClientProvider`, one provider per repository | Tests override the repositories with fakes |
| `session.dart` | `sessionProvider` (`SessionController`) | `requestOtp`, `verifyOtp` (sends the guest cart token for the merge, stores tokens, registers the push token), `signOut`; the interceptor calls `onSessionEnded` when refresh fails |
| `location_state.dart` | `pincodeProvider`, `serviceabilityProvider(pincode)`, `currentServiceabilityProvider`, `deliverySlotsProvider(pincode)` | Pincode persists in shared_preferences and goes out as `X-Pincode` |
| `catalog_state.dart` | `homeProvider`, `categoriesProvider`, `productListProvider(ProductRequest)` (paged, `loadMore`), `productDetailProvider`, `searchSuggestionsProvider` | Re-read when the pincode changes (prices are per location) |
| `cart_state.dart` | `cartProvider` (`CartController`), `cartLineCountProvider` | Every write replaces state with the cart the server returns. `CART_NOT_FOUND` → empty cart and the guest token cleared. Re-read on sign-in/out |
| `checkout_state.dart` | `checkoutProvider` (`CheckoutController`, auto-dispose) | Selection → preview (stale responses dropped by sequence number; a new address pincode clears the slot) → place order. Owns the Idempotency-Key (§5) and the `PRICE_CHANGED` accept/decline flow |
| `orders_state.dart` | `ordersProvider` (paged), `orderDetailProvider(id)`, `orderTrackingProvider(id)`, `orderComplaintsProvider(id)`, `orderActionsProvider` | Actions re-read the order after cancel / complaint; reorder replaces the cart |
| `account_state.dart` | `profileProvider`, `addressesProvider`, `wishlistProvider` + `wishlistIdsProvider`, `inboxProvider`, `preferencesProvider` | Writes re-read the list (the default address moves on the server) |

Theme mode is `themeModeProvider` in `lib/app.dart`, saved in shared_preferences.

## 5. API layer (`lib/api/`)

| File | What it does |
|---|---|
| `api_client.dart` | `createDio()`: base URL `<API_BASE_URL>/api/v1/`, JSON, timeouts, interceptors. `ContextHeadersInterceptor` adds `X-Pincode` (unless the request already has a `pincode` query) and `X-Cart-Token` (guest only; a new token from the response header or the cart body is saved). `ApiClient` turns every failure into `ApiError`. Helpers: `newIdempotencyKey()` (UUID v4), `ifMatch(version)` |
| `auth_interceptor.dart` | Adds `Authorization: Bearer`. Refreshes **before** a request when the access token is missing or expires within 30 s, and **after** a `401 TOKEN_EXPIRED`, then retries once. Refresh is single-flight: concurrent requests share one `POST auth/refresh`. A 401/403 from refresh ends the session; a network error keeps it |
| `token_store.dart` | Access token in memory only. Refresh token and guest cart token in `flutter_secure_storage` (Keychain on iOS, EncryptedSharedPreferences/Keystore on Android) |
| `api_error.dart` | RFC 7807 Problem Details → `ApiError {status, code, title, detail, errors (field → messages), traceId, retryAfterSeconds, extensions}`. Network failures get `NETWORK_ERROR` / `TIMEOUT` (status 0). Screens branch on `code`, never on wording |
| `repositories.dart` | One interface + HTTP implementation per area (auth, locations, catalog, cart, orders, account) |
| `models/*.dart` | Hand-written models (below) |

**Idempotency-Key rules** (place order, cancel, complaint): one key per user action. The same key is reused
when the outcome is unknown (network error, timeout, 5xx, `IDEMPOTENCY_IN_PROGRESS`, `CONCURRENT_SAVE`), so a
retry cannot create a second order. A new key is made after any other 4xx, after the customer changes the
selection, and after accepting a new price.

**`PRICE_CHANGED`**: `POST orders` sends `expectedTotalPaise` from the preview. On 409 `PRICE_CHANGED` the
response carries the new preview; the app shows old and new totals and places the order again only if the
customer accepts.

### TEMPORARY model files

Every file in `apps/mobile/lib/api/models/` starts with
`// TEMPORARY: replace with fresh_api generated models after Phase 3`:
`json.dart`, `common.dart`, `auth.dart`, `locations.dart`, `catalog.dart`, `cart.dart`, `checkout.dart`,
`orders.dart`, `account.dart`. They follow `docs/api/README.md` and, where a module is merged, its actual
contracts (Identity, Customers, Notifications, Locations, Catalog, Pricing). Parsers are tolerant of optional
fields. When `packages/flutter/fresh_api` is generated, replace the imports in `repositories.dart` and the
state layer, then delete these files.

## 6. API calls per screen

Paths are under `/api/v1/`.

| Screen | Calls |
|---|---|
| Location | `GET serviceability?pincode=`; `PUT cart/location` (`?force=true` after confirm) |
| Home | `GET home`, `GET categories` |
| Shop | `GET categories`, `GET products?category=&sort=&…&page=` |
| Search | `GET search/suggest?q=`, `GET products?q=` |
| Product detail | `GET products/{slug}?preparation=`, `POST cart/items`, `PUT/DELETE me/wishlist/{productId}` |
| Wishlist | `GET me/wishlist` |
| Cart | `GET cart`, `PATCH/DELETE cart/items/{lineId}`, `DELETE cart`, `PUT/DELETE cart/coupon`; product detail on demand for the quantity sheet |
| Login | `POST auth/otp/request`, `POST auth/otp/verify` (with `guestCartToken`), `PUT me/device-tokens` (when push is set up) |
| Checkout | `GET me/addresses`, `POST me/addresses`, `GET delivery-slots?pincode=`, `POST checkout/preview`, `POST orders` (`Idempotency-Key`) |
| Order success | `GET orders/{id}` |
| My orders | `GET orders?page=&pageSize=&sort=-placedAt` |
| Order tracking | `GET orders/{id}`, `GET orders/{id}/tracking`, `GET orders/{id}/complaints`, `POST orders/{id}/cancel` (IK), `POST orders/{id}/complaints` (multipart, IK), `POST orders/{id}/reorder`, `GET orders/{id}/invoice` |
| Account / profile | `GET me`, `PATCH me` (`If-Match`), `DELETE me` (`If-Match`), `POST auth/logout` |
| Addresses | `GET/POST me/addresses`, `PUT/DELETE me/addresses/{id}` (`If-Match` on PUT), `POST me/addresses/{id}/default` |
| Notifications | `GET me/notifications`, `POST me/notifications/{id}/read`, `POST me/notifications/read-all` |
| Preferences | `GET/PUT me/notification-preferences` |
| Any | `POST auth/refresh` (interceptor) |

## 7. Accessibility

- Every icon-only button has a tooltip, which is also its semantics label (for example
  "Increase quantity of Seer Fish", "Remove Seer Fish", "Copy delivery code").
- Tap targets are at least 48 dp (Material 3 defaults, stepper buttons sized explicitly).
- Prices have semantic labels ("Estimated ₹…", "at most ₹…"); the delivery code is read digit by digit.
- Section titles are marked as headers. Errors and loading use live regions.
- No fixed text heights; layouts wrap or scroll, so system text scaling works. Widget tests run at phone width.

## 8. API gaps found (for the backend owners)

| Gap | What the app does now |
|---|---|
| Product detail has no estimate or maximum chargeable for a chosen quantity | Shows the server unit price and preparation charge; after adding (or when the item is already in the cart), shows the cart line's `estimatedTotalPaise` and `maxChargeablePaise`. A `GET products/{slug}/estimate?quantity=` or fields on the detail would let it show them before adding |
| Cart lines lack `minOrder`, `maxOrder`, `orderStep` | The cart's quantity sheet loads the product detail on demand to get them |
| Wishlist: docs say products, merged backend returns `{productId, addedAt}` | Both shapes are parsed; entries without product data show a link by id |
| Orders list item shape is not documented | `OrderSummary` reads the common fields and tolerates the rest |
| Support contacts are only in the admin business profile | Taken from `SUPPORT_PHONE` / `SUPPORT_EMAIL` defines until a public endpoint exists |
| Cart and Ordering modules are not merged yet | Their models follow `docs/api/README.md`; re-check when they land |

## 9. Not done or not verified

- No build on a device or emulator: this machine has no Android SDK, and Xcode/CocoaPods are incomplete
  (`flutter doctor`). Only `flutter analyze` and `flutter test` were run.
- Not run against a live API. An end-to-end integration test (browse → cart → checkout) is still to write once
  Cart and Ordering are merged.
- Complaint photos: the complaint is sent as multipart without images; an image picker is still to add.
- Fonts (Inter and others, OFL) are not bundled; the theme falls back to system fonts.
- FCM is not added (below).

## 10. Push notifications (FCM)

The app depends only on `PushService` (`lib/core/push_service.dart`); `NoopPushService` is used today.
`firebase_messaging` is **not** added until the owner creates a Firebase project. Steps then:

1. Create a Firebase project (free Spark plan). Add an Android app `in.freshorbit.app` and an iOS app
   `in.freshorbit.app`.
2. Download `google-services.json` → `apps/mobile/android/app/`, `GoogleService-Info.plist` →
   `apps/mobile/ios/Runner/`. Keep both out of git (they are per-environment); CI writes them from secrets.
3. Add `firebase_core` and `firebase_messaging` (BSD-3) with pinned versions; run `flutterfire configure` or
   add the Google services Gradle plugin by hand.
4. iOS: upload an APNs key (.p8) to Firebase; enable Push Notifications and Background Modes → Remote
   notifications in Xcode.
5. Write `FcmPushService implements PushService` (permission request, `getToken`, `onTokenRefresh`,
   `onMessageOpenedApp` → `PushOpen`), and override `pushServiceProvider` in `main.dart`.
6. The session already sends the token with `PUT me/device-tokens` after sign-in and removes it on sign-out.
7. Backend: configure the FCM adapter behind `IPushSender` (D28) with the service account.

## 11. Release needs

**Android (Google Play)**

- Android SDK + Android Studio (or command-line tools) on the build machine; `flutter doctor --android-licenses`.
- An upload keystore (`keytool -genkey -v -keystore upload.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload`),
  stored outside git; `android/key.properties` (git-ignored) and a `signingConfigs.release` block in
  `android/app/build.gradle.kts`. Today release builds use the debug key (flutter create default).
- Google Play Console account (one-time fee, paid by the owner), app entry `in.freshorbit.app`, Play App Signing,
  privacy policy URL, data safety form, content rating.
- `flutter build appbundle --dart-define=API_BASE_URL=https://…` → upload to the internal testing track first.

**iOS (App Store)**

- A Mac with Xcode and CocoaPods (`brew install cocoapods`); `flutter doctor` must pass for iOS.
- Apple Developer Program membership (yearly fee, paid by the owner); bundle id `in.freshorbit.app`; team set in
  `ios/Runner.xcodeproj`.
- App Store Connect app record, privacy details, screenshots.
- `flutter build ipa --dart-define=API_BASE_URL=https://…` → upload with Xcode or Transporter → TestFlight →
  review.

Both stores need the production API on HTTPS. Android also has the `INTERNET` permission and `<queries>` for
`tel:`, `mailto:` and `https:` (url_launcher) in the main manifest.

## 12. Packages

All from pub.dev, permissive licences, versions pinned exactly in `pubspec.yaml`; `pubspec.lock` is committed.

| Package | Version | Licence | Why |
|---|---|---|---|
| `dio` | 5.11.1 | MIT | HTTP, interceptors |
| `flutter_riverpod` | 3.4.3 | MIT | State |
| `go_router` | 18.1.1 | BSD-3-Clause | Navigation, tab shell, redirects |
| `flutter_secure_storage` | 11.2.0 | BSD-3-Clause | Refresh token and guest cart token |
| `shared_preferences` | 2.5.6 | BSD-3-Clause | Pincode, onboarding flag, theme mode |
| `intl` | 0.20.3 | BSD-3-Clause | IST date and time formatting |
| `uuid` | 4.6.0 | MIT | Idempotency keys |
| `url_launcher` | 6.3.3 | BSD-3-Clause | Call rider / support, e-mail, invoice PDF |
| `fresh_theme` | path | Ours | Theme from design tokens |
| `flutter_lints` (dev) | 6.0.0 | BSD-3-Clause | Lints |

**Lints:** `flutter_lints` with `strict-casts`, `strict-inference`, `strict-raw-types` and extra rules
(`avoid_dynamic_calls`, `unawaited_futures`, `prefer_final_locals`, `require_trailing_commas`, and others in
`analysis_options.yaml`). Not `very_good_analysis`: it requires doc comments on every public member
(`public_member_api_docs`), which is noise in an app nobody imports. Format with `dart format` at page width
120 (set in `analysis_options.yaml`).

## 13. Tests

`flutter test` (no device): 59 tests.

| Area | File | What it proves |
|---|---|---|
| Formatters | `test/core/format_test.dart` | ₹ Indian grouping, negatives, kg/g, IST dates, phone, pincode shape |
| Error mapping | `test/api/api_error_test.dart` | Problem Details → code, field errors, traceId, extensions, `Retry-After`; network and invalid-response codes; `X-Pincode` / `X-Cart-Token` headers |
| Auth refresh | `test/api/auth_interceptor_test.dart` | Bearer header, single-flight refresh for concurrent 401s, retry, proactive refresh, session end on refresh 401 and on a second 401, kept on network error |
| Cart provider | `test/state/cart_controller_test.dart` | Server cart replaces state, refused writes keep the cart, guest token from the body, `CART_NOT_FOUND`, `CART_LOCATION_CHANGED` then force |
| Checkout provider | `test/state/checkout_controller_test.dart` | Preview on selection, key reuse on transient errors, new key after 4xx and after price acceptance, `PRICE_CHANGED` |
| Widgets | `test/widgets/*` | Location (serviceable / not / invalid), product detail, cart, checkout `PRICE_CHANGED` dialog, order tracking (delivery code, cancel with key) |
