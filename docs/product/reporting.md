# Reporting module

Owner: E5. Schema `reporting`. Endpoints: `docs/api/README.md` §4.20. Permission `reports.view`.
Reads other schemas read-only through SQL (materialized views and dashboard queries); writes only `reporting.*`.

## 1. Aggregates and boundaries

| Object | Kind | Grain (unique index, needed for `REFRESH … CONCURRENTLY`) | Source tables |
|---|---|---|---|
| `daily_sales` | materialized view | (business_date, fulfillment_location_id) | `ordering.orders`, `payments.refunds`, `locations.fulfillment_locations`, `locations.cities` |
| `product_performance` | materialized view | (business_date, fulfillment_location_id, variant_id, preparation_code) | `ordering.order_items`, `ordering.orders`, `inventory.stock_movements` (SALE), `inventory.stock_items` |
| `contribution_margin` | materialized view | (business_date, fulfillment_location_id) | `ordering.orders`, `inventory.stock_movements` (SALE), `payments.refunds` |
| `waste_summary` | materialized view | (business_date, fulfillment_location_id, variant_id, reason_code) | `inventory.stock_movements` (WASTE, SALE), `inventory.stock_items`, `catalog.product_variants`, `catalog.products` |
| `customer_daily_orders` (backs repeat-customers) | materialized view | (business_date, fulfillment_location_id, customer_id) | `ordering.orders` |
| `refresh_state` | table (Reporting-owned) | `view_name` PK; `refreshed_at`, `source_watermark`, `duration_ms`, `last_error` | written by the refresh job |
| Dashboard | live queries, not stored | — | `ordering.orders`, `delivery.delivery_slots`, `inventory.stock_items`, `ordering.complaints`, `payments.refunds`, `payments.reconciliation_issues` |

| Boundary rule |
|---|
| Reporting never writes another schema and raises no domain events. No `.Public` API; no module calls Reporting. |
| View SQL lives in `Reporting.Infrastructure/Persistence/Sql/*.sql`; the manager's migration executes it; the test fixture runs the same files after `EnsureCreated` (Q1). |
| Architecture tests allow raw SQL against other schemas only in `Reporting.Infrastructure` (same allowlist idea as Catalog `Reads`). |
| Numbers are computed in SQL / on the server; the admin client only renders. |

## 2. States and transitions

| Object | States | Transition |
|---|---|---|
| `refresh_state` row | NEVER → FRESH ↔ STALE | job run with new source rows → refresh → FRESH (`refreshed_at`, `source_watermark`); STALE is derived (source watermark > stored), not stored |
| Report request | — | stateless reads |

Source states used (owned elsewhere): order `DELIVERED` / `CANCELLED` and their timestamps; refund `SUCCEEDED`; movement kinds `SALE` / `WASTE`.

## 3. Business rules

