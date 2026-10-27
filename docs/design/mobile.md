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


No city, price, slot or policy value is written in the app.

## 2. Screens

Status: **Done** means the screen is built, analysed, and wired to the documented API contract. None of them
has been run against a live API or on a device yet (see §9).

| Screen | Route | Status | Tests |
|---|---|---|---|
| Splash | `/splash` | Done. Loads prefs and session, then goes to onboarding, location or home | — |
| Onboarding (3 pages) | `/onboarding` | Done | — |
| Location (pincode, serviceability, unserviceable state) | `/location` (`?change=1` from the pincode chip) | Done. Moving the cart asks first on `CART_LOCATION_CHANGED` | Widget (4) |
| Home | `/home` (tab) | Done. Rich home on one grid (§16): delivery header, heading, harbour, hero, two cards, categories, Today's special; fits one screen (D182) | Widget (`home_screen_test.dart`) |
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
| Product detail has no estimate or maximum chargeable for a chosen quantity | Closed: the product page shows `GET /price-quote` for the chosen size, cut and quantity (§17) |
| Cart lines lack `minOrder`, `maxOrder`, `orderStep` | The cart's quantity sheet loads the product detail on demand to get them |
| Wishlist: docs say products, merged backend returns `{productId, addedAt}` | Both shapes are parsed; entries without product data show a link by id |
| Orders list item shape is not documented | `OrderSummary` reads the common fields and tolerates the rest |
| Support contacts are only in the admin business profile | Closed: Help and support reads `GET /business-profile` (§17); the `SUPPORT_*` defines are gone |
| Cart and Ordering modules are not merged yet | Their models follow `docs/api/README.md`; re-check when they land |
| Product summary has no default size, cut or limits (D88 card ADD) | The card's ADD and − / + read the product detail first (one extra call per tap) |
| No discount percentage, no catch time, no delivery ETA in the API | The mockup's "% off", "caught 4:30 am" and "arrives in 25 min" are not shown; the slot window is shown instead |
| No "Notify me" (back in stock) endpoint | Closed: a sold-out size offers Notify me (`POST /me/stock-alerts`, §17) |

