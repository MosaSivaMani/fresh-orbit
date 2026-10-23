# Next steps (handoff)

Last updated: 2026-10-24, when the owner paused work for the night. Everything on `main` is merged, verified and pushed. Start here when work resumes.

## 1. State of `main` (434a8dd and later)
| Part | Status |
|---|---|
| Backend modules | All on `main` except Cart, Promotions and Ordering, which are on `integration/phase3` (see §2) |
| Customer website `apps/web` | Merged (Phase 7). Uses TEMPORARY API types until Phase 3 lands |
| Admin dashboard `apps/admin` | Merged (Phase 6). Home banners screen not built (needs Promotions) |
| Mobile app `apps/mobile` (Flutter) | Merged (Phase 8). Not yet run on a device; no Android SDK or full Xcode on the dev Mac |
| Infrastructure `infra/` | Merged (Phase 9), including web and admin images. Not yet run on a real server |
| Security review | `security/review-2026-10.md`; the fixes wait on `fix/security-high` (see §2) |
| Front-end checks | `pnpm build/typecheck/lint/test/format:check` pass on `main`; `flutter analyze/test` pass |

## 2. Branches waiting to merge (in this order)
| Branch | What | Waiting for |
|---|---|---|
| `integration/phase3` | Cart, Promotions, Ordering, plus shared changes | Another manager session (`fresh-orbit-3a`) merges it when build, tests and smoke are green. Do not merge `backend/` changes before it |
| `fix/security-high` | SEC-1 (no OTP in logs, DLT SMS stub), SEC-2 (forwarded headers, split OTP limits), part of SEC-3; D36; `runbooks/security-configuration.md` | Phase 3 merge. Then: rename `RateLimiting:Otp` to `OtpRequest`/`OtpVerify` in the Phase 3 test factories, and add `.RequireRateLimiting(RateLimitPolicies.Coupon)` to `PUT /cart/coupon` |
| `worktree-agent-a68eae478be810d8f` | Sample data (`backend/data/sample`), content drafts (`docs/content/`), validator in CI | Phase 3 merge. Log decisions S-D1 to S-D8 from `product/sample-data.md` §7 when merging |

## 3. After the Phase 3 merge
1. Merge the two branches above and run `dotnet test` (608+ tests expected).
2. Create the first EF Core migration. Deploys stop at the migration step until it exists.
3. Write the sample-data loaders (`product/sample-data.md`), then run the stack with sample data.
4. Regenerate `@fresh/api-client` and `fresh_api` from the full OpenAPI. Replace the TEMPORARY types in web (`apps/web/src/api/types`), admin (16 files, `design/admin.md`) and mobile.
5. Close the API gaps listed in `design/web.md` §6 (W1–W10), `design/admin.md` §5 (G1–G12) and `design/mobile.md` §8.
6. Publish clickable demo links for the website and admin running on sample data.

## 4. Illustrated UI redesign (D42), not started
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
- SEC-3 remainder: staff login limit per email + IP, customer and admin limits, global per-IP limit.
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
