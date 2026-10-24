# Next steps (handoff)

Last updated: 2026-10-24, after I1 integrated `fix/security-high` and the sample data on `integration/sec-sample` (waiting for the manager to merge it to `main`). Start here when work resumes.

## 1. State of `main` (434a8dd and later)
| Part | Status |
|---|---|
| Backend modules | All on `main`, including Cart, Promotions and Ordering: Phase 3 merged by session `fresh-orbit-3a` at 4c3b36e. Initial EF migration `InitialCreate` in `FreshOrbit.Migrations/Persistence/Migrations`. On `integration/sec-sample` (not yet on `main`): the security fixes and sample data; build 0 warnings, `dotnet test` 926/926, smoke 52/52 |
| Customer website `apps/web` | Merged (Phase 7). Uses TEMPORARY API types until Phase 3 lands |
| Admin dashboard `apps/admin` | Merged (Phase 6). Home banners screen not built (needs Promotions) |
| Mobile app `apps/mobile` (Flutter) | Merged (Phase 8). Not yet run on a device; no Android SDK or full Xcode on the dev Mac |
| Infrastructure `infra/` | Merged (Phase 9), including web and admin images. Not yet run on a real server |
| Security review | `security/review-2026-10.md`; SEC-1 and SEC-2 fixed and SEC-3 partly fixed on `integration/sec-sample` (D36, D62); remainder in §5 |
| Front-end checks | `pnpm build/typecheck/lint/test/format:check` pass on `main`; `flutter analyze/test` pass |

## 2. Branches waiting to merge (in this order)
| Branch | What | Waiting for |
|---|---|---|
| `integration/sec-sample` | `fix/security-high` (SEC-1, SEC-2, part of SEC-3; D36) and the sample data branch `worktree-agent-a68eae478be810d8f` (`backend/data/sample`, `docs/content/`, CI validator; D54–D61), adapted to Phase 3: old `RateLimiting:Otp` and `login` names gone, `coupon` limit on `PUT /cart/coupon`, `public` limit on `/cart*` and `/promotions` (D62), smoke script reads OTPs from `GET /api/v1/dev/sms` | Manager review and merge to `main`. Deployed configs must rename `RateLimiting:Otp` to `OtpRequest`/`OtpVerify` and set `Notifications:Sms:Provider` (`runbooks/security-configuration.md`) |

## 3. Next, now that Phase 3 is merged
1. Merge `integration/sec-sample` (§2) to `main`; `dotnet test` 926 tests expected.
2. ~~Check that a deploy runs migrations and the reference loader end to end.~~ Done on branch `infra/e2e-check-work`: `infra/scripts/e2e-check.sh` (`runbooks/e2e-check.md`), smoke 28/28 (Production) and 52/52 (Testing); open items E2E-1 to E2E-7 in `research/e2e-deploy-check-2026-10.md` §3.
3. Write the sample-data loaders (`product/sample-data.md`), then run the stack with sample data.
4. Regenerate `@fresh/api-client` and `fresh_api` from the full OpenAPI. Replace the TEMPORARY types in web (`apps/web/src/api/types`), admin (16 files, `design/admin.md`) and mobile.
5. Close the API gaps listed in `design/web.md` §6 (W1–W10), `design/admin.md` §5 (G1–G12) and `design/mobile.md` §8.
6. Publish clickable demo links for the website and admin running on sample data.

## 4. Illustrated UI redesign (D53), not started
The owner asked for a more modern UI with AI-generated 3D cartoon characters on every page and state. They shared screenshots from a friend's food app as reference only; nothing is to be copied. Two engineers were briefed and then stopped before writing any code. Relaunch them from these briefs:
- **U1, design system** (start first): package `packages/illustrations`.
  - Package contents: `manifest.json` with the 33 slots below, a style prompt and a character sheet, our own placeholder SVGs, a `validate` script and a `sync-mobile` script.
  - Tokens: sand surfaces, a coral sticker accent, an ink outline, larger radii, a sticker shadow, and Baloo 2 as the display face.
  - New `@fresh/ui` components: `Illustration`, `EmptyState`, `FeatureCard`, `PolaroidCard`, `Sticker`, `HelpHero` and `ReceiptCard` (shows server values only).
  - Also: a modernised look for the existing components, an "Illustrated" page in the playground, and `design/illustrations.md`.
- **U4, mobile** (in parallel with U1): an app-side theme refresh, with Baloo 2 and Inter bundled.
  - An `Illustration` widget with a placeholder fallback, plus the same set of widgets.
  - Every screen uses its slot.
  - Optional: a demo-mode Flutter web build.