**Backend Track E (2026-10-24).** The backend now closes these gaps, either on `main` (#5) or on draft PR #6 (not merged):
- price quote: `GET /price-quote`
- cart line order rules
- priced wishlist cards
- public `GET /business-profile`
- `defaultVariant`
- `discountBp`, `freshness`, and the ETA from serviceability `nextSlot`
- "Notify me": `POST/GET/DELETE /me/stock-alerts`

The app now uses the price quote, the business profile and stock alerts (§17); the others are not adopted yet.

## 9. Not done or not verified

- No build on a device or emulator: this machine has no Android SDK, and Xcode/CocoaPods are incomplete
  (`flutter doctor`). Only `flutter analyze` and `flutter test` were run.
- Not run against a live API. An end-to-end integration test (browse → cart → checkout) is still to write once
  Cart and Ordering are merged.
- Complaint photos: the complaint is sent as multipart without images; an image picker is still to add.
- Final illustrations are not in yet: every slot draws its placeholder until the owner's PNGs land (§14).
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
| `image_picker` | 1.1.2 | BSD-3-Clause (Apache-2.0 for the Android part) | Photos for Report a problem; `pubspec.lock` must be refreshed with `flutter pub get` |
| `fresh_theme` | path | Ours | Theme from design tokens |
| `flutter_lints` (dev) | 6.0.0 | BSD-3-Clause | Lints |

**Lints:** `flutter_lints` with `strict-casts`, `strict-inference`, `strict-raw-types` and extra rules
(`avoid_dynamic_calls`, `unawaited_futures`, `prefer_final_locals`, `require_trailing_commas`, and others in
`analysis_options.yaml`). Not `very_good_analysis`: it requires doc comments on every public member
(`public_member_api_docs`), which is noise in an app nobody imports. Format with `dart format` at page width
120 (set in `analysis_options.yaml`).

## 13. Tests

`flutter test` (no device): 106 tests.

| Area | File | What it proves |
|---|---|---|
| Formatters | `test/core/format_test.dart` | ₹ Indian grouping, negatives, kg/g, IST dates, phone, pincode shape |
| Error mapping | `test/api/api_error_test.dart` | Problem Details → code, field errors, traceId, extensions, `Retry-After`; network and invalid-response codes; `X-Pincode` / `X-Cart-Token` headers |
| Auth refresh | `test/api/auth_interceptor_test.dart` | Bearer header, single-flight refresh for concurrent 401s, retry, proactive refresh, session end on refresh 401 and on a second 401, kept on network error |
| Cart provider | `test/state/cart_controller_test.dart` | Server cart replaces state, refused writes keep the cart, guest token from the body, `CART_NOT_FOUND`, `CART_LOCATION_CHANGED` then force |
| Checkout provider | `test/state/checkout_controller_test.dart` | Preview on selection, key reuse on transient errors, new key after 4xx and after price acceptance, `PRICE_CHANGED` |
| Widgets | `test/widgets/*` | Location (serviceable / not / invalid), product detail, cart, checkout `PRICE_CHANGED` dialog, order tracking (delivery code, cancel with key) |
| Bold blocks widgets | `test/widgets/bold_test.dart` | `AvatarCircle` (size, circle crop, JPEG cropped with no rectangle frame, decorative), `ColorBlockScreen` (eyebrow, one heading header, avatar, sticker, back; reduced motion runs nothing; dark mode), `Heading` script tilt and font, `ScriptTitle`, tab bar (icon over label, selected, "Cart, 2 items"), View cart bar, card ADD → stepper, disabled ADD without a price, bubbles and pills, segmented pills, slot buttons, progress steps, teal action |
| Bold blocks screens | `test/widgets/bold_screens_test.dart` | Card ADD sends the server defaults and turns into a stepper; + sends the next server step, − below the minimum removes; settings stores theme and language; OTP boxes; empty orders and saved blocks |
| Illustrated UI | `test/widgets/illustrated_test.dart` | Slot codes and PNG names, order status → slot, placeholder when no asset, PNG preferred over WebP, WebP fallback, a broken file falls back, every slot draws in light and dark, semantics label or excluded, `EmptyState`, `ReceiptCard` shows exactly the server values given, reduced motion runs no animation |
| Demo backend | `test/demo/demo_backend_test.dart` | Demo is off without `DEMO=true`, every fixture parses with the app models, empty and offline switches, unknown order is 404 |

## 14. Illustrated UI (D53)

The app follows the illustrated style concept: sand surfaces, a teal and coral palette, a clay-look cast of
characters, stickers and receipts. `packages/flutter/fresh_theme` is not edited; the app extends it.

### Theme (`lib/theme/app_theme.dart`)

- `AppTheme.light()` / `AppTheme.dark()` start from `FreshTheme` and add the `IllustratedColors` theme extension
  (read it with `illustratedColors(context)`).
- Teal `#0B7D70` (`tealText`) is used for anything that carries text. `#0E9384` (`tealDecor`) is for decoration
  only. Coral is the sticker and focus-ring accent.
- Dark mode is designed, not inverted: deep sea-green sand `#14201F`, paper `#1A2826`, ink `#F4EBDD`, teal
  `#3FC0AE`.
- Fonts are bundled from `assets/fonts/` under the SIL Open Font License; the licence files sit next to them.
  Baloo 2 (600–800) is used for display, headline and large titles, and Inter (400–700) for everything else.
  Both are subset to Latin plus ₹.
- Shape: cards have a 24 radius (heroes and sheets 28, fields 16), buttons are pills, shadows are soft.

### Widgets

| Widget | File | Notes |
|---|---|---|
| `Illustration(slot:, size:)` | `lib/illustrations/illustration.dart` | Loads `assets/illustrations/<slot_lower_snake>.png`, then `.webp`. If neither file is in the asset manifest, or the file fails to decode, it draws `IllustrationPlaceholder`. It gets a semantics label from the slot, or is excluded from semantics when `decorative: true` |
| `IllustrationPlaceholder` | same | A clay-look `CastPainter` drawing (`lib/illustrations/cast_painter.dart`) with a prop badge. Cast colours follow `docs/design/characters/character-sheet.jpg`: Padma, Ravi, Anil, Ammamma and Chanduva the pomfret |
| `EmptyState(slot, title, message, action)` | `lib/widgets/states.dart` | The illustration is decorative; the title is a semantics header |
| `ErrorState`, `NotFoundScreen` | same | `slotForError`: `NETWORK_ERROR` / `TIMEOUT` → `OFFLINE`, 404 → `NOT_FOUND`, anything else → `ERROR_GENERIC` |
| `Sticker`, `PolaroidCard`, `FeatureCard`, `HelpHero`, `ReceiptCard` | `lib/widgets/illustrated.dart` | Stickers use Baloo 2 800 with a 2.5px ink outline and an offset shadow. `ReceiptCard` has a scalloped edge, a teal-soft total band and a coral-soft note. **It computes nothing**: every row, the total and the note are strings the caller formats from server values |
| `EntranceMotion`, `motionAllowed` | `lib/widgets/motion.dart` | A gentle fade and rise that runs once. With `MediaQuery.disableAnimations` it shows the child at once, with no ticker |

### Slot per screen

| Screen | Slot(s) |
|---|---|
| Splash | `HOME_HERO` |
| Onboarding (3 polaroid pages) | `ONBOARDING_FRESH`, `ONBOARDING_CLEANED`, `ONBOARDING_DELIVERY` |
| Location / not serviceable | `LOCATION_PICK`, `NOT_SERVICEABLE` |
| Home hero, 4 feature cards | `HOME_HERO`; `FEATURE_HONEST_WEIGHT`, `FEATURE_CLEANED_YOUR_WAY`, `FEATURE_COLD_CHAIN`, `FEATURE_SAME_DAY` |
| Empty cart, wishlist, orders, notifications, addresses | `EMPTY_CART`, `EMPTY_WISHLIST`, `EMPTY_ORDERS`, `EMPTY_NOTIFICATIONS`, `EMPTY_ADDRESSES` |
| Search and category with no results | `NO_SEARCH_RESULTS` |
| Product sold out | `OUT_OF_STOCK` |
| OTP login, signed-out account | `LOGIN_OTP` |
| Order success (receipt) | `ORDER_PLACED` |
| Tracking status card | `slotForOrderStatus`: placed/confirmed → `ORDER_PLACED`; `PREPARING`, `PACKED` → `ORDER_PACKING`; `OUT_FOR_DELIVERY` → `ORDER_ON_THE_WAY`; `DELIVERED` → `ORDER_DELIVERED`; `CANCELLED`, `PAYMENT_FAILED`, `RETURNED` → `ORDER_CANCELLED`; `DELIVERY_FAILED` → `ERROR_GENERIC` |
| Complaint sent (dialog) | `COMPLAINT_RECEIVED` |
| Help | `HELP_SUPPORT` (`HelpHero`) |
| Error, offline, unknown route | `ERROR_GENERIC`, `OFFLINE`, `NOT_FOUND` |

§15 updates this table for the Bold blocks look.

**Final art:** drop `<slot_lower_snake>.png` files (for example `empty_cart.png`, `order_on_the_way.png`) into
`apps/mobile/assets/illustrations/`. No code change is needed; WebP with the same stem is also read.

### Demo mode (web preview)

`lib/demo/` holds a fixture backend that implements every repository with sample data (6 products, one sold
out; 5 orders in different states; pincode `999999` is not served). It is compiled in only with
`--dart-define=DEMO=true` (`const demoMode = bool.fromEnvironment('DEMO')`), so normal builds tree-shake it,
and a release build for a device refuses to start with it. The Account tab shows a Demo panel: switches for
empty lists and offline, and jumps to each illustrated state.

```sh
cd apps/mobile
flutter build web --no-web-resources-cdn --dart-define=DEMO=true   # output: apps/mobile/build/web (~42 MB)
```

The page makes only same-origin requests. CanvasKit is bundled (`useLocalCanvasKit: true`), and
`web/flutter_bootstrap.js` points `fontFallbackBaseUrl` at a local path. The gstatic URLs left in the
compiled JS are the engine's defaults, and this config overrides them. Serve the folder with any static
server. The build warns that the CupertinoIcons font is not bundled; the app does not use it.

## 15. Bold blocks look (D88, D89)

The owner chose look B "Bold blocks" (D88) and the fonts Baloo 2 + Inter with Dancing Script second lines
(D89 amended). The reference is `docs/design/mockups/app-bold-blocks.html`. The app follows it closely. There
is no halo, orbiting dot, scrolling ticker or serif mark (D88 amended; D94 withdrawn). Everything in §14 still
applies (slots, placeholders, `Illustration`, demo mode); this section lists what changed.

### Theme and fonts (`lib/theme/app_theme.dart`)

- Colours come from the mockup's `:root` and `[data-theme=dark]`: page `#F4EEE3`, paper `#FFFDF8`, ink
  `#14302F`, teal `#0B7D70` (deep `#075F55`, soft `#D8EEEA`), coral `#F07A5A`, mango `#F5B544`, ice
  `#BFE3EE`. Dark: page `#101918`, paper `#1A2826`, ink `#F4EBDD`, teal `#2FA595`, coral `#E0714F`, mango
  `#E0A43C`, ice `#4D93A6`.
- **Deviation:** in dark mode the mockup puts paper text on teal `#2FA595`. That is 2.98:1, below WCAG AA, so
  `onTeal` is dark ink `#0C1B1A` in dark mode (proposed decision).
- Block colours: each colour-block screen has one `BlockTone` (coral, teal, mango, ice) with its background,
  its text colour and its status-bar style.
- **One constant per font role** (`AppFonts`):

  | Role | Constant | Family | Bundled |
  |---|---|---|---|
  | Headings, prices, buttons on blocks | `AppFonts.display` | `Baloo2` (800) | Yes, `assets/fonts/Baloo2-*.ttf` |
  | Body text | `AppFonts.body` | `Inter` (400–700) | Yes, `assets/fonts/Inter-*.ttf` |
  | Handwritten lines (script heading lines, the onboarding tagline and banner line) | `AppFonts.script` | `DancingScript` (700) | Yes, `assets/fonts/DancingScript-Variable.ttf` |

  Dancing Script is bundled as the Google Fonts variable font (`DancingScript[wght].ttf` from
  `google/fonts/ofl/dancingscript`, SIL OFL 1.1, licence in `assets/fonts/DancingScript-OFL.txt`), about 130 KB.
  `scriptStyle` (`lib/widgets/heading.dart`) sets `fontWeight: w700` and `FontVariation('wght', 700)`, so the
  variable font draws its bold instance.

  To change a font later, change its constant and the `fonts:` entry. The app never calls Google Fonts at
  runtime.
- Inter is bundled up to 700. Labels the mockup sets at 800 use the 700 file.
- Buttons: the default `FilledButton` is ink with paper text and a radius of 20. Sticky bars use the teal
  `TealActionButton`. Chips turn ink when selected; switches and radios are teal.

### Widgets (`lib/widgets/bold.dart`, `lib/widgets/heading.dart`)

| Widget | What it is |
|---|---|
| `ColorBlockScreen` | A full colour block with an eyebrow, a big `Heading`, the character in an `AvatarCircle`, an optional sticker, and a paper sheet that rises over the circle's lower half (`EntranceMotion`, instant with reduced motion). The whole screen scrolls. Used for onboarding, location and not serviceable, OTP, empty cart, saved, orders, notifications and signed-out orders |
| `AvatarCircle` | A ringed circle: a paper border, a 3 px ink ring and the slot cropped inside (`Illustration(inCircle: true, focus:)`). A full-scene JPEG is cropped by the circle and never shown as a pasted rectangle. `BlockTone.avatarFocus` holds the mockup's `object-position` per slot. Decorative by default; key `avatar-<SLOT>` |
| `Heading`, `HeadingLine`, `ScriptTitle`, `BrushSwipe` | Big headings in Baloo 2 800. A `script` line is Dancing Script, tilted −3°, at 1.12×. A `brush` line sits on a painted mango swipe with ink text (paper on mango blocks). Three short dashes sit at the top corner (`doodle`). Screen readers hear one header with the lines joined. `ScriptTitle` is the inline "Your *cart*" title |
| `Sticker` | The mockup badge: a 2 px ink outline, a 2,2 offset shadow and a slight tilt. Its colours are the same in light and dark |
| `FoodTabBar`, `FoodTab` | Home, Shop, Cart, Orders, Account (D181, shared by all tabs): a floating rounded paper menu (radius 22, 58 dp, 16 dp gutters, soft shadow; it sits the home-indicator inset minus 20 above the bottom, at least 8), icon over label, a 48 × 28 teal-soft pill behind the active icon, and the cart's line count on a coral pip ("Cart, 2 items"). Keys `tab-<label>` |
| `FreshCartBar` (fresh_theme, D207) | A floating deep-teal bar (`#0C4A43`, radius 18, 52 dp) above the menu on Home and Shop while the cart has lines: up to two overlapping line pictures (only lines that have one; none leaves the space out, D221), "2 ITEMS ADDED", the server's estimated total and "View cart ›". It sits in the bottom bar slot, so it never covers content. Key `viewCartBar` |
| `ProductCard` + `CardCartControl` | Paper card with a tinted picture. The ADD pill sits on the picture's bottom edge (inside the card's bounds so it can be tapped). In the cart it becomes a − n + stepper; with several cuts in the cart it reads "n IN CART" and opens the cart |
| `CategoryBubble`, `FilterPill` | Round category bubbles (ink ring when chosen) and small filter pills (ink when on) |
| `CompactStepper` | The − n + stepper for cards, cart lines and the product bar. The label is the server's quantity |
| `SlotButton`, `SegmentedPills`, `SectionBox`, `SectionTitle`, `MenuCard`, `MenuRow`, `ProgressSteps`, `StickyActionBar`, `TealActionButton`, `RoundIconButton`, `PageDots` | Slot choice, EN / తెలుగు / हिं and Light / Auto / Dark controls, paper boxes, menus, four tracking bars and the sticky pay bar |

