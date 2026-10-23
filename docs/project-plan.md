# Fresh Orbit — Project Plan

The single plan for the whole product: what we build, in what order, who builds it, and when each part counts as done.
Detailed specs live in the files linked from each section. Manager rulings are in `docs/decisions/log.md`.

## 1. Product

| Item | Decision |
|---|---|
| Business | Our own seafood store (not a marketplace) selling fresh chilled fish, prawns, crabs, shellfish, cut and cleaned seafood |
| Market | India; one city at launch, more cities later; pincode-based service areas |
| Interfaces | Customer website, customer mobile app (Flutter: one codebase for Android and iOS; Android released first), admin dashboard |
| Backend | One ASP.NET Core modular monolith, one PostgreSQL database, one repository |
| Payments at launch | Cash / UPI on delivery; online gateway adapter built and tested in test mode, switched on after business registration |
| Selling rule | Customers pay for **net** weight after cleaning; actual packed weight charged up to +10%, shortfall refunded or reduced |
| Pricing | Per fulfillment location, tax-inclusive, time-bounded price periods |
| Invoices | GST invoice issued at delivery from final weights |

## 2. Technology

| Layer | Choice | Licence / cost |
|---|---|---|
| Website | Next.js, React, TypeScript, Tailwind | Open-source |
| Admin | React + Vite, TypeScript, Tailwind, TanStack Query/Table | Open-source |
| Mobile | Flutter (Dart), Riverpod, go_router, Dart API client generated from OpenAPI, theme generated from design tokens | Open-source (BSD); store fees paid |
| Shared packages | In this repo (D29): pnpm + Turborepo workspace with `@fresh/config`, `@fresh/design-tokens`, `@fresh/core`, `@fresh/ui`, `@fresh/api-client`; Dart `fresh_api`, `fresh_theme` | Open-source |
| Backend | .NET 10, ASP.NET Core minimal APIs, EF Core 10, FluentValidation, Hangfire, Serilog, Scalar | Open-source |
| Database | PostgreSQL 17 (citext, pg_trgm, btree_gist) | Open-source |
| Files | Local disk via `IFileStorage`; S3-compatible adapter later | Open-source |
| Tests | xUnit v3, Testcontainers, AwesomeAssertions, NetArchTest, Vitest, Playwright | Open-source |
| CI | GitHub Actions | Free tier |
| Runtime | Docker Compose + Caddy (auto HTTPS) | Open-source |

## 3. Hosting

| Stage | Where | Cost |
|---|---|---|
| Development | Developer Mac, Docker Compose | Free |
| Staging + soft launch | Oracle Cloud Always Free (ARM, Mumbai/Hyderabad) | Free, no uptime guarantee |
| Production | Hostinger VPS KVM 2 (8 GB) in India, or Oracle if stable | Paid |
| DNS / protection | Cloudflare free plan | Free tier |
| Offsite backups | Cloudflare R2 or Backblaze B2 free tier | Free tier, limits apply |

Unavoidable costs: domain (yearly), VPS (production), SMS per message, Google Play one-time fee, Apple yearly fee (iOS later), payment gateway fees once online payments go live.

## 4. Team

| Role | Who | Owns |
|---|---|---|
| Manager | Claude main session | Plan, rulings, merges, migrations, shared code, verification |
| E1 | Agent | Identity, Customers, Settings, Audit, Notifications |
| E2 | Agent | Locations, Catalog, Pricing, file storage |
| E3 | Agent | Inventory, Delivery |
| E4 | Agent | Cart, Promotions, Ordering, shared line pricing |
| E5 | Agent | Payments, Reporting |
| Owner | You | Business decisions, accounts, legal registrations, final approval |

Frontend phases reassign the same five engineers (section 6).

## 5. Phases and status

