# Legal policies: the rules each one follows (2026-10)

Author: LG. Date: 2026-10-24. Drafts: `docs/legal/`. **Not legal advice.** This note records which Indian laws and
rules each policy draft is written against, what in the drafts comes from the system's own behaviour, and how sure we
are of each legal point. A lawyer (and, where marked, the CA or a food-safety consultant) must confirm everything in
the "Needs checking" column before any policy is published.

## Method

1. The **business behaviour** in every policy comes from the repo, not from law: module specs in `docs/product/`,
   decisions in `docs/decisions/log.md`, the default order policy in
   `backend/data/reference/settings/order-policies.json`, notification templates in
   `backend/data/reference/notifications/`, the security review and runbooks. Where the spec and the code differ (for
   example complaint photos), the policy follows the spec and `docs/legal/README.md` lists the build item.
2. The **legal points** come from general knowledge of Indian law, plus two web searches on 2026-10-24 that confirmed
   the DPDP Rules 2025 notification (13 November 2025, phased over 18 months to about 13 May 2027, grievance answers
   within at most 90 days, detailed breach report to the Board within 72 hours after an immediate first notice) and
   the E-Commerce Rules grievance timelines (acknowledge within 48 hours, redress within one month). Sources:
   [Bar & Bench](https://www.barandbench.com/amp/story/view-point/meity-notifies-final-digital-personal-data-protection-rules-2025),
   [EY](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025),
   [Cyril Amarchand Mangaldas alert](https://www.cyrilshroff.com/wp-content/uploads/2025/11/Client-Alert-DPDP-Rules-Nov-2025.pdf),
   [IndiaLaw summary of the E-Commerce Rules](https://www.indialaw.in/blog/civil/consumer-protection-e-commerce-rules/).
   No gazette text was read; **rule and section numbers below are from memory unless marked confirmed**.
3. No code was copied from anywhere, and no template policy text from other sites was used (rule 14).

Confidence: **Sure** = the law says this and we are confident of it; **Likely** = we believe it but the exact rule
number, wording or applicability must be checked; **Open** = a question only a lawyer can answer.

## 1. Terms of use (`docs/legal/terms-of-use.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| Consumer Protection Act 2019 (unfair contract terms s.2(46), unfair trade practice s.2(47), e-commerce s.94) | No clause limits statutory rights; pricing-error, blocking and liability clauses flagged | Sure that the Act applies; Open on each clause's wording |
| Consumer Protection (E-Commerce) Rules 2020, rule 4(2): show legal name, address of HQ and branches, website, customer-care and grievance-officer contacts | §1 "Who we are" with placeholders | Sure on the duty (confirmed in summary); Likely on rule number; Open whether every store address must be listed |
| E-Commerce Rules rule 4(6): cancellation charges only if we bear similar charges when we cancel | No cancellation fee anywhere | Sure on the principle (confirmed); Likely on rule number |
| E-Commerce Rules rule 7 (inventory e-commerce entities): accurate refund/delivery/payment terms; total price with breakup; country of origin; no refusal of refund for defective, not-as-described or late goods | Prices tax-inclusive with charges shown; refund and delivery terms linked; country-of-origin flagged | Likely (rule 7 content and numbering not confirmed by search) |
| Consumer Protection Act s.34(2): consumer may file where they live or work | Jurisdiction clause made subject to that, flagged | Sure |
| CGST Act / GST invoice at supply | "GST invoice issued at delivery" matches ORD-R48 | Sure on behaviour; tax invoice vs bill of supply for exempt fish is Open (CA, D42) |
| Majority age (Indian Contract Act 1872 s.11; DPDP s.9 under 18 = child) | 18 or older to hold an account | Sure on the ages; Open on how to check |

System facts used: OTP sign-in valid 5 minutes (IDN-R1); browse without sign-in, sign in to order; deletion refused
with an active order (`ACTIVE_ORDERS_EXIST`); staff block (CUS-R8); tax-inclusive prices (D2); prices per pincode's
store (LOC-R1, rule 7 of CLAUDE.md); price-change confirmation (ORD-R10); acceptance at confirmation (ORD-R14);
10 % over-weight cap (ORD-R18, R27); substitutions (ORD-R33–R36); 500-character notes (ORD-R16); COD limit on max
total (ORD-R9, D17); coupons one per order, first-order, per-customer and total limits, given back on cancel, kept on
return (PROMO-R6–R10, ORD-R41, D19).

## 2. Privacy policy (`docs/legal/privacy-policy.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| DPDP Act 2023 s.5 + DPDP Rules 2025 Rule 3: notice before consent (itemised data, purposes, how to withdraw, how to complain to the Board) | §2 itemised table; §3 says a separate notice is shown at first sign-in; flagged as **not built** | Sure on the duty; Likely on Rule 3 numbering |
| DPDP s.6: consent free, specific, informed, unambiguous; withdrawal as easy as giving it (s.6(4)) | §3, §6 | Sure |
| DPDP s.7: legitimate uses without consent | Mapping each purpose flagged | Open |
| DPDP s.8(5): reasonable security safeguards; Rules: security measures and logs | §8 lists real controls (HTTPS, hashed OTP and passwords, lockout, location-scoped staff, append-only audit) | Sure on behaviour |
| DPDP s.8(6) + Rules: breach notice to the Board and each affected person; detailed report within 72 hours | §8 | Sure (confirmed by search) on "without delay" + 72-hour detailed report |
| DPDP s.8(7): erase when the purpose is served unless law requires retention | §5 retention table matches D34 / `customers.md` retention (72 months for orders and invoices, CGST Act s.36) | Sure on the principle; 72 months is Sure under s.36; its start date is Open (CA) |
| DPDP Rules: keep processing logs at least one year | Security logs "one year" placeholder | Likely; confirm and check interplay with shorter periods |
| DPDP s.8(9): publish contact of a person who answers data questions (DPO only for Significant Data Fiduciaries, s.10) | Grievance officer named for this | Sure on the duty; Open whether one person can be both |
| DPDP s.8(10), s.13: grievance redressal; the principal must use it before going to the Board (s.13(3)) | §6 and grievance policy §4 | Sure |
| DPDP Rules: answer grievances within at most 90 days | Owner sets a shorter number, never above the Rules | Sure (confirmed by search) on 90 days |
| DPDP s.9: children under 18 need verifiable parental consent | Service limited to 18+; age check flagged | Sure on the rule; Open on adequate check |
| DPDP s.11, s.12, s.14, s.15: access, correction/erasure, nomination, duties of the principal | §6, §7 | Sure |
| DPDP s.16: transfers abroad allowed unless the Government restricts a country | Providers listed with placeholders; flagged | Sure on the rule; Open on our providers' locations |
| Commencement: DPDP Rules notified 13 Nov 2025, phased to about 13 May 2027 | Policy written to the full regime now | Sure (confirmed); Open which duties apply before May 2027 |
| IT Act 2000 s.43A and SPDI Rules 2011 (privacy policy, grievance officer, one month) until DPDP s.44 repeals them | Grievance policy cites rule 5(9) | Likely; Open on the exact date they stop applying |

System facts used: data items from `docs/database/README.md` (users, customers, addresses with optional lat/long,
orders snapshot, payments with UPI reference, refunds with `payee_upi_id`, complaints, wishlist, carts, notification
messages, device tokens, preferences, `otp_challenges` with phone and IP); no bank details stored (payments Q10);
UPI IDs masked in staff lists (PAY-R24); cookies `fo_pincode`, `fo_cart`, `fo_rt` (httpOnly), `fo_session`
(`apps/web/src/lib/cookies.ts`, D37) and no analytics code found in `apps/web` or `apps/mobile`; ORDER_UPDATES SMS
cannot be disabled, OFFERS off by default (NOT-R2, NOT-R8); deletion effects (CUS-R7, CUS-R9, D34); carts 30 days
(D19); backups 7 days local / 30 days off-site, encrypted before leaving the server (`runbooks/backup-restore.md`);
hosting Oracle Cloud India region now, Hostinger later (D25); SMS via a DLT provider (NOT-R12); known retention gaps
SEC-13 and D77.

## 3. Refund and cancellation policy (`docs/legal/refund-cancellation-policy.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| E-Commerce Rules rule 4(6): cancellation charge symmetry | No charge | Sure on principle (confirmed) |
| E-Commerce Rules rule 4(10) (refunds within a reasonable period / as RBI prescribes) | Refund days are owner placeholders, flagged | Likely |
| E-Commerce Rules rule 7 (no refusal of refund for defective, deficient, not-as-described, late goods) | Complaint flow with refund up to line total; late delivery at order level; 24 h window and staff discretion flagged | Likely on rule; Open on whether our window and discretion comply |
| Consumer Protection Act 2019 (statutory rights cannot be excluded) | "Nothing in this policy takes away any right you have" | Sure |
| Legal Metrology Act 2009 and (Packaged Commodities) Rules 2011: sale by weight, verified scales, declarations for pre-packed goods sold online | Charge on packed net weight with a stated maximum; flagged | Open: whether our cut-to-order packs are "pre-packaged", and whether stamped scales and product-page declarations are needed |
| RBI rules on refunds for failed digital payments (turn-around times) | Only relevant once online payment starts | Likely; Open later |

System facts used: customer cancel until `CONFIRMED` by default, `PENDING_PAYMENT` always (ORD-R38, SET-R2); staff
cancel up to `PACKED` (ORD-R39); reason codes (ORD-R40); coupon reversed on cancel (ORD-R41); 15-minute online payment
window (ORD-R14, D17); over-weight cap and final amount (ORD-R18, R27–R31); prepaid capped at paid amount (D15);
substitutions (ORD-R33–R36, D19); complaint window 24 h, kinds MISSING/DAMAGED/QUALITY/WRONG_ITEM/LATE, ≤ 4 images ≤ 5 MB,
description 10–1000 chars, one open per line, refund up to line or order final total (ORD-R54–R57); REPLACEMENT recorded
only (ORD Q11); goodwill reduces amount due before collection (ORD-R58); COD/UPI-on-delivery refunds by UPI, bank
transfer or cash with a reference, second-person approval (PAY-R13, R14, R17); system refunds auto-approved (PAY-R15,
D15); returned prepaid order refunded after approval, coupon kept (PAY-H5, D19); notifications per template channels
(complaint received is email + in-app, decision is SMS; preparing and packed are push + in-app only).

## 4. Shipping and delivery policy (`docs/legal/shipping-delivery-policy.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| E-Commerce Rules rule 7: delivery and shipment terms shown before purchase | Slots, cut-offs, fees, minimum order, pay-on-delivery limit described; values shown live at checkout | Likely on rule number; Open whether the slot is the "stated delivery schedule" |
| FSSAI cold-chain expectations for chilled/frozen fish | Our own ranges described (chiller −1…4 °C, freezer ≤ −18 °C, receipt ≤ 4 °C, D11) | Sure these are the system's ranges; Open whether they match FSSAI standards |

System facts used: pincode → service area → store → city resolution and pause behaviour (LOC-R1, R3, R5, R8); 7-day
slot horizon, cut-off, capacity, closures (DEL-R9, R10, R13); COD/UPI on delivery only at launch (D26) with ₹5,000
limit (D17); 4-digit delivery code shown only while out for delivery (DEL-R7, R14, D12); failed-attempt outcomes and
same-day reattempts up to 3 (DEL-R16, D12); store activation needs FSSAI number and cold storage (LOC-R11, D5); dispatch
temperature recorded, > 4 °C flagged, cold box recorded (DEL-R6, R17, ORD-R32); expiry write-off and FEFO (inventory
jobs); invoice at delivery (ORD-R48).

## 5. Grievance redressal (`docs/legal/grievance-redressal.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| E-Commerce Rules: appoint a grievance officer and show name, designation and contacts | §2 table of placeholders | Sure (confirmed) |
| E-Commerce Rules: acknowledge within 48 hours, redress within one month | §3 | Sure (confirmed); Likely that it is rule 4(5) |
| E-Commerce Rules rule 4(1): resident nodal contact for companies | Flagged | Open (depends on business type; numbering not confirmed) |
| DPDP s.8(10), s.13 and Rules (≤ 90 days) | §3 data requests row | Sure (confirmed) |
| SPDI Rules 2011 rule 5(9): grievance officer, one month | Cited at the top | Likely |
| National Consumer Helpline, e-Daakhil, District Commission | §4, number left as owner check (believed 1915) | Likely; check names and numbers at publication |

## 6. Food safety and FSSAI note (`docs/legal/food-safety-fssai.md`)

| Rule followed | What the draft does | Confidence |
|---|---|---|
| Food Safety and Standards Act 2006; FSS (Licensing and Registration of Food Businesses) Regulations 2011 | Licence table per store; licence type flagged | Sure the Act applies; Open which licence each store needs |
| FSSAI directions for e-commerce food businesses | Flagged | Open (directions exist; current text not checked) |
| FSSAI direction that the licence/registration number be shown on bills and invoices | Placeholder; invoice PDF not built | Likely |
| FSS (Labelling and Display) Regulations 2020: allergen declaration | Allergen section; marinated items flagged | Likely on the regulation; Open on exact allergen list (whether molluscs are listed) and online display |
| FSS (Advertising and Claims) Regulations 2018: claims such as "fresh" | Flagged | Open |
| FSS (Food Products Standards and Food Additives) Regulations 2011 and Schedule 4 hygiene | Temperature ranges flagged | Open (food-safety consultant) |

System facts used: product `source_type` WILD/FARMED, `source_region`, `storage_instructions` (`docs/database`);
batch code, catch/harvest date, best-before, receipt temperature (INV-R4); cold-chain ranges and out-of-range flags
(INV-R18, D11); batch expiry write-off (inventory jobs); business FSSAI number 14 digits (SET-R1).

## 7. What we are sure of overall

- The DPDP Act 2023 applies to us as a Data Fiduciary, and the DPDP Rules 2025 were notified on 13 November 2025 with
  an 18-month phased start (confirmed).
- The E-Commerce Rules 2020 require a displayed grievance officer, 48-hour acknowledgement and one-month redress, and
  no one-sided cancellation charges (confirmed).
- GST records must be kept 72 months (CGST Act s.36); the start date needs the CA.
- Every behaviour statement in the drafts matches the specs and defaults at commit `6c846b5`, except the build items
  listed in `docs/legal/README.md`.

## 8. What needs checking

All "Likely" and "Open" rows above; the consolidated list is "Open legal questions" in `docs/legal/README.md`.
