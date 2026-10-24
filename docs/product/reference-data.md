# Reference data

Owner: E5. Becomes JSON under `backend/data/reference/` in Phase 2, loaded by `FreshOrbit.Migrations` on every
deploy (all environments, Production included), idempotent. Not sample data (`backend/data/sample` is separate).
Sources: `docs/api/README.md` §2–§4, `docs/database/README.md` §3, §5, `docs/product/*.md`.

## 1. Files and load rules

| File | Target table(s) | Owner module | Load mode | Rule |
|---|---|---|---|---|
| `permissions.json` | `identity.permissions` | Identity | SYNC | Upsert `code`, `description`. A code in the DB but not in the file → loader fails (removal needs an explicit migration). |
| `roles.json` | `identity.roles`, `identity.role_permissions` | Identity | INSERT_IF_MISSING + ADMIN sync | System role inserted with its permissions only when the role code is absent (admins may edit system-role permissions later). ADMIN always receives every permission in `permissions.json` (adds missing, never removes). |
| `preparation-methods.json` | `catalog.preparation_methods` | Catalog | SYNC | Upsert by `code`: `name`, `description`, `sort_order` (read-only via API). Never deleted. |
| `notification-templates.json` | `notifications.templates` | Notifications | INSERT_IF_MISSING | Insert by `(code, channel)`; never overwrite an edited subject/body. `variables` list is also compiled into code for `TEMPLATE_PLACEHOLDER_UNKNOWN`. |
| `tax-categories.json` | `pricing.tax_categories` (new, RD-G1) | Pricing | SYNC | Codes only; no `tax_rules` rows shipped. |
| `order-policies.json` | `settings.order_policies` | Settings | INSERT_IF_MISSING | Default row (`city_id` null) only; never overwrite admin edits. |
| `number-series.json` | `settings.number_series` | Settings | INSERT_IF_MISSING | Rows for current and next financial year; never touched once a number is issued. |

| Loader rule |
|---|
| Each file is validated against a JSON schema before any write; one transaction per file; invalid file → loader exits non-zero, deploy stops. |
| `created_by` / `modified_by` = `SYSTEM`. Each file carries `"version": n`; the loader logs inserted / updated / skipped counts. |
| Codes are UPPER_SNAKE (permission codes are dotted lowercase as in `docs/api` §3). |

## 2. Permissions (`permissions.json`) — 26 rows (the 25 below plus `payments.manage`)

| Code | Description |
|---|---|
| `catalog.manage` | Categories, products, variants, preparations, images, home promotion banners |
| `pricing.manage` | Variant prices, preparation charges; view tax rules |
| `tax.manage` | Schedule GST rates (tax rules) for every city (D35) |
| `promotions.manage` | Coupons |
| `inventory.view` | Stock, batches, movements, cold-chain logs |
| `inventory.receive` | Receive stock (batches) |
| `inventory.adjust` | Waste, adjustments, counts, batch discard, cold-chain entries |
| `orders.view` | Order lists, detail, history |
| `orders.pack` | Start preparing, weigh and pack, propose substitutions |
| `orders.cancel` | Cancel orders |
| `complaints.manage` | Decide complaints |
| `refunds.request` | Create refund requests |
| `refunds.approve` | Approve / reject refunds |
| `payments.view` | Payments, webhook events |
| `cash.settle` | Rider cash settlements |
| `delivery.manage` | Slots, templates, riders, shipments, dispatch, delivery attempts, cash collected |
| `locations.manage` | Cities, fulfillment locations, service areas, pincodes, hours, closures |
| `cities.activate` | Activate / pause / retire a city |
| `customers.view` | Customer list and detail |
| `customers.manage` | Block / unblock, edit customer contact |
| `notifications.manage` | Templates, test sends |
| `reports.view` | Reports |
| `settings.manage` | Business profile, order policies, number series |
| `staff.manage` | Staff, roles, location access |
| `audit.view` | Audit log |

Proposed, not shipped until `docs/api` adds it: `payments.manage` (webhook reprocess, payment-issue resolution; ADMIN, FINANCE) — see `payments.md` Q7.

## 3. Roles (`roles.json`) — 6 system roles (`is_system = true`)

| Code | Name | Permissions | Location scope (staff default) |
|---|---|---|---|
| `ADMIN` | Administrator | all 26 | all locations |
| `STORE_MANAGER` | Store manager | `pricing.manage`, `inventory.view`, `inventory.receive`, `inventory.adjust`, `orders.view`, `orders.pack`, `orders.cancel`, `complaints.manage`, `refunds.request`, `payments.view`, `cash.settle`, `delivery.manage`, `customers.view`, `reports.view` (14) | scoped |
| `PACKER` | Packer | `inventory.view`, `orders.view`, `orders.pack` (3) | scoped |
| `DISPATCHER` | Dispatcher | `orders.view`, `delivery.manage` (2) | scoped |
| `SUPPORT` | Customer support | `orders.view`, `orders.cancel`, `complaints.manage`, `refunds.request`, `payments.view`, `customers.view`, `customers.manage` (7) | all locations |
| `FINANCE` | Finance | `inventory.view`, `orders.view`, `refunds.approve`, `payments.view`, `cash.settle`, `customers.view`, `reports.view`, `audit.view`, `tax.manage` (9; the file also grants `payments.manage`) | all locations |