### Screens

| Screen | Look |
|---|---|
| Splash | Teal with `HOME_HERO` in the circle |
| Onboarding ×3 | The owner's reference layout (below): coral "Straight off / the boats", teal "Cleaned / your way", mango "At your / door", second line on the brush. Stickers: Morning catch, Your cut, Kept cold. Taglines, three feature tiles, a food banner, pinned dots and Next / Get started |
| Location, not serviceable | Ice block: "Where do we / *deliver?*" with the pincode field; "Not here / *yet*" with the Coming soon sticker |
| OTP | Mango block, six OTP boxes over one real text field (SMS autofill still works) |
| Home | The rich home (§16): delivery header, greeting, "What's cooking *today?*", harbour scenery, today's catch hero, Cleaned your way / Kept cold cards, six category columns, Today's special |
| Shop | "Shop *fresh*", search, bubbles, pills (Sort, In stock, Wild caught, Farmed; the filter sheet is kept), grid with ADD |
| Product | Teal-soft hero with back and save, name and local names, price, "Choose your cut" tiles (the server's charge: Free, +₹40/kg, or Not available here), and a sticky bar with the stepper and Add to cart |
| Cart | "Your *cart*", lines with steppers and remove, the coupon strip (mango-soft), up to three open slots, server notes, bill details, and a sticky bar ("Pay on delivery · Cash / UPI", Checkout + the server's estimated total). Empty: coral "Basket's / *empty*" |
| Checkout | Steps in paper boxes, slots as buttons (it starts with the slot picked in the cart when the address offers it), and a pay bar with the payment method and Place order + the server's `expectedTotalPaise` |
| Orders, tracking, order placed | Orders: cards with the status circle. Empty: mango "No orders / *yet*". Tracking: a teal block with the status and its character, a "Code ####" sticker while out for delivery, then a paper sheet with the slot, four progress bars, the delivery code, rider, timeline, receipt and actions. No moving dot. Order placed: a teal block with `ORDER_PLACED` and the receipt |
| Saved, notifications | Coral "*Nothing* / saved yet"; ice "*All* / quiet" |
| Account, settings | "My *account*", a mango profile card, quick tiles (Orders, Saved, Addresses) and a menu. Settings (`/account/settings`, open when signed out too): notification switches, Language EN / తెలుగు / हिं, Theme Light / Auto / Dark, Privacy and terms, Log out, Delete my account |

### Onboarding (owner's reference, D156)

The owner redesigned onboarding page 1 (`docs/design/mockups/onboarding-reference-owner.jpg`) and approved a
preview of all three pages (D156, `docs/design/mockups/app-onboarding-rich.html`, drawn at 390 × 873). The app
follows the preview's numbers (`lib/features/onboarding/onboarding_screen.dart`; drawings traced from the
preview's SVG in `onboarding_art.dart`).

After the owner tried it on a real phone (about 390 × 844 with Android system bars), the page is laid out to the
screen instead of being shrunk as a whole (a whole-page `FittedBox` narrowed the block and banner and left cream
gaps at the sides). Numbers as built (dp, 390-wide phone):

| Part | What it is |
|---|---|
| Layout | The page area (above the dots and button) is split: the colour block takes 45% of its height, clamped to 300–400 (and at most 62%); the sheet takes the rest. The block colour runs on under the sheet, whose top is a clean 28 radius curve. Block and sheet always span the full width. No scrolling below a system text scale of 1.5; at 1.5 and up the page scrolls with `ClampingScrollPhysics` (D182) |
| Colour block | Eyebrow "STEP n OF 3" (Inter 700, 12.5, letter-spaced, 78%) and Skip (Inter 700, 17) in one 48 dp row; a two-line Baloo 2 800 heading, 40 scaled with the width (at least 34), its second line on the yellow brush (`#F5D04A`, −1.5°), a spark after each line (cream on coral and mango, yellow on teal); a Dancing Script tagline (23, two lines, −6°) with a swoosh, left of the circle. 20 dp gutters; gaps 8 |
| Drawn scene | `BlockScenePainter`, static and excluded from semantics: pale clouds, darker hills behind the circle, the silhouette (coral: the trawler; teal: the cutting board and knife; mango: the scooter with its cold box) fully visible between x 8 and the circle's left edge − 8, scaled to fit below the tagline and sitting in the sea, three birds above it, and two sea bands with a white wave line that end 4 above the sheet (nothing cuts the sheet's edge). Shades: coral `#C65A3F`, teal `#075E54`, mango `#CF8E22` |
| Circle and sticker | The `ONBOARDING_*` character in a ringed circle (7 px ink ring inside a 6 px paper ring), 212 on a 390 phone (shrinks with the block, 150–212), centred at 62% of the width, reaching 28 into the sheet. The sticker (Inter 800, 14, ink outline, 3 px ink drop shadow, −8°) sits on the circle's lower-left edge, 64% down (over the apron and the catch, never the face), with a yellow spark above it |
| Sheet | Grey-teal birds in both top corners; title Inter 800, 22; body Inter 400, 14.5 / 1.4, muted; tiles 16 below; 20 dp gutters. The text starts 36 below the sheet top, clear of the circle. If even the text and tiles do not fit, they are scaled down at full width (`FreshFitWidth`), never narrowed |
| Feature tiles | The `ONBOARDING_TILE_*` picture (96 × 84) popping out of a 98 × 66 pastel blob (green, blue, peach), a spark on the corner (coral; a blue snowflake on the blue tile; coral or teal), a label (Inter 600, 13.5) and a muted caption (Inter 400, 12), two lines at most each, the three tiles top-aligned, 8 apart. One semantics node: "label, caption". Until the PNG is bundled the blob shows an icon (`Illustration(fallback:)`); all twelve are bundled now |
| Food banner | Inside 16 dp gutters, radius 20, as tall as the room left under the tiles (96–150; left out under 96, as on a 360 × 640 phone), the spare room split above and below it: two teal wave layers, the `ONBOARDING_BANNER_*` picture (2:1) on the left, the Dancing Script line (22, −8°) on the right with a coral underline and a coral arrow towards the food. Read as one line |
| Bottom | 8 px page dots (the current one a 26 px ink pill) 12 above the full-width ink "Next →" (54, stadium, Inter 700, 18, 16 dp gutters), 12 below it plus the safe area; they never move. The last page says "Get started →" |

Copy per page: tiles Morning catch / Kept chilled / Delivered fresh, Your cut / Cleaned by hand / Pay net weight,
Pick a slot / Cold box / Cash or UPI; banner lines "From / today's catch / to your kitchen", "Ready / for your
pan", "From our store / to your door".

Tests (`test/widgets/onboarding_screen_test.dart`) cover the copy, the pictures and the fallbacks, semantics,
Next / Get started, Skip, reduced motion, no overflow and no vertical scroll at 360 × 640 and 390 × 844 with text
scale 1.0 and 1.3, the block and sheet at the full screen width, the sticker below the circle's centre and the
circle's size, centre and overlap.

Slot changes since §14: the home uses `HOME_HERO_CATCH`, `HOME_CARD_CLEANED` and `HOME_CARD_RIDER` (§16);
`HOME_HERO` appears only on the splash; the four `FEATURE_*` slots are no longer on Home (the `FeatureCard`
widget stays).

### Rules kept

- **Server values only (rule 4).** Card prices, line totals, the bill, the View cart amount (cart
  `subtotalPaise`), the Checkout amount (`estimatedTotalPaise`) and the Place order amount
  (`expectedTotalPaise`) are server values, only formatted. The app shows no "% off": the API sends no
  discount percentage, and working one out would be a client calculation.
- **ADD on a card** (`CartController.quickAdd`) reads the product detail and adds the first priced, orderable
  size with the server's default orderable preparation and the server's `minOrder`. When nothing fits, it
  opens the product page. The snack bar offers "Change cut". The card's − / + (`stepLine`) reads the product's
  limits for that size and cut first (cart lines carry none). Below the minimum, − removes the line.
- **Language** is stored (`fo.language`) but the app is English only for now; choosing తెలుగు or हिं says so.
- `DEMO=true` works as before; the Demo panel stays at the top of Account.

## 16. Rich home on one grid (D176, D181, D182)

`lib/features/catalog/home_screen.dart` follows `docs/design/mockups/app-home-rich.html` (drawn at 390 × 873;
its CSS is the source of truth). Every size below is in dp at a 390 width.

**Grid.** 16 dp gutters (`FreshLayout.gutter`), 12 dp gaps (`FreshLayout.cardGap`), the two cards are two equal
`FreshColumns` with `stretch`, the categories are six equal columns, and the Today's special row starts on the
gutter and scrolls sideways. The cart bar and the menu sit in the shell's bottom bar slot, so they never cover
content.

| Part | What it is |
|---|---|
| Delivery header | Pin, "Delivering to <pincode> ⌄" and the server's city name (`serviceability.cityName`), then 46 dp search and Saved circles. Tap: `/location?change=1`. Key `homeDeliveryTo` |
| Greeting and heading | "Good morning, <name>" (no name when signed out), then Baloo 2 800 32 "What's cooking *today?*" with "today?" on the yellow brush and coral sparks (one header for screen readers), then a coral Dancing Script line (19, −3°) with a swoosh |
| Harbour | `HarbourScene` from `fresh_theme` at the top right (x 168–390), soft palms at the left and right edges (`PalmsPainter`). Decoration only |
| Hero | Radius 24, `#075E54`, 194 tall: the next slot chip (server `nextSlot`, hidden when there is none), "Today's catch / is in!" (Baloo 29) with a yellow spark, body 12.5, and the "Shop fresh catch →" pill. The `HOME_HERO_CATCH` picture is anchored bottom-right at 236 / 358 of the width, natural 3:2 aspect, no wavy edge, no fade. Tap: `/shop`. Key `homeHero` |
| Two cards | Radius 20, 122 tall. Cleaned your way (`#F59B7C`, deep coral pill text, `HOME_CARD_CLEANED` 84 × 84 bottom-right) → `/shop`. Kept cold till your door (`#F7C66B`, teal pill text, `HOME_CARD_RIDER` 100 × 100 at the top-right, popping 16 above the card) → a sheet with the server's delivery terms (service area, next slot, fee, free above, minimum order). One label each |
| Categories | The first five server categories in 50 dp tinted circles (picture or initial) and More → `/shop`. Keys `homeCategory-<slug>`, `homeCategoryMore` |
| Today's special | Shown only when the server sends banners: "Today's *special*" with "See all →", then 200 × 96 cards (radius 16, picture 54 tall, title, subtitle). The chevron and the button role appear only when the banner links somewhere (`linkType` CATEGORY → `/shop?category=`, PRODUCT → `/product/<slug>`) |
| Cart bar and menu | The restyled shell `FreshCartBar` (top 746, 52 tall) and floating `FoodTabBar` (58 tall, radius 22), §15 |

**Fits one screen (D182).** At a normal text size the page is laid out at its natural height inside
`FreshFitWidth` (key `homeFit`): if it is taller than the screen it is laid out wider and scaled down, so it
always spans the full width (a `FittedBox` would narrow it and leave side gaps). No scroll view, no bounce, no
pull-to-refresh. The two cards match heights with `FreshColumns(stretch: true)`, which lays them out twice
instead of using intrinsic sizes. Only at a
system text scale of 1.5 or more does it scroll (`SingleChildScrollView`, key `homeScroll`,
`ClampingScrollPhysics`). The sideways specials row uses `ClampingScrollPhysics` too. "Popular today" moved off
Home (it lives in Shop).

**Pictures.** The owner's hero, cleaned-cut and rider pictures are bundled in `assets/illustrations/` (resized
with `sips`: hero 1024 wide, cards 512, alpha kept). The package manifest keeps all three home slots PLACEHOLDER until
`import` + `optimize` run. Until a picture is bundled a stand-in shows (`ONBOARDING_FRESH` in the hero,
`ONBOARDING_BANNER_CLEANED` and `ONBOARDING_TILE_COLD_BOX` in the cards), never a broken image.

