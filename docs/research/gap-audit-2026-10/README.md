# Gap audit, October 2026 (every screen, production readiness)

Owner, 2026-10-29: "find what we missed in every screen … every small one also … and missing features and
missing details." Eight read-only audits of `main` at `5e0907d`, one per area, each with a gaps table (Blocker /
High / Medium / Low, with `path:line` evidence) and a "Missing features and details" table (Must / Should / Nice).
Nothing was built or run for the audits (company laptop, CLAUDE.md rule 18).

| Area | File | Blocker | High | Medium | Low | Gaps | Must | Should | Nice |
|---|---|---|---|---|---|---|---|---|---|
| App: splash, onboarding, pincode, sign-in | [mobile-entry.md](mobile-entry.md) | 1 | 7 | 15 | 39 | 62 | 3 | 13 | 4 |
| App: home, shop, search, product, wishlist | [mobile-browse.md](mobile-browse.md) | 0 | 9 | 25 | 36 | 70 | 10 | 14 | 9 |
| App: cart, checkout, orders, tracking, complaints | [mobile-buy.md](mobile-buy.md) | 0 | 5 | 14 | 40 | 59 | 3 | 11 | 16 |
| App: account, settings, help, store release | [mobile-account-app.md](mobile-account-app.md) | 3 | 6 | 15 | 37 | 61 | 3 | 13 | 6 |
| Customer website | [web.md](web.md) | 3 | 7 | 18 | 31 | 59 | 7 | 13 | 10 |
| Admin dashboard | [admin.md](admin.md) | 0 | 9 | 24 | 52 | 85 | 9 | 17 | 9 |
| Backend | [backend.md](backend.md) | 2 | 6 | 30 | 51 | 89 | 6 | 13 | 3 |
| Launch: infra, CI, legal, compliance | [launch.md](launch.md) | 10 | 23 | 26 | 37 | 96 | 20 | 14 | 6 |
| **Total** | | **19** | **72** | **167** | **323** | **581** | **61** | **108** | **63** |

Some items appear in more than one audit (privacy and terms: ME-1, MA-3, W-1, L-8; GSTIN: BE-1, L-4; silent
e-mail and push fall-backs: BE-3/BE-4, L-19/L-20; release signing: MA-1, L-21), so the distinct count is lower.

## Blockers and who fixes them

| Blocker | Fix | Status |
|---|---|---|
| BE-1 / L-4 GSTIN found only at delivery | Refuse checkout and fail readiness until the business profile is complete | Engineer, branch `backend/launch-blockers` (D295–D296) |
| BE-2 Outbox has no backoff | Backoff with `next_attempt_at`, dead-letter alert, admin replay | Same branch |
| MA-1 / L-21 Debug-signed release | Release signing from key.properties or CI secrets; one stable demo key (MA-15) | Engineer, branch `mobile/launch-blockers` (D297–D299); owner adds the keystore |
| MA-2 Default Flutter icons | Icons from the brand logo | Same branch |
| MA-3 / ME-1 / L-25 No privacy or terms in the app | Links to the website's policy pages | Same branch |
| W-2 / L-6 / L-7 No legal details or grievance officer on the website | Footer from the server's business profile; grievance block | Engineer, branch `web/legal-blockers` (D300–D301) |
| W-3 / L-26 No consent at sign-in | Consent line with links | Same branch (website); app in `mobile/launch-blockers` |
| W-1 / L-8 / L-9 Policy texts are outlines | Owner's text, lawyer review | **Owner** |
| L-1 Domain · L-2 production server · L-10 offsite backups | Buy domain, rent server, create R2/B2 bucket | **Owner** |
| L-3 SMS DLT registration | Register sender, templates and link domains on DLT | **Owner** |
| L-5 FSSAI licence | Licence number into the business profile | **Owner** |

## Order of work after the blockers

1. High items per area, one engineer per area, after that area's blocker branch merges (to avoid conflicts);
   admin first, since it has no blocker branch.
2. Must features, after the owner picks from the tables.
3. Medium, then Low items, grouped per screen.

Owner decisions asked: MB-16 (home text scale), MB-20 (pull-to-refresh), MB-17 / MB-18 (hard-coded claims),
GitHub branch protection and secret scanning (L-14, L-15).