| Key | Rule |
|---|---|
| REP-R1 | **Business date** = local date at the fulfillment location: `(ts AT TIME ZONE cities.time_zone)::date`. `fromDate` / `toDate` are inclusive local dates. |
| REP-R2 | **Revenue recognition.** Revenue counts on `DELIVERED`, dated by `delivered_at`, from final amounts (`final_total_paise`, which already reflect weight, short supply, substitutions, goodwill before delivery). `CANCELLED`, `RETURNED`, `PAYMENT_FAILED` orders carry no revenue. |
| REP-R3 | **daily_sales** columns: `business_date`, `city_id`, `fulfillment_location_id`, `orders_placed` (by `placed_at` date, excl. `PAYMENT_FAILED`), `orders_delivered` (by `delivered_at`), `orders_cancelled` (by `cancelled_at`), `gross_paise` = Σ final items + final preparation of delivered orders, `discount_paise` = Σ `discount_paise`, `delivery_fee_paise` = Σ `delivery_fee_paise`, `tax_paise` = Σ `tax_paise`, `net_revenue_paise` = Σ `final_total_paise` (= gross − discount + delivery fee), `refunds_paise` = Σ SUCCEEDED refunds by `succeeded_at` date (all reasons), `post_delivery_refunds_paise` = SUCCEEDED refunds with reason COMPLAINT / GOODWILL on delivered orders, `avg_order_value_paise` = `net_revenue_paise / orders_delivered` (integer division; null when 0). |
| REP-R4 | **product_performance** columns: `business_date` (delivered), `fulfillment_location_id`, `product_id`, `variant_id`, `sku`, `product_name`, `variant_name`, `preparation_code`, `stock_unit`, `orders_count` (distinct orders), `lines_short_supplied`, `ordered_quantity`, `packed_quantity`, `charged_quantity` = Σ min(packed, max_chargeable), `revenue_paise` = Σ line `final_total_paise`, `sale_cost_paise` = Σ abs(SALE `cost_paise`) of the line (Q3), `gross_margin_paise` = revenue − sale cost. Delivered orders only; names from the order snapshot. Σ `revenue_paise` per day/location = `daily_sales.gross_paise`. |
| REP-R5 | **contribution_margin** columns: `business_date`, `fulfillment_location_id`, `revenue_ex_tax_paise` = `net_revenue_paise − tax_paise` (orders delivered that date), `sale_cost_paise` = Σ abs(SALE `cost_paise`) of those orders, `refunds_paise` = `post_delivery_refunds_paise` (by refund date), `delivery_cost_paise` = Σ recorded delivery cost, 0 while no source exists (Q4), `contribution_paise` = revenue_ex_tax − sale_cost − refunds − delivery_cost, `contribution_margin_bp` = `contribution × 10000 / revenue_ex_tax` (integer, truncated toward zero; null when revenue 0). Shortfall and cancellation refunds are excluded (that money never counted as revenue). SALE cost of orders cancelled after packing is excluded (it appears as waste if wasted). |
| REP-R6 | **waste_summary** columns: `business_date` (movement `occurred_at`), `fulfillment_location_id`, `product_id`, `variant_id`, `product_name`, `variant_name`, `stock_unit`, `reason_code` (SPOILED, TEMPERATURE_BREACH, DAMAGED, EXPIRED, QC_REJECTED), `movements_count`, `waste_quantity` = Σ abs(quantity), `waste_cost_paise` = Σ abs(`cost_paise`), `sold_quantity` = Σ abs(SALE quantity) same day/variant, `waste_share_bp` = waste × 10000 / (waste + sold) (truncated; null when both 0). |
| REP-R7 | **Repeat customers.** View `customer_daily_orders` (business_date, location, city, customer_id, delivered_orders, net_revenue_paise). Endpoint computes for the range: `activeCustomers` (≥1 delivered order in range), `repeatCustomers` (active with ≥2 delivered orders up to `toDate`, locations in scope), `repeatRateBp` = repeat × 10000 / active, `newCustomers` (first delivered order in range); cohorts: `cohortMonth` (month of first delivered order) × `monthOffset` 0–11 → `activeCustomers`, `retentionBp`. |
| REP-R8 | **Dashboard** (live, `asOf` = now): orders by status for today's slots, slot fill per slot today (`bookedCount`, `capacity`, `fillBp`), low-stock items (available ≤ threshold), open complaints, refunds REQUESTED, OPEN payment issues. One SQL per tile; all scoped (REP-R11). |
| REP-R9 | **Refresh job** (Worker, every 15 min, single instance via `DisableConcurrentExecution`): read `refresh_state` and the current watermark = max of `ordering.orders.modified_at`, `inventory.stock_movements.created_at`, `payments.refunds.modified_at` (one SQL). Watermark ≤ stored → exit without refreshing. Else `REFRESH MATERIALIZED VIEW CONCURRENTLY` each view, then write `refresh_state` once. A failing view keeps its previous data and `refreshed_at`, stores `last_error`; next run retries. |
| REP-R10 | **Response.** `{ asOf, fromDate, toDate, filters, totals, items, page, pageSize, totalCount }`; `asOf` = oldest `refreshed_at` of the views used; `totals` over all matching rows, not the page. Defaults: `toDate` = today (local date of the locations in scope), `fromDate` = `toDate − 29 days`; range > 366 days or `fromDate > toDate` → 422 `VALIDATION_FAILED`. Default sort `-businessDate`; allowed sorts = the report's columns. |
| REP-R11 | **Location scoping.** Staff `loc` claim restricts rows silently; `fulfillmentLocationId` outside access → 403 `LOCATION_ACCESS_DENIED`; `cityId` → locations of that city ∩ access; `cityId` and `fulfillmentLocationId` inconsistent → 422 `VALIDATION_FAILED`. |
| REP-R12 | **CSV export** (`format=csv`): `text/csv; charset=utf-8` with BOM, `Content-Disposition: attachment; filename="<report>_<fromDate>_<toDate>.csv"`; header = snake_case column names; all rows (no paging), max 100 000 rows else 422 `VALIDATION_FAILED` on `toDate`; streamed; money as `_paise` integers plus `_inr` decimal strings (`1234.50`) (Q5); cells starting with `=`, `+`, `-`, `@` prefixed with `'`; final `TOTAL` row; response header `X-Report-As-Of`. Dashboard has no CSV (422). |
| REP-R13 | Reports are read-only: no `If-Match`, no `Idempotency-Key`; `Cache-Control: private, max-age=60`. |