**Shared parts.** The brush, sparks, script line, swoosh, snowflake, harbour, palms, birds, trawler and the
layout numbers come from `packages/flutter/fresh_theme` (README there); onboarding uses the same copies.

**Semantics.** Harbour, palms, sparks, swooshes and pictures are excluded; the hero, cards, categories, offers
and header are labelled buttons. No animation.

**Tests** (`test/widgets/home_screen_test.dart`, `test/widgets/fresh_theme_parts_test.dart`): header, city,
greeting, chip; gutter, equal cards, 12 dp gap and equal category columns; no vertical scroll and no
RefreshIndicator at 390 × 844 and text scale 1.0; clamped scrolling at 1.5; no overflow at 360 × 640 and
390 × 844 with text scale 1.0 and 1.3; offer routes and a non-link offer; specials hidden without banners;
category tap; delivery sheet; labels; stand-ins and bundled pictures.

**API gaps (rule 4).** Banners have no badge field (the sticker shows only if one is added); the city response
has no state name (only the city is shown); there is no catch time or "kept cold" fact from the server, so the
card copy is fixed text and the sheet shows only server delivery terms.

## 17. Every screen in the home theme (D181)

Every customer screen after the home and onboarding follows the home's look. The rules:

- **One grid.** 16 dp gutters (`FreshLayout.gutter`), 12 dp gaps (`cardGap`), 24 dp between sections. Lists
  pad with `FreshLayout.list`. Small cards, tiles and section boxes use radius 20 (`cardRadius`, D189); fields
  and option tiles 16 (`tileRadius`).
