# Legal policies (drafts)

**Status: drafts for owner and lawyer review, 2026-10-24. Nothing here is published, and nothing here is legal
advice.** Each policy is written from what the system actually does (sources are named at the top of each file). The
rules each policy follows, and which points are certain, are in
[`docs/research/legal-policies-2026-10.md`](../research/legal-policies-2026-10.md).

These files replace the earlier drafts in `docs/content/` (`terms.md`, `privacy-policy.md`,
`cancellation-refund-policy.md`, `delivery-policy.md`), which now point here. The website pages under
`apps/web/src/app/support/` still show outlines only; they are to be filled from these files once the owner and a
lawyer have approved them (the web team's job, not done here).

## Files

| File | Policy | Website page today |
|---|---|---|
| [`terms-of-use.md`](terms-of-use.md) | Terms of use | none yet |
| [`privacy-policy.md`](privacy-policy.md) | Privacy policy (DPDP Act 2023) | none yet |
| [`refund-cancellation-policy.md`](refund-cancellation-policy.md) | Refund and cancellation policy | `/support/refund-policy`, `/support/cancellation-policy` (outlines) |
| [`shipping-delivery-policy.md`](shipping-delivery-policy.md) | Shipping and delivery policy | `/support/delivery-policy` (outline) |
| [`grievance-redressal.md`](grievance-redressal.md) | Grievance redressal (Consumer Protection (E-Commerce) Rules 2020) | none yet |
| [`food-safety-fssai.md`](food-safety-fssai.md) | Food safety and FSSAI note | none yet |

## Conventions

- `{{OWNER: …}}` marks a fact or a business decision only the owner can supply. Never replace one with a guess.
- **Check with lawyer:** marks a point a lawyer (or, for food safety, a food-safety consultant; for GST, the CA) must
  confirm before publishing.
- *(setting)* marks a number that comes from the order policy or store settings in the admin. Current defaults:
  over-weight cap 10 %, pay-on-delivery limit ₹5,000, complaint window 24 hours, substitution answer 15 minutes,
  online payment window 15 minutes, customer can cancel until *Confirmed*, 3 delivery attempts
  (`backend/data/reference/settings/order-policies.json`). If the business changes a setting, the published text must
  change too; better, the website should read these values from the API.
- Do not publish a policy while any `{{OWNER: …}}` or **Check with lawyer:** is left in it.

## What the owner must fill in

| Placeholder | Used in |
|---|---|
| `{{OWNER: legal name of the business}}` | all |
| `{{OWNER: type of business, e.g. proprietorship, partnership, LLP or private limited company}}` | terms |
| `{{OWNER: registered office address}}` | terms, privacy |
| `{{OWNER: CIN or LLPIN, if a company or LLP; delete otherwise}}` | terms |
| `{{OWNER: GSTIN}}` | terms |
| `{{OWNER: FSSAI licence number for each store}}`, `{{OWNER: FSSAI licence number}}`, `{{OWNER: store name}}`, `{{OWNER: store address}}` | terms, food safety |
| `{{OWNER: FSSAI licence or registration number for the business as a whole, if different from the stores}}` | food safety |
| `{{OWNER: store names and addresses, or a link to a live list}}` | delivery |
| `{{OWNER: website address}}` | terms, privacy |
| `{{OWNER: support phone}}`, `{{OWNER: support email}}`, `{{OWNER: support hours}}` | refund, delivery, terms, grievance, food safety |
| `{{OWNER: grievance officer name}}`, `… designation}}`, `… email}}`, `… phone}}`, `… postal address}}`, `{{OWNER: hours when the grievance officer can be reached}}` | grievance, privacy |
| `{{OWNER: date of publication}}` | all |
| `{{OWNER: city for courts}}` | terms |
| `{{OWNER: number}}` (refund days; days to answer a data request; days to use refrigerated seafood) | refund, privacy, grievance, food safety |
| `{{OWNER: e.g. "the same day"}}` (when online refunds start) | refund |
| `{{OWNER: confirm whether cash refunds will be offered …}}` and the matching line in terms | refund, terms |
| `{{OWNER: add ", or sends a replacement" only if replacements will be offered}}` | refund |
| `{{OWNER: say whether customers must keep the product until the complaint is decided …}}` | refund |
| `{{OWNER: describe the packing actually used …}}` | delivery |
| `{{OWNER: decide whether repeated refusal of pay-on-delivery orders may lead to pay-on-delivery being withdrawn …}}` | delivery |
| `{{OWNER: decide how a blocked customer is told …}}` | terms |
| `{{OWNER: SMS provider name …}}`, `{{OWNER: email provider name}}`, `{{OWNER: app notification service …}}` | privacy |
| `{{OWNER: hosting provider and data centre location}}`, `{{OWNER: backup storage provider and location}}` | privacy |
| `{{OWNER: retention for complaints and photos …}}`, `{{OWNER: retention for sent messages}}` | privacy |
| `{{OWNER: confirm 24 hours as planned (security item SEC-13)}}`, `{{OWNER: confirm one year}}`, `{{OWNER: confirm 7 days on the server and 30 days off-site …}}` | privacy |
| `{{OWNER: if analytics or advertising tools are added later …}}` | privacy |
| `{{OWNER: shorter targets you want to commit to …}}` | grievance |
| `{{OWNER: confirm current number, believed to be 1915}}` (National Consumer Helpline) | grievance |
| `{{OWNER: describe your sourcing …}}`, `{{OWNER: describe hygiene practice …}}`, `{{OWNER: confirm these handling tips …}}` | food safety |
| `{{OWNER: confirm once the invoice PDF is built …}}` (FSSAI number on invoice) | food safety |
| `{{OWNER: confirm current name, believed to be Food Safety Connect}}` | food safety |

## Open legal questions

1. **DPDP consent notice.** Which purposes rest on consent and which on legitimate use (s.7); the wording of the
   stand-alone notice (s.5, Rule 3); how consent is recorded. The app and website show no such notice today.
2. **DPDP timing.** When each DPDP Rules 2025 obligation starts for us, and whether the IT Act s.43A / SPDI Rules 2011
   still apply until then.
3. **Rights response time** (we understand at most 90 days under the DPDP Rules) and identity checks for requests by
   email.
4. **Children.** Whether "18 or older" plus a check box at sign-in meets s.9; we do not check age today.
5. **Data abroad.** Whether any provider (SMS, email, push, hosting, backups) stores data outside India (s.16).
6. **Retention.** Start of the 72-month GST period (CA); whether the DPDP Rules' one-year log retention applies; periods
   for complaint photos, messages, OTP records and security logs.
7. **Grievance timelines.** That rule 4(5) of the E-Commerce Rules (acknowledge in 48 hours, resolve in one month)
   applies to us; whether a resident nodal contact (rule 4(1)) is needed for our business type.
8. **Complaint window and late delivery.** Whether 24 hours is reasonable for perishables, and whether a late delivery
   must be fully refunded (E-Commerce Rules rule 7).
9. **Weight over-charge.** Whether charging up to 10 % above the ordered weight, with a "most you could pay" shown at
   checkout, is clear consent; whether Legal Metrology rules treat our packs as pre-packaged (declarations on the
   product page) and require stamped scales.
10. **Coupon on a returned order** stays used; fair when the failure was not the customer's?
11. **Account blocking** grounds and notice; **pricing-error** clause; **limitation of liability** and force majeure;
    **jurisdiction** clause given Consumer Protection Act s.34.
12. **Information to show** under E-Commerce Rules rule 4(2) and rule 7 (all store addresses, country of origin,
    seller details).
13. **FSSAI.** Licence type per store; e-commerce requirements; licence number on website, product pages and bills;
    temperature ranges; allergen labelling for marinated items; use of the word "fresh".
14. **GST.** Tax invoice or bill of supply for exempt fresh fish (D42, CA).

## Build items the policies depend on (not legal questions)

The text describes the system's specified behaviour. These parts are not built yet and must be before publishing, or
the text must change:

| Item | Policy | Where it is tracked |
|---|---|---|
| Complaint photo upload (up to 4 photos) — the API takes complaints without photos today | refund, grievance | `docs/api/README.md` Phase 2 notes; `docs/team/next-steps.md` (SEC-5 guard) |
| DPDP consent notice at first sign-in, and a record of consent | privacy | new; see open question 1 |
| Retention jobs: OTP requests (24 h), outbox, webhook payloads, staff login failures, orders and invoices after 72 months; plus whatever is chosen for complaint photos and messages | privacy | `docs/security/review-2026-10.md` SEC-13, D77, `docs/team/next-steps.md` |
| FSSAI licence number on the invoice PDF (the PDF itself is not built) | food safety | `docs/product/ordering.md` ORD-R53 |
| A grievance log for complaints that arrive by email or phone | grievance | new (can be a spreadsheet) |
| Account deletion is allowed while a complaint or pay-on-delivery refund is open | privacy | new; decide whether to block |
| Online payment is not enabled at launch (D26); the "when we offer it" sections describe the built-but-off behaviour | refund, terms | D26 |