| Rule |
|---|
| Matrix is exactly `docs/api` §3. Location scope is a staff attribute (`staff_location_access`), not a role column; the column above is guidance for the admin screen. |
| `refunds.request` and `refunds.approve` are never in the same non-ADMIN role (separation of duties; the requester ≠ approver check still applies to ADMIN). |
| No staff user is shipped. The first ADMIN is created by a one-time CLI command of the Migrations host (`--create-admin`), not by reference data. |

## 4. Preparation methods (`preparation-methods.json`) — 16 rows

Guidance yield is advisory only: it pre-fills `variant_preparations.yield_bp` in the admin screen; staff set the real
per-variant yield from test cuts. Non-KG variants always use 10000 (CAT-R6).

| Sort | Code | Name | Description | Guidance yield (bp) | Typical use |
|---|---|---|---|---|---|
| 10 | `WHOLE` | Whole | As received; not cleaned or cut (live when the variant is sold live) | 10000 | all |
| 20 | `CLEANED` | Cleaned, whole | Scaled, gutted, gills and fins removed; head on | 8500 | fish |
| 30 | `CLEANED_HEADLESS` | Cleaned, head off | As `CLEANED` with head removed | 7000 | fish |
| 40 | `CURRY_CUT` | Curry cut | Cleaned, head off, cut into bone-in pieces (3–4 cm) | 6500 | fish |
| 50 | `STEAK` | Steaks | Cleaned, cross-cut slices about 1 cm with centre bone | 7000 | large fish |
| 60 | `FRY_CUT` | Fry cut | Cleaned, thin bone-in slices for frying | 6500 | fish |
| 70 | `FILLET` | Fillet, skin on | Boneless fillets with skin | 5500 | fish |
| 80 | `FILLET_SKINLESS` | Fillet, skinless | Boneless, skin removed | 4500 | fish |
| 90 | `BONELESS_CUBES` | Boneless cubes | Skinless boneless fillet cut into cubes | 4500 | fish |
| 100 | `BUTTERFLY` | Butterflied | Cleaned, opened flat along the back, spine removed, halves joined | 6000 | fish, prawns |
| 110 | `DEVEINED_SHELL_ON` | Deveined, shell on | Head off, shell on, vein removed | 6500 | prawns |
| 120 | `PEELED_TAIL_ON` | Peeled, tail on | Head and shell off except tail, vein removed | 6200 | prawns |
| 130 | `PEELED_DEVEINED` | Peeled and deveined | Head, shell and tail off, vein removed | 6000 | prawns |
| 140 | `CRAB_CLEANED_CUT` | Crab, cleaned and cut | Top shell, gills and innards removed; halved or quartered | 7000 | crab |
| 150 | `SQUID_CLEANED` | Squid, cleaned | Head, ink sac, quill and skin removed; tube and tentacles kept | 7000 | squid, cuttlefish |
| 160 | `SQUID_RINGS` | Squid rings | Cleaned tube cut into rings | 6000 | squid |

| Note |
|---|
| `docs/api` examples use `CLEANED` = "Peeled and deveined" for prawns and two different `WHOLE` names ("Whole, uncleaned", "Whole, live"). With this list prawns use `PEELED_DEVEINED`; the per-variant wording needs RD-G2. |

## 5. Notification templates (`notification-templates.json`)

Channels: `SMS`, `EMAIL`, `PUSH`, `IN_APP`. Category maps to `notifications.preferences` (`ORDER_UPDATES` SMS
cannot be disabled). Variables are pre-formatted strings from the server (`…Text` = display text such as `₹2,450.51`,
`23 Oct, 2:00–4:00 PM`). Common variables available to every template: `businessName`, `supportPhone`,
`supportEmail` (business profile), `recipientName`.

### Customer