- **Header.** Each inner screen's `Scaffold.appBar` is `PageHeader` (64 dp): a round paper back button
  (`pageBack`; pops, else goes home), the title with its key word on the yellow brush and coral sparks
  ("Shop *fresh*", "Your *cart*", "Almost *there*", "My *orders*", "My *account*", "Saved *fish*"), and round
  paper action buttons with an optional coral count. Section titles inside a page use `PageTitle`.
- **Pages stand still (D182).** `FreshScrollBehavior` on the app makes every scroll clamp; no bounce.
- **Fit by layout (D183, D195).** Content fits through the grid and `FreshFitWidth`, never `FittedBox`.
- **Pictures are 3:2 (D193).** Product cards and the product hero use `FreshPictureFrame` / `pictureAspect`.
- **Parts come from `fresh_theme`.** Every reusable part lives in the package (owner rule, its README lists
  them); screens compose them. No orbit halo, moving dot, ticker, Instrument Serif or Bricolage.

**Connected flows.** Browse → product → cart → OTP sign-in → address and pincode → slot → cash on delivery →
order detail → account all link up; no button is dead.

| Screen | Server data it now uses |
|---|---|
| Product | `GET /price-quote` for the chosen size, cut and quantity: "Estimated ₹… for 500 g" and "You pay at most ₹… (up to …)" (`priceQuote`); hidden when the server gives no quote. A sold-out size shows **Notify me** (`notifyMe`): signed out it opens sign-in, signed in it sends `POST /me/stock-alerts` with the variant and the store from `/serviceability` (`fulfillmentLocation.id`); "Not sold in your area" stays disabled |
| Cart | **View offers** (`viewOffers`) opens the coupons the customer may use in the pincode's city (`GET /coupons`, pincode as `X-Pincode`), each with Apply (`PUT /cart/coupon`). "Change cut ›" re-cuts a line (`PATCH /cart/items/{id}`). The shell's cart bar shows `totals.estimatedTotalPaise` |
| Address form | A new address starts with the shopping pincode; the city and the state code the server knows for a pincode (`/serviceability` `city.name`, `city.stateCode` when sent) fill only empty fields |
| Help and support | Call and e-mail rows (and support hours) from `GET /business-profile`; a note when it is not set up (404 `BUSINESS_PROFILE_NOT_CONFIGURED`) |
| Order detail | Report a problem sends JSON (`POST /orders/{id}/complaints`), then each picked photo to `/complaints/{cid}/photos` (field `file`, up to 4, `image_picker`, resized to 2048 px). Proposed replacements (`substitutions`) show Accept / No thanks (`POST …/substitutions/{sid}/accept|reject`), with the server's price difference |
| Saved fish | Reads the nested wishlist shape `{productId, addedAt, product: {…}}`; products no longer sold say so |