| # | Phase | Status |
|---|---|---|
| 0 | Planning: business, architecture, database, API, module specs | ✅ Done |
| 1 | Backend foundation | ✅ Done (224 tests green) |
| 2 | Backend modules (5 engineers in parallel) | 🔄 In progress |
| 3 | Merge, event wiring, initial migration, full test run | ⏳ Next |
| 4 | Sample data, end-to-end API tests, security review | ⏳ |
| 5 | Design system and shared frontend packages | ⏳ |
| 6 | Admin dashboard | ⏳ |
| 7 | Customer website | ⏳ |
| 8 | Customer mobile app (Flutter, Android + iOS) | ⏳ |
| 9 | Infrastructure, CI/CD, staging on Oracle | ⏳ |
| 10 | Pilot (soft launch) and production launch | ⏳ |
| 11 | After launch: online payments, iOS, second city | ⏳ |

Admin comes before the website because stock, prices and slots must exist before customers can shop.

## 6. Backlog with acceptance criteria

### Phase 0 — Planning ✅
| Deliverable | File |
|---|---|
| Architecture, module map, dependency rules | `docs/architecture/README.md` |
| Database design, transaction boundaries, seed strategy | `docs/database/README.md` |
| API specification, errors, permissions, roles | `docs/api/README.md` |
| Module rules (15 modules + reference data) | `docs/product/*.md` |
| Team plan and rules | `docs/team/plan.md`, `CLAUDE.md` |

### Phase 1 — Backend foundation ✅
Accepted: solution builds with 0 warnings; 70 projects; outbox/inbox, idempotency, If-Match, Problem Details, JWT, permissions, Hangfire worker, migrations host, test fixtures; architecture tests enforce module boundaries.

### Phase 2 — Backend modules 🔄
| Engineer | Done when |
|---|---|
| E1 | OTP login with lockout, refresh rotation with reuse detection, staff login and roles, last-admin protection, addresses, wishlist, number series gapless under concurrency, audit log, notifications queued and sent via dev senders; tests green |
| E2 | Serviceability, city readiness/activation refusal with missing items, catalog visible only with price, search by local name with typos, price periods never overlap, bulk upload all-or-nothing, images in 3 sizes; tests green |
| E3 | Concurrent reservation of last stock — exactly one wins; yield-adjusted reservation; FEFO with exact cost; waste cost; slots generated per city time zone; slot full and cutoff refused; delivery code lock; tests green |
| E4 | Cart priced on every read; coupons with limits; checkout in one transaction with retry; duplicate Idempotency-Key replays; PRICE_CHANGED; pack with overweight and prepaid caps; substitutions; cancel rules; invoices gapless; tests green |
| E5 | COD and Fake providers; duplicate webhook has one effect; invalid signature refused; concurrent refunds never exceed paid; reconciliation issues; report numbers match the seeded dataset; tests green |

### Phase 3 — Integration (manager)
| Task | Done when |
|---|---|
| Merge five branches into `main` | Clean history, conflicts resolved |
| Add event handler adapters between modules | Every event in `docs/product/ordering.md` §4 and payments/delivery events has its consumers wired |
| Register host extras (media serving, create-admin) | Api and Migrations hosts updated |
| Initial EF migration including `DatabaseObjectsSql` | `FreshOrbit.Migrations` creates the full database from empty |
| Full build and test | 0 warnings; every test green |
| Smoke run | API + Worker against dev Postgres; place a COD order through the API by hand |

### Phase 4 — Hardening
| Task | Owner | Done when |
|---|---|---|
| Sample data loader (`--sample`, `--purge-sample`) | E1 | One sample city, location, 3 service areas, ~25 products with photos placeholders, prices, batches, slots, demo customers; refused in Production |
| End-to-end API tests | E2 | Browse → cart → checkout → pack → dispatch → deliver → invoice; cancel → stock/slot/coupon restored; payment webhook duplicate; refund flows; two customers racing for last stock |
| Security review | E3 | OWASP API top 10 checklist, auth/permission matrix test, rate limits, secrets, PII exposure, dependency scan; findings fixed |
| Performance check | E4 | Catalog list and checkout under load (k6, open-source) within targets: p95 catalog < 300 ms, checkout < 800 ms on KVM 2 sizing |
| Docs update | E5 | API docs match the implementation; OpenAPI generated and committed |