- **U2, web** and **U3, admin** (after U1 merges): apply the components to every page. Admin uses illustrations only on login, empty, "all caught up" and no-access states.
- **The owner** generates the final images from the prompts in the manifest, using a tool whose licence allows commercial use.

Slots:
- Onboarding and location: ONBOARDING_FRESH, ONBOARDING_CLEANED, ONBOARDING_DELIVERY, LOCATION_PICK, NOT_SERVICEABLE.
- Empty states: EMPTY_CART, EMPTY_WISHLIST, EMPTY_ORDERS, EMPTY_NOTIFICATIONS, EMPTY_ADDRESSES, NO_SEARCH_RESULTS, OUT_OF_STOCK.
- Login and orders: LOGIN_OTP, ORDER_PLACED, ORDER_PACKING, ORDER_ON_THE_WAY, ORDER_DELIVERED, ORDER_CANCELLED, COMPLAINT_RECEIVED.
- Help and errors: HELP_SUPPORT, ERROR_GENERIC, OFFLINE, NOT_FOUND.
- Home: HOME_HERO, FEATURE_HONEST_WEIGHT, FEATURE_CLEANED_YOUR_WAY, FEATURE_COLD_CHAIN, FEATURE_SAME_DAY.
- Admin: ADMIN_LOGIN, ADMIN_EMPTY, ADMIN_ALL_CAUGHT_UP, ADMIN_NO_ACCESS.

## 5. Open security work (after the merges)
- D33 time-based staff lockout and `--create-admin`.
- D34 address anonymisation.
- D35 `tax.manage` permission.
- SEC-3 remainder: staff login limit per email + IP, customer and admin limits (a per-user limit for signed-in cart use, D62), global per-IP limit.
- SEC-5 image dimension guard.
- SEC-6 webhook body cap.
- SEC-7 audit writes.
- Admin refresh tokens in httpOnly cookies (D40 follow-up).

## 6. Needed from the owner
- Domain and launch city.
- GSTIN, FSSAI and legal name.
- An Oracle Cloud account.
- GitHub deploy secrets and variables (`runbooks/deploy.md`).
- An SMS provider registered on DLT.
- An offsite backup bucket (R2 or B2).
- A Google Play account, an Apple developer account and a Firebase project.
- Product photos and the AI character images.
- CA confirmation of GST rates.
- A check of the fish names, yields and pincode groupings in the sample data.

## 7. Notes
- **Company laptop (CLAUDE.md rule 18):** install nothing; the owner generates character images in ChatGPT or Gemini from the prompt pack (style and cast in `design/illustrations.md` once U1 lands).
- Decision numbers: D42–D52 are Phase 3 (from session `fresh-orbit-3a`); the illustrated UI decision was renumbered from a clashing D42 to **D53**; D54–D61 are sample data S-D1–S-D8 (all accepted); D62 is the Phase 3 rate limits; D63 is the OpenAPI document and generated front-end types (F1). The next free number is **D64**.
- Open with the owner: the wording of CLAUDE.md rule 2 for D30 (a Domain project may reference its own Public project).

## 8. Open items from Phase 3 integration (session `fresh-orbit-3a`)
- Not built yet: wishlist with prices, `/home` banners from Promotions, `isWishlisted` on products.
- `QuantityRules` exists in both Catalog and Cart; move to SharedKernel next to `LinePricing` when convenient.
- Invoices need a saved business profile with GSTIN (D42). CA to confirm whether exempt fresh fish needs a bill of supply instead; until a GSTIN is saved, `ShipmentDelivered` retries and then dead-letters in Ordering.
- Test hygiene: integration tests share one database per test assembly; list assertions must filter (e.g. `q=orderNumber`), never assume page 1 (fixed in `OrderLifecycleTests` at merge).
- Old agent worktrees under `.claude/worktrees/` (Phase 2/3/5 branches, all merged) can be removed with `git worktree remove` once nobody needs them.
- No agents of `fresh-orbit-3a` are running; nothing is in flight.

## 9. Open items from the end-to-end deploy check (`runbooks/e2e-check.md`)
- E2E-1 Notifications: DLT SMS sender is a stub, so OTP sign-in returns 500 in Production until a provider is chosen and wired.
- E2E-2 Api: a failed options validation exits 139 (unhandled); catch it and exit 1.
- E2E-3 Data Protection keys are not persisted; persist them or confirm unused.
- E2E-4 Identity: staff login query needs `AsSplitQuery` (EF warning).
- E2E-5 EF logs one harmless Error on an empty database.
- E2E-6 web: footer city list is cached 1 hour, so a newly activated city appears late.
- E2E-7 `deploy.sh` itself is not yet run (needs Linux: flock, bash 4); run it on the staging server.