The demo build refuses price quotes (no price engine; the page shows the unit price only), lists one sample
coupon that it does not apply, and answers stock alerts and the business profile with sample values.

## 18. Requests the server accepts (API contract audit, D235–D241)

Every call the app sends was checked against the server's validators and the Render demo
(`docs/research/mobile-api-audit-2026-10.md`). What the app does so no screen meets a refusal it cannot get out of:

| Where | Behaviour |
|---|---|
| Search | A search is sent only when it has 2 to 60 characters after trimming and at least one letter or digit (CAT-R12, `core/search_text.dart`); otherwise the "Search the catch" hint stays and neither `/products` nor `/search/suggest` is called. The field stops at 60 characters (D235) |
| Any catalogue error | `PINCODE_NOT_SERVICEABLE` or `LOCATION_REQUIRED` (a stored pincode that is no longer served, or none) shows "Choose where to deliver" with **Change pincode** (opens `/location?change=1`) beside Try again (D236) |
| Clear cart | `DELETE /cart` answered 404 `CART_NOT_FOUND` (the cart was ordered or expired) reads as the empty cart (D237) |
| Home header | The city line is "City, State" when `/serviceability` sends `city.stateName` (D209), else the city alone (D238) |
| Home "Kept cold" card | Says "Kept cold till your door" and the delivery sheet "Packed on ice in a cold box." only when `fulfillmentLocation.hasColdStorage` is true; otherwise "Slots to your door", "Fee and minimum order." (D212, D239) |
| Product page | "Caught Fri, 23 Oct" from the chosen size's `freshness.caughtOn` (CAT-R25); nothing when the server sends none (D240) |
| Orders list | `GET /orders` sends `page` and `pageSize` only; the list comes newest first from the server (D241) |
