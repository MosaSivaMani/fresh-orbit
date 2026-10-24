# Next steps (handoff)

Last updated: 2026-10-24, after the manager merged security round 2 (Track A, `security/round-2`), the sample-data loaders (L1, `backend/sample-loaders`) and the illustrated design system (U1) to `main`: `dotnet test` 999/999, all `pnpm` checks pass, no pending model changes. Start here when work resumes.

## 0. Owner rules since 2026-10-24 (read first)
- Nothing runs on the dev Mac (company laptop; owner: "dont run anything here"): edit and git only. Verification is GitHub CI (push a branch, open a draft PR, `gh pr checks`); designs are shown as artifacts.
- Hosting (D127), owner's choice 2026-10-24: testing now with Option A (Codespaces + demo APK) and Option B (free Render + Neon demo link); later Option C (retry Oracle Always Free) and Option D (Hostinger KVM 1/2 at launch).
- Hosting detail (D127): Oracle sign-up failed; test with Codespaces and the demo APK, rent Hostinger KVM 2 only near launch. Earlier note: Oracle sign-up hit "Forbidden – number of requests exceeded"; retry later from a personal device and network, or fall back to Hostinger KVM 2. First deploy also needs DLT approval (API refuses to start without the SMS values).
- CI uses SDK 10.0.102 with `rollForward: latestPatch`, so it can pick up a newer analyser than the laptop: CA1873 (log arguments) broke main's Release build; fixed in SmsSenders, HostRunner, MigrationRunner and DltSms.

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
| (none) | Track A, L1 and U1 merged to `main` on 2026-10-24 | |
| `backend/deploy-gaps-ops` | E2E-3, E2E-5, E2E-6 and E2E-7 (§9). No schema change, no new package | Manager review; decisions to number from D94 |

## 3. Next, now that Phase 3 is merged
1. ~~Merge `integration/sec-sample`~~ Merged.
2. ~~Check that a deploy runs migrations and the reference loader end to end.~~ Done on branch `infra/e2e-check-work`: `infra/scripts/e2e-check.sh` (`runbooks/e2e-check.md`), smoke 28/28 (Production) and 52/52 (Testing); open items E2E-1 to E2E-7 in `research/e2e-deploy-check-2026-10.md` §3.
3. ~~Write the sample-data loaders~~ Merged to `main` (L1; D78–D80). Load with `--sample`, remove with `--purge-sample` (`runbooks/local-development.md` §2); the dev database has the sample store loaded.
4. Regenerate `@fresh/api-client` and `fresh_api` from the full OpenAPI. Replace the TEMPORARY types in web (`apps/web/src/api/types`), admin (16 files, `design/admin.md`) and mobile.
5. Close the API gaps listed in `design/web.md` §6 (W1–W10), `design/admin.md` §5 (G1–G12) and `design/mobile.md` §8.
6. Publish clickable demo links for the website and admin running on sample data.