### Phase 5 — Shared packages and design system (same repo, D29)
| Task | Done when |
|---|---|
| pnpm 10 workspace + Turborepo at the repo root | `pnpm install`, `pnpm build`, `pnpm typecheck`, `pnpm lint`, `pnpm test` work from the root |
| `@fresh/config` | Shared tsconfig bases, ESLint flat presets (base, react, next), Prettier, Tailwind preset |
| `@fresh/design-tokens` | One JSON source: colours (#073B4C, #0E9384, #F4F8F7, #E9AD55), type scale, spacing, radii, shadows; builds CSS variables, Tailwind theme and Flutter `ThemeData` |
| `@fresh/core` | HTTP client (Bearer, refresh rotation, Problem Details → typed errors, Idempotency-Key and If-Match helpers), TanStack Query setup, display formatting (₹, kg/g, dates in IST); no business calculations |
| `@fresh/ui` | Accessible React components (Radix + Tailwind): Button, Input, Select, Combobox, Checkbox, Radio, Switch, Card, Badge, Dialog, Drawer, Toast, Tabs, Tooltip, Skeleton, EmptyState, ErrorState, Pagination, QuantityStepper, PriceTag, WeightLabel, FormField; app shell (sidebar, top bar); DataTable and FilterBar for admin |
| `@fresh/api-client` | Generated from the backend OpenAPI (openapi-typescript + openapi-fetch); CI fails when stale |
| Flutter packages | `packages/flutter/fresh_api` (openapi-generator dart-dio) and `packages/flutter/fresh_theme` (from tokens) |
| `apps/playground` | Every component with light/dark themes; Vitest + Testing Library tests; axe accessibility checks |
| Design doc | `docs/design/README.md`: tokens, components, wireframe specs for primary screens |

### Phase 6 — Admin dashboard
| Area | Done when |
|---|---|
| Shell, login, permissions, location switcher | Staff see only permitted menus; API enforces |
| Dashboard | Orders, sales, pending, low stock, delivery exceptions from real data |
| Products, categories, variants, preparations, images | Full CRUD with validation errors at fields |
| Pricing | Price periods, bulk upload with dry run |
| Inventory | Receive, adjust, waste, counts, batches, cold-chain log, history |
| Orders | List, detail, pick list, weigh and pack, substitutions, cancel, complaints, refunds |
| Delivery | Slots, capacity, dispatch, assign rider, deliver with code, failed attempts, cash collection, settlements |
| Cities and locations | Cities, locations, service areas, pincodes, hours, closures, readiness and activation |
| Customers, reports, settings | Lists and details, report filters and CSV, staff and roles, business profile, policies, audit log |
| Tests | Playwright on pack, receive stock, price change, city activation |

### Phase 7 — Customer website
| Page | Done when |
|---|---|
| Home | Pincode selector, search, categories, offers, bestsellers from real sales, delivery and trust info |
| Shop / categories / search | Filters, sorting, availability per pincode; server-rendered for SEO |
| Product detail | Variants, preparation, weight selection, estimated price, max chargeable, storage info |
| Cart | Server-priced lines, coupon, delivery fee, availability messages |
| Checkout | Login with OTP, address, slot, COD/UPI on delivery, preview blockers, PRICE_CHANGED handling, success and failure pages |
| Order tracking, account | Status timeline, delivery code, cancel, complaints, reorder, addresses, wishlist, notifications |
| About, support | Policies (delivery, cancellation, refunds), FAQ, contact |
| Quality | Lighthouse ≥ 90 performance/accessibility on mobile; Playwright browse → checkout |

### Phase 8 — Mobile app (Flutter)
| Screen group | Done when |
|---|---|
| Splash, onboarding, location | Pincode stored; unserviceable state handled |
| Home, shop, search, product, wishlist | Same data and rules as website through the generated Dart client `fresh_api` |
| Cart, checkout, tracking, orders | Same server-side pricing; push notifications via Firebase Cloud Messaging (free service, not open-source) |
| Account, help | Login with OTP, addresses, notifications, support |
| Release | Android: Google Play internal testing track. iOS: TestFlight (needs Apple developer account; builds on your Mac with Xcode) |
| Quality | `flutter analyze` clean, widget tests, integration test of browse → checkout on an emulator |

### Phase 9 — Infrastructure and CI/CD
| Task | Done when |
|---|---|
| Dockerfiles (api, worker, migrations, web, admin) | Multi-arch images (ARM64 for Oracle) |
| Production compose + Caddy | HTTPS on domain, admin and API subdomains, `/media` served |
| GitHub repo and Actions | CI on every PR (build, tests, lint, typecheck, API client check, vulnerability scan); deploy workflow |
| Oracle server setup script | Docker, firewall, fail2ban, swap, non-root user, SSH keys only |
| Backups | Nightly pg_dump + files to offsite storage; restore tested |
| Monitoring | Logs and errors visible (Loki/Grafana or GlitchTip); health alerts |

### Phase 10 — Pilot and launch
| Step | Done when |
|---|---|
| Staging on Oracle with sample data | Team tests every journey |
| Real data entry | Real city, location, pincodes, products, photos, prices, stock |
| Pilot | Friends and family orders for 2 weeks; issues fixed |
| Launch | Domain live, SMS live, play store listing, support number active |
| Move to Hostinger (if needed) | Backup restored, DNS switched, smoke test passed |

### Phase 11 — After launch
Online payment gateway live (after business KYC); iOS app; second city via admin (no code change); credit notes; rider app or mobile web for riders; S3-compatible storage; promotions engine beyond coupons.

## 7. Owner checklist (outside the code)

| Item | Needed by |
|---|---|
| Business registration and current bank account | Phase 10 (and gateway KYC) |
| GSTIN; CA confirmation of GST rates, invoice format, discount allocation, cleaning/delivery charge tax | Phase 10 |
| FSSAI licence for each fulfillment location | City activation |
| Domain name | Phase 9 |
| Oracle Cloud account | Phase 9 |
| SMS provider + DLT registration of OTP and order templates | Phase 10 |
| Google Play developer account | Phase 8 release |
| Apple developer account | iOS release (can follow Android) |
| Firebase project (free) for push notifications | Phase 8 |
| Product photos, descriptions, local names, sourcing details | Phase 10 |
| Policies text: delivery, cancellation, refund, privacy, terms | Phase 7 |
| At least two staff who can approve refunds (ADMIN + FINANCE) | Phase 10 |

## 8. Risks

| Risk | Mitigation |
|---|---|
| Oracle free capacity or account reclaim | Same compose on Hostinger; tested backups; move in hours |
| Overselling under load | Row-version reservations, retries, DB checks, concurrency tests |
| Weight disputes | Transparent net-weight rule, cap, shown at product, cart and invoice |
| COD fake orders | OTP login, COD order limit, repeat no-show blocking (later) |
| SMS blocked without DLT | Register templates early; email + push as fallback |
| Tax errors | All rates as data; CA sign-off before launch |
| Merge conflicts from parallel work | Strict file ownership; manager merges; architecture tests |

## 9. Working rules

- Small, verifiable increments; every phase ends with green build and tests verified by the manager.
- Business rules only in the backend; clients render what the API returns.
- No hardcoded prices, availability, slots or analytics; sample data clearly marked and purgeable.
- Every decision recorded in `docs/decisions/log.md`.
