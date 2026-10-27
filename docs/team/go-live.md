# Go-live plan: Fresh Orbitt (checked 2026-10-27)

> **On hold (owner, 2026-10-27, D208):** the next few days are product development only. Licences, DLT, domain,
> server and Play account wait; the owner tests on their own devices with the live demo APK and the Render demo.

The owner wants to go live in the coming days. This page lists what is ready, what blocks a real launch, and the
fastest safe order. Decisions behind it: project plan §6 Phase 10 and §7, D127 (Hostinger near launch), COD launch.

## 1. Where the product stands

| Part | State |
|---|---|
| API, database, admin | Built; demo live on Render + Neon (free, sleeps, sample data only — not for real customers) |
| Customer buying flow | Works end to end on the demo (QA audit 2026-10-27: browse → cart → OTP → address → coupon → COD order) |
| Website | Sign-in fixed (#18). New look in progress: home (#20), every other page (web/screens-theme) |
| Mobile app | Home + onboarding done (#16). Other screens restyled and wired (PR #21), onboarding/home fixes in progress |
| Admin | Track E API adopted (#17); new look in progress (admin/screens-theme) |
| Known gaps before launch | Complaint send in the app (415), substitutions accept/reject, developer notes on support/policy pages — all assigned |

## 2. What blocks a real launch (owner items, outside the code)

| # | Item | Why it blocks | Typical time |
|---|---|---|---|
| 1 | Domain (e.g. freshorbitt.in) | Website address, SMS sender links, Play listing | Same day |
| 2 | Hostinger VPS KVM 2 (India) | Render free tier sleeps and is not for real orders; Neon demo password was shared in chat — never reuse it | Same day; we deploy with `runbooks/deploy.md` |
| 3 | SMS provider + DLT registration (entity, sender id, OTP and order templates) | Without it no customer can sign in (OTP). Code is ready (Fast2SMS adapter, 13 templates need DLT ids) | Several working days; needs PAN/GST or business documents |
| 4 | FSSAI registration for the fulfilment location | Legal requirement to sell food | Days to weeks |
| 5 | GSTIN and CA check of GST rates and invoice format | Invoices at delivery | Depends on registration |
| 6 | Real data: city, location, pincodes, products, photos, prices, stock, slots | Missing price = not for sale (rule 7) | 1–2 days of admin entry |
| 7 | Policy texts (delivery, cancellation, refund, privacy, terms) approved by the owner | Drafts exist in `docs/legal/`; pages must not show drafts | Owner review |
| 8 | Support phone and email | Shown on support pages and in the app (from business profile) | Same day |
| 9 | Google Play developer account | App on the Play Store. A new **personal** account must run a closed test with at least 12 testers for 14 days before production; an **organisation** account (needs a D-U-N-S number) does not | Personal: ≥ 2 weeks. Until then the app is shared as an APK |
| 10 | Two staff logins (ADMIN + FINANCE) | Refund approval needs two people | Same day |

## 3. Fastest safe order

1. **Now (code):** merge the open UI and fix PRs when green; finish the QA fixes; remove sample-only shortcuts from
   the production configuration (sample data is refused in Production, rule 16).
2. **Day 1 (owner):** buy the domain and the Hostinger VPS; start DLT and FSSAI registration (longest waits);
   create the Play developer account and start the closed test.
3. **Day 1–2 (us):** deploy production to Hostinger with backups (`runbooks/deploy.md`, `runbooks/backup-restore.md`),
   Cloudflare in front, real secrets, smoke test (`runbooks/e2e-check.md`).
4. **Day 2–3 (owner + admin):** enter real catalogue, prices, stock, slots, pincodes, policies, support contacts.
5. **Pilot:** friends and family order with COD on the website and the APK for one to two weeks; fix issues.
6. **Launch:** once DLT SMS is live (real OTP), FSSAI is in hand and the pilot is clean. Play Store release follows
   the 14-day closed test if the account is personal.

**Realistic date:** the website and APK can be pilot-ready within days of the domain, server and DLT approval. DLT
approval and FSSAI are the dates we cannot control, so start them first.