## 4. Illustrated UI redesign (D53): U1 and U4 merged, U2 (web) and U3 (admin) next; 9 of 32 owner finals imported
The owner asked for a more modern UI with AI-generated 3D cartoon characters on every page and state. They shared screenshots from a friend's food app as reference only; nothing is to be copied. Two engineers were briefed and then stopped before writing any code. Relaunch them from these briefs:
- **U1, design system** (start first): package `packages/illustrations`.
  - Package contents: `manifest.json` with the 32 slots below (D85), a style prompt and a character sheet, our own placeholder SVGs, a `validate` script and a `sync-mobile` script.
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
Merged to `main` (D64–D77; status table in `security/review-2026-10.md` §7): D33 lockout and the staff login limit per email + IP, D34 address anonymisation, D35 `tax.manage`, SEC-5, SEC-6, SEC-7, and the round's migration `SecurityRound2`. Still open:
- Admin app: tax-rules gating by `tax.manage`, 429 on login, drop the `ACCOUNT_LOCKED`/`ACCOUNT_DISABLED` messages (`design/admin.md` row 5).
- A break-glass reset for a locked or forgotten last admin (`--create-admin` is refused while it exists; SQL steps in `runbooks/security-configuration.md` §5 until then).
- The reference loader does not add new permissions to existing system roles, so an already-seeded FINANCE role lacks `tax.manage` until an admin adds it.
- SEC-3 remainder: customer and admin limits (a per-user limit for signed-in cart use, D62), global per-IP limit, a limit counting only rejected webhooks per IP (SEC-6).
- SEC-5 guard for complaint photos when Ordering adds them.
- Purge job for `identity.staff_login_failures` (with SEC-13) and for order/invoice snapshots after 72 months (CA to confirm the start).
- Audit: `actorName` is null; move the central action table onto each module's endpoints with `.WithAuditAction`.
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
- Decision numbers: D42–D52 are Phase 3 (from session `fresh-orbit-3a`); the illustrated UI decision was renumbered from a clashing D42 to **D53**; D54–D61 are sample data S-D1–S-D8 (all accepted); D62 is the Phase 3 rate limits; D63 is the OpenAPI document and generated front-end types (F1); D64–D77 security round 2 (Track A); D78–D80 sample loaders (L1, D80 proposed); D81–D87 illustrated design system (U1); D88–D89 reserved for the app look the owner is choosing; D90–D93 admin Track C. D94 typography experiment withdrawn. D95 website look; D96–D106 production blockers (Track D). D107–D111 mobile Bold blocks. D112 legal policy drafts (proposed). D113–D118 food-app website (B1). D119 brand logo. D120–D125 website pages (W2). D126 microsecond times (PR #7). D127 hosting without Oracle. D128–D155 Track E API gaps (PR #6). D156 rich onboarding. D157–D165 Render demo (PR #10); D166–D169 owner testing 2 (PR #9). D170 brand name "Fresh Orbitt"; D171 chicken and later vegetables. D172–D175 backend follow-ups (PR #12). D176 rich home and shared highlights. The next free number is **D177**.
- Open: `sync-mobile` writes `lib/core/illustrations/illustration_slots.g.dart` but the app's widget is `apps/mobile/lib/illustrations/` (D87); align before using it. After an `import`, run `pnpm --filter @fresh/illustrations placeholders` so SCENE placeholders match. Regenerate API types after the merge (413, 429 on staff login).
- Open with the owner: the wording of CLAUDE.md rule 2 for D30 (a Domain project may reference its own Public project).

## 8. Open items from Phase 3 integration (session `fresh-orbit-3a`)
- Not built yet: wishlist with prices, `/home` banners from Promotions, `isWishlisted` on products.
- `QuantityRules` exists in both Catalog and Cart; move to SharedKernel next to `LinePricing` when convenient.
- Invoices need a saved business profile with GSTIN (D42). CA to confirm whether exempt fresh fish needs a bill of supply instead; until a GSTIN is saved, `ShipmentDelivered` retries and then dead-letters in Ordering.
- Test hygiene: integration tests share one database per test assembly; list assertions must filter (e.g. `q=orderNumber`), never assume page 1 (fixed in `OrderLifecycleTests` at merge).
- Old agent worktrees under `.claude/worktrees/` (Phase 2/3/5 branches, all merged) can be removed with `git worktree remove` once nobody needs them.
- No agents of `fresh-orbit-3a` are running; nothing is in flight.

## 9. Open items from the end-to-end deploy check (`runbooks/e2e-check.md`)
- E2E-1 Notifications: done on `backend/deploy-gaps-sms` (DLT sender + Fast2SMS adapter; failed send → 503 `SMS_PROVIDER_UNAVAILABLE`; all 13 SMS templates need a DLT template id at start-up). Owner: DLT registration in `runbooks/security-configuration.md` §2.1. Decisions proposed, not yet numbered.
- E2E-2 Api: done on `security/round-2` (`HostRunner.RunFreshOrbitAsync`: Api and Worker log one Critical line and exit 1).
- E2E-3 Data Protection: done on `backend/deploy-gaps-ops`. Nothing protects data today (JWT only). The Api keeps its key ring on the `dataprotection-keys` volume (`DataProtection:KeysPath`, required outside Development and Testing, `SetApplicationName("FreshOrbit")`). The keys are not encrypted and not backed up; that is to be reviewed when a feature starts using Data Protection (`research/deploy-gaps-ops-2026-10.md` §1).
- E2E-4 Identity: done on `security/round-2` (staff loads use split queries).
- E2E-5 EF Error on an empty database: done on `backend/deploy-gaps-ops`. The Migrations host creates the history table first, with EF's own idempotent script. No log filter.
- E2E-6 web footer cities: done on `backend/deploy-gaps-ops`. Cached for 5 minutes instead of 1 hour (fetch and root layout `revalidate`).
- E2E-7 `deploy.sh`: done on `backend/deploy-gaps-ops`. The script is portable to bash 3.2 without `flock` (mkdir lock fallback, shell check) and was run on the Mac against `fo-e2e` (`runbooks/e2e-check.md` §5). On Linux it behaves exactly as before. It has still not run on a real server: do that on the first staging deploy.