| Code | Channels | Category | Variables | Trigger (event) |
|---|---|---|---|---|
| `OTP_LOGIN` | SMS | — (always) | `code`, `expiresInMinutes` | OTP request, purpose LOGIN |
| `OTP_VERIFY_PHONE` | SMS | — (always) | `code`, `expiresInMinutes` | OTP request, purpose VERIFY_PHONE |
| `ORDER_CONFIRMED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `slotDateText`, `slotWindowText`, `estimatedTotalText`, `paymentMethodText`, `orderUrl` | order → CONFIRMED |
| `PAYMENT_RECEIVED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `amountText`, `paymentMethodText` | `PaymentCaptured`, `PaymentCollected` |
| `PAYMENT_FAILED` | SMS, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `amountText`, `retryUrl` | `PaymentFailed` |
| `ORDER_PREPARING` | PUSH, IN_APP | ORDER_UPDATES | `orderNumber` | → PREPARING |
| `SUBSTITUTION_PROPOSED` | SMS, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `itemName`, `proposedItemName`, `proposedQuantityText`, `priceDifferenceText`, `respondByText`, `orderUrl` | substitution proposed |
| `ORDER_PACKED` | PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `finalTotalText`, `amountDueText`, `weightNoteText` | → PACKED |
| `ORDER_OUT_FOR_DELIVERY` | SMS, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `riderName`, `riderPhone`, `deliveryCode`, `amountDueText`, `slotWindowText`, `trackUrl` | → OUT_FOR_DELIVERY |
| `ORDER_DELIVERED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `invoiceNumber`, `finalTotalText`, `orderUrl` | → DELIVERED |
| `DELIVERY_FAILED` | SMS, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `reasonText` | → DELIVERY_FAILED |
| `ORDER_CANCELLED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `reasonText`, `refundAmountText` (empty when none) | → CANCELLED |
| `REFUND_INITIATED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `amountText`, `refundMethodText` | `RefundApproved` |
| `REFUND_SUCCEEDED` | SMS, EMAIL, PUSH, IN_APP | ORDER_UPDATES | `orderNumber`, `amountText`, `refundMethodText`, `referenceText` | `RefundSucceeded` |
| `COMPLAINT_RECEIVED` | EMAIL, IN_APP | ORDER_UPDATES | `orderNumber`, `itemName`, `complaintKindText` | complaint raised |
| `COMPLAINT_RESOLVED` | SMS, EMAIL, IN_APP | ORDER_UPDATES | `orderNumber`, `resolutionText`, `refundAmountText` | complaint decided |

### Staff (EMAIL only until a staff in-app inbox exists, RD-G4)

| Code | Channels | Variables | Recipients | Trigger |
|---|---|---|---|---|
| `STAFF_ACCOUNT_CREATED` | EMAIL | `staffName`, `email`, `loginUrl` | the new staff member | staff created (no password in the message) |
| `STAFF_PASSWORD_RESET` | EMAIL | `staffName`, `loginUrl` | the staff member | reset-password |
| `STAFF_PAYMENT_ALERT` | EMAIL | `alertKindText`, `orderNumber`, `locationName`, `detailsText`, `adminUrl` | `payments.view` at the location | `PaymentAlertRaised` |
| `STAFF_REFUND_APPROVAL_NEEDED` | EMAIL | `orderNumber`, `amountText`, `reasonText`, `requestedBy`, `adminUrl` | `refunds.approve` | `RefundRequested` |
| `STAFF_REFUND_FAILED` | EMAIL | `orderNumber`, `amountText`, `failureReasonText`, `adminUrl` | `refunds.approve` | `RefundFailed` |
| `STAFF_CASH_DIFFERENCE` | EMAIL | `riderName`, `settlementDateText`, `expectedText`, `receivedText`, `differenceText`, `adminUrl` | `cash.settle` at the location | `CashSettlementRecorded` with difference ≠ 0 |
| `STAFF_COLD_CHAIN_ALERT` | EMAIL | `locationName`, `storageUnit`, `temperatureText`, `recordedAtText` | `inventory.adjust` at the location | out-of-range cold-chain log |
| `STAFF_ACCOUNT_LOCKED` | EMAIL | `staffEmail`, `lockedUntilText`, `lockoutCountText` | `staff.manage` holders (ADMIN always), active only | `StaffAccountLocked` (D33) |

| Template rule |
|---|
| 16 customer codes, 47 customer rows; 8 staff codes, 8 rows; 55 rows total. |
| EMAIL rows carry `subject`; SMS bodies ≤ 160 GSM-7 characters where possible; PUSH title ≤ 50, body ≤ 150. |
| Commercial SMS in India must use templates registered with the operator's DLT platform; the SMS body text and its DLT template id are confirmed by the business before go-live (RD-Q3). |
| No `OFFERS` (marketing) templates at launch. |

## 6. Tax categories (`tax-categories.json`) — codes only, no rates

| Code | Name | Applies to |
|---|---|---|
| `SEAFOOD_FRESH_CHILLED` | Fresh or chilled seafood | fish, shellfish sold fresh or chilled, whole or cut |
| `SEAFOOD_FROZEN` | Frozen seafood | frozen products |
| `SEAFOOD_DRIED_SALTED` | Dried or salted seafood | dry fish and similar |
| `SEAFOOD_MARINATED` | Marinated / ready-to-cook seafood | marinated or coated packs |
| `SEAFOOD_READY_TO_EAT` | Cooked / ready-to-eat seafood | cooked products |
| `GROCERY_ADDON` | Grocery add-ons | masalas, condiments sold with orders |
| `DELIVERY_SERVICE` | Delivery charge | the order delivery fee |

| Rule |
|---|
| Rates (`pricing.tax_rules.rate_bp`) and the category ↔ HSN mapping are configured by the business with its CA; nothing here asserts a rate. No category has a rule until staff add one → `TAX_RULE_MISSING` blocks sale (never a silent 0). |
| A line's preparation charge uses the line's tax category unless the CA advises otherwise (RD-Q4). |

## 7. Order policy defaults (`order-policies.json`) — one row, `city_id` null

| Column | Value | Source |
|---|---|---|
| `reservation_minutes` | 15 | proposal (ONLINE payment window) |
| `max_overweight_bp` | 1000 | decided (+10%, database §3) |
| `cancel_allowed_until_status` | `CONFIRMED` | `docs/api` §6 default |
| `cod_max_order_paise` | 500000 (₹5,000) | proposal; business confirms |
| `enabled_payment_methods` (new, RD-G3) | `["COD", "UPI_ON_DELIVERY"]` | ONLINE off until a gateway is live |
| `substitution_response_minutes` (new, RD-G3) | 15 | `docs/api` §4.15 |
| `complaint_window_hours` (new, RD-G3) | 24 | `docs/api` §6 |

## 8. Number series (`number-series.json`)

Period = Indian financial year (1 April – 31 March), label `YYYY-YY`, decided in the business time zone (RD-G5).

| Code | Period | Prefix | Pad width (new, RD-G5) | `next_value` | First number |
|---|---|---|---|---|---|
| `ORDER` | `2026-27` | `FO-26` | 4 | 1 | `FO-260001` |
| `ORDER` | `2027-28` | `FO-27` | 4 | 1 | `FO-270001` |
| `INVOICE` | `2026-27` | `INV/26-27/` | 6 | 1 | `INV/26-27/000001` |
| `INVOICE` | `2027-28` | `INV/27-28/` | 6 | 1 | `INV/27-28/000001` |

| Rule |
|---|
| Number = prefix + `next_value` left-padded to pad width; past 9999 orders the number simply grows (`FO-2610000`), never wraps. |
| Invoice format deviates from `docs/database` (`INV/2026-27/000001`, 18 characters): GST invoice serial numbers are understood to be limited to 16 characters; `INV/26-27/000001` is 16. Confirm with the CA (RD-Q1). |
| A missing row for the current period is refused (`CONFIGURATION_MISSING`), so each release ships the next year's rows; an admin can also add them via `PUT /admin/settings/number-series/{code}/{period}`. |

## 9. Gaps and open questions

| # | Gap / question | Proposed answer |
|---|---|---|
| RD-G1 | No table for tax categories; `products.tax_category_code` is free text. | Add `pricing.tax_categories (code PK, name, description, is_active)`; FK from `products` and `tax_rules`. |
| RD-G2 | One preparation name for all variants (`WHOLE` = "Whole, live" for crab vs "Whole, uncleaned" for fish). | Add `variant_preparations.display_name` (nullable override). |
| RD-G3 | `order_policies` has no column for enabled payment methods (needed for `PAYMENT_METHOD_NOT_ALLOWED`), substitution window or complaint window. | Add the three columns shown in §7. |
| RD-G4 | No staff in-app inbox endpoint. | Staff templates EMAIL only; add `/admin/me/notifications` later. |
| RD-G5 | `number_series` has no pad width; no business time zone for the FY boundary. | Add `number_series.pad_width`; add `business_profile.time_zone` (data `Asia/Kolkata`). |
| RD-G6 | `preparation_methods` has no column for guidance yield. | Add `guidance_yield_bp` (advisory; never used for reservations). |
| RD-Q1 | Invoice number format and length. | `INV/26-27/000001` (16 chars) pending CA confirmation. |
| RD-Q2 | `cod_max_order_paise` and `reservation_minutes` values. | ₹5,000 and 15 min as starting values; business owner confirms. |
| RD-Q3 | DLT template registration for SMS. | Business registers bodies before go-live; add `dlt_template_id` column to SMS templates (Notifications, E1). |
| RD-Q4 | GST treatment of preparation charge and delivery fee. | CA decides; data model already supports a separate category for delivery (`DELIVERY_SERVICE`). |
| RD-Q5 | Who creates the first ADMIN. | Migrations host `--create-admin --email` prompts for a password; refused if an active ADMIN exists. |