## 4. Events

| Direction | Event | Note |
|---|---|---|
| Raised | none | |
| Consumed | none | Freshness is driven by the watermark check (REP-R9), so a lost event can never leave a report stale. |

## 5. Commands and queries (Read → Decide → Commit → Follow-on)

| Command / query | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| RefreshReports | Worker recurring `*/15 * * * *` | `refresh_state` + watermark (one SQL) | REP-R9 | refresh views; update `refresh_state` once | — |
| GetDailySales | `GET /api/v1/admin/reports/daily-sales` | `daily_sales` | REP-R10, R11 | — | — |
| GetProductPerformance | `GET /api/v1/admin/reports/product-performance` | `product_performance` | REP-R10, R11 | — | — |
| GetContributionMargin | `GET /api/v1/admin/reports/contribution-margin` | `contribution_margin` | REP-R10, R11 | — | — |
| GetWaste | `GET /api/v1/admin/reports/waste` | `waste_summary` | REP-R10, R11 | — | — |
| GetRepeatCustomers | `GET /api/v1/admin/reports/repeat-customers` | `customer_daily_orders` | REP-R7, R11 | — | — |
| GetDashboard | `GET /api/v1/admin/reports/dashboard` | live SQL per tile | REP-R8, R11 | — | — |
| ExportCsv | the five reports with `format=csv` | same | REP-R12 | — | audit entry `REPORT_EXPORTED` (who, report, filters) |

### API gaps

| # | Gap | Proposal |
|---|---|---|
| G1 | Report-specific filters missing | product-performance: `variantId`, `categoryId`, `preparationCode`; waste: `reasonCode`, `variantId` |
| G2 | No collections / refunds report for finance | `GET /admin/reports/collections`: per day × location × method — collected / captured, refunds by reason and status, cash expected / received / difference |
| G3 | Report response envelope (`asOf`, `totals`) not in docs/api | Add the REP-R10 shape to §4.20 |
| G4 | `format=csv` on dashboard undefined | Not supported → 422 `VALIDATION_FAILED` (`format`) |
| G5 | Cohort length | `cohortMonths` (1–12, default 6) on repeat-customers |

## 6. Test scenarios

### Seeded fixture (integration; one city `Asia/Kolkata`, location L1; dates 2026-10-23 IST unless stated)

| Item | Data |
|---|---|
| Batches | SEER total cost 2 131 500 / 24 500 g (87 paise/g); PRAWN 60 paise/g; CRAB 40 000 paise/piece |
| Order A (COD, customer X) | API example FO-260001: seer STEAK packed 540 g (yield 7000 → SALE 772 g, cost 67 164); prawns CLEANED packed 580 g, charged 550 g (yield 6000 → SALE 967 g, cost 58 020); crab 2 pcs (cost 80 000). Final items 249 591, prep 5 460, discount 10 000, fee 0, tax 0, total 245 051. DELIVERED |
| Order B (ONLINE, customer Y) | seer STEAK ordered 500 g, packed 450 g → items 58 455, prep 1 800, total 60 255; captured 66 950; WEIGHT_SHORTFALL refund 6 695 SUCCEEDED; SALE 643 g cost 55 941; DELIVERED; COMPLAINT refund 5 000 SUCCEEDED same day |
| Order C (ONLINE) | captured 47 950, cancelled at CONFIRMED; CANCELLATION refund 47 950 SUCCEEDED |
| Order D (COD) | placed, cancelled |
| Customer X history | one more DELIVERED order on 2026-09-10 (total 50 000) |
| Waste | SEER 1 000 g SPOILED, cost 87 000 |

### Expected numbers (L1, 2026-10-23)

| # | Report | Expect |
|---|---|---|
| R1 | daily_sales | placed 4, delivered 2, cancelled 2, gross 315 306, discount 10 000, fee 0, tax 0, net revenue 305 306, refunds 59 645, post-delivery refunds 5 000, AOV 152 653 |
| R2 | product_performance | SEER/STEAK: orders 2, ordered 1 000, packed 990, charged 990, revenue 132 561, cost 123 105, margin 9 456. PRAWN/CLEANED: packed 580, charged 550, revenue 52 745, cost 58 020, margin −5 275. CRAB/WHOLE: 2 pcs, revenue 130 000, cost 80 000, margin 50 000. Σ revenue 315 306 = R1 gross |
| R3 | contribution_margin | revenue ex tax 305 306, SALE cost 261 125, refunds 5 000, delivery cost 0, contribution 39 181, margin 1 283 bp |
| R4 | waste_summary | SEER SPOILED: movements 1, quantity 1 000, cost 87 000, sold 1 415 (772 + 643), waste_share 4 140 bp |
| R5 | repeat-customers 2026-10-01..2026-10-31 | active 2, repeat 1 (X), repeat rate 5 000 bp, new 1 (Y) |

### Behaviour

| # | Scenario | Expect |
|---|---|---|
| T1 | Job runs with no new orders / movements / refunds since watermark | no refresh executed; `refresh_state.refreshed_at` unchanged |
| T2 | New delivered order after a refresh | report unchanged with old `asOf`; after the next run → included |
| T3 | Order delivered 2026-10-23 23:30 IST (18:00 UTC) | counted on 2026-10-23 |
| T4 | Staff scoped to L1, data in L1 and L2 | only L1 rows and totals; `fulfillmentLocationId=L2` → 403 `LOCATION_ACCESS_DENIED` |
| T5 | `format=csv` | BOM, header row, `TOTAL` row equals JSON totals; product name `=HYPERLINK(...)` exported as `'=HYPERLINK(...)` |
| T6 | Range 400 days; `fromDate > toDate` | 422 `VALIDATION_FAILED` |
| T7 | Read during refresh | previous data returned, no error (CONCURRENTLY) |
| T8 | Staff without `reports.view` | 403 `FORBIDDEN` |

## 7. Open questions (with proposed answers)

| # | Question | Proposed answer |
|---|---|---|
| Q1 | `EnsureCreated` does not create materialized views; who owns the DDL? | SQL files in Reporting.Infrastructure; the manager's migration runs them (`migrationBuilder.Sql`); the test fixture runs them after `EnsureCreated`. |
| Q2 | Final item vs preparation split per line is not stored. | Ordering adds `order_items.final_item_paise`, `final_preparation_paise` (E4); until then `gross_paise` = Σ line `final_total_paise` and the split is not shown. |
| Q3 | SALE movements reference the order, not the line; per-variant cost is ambiguous for two lines of one variant. | Inventory writes SALE with `reference_type = ORDER_ITEM`, `reference_id = order_item_id` (E3). |
| Q4 | Delivery cost has no source. | Column present, value 0; later `delivery.shipments.delivery_cost_paise` (rider payout per drop). |
| Q5 | CSV money in paise or rupees? | Both: `_paise` integers (exact) and `_inr` decimal strings; JSON stays paise only. |
| Q6 | Refunds on cancelled orders in daily_sales while revenue excludes them? | Yes in `refunds_paise` (cash view); margin uses post-delivery refunds only (REP-R5). |
| Q7 | Tax-exclusive revenue while GST rates await the CA. | Use the stored `tax_paise` snapshot; configured rates flow through automatically. |
| Q8 | Refresh cost as history grows. | Full refresh until it exceeds 60 s; then incremental tables keyed by business date, recomputing the last 3 days each run. |
| Q9 | `refunds.succeeded_at` does not exist yet. | Added by Payments schema change S5 (payments.md). |
