# Database design

One PostgreSQL 17 database `freshorbit`, one schema per module plus `platform` and `hangfire`.

## 1. Conventions

| Topic | Rule |
|---|---|
| Keys | `uuid` (UUIDv7 from `Guid.CreateVersion7()`); `audit.entries.id` is `bigint` identity |
| Human numbers | `order_number` `FO-260001`, `invoice_number` `INV/2026-27/000001` from `settings.number_series` |
| Money | `bigint` paise, `_paise` |
| Weight / count | `integer`; `_grams` for weight, plain `quantity` in the variant's `stock_unit` (GRAM or PIECE) |
| Rates | `integer` basis points, `_bp` (500 = 5%) |
| Time | `timestamptz` UTC; dates `date`; times of day `time` |
| Codes | `varchar(40)` UPPER_SNAKE, with `CHECK` constraint |
| System columns | `created_at`, `created_by`, `modified_at`, `modified_by` on every table (set by DbContext) |
| Row version | Postgres `xmin` as EF concurrency token on aggregate roots |
| Naming | `snake_case`, plural tables (EFCore.NamingConventions) |
| Deleting | Master data: `is_active` / `status`. Orders, payments, movements, audit: never deleted or edited after the fact |
| Extensions | `citext`, `pg_trgm`, `btree_gist` |
| Foreign keys | Allowed across schemas, `ON DELETE RESTRICT`. Code still never navigates into another module's entities |

## 2. Overview

```
identity.users 1─1 customers.customers 1─N customers.addresses
                                   └─1─N customers.wishlist_items ─► catalog.products
identity.users 1─1 identity.staff_members N─M identity.roles N─M identity.permissions
                                   └─N─M locations.fulfillment_locations (staff_location_access)
locations.cities 1─N fulfillment_locations 1─N service_areas 1─N pincodes
catalog.categories 1─N products 1─N product_variants N─M preparation_methods (variant_preparations, yield_bp)
                         └─1─N product_images
pricing.variant_prices (location × variant × period)  pricing.preparation_charges (location × variant × prep × period)
pricing.tax_rules (tax category × period)
inventory.stock_items (location × variant) 1─N stock_movements N─1 batches
                 └─1─N stock_reservations 1─1 ordering.order_items
cart.carts 1─N cart_items
ordering.orders 1─N order_items 0─N substitutions; orders 1─N order_status_history, order_adjustments, complaints
ordering.orders 1─1 invoices 1─N invoice_lines; orders N─1 delivery.delivery_slots; 1─1 delivery.shipments 1─N delivery_attempts
ordering.orders 1─N payments.payments 1─N payments.refunds; orders 0─1 promotions.coupon_redemptions N─1 coupons
```

## 3. Tables

### identity
| Table | Columns | Constraints |
|---|---|---|
| users | id, phone (E.164), email citext, password_hash, user_type CUSTOMER/STAFF, status ACTIVE/LOCKED/DISABLED, phone_verified_at, last_login_at, failed_login_count | unique phone, unique email; check phone or email |
| otp_challenges | id, phone, purpose LOGIN/VERIFY_PHONE, code_hash, expires_at, attempt_count, consumed_at, request_ip | index (phone, created_at desc) |
| refresh_tokens | id, user_id, token_hash, family_id, expires_at, revoked_at, replaced_by_id, device_label | unique token_hash; reuse of a rotated token revokes the family |
| staff_members | id, user_id, full_name, all_locations, is_active | unique user_id |
| roles | id, code, name, is_system | unique code |
| permissions | code PK, description | reference data |
| role_permissions | role_id, permission_code | PK both |
| staff_roles | staff_member_id, role_id | PK both |
| staff_location_access | staff_member_id, fulfillment_location_id | PK both |

### customers
| Table | Columns | Constraints |
|---|---|---|
| customers | id, user_id, full_name, email, marketing_opt_in, status ACTIVE/DELETED | unique user_id |
| addresses | id, customer_id, label HOME/WORK/OTHER, recipient_name, phone, line1, line2, landmark, locality, city_name, state_code, pincode, latitude, longitude, is_default, is_active | unique (customer_id) where is_default and is_active |
| wishlist_items | customer_id, product_id, created_at | PK both |

### locations
| Table | Columns | Constraints |
|---|---|---|
| cities | id, code, name, state_code, time_zone, status DRAFT/ACTIVE/PAUSED/RETIRED, activated_at, activated_by | unique code |
| fulfillment_locations | id, city_id, code, name, address_line, pincode, phone, latitude, longitude, has_cold_storage, fssai_license_no, status DRAFT/ACTIVE/PAUSED | unique code |
| service_areas | id, city_id, fulfillment_location_id, name, delivery_fee_paise, free_delivery_above_paise, min_order_paise, is_active | |
| pincodes | id, pincode, service_area_id, is_active | unique (pincode) where is_active |
| operating_hours | fulfillment_location_id, day_of_week, opens_at, closes_at | PK (location, day) |
| location_closures | id, fulfillment_location_id, closed_on, reason | unique (location, closed_on) |

City activation is refused unless the city has an active fulfillment location, service area, pincode, slot template, price and stock item; the refusal lists what is missing.

### catalog
| Table | Columns | Constraints |
|---|---|---|
| categories | id, parent_id, slug, name, description, image_key, sort_order, is_active | unique slug |
| products | id, category_id, slug, name, local_names text[], species_scientific_name, description, source_type WILD/FARMED, source_region, storage_instructions, handling_notes, hsn_code, tax_category_code, status DRAFT/ACTIVE/ARCHIVED, featured_rank, published_at, search_vector (generated tsvector) | unique slug; GIN search_vector; GIN trigram on name and on local_names (immutable wrapper function) |
| product_images | id, product_id, storage_key, alt_text, width, height, sort_order | index (product_id, sort_order) |
| product_variants | id, product_id, sku, name, sell_unit KG/PIECE/PACK, stock_unit GRAM/PIECE, pack_size_grams, avg_piece_grams, min_order, max_order, order_step, is_weight_variable, is_active | unique sku |
| preparation_methods | id, code WHOLE/CLEANED/CURRY_CUT/STEAK/FILLET/..., name, description, sort_order | unique code |
| variant_preparations | variant_id, preparation_method_id, yield_bp, is_default, is_active | PK both; one default per variant |

**Net weight rule (decided):** customers order and pay for net weight after preparation. Stock is gross raw weight.
Reserve `ceil(net × 10000 / yield_bp)` grams (500 g curry cut at 6500 bp reserves 770 g).

### pricing
| Table | Columns | Constraints |
|---|---|---|
| variant_prices | id, fulfillment_location_id, variant_id, unit_price_paise, compare_at_paise, valid_from, valid_to | EXCLUDE gist (location =, variant =, tstzrange(valid_from, valid_to) &&) |
| preparation_charges | id, fulfillment_location_id, variant_id, preparation_method_id, charge_paise, charge_basis PER_KG/PER_ITEM/FLAT, valid_from, valid_to | same no-overlap rule |
| tax_rules | id, tax_category_code, rate_bp, valid_from, valid_to | no overlap per category |

Price per fulfillment location (decided). No price = not for sale there. A price change closes the current period and opens a new one. GST rates are data to be confirmed by the CA. Selling prices are tax-inclusive (B2C retail); `tax_paise` is the tax contained in the total, shown on the invoice.

### inventory
| Table | Columns | Constraints |
|---|---|---|
| stock_items | id, fulfillment_location_id, variant_id, stock_unit GRAM/PIECE, on_hand, reserved, low_stock_threshold | unique (location, variant); check on_hand >= 0, reserved >= 0, low_stock_threshold >= 0 (no reserved <= on_hand check, see decision D7) |
| batches | id, stock_item_id, fulfillment_location_id, variant_id, batch_code, supplier_name, source_landing_center, caught_or_harvested_on, received_at, received_quantity, remaining_quantity, remaining_cost_paise, best_before_at, receipt_temperature_c numeric(4,1), total_cost_paise, status ACTIVE/DEPLETED/EXPIRED/DISCARDED | unique (location, batch_code); index (variant_id, best_before_at) where ACTIVE; index (best_before_at) where ACTIVE; remaining_cost_paise is the cost still on the shelf, so the draining movement takes exactly the remainder |
| stock_movements | id, stock_item_id, batch_id (nullable: batchless PACK_SHORTFALL pair), kind RECEIPT/SALE/WASTE/ADJUSTMENT_IN/ADJUSTMENT_OUT/RETURN/COUNT_CORRECTION, quantity (signed), cost_paise (same sign as quantity: round_half_away(quantity × batch total_cost_paise / received_quantity), or minus the batch remaining_cost_paise when the movement drains it), reason_code (adds PACK_SHORTFALL, EXPIRED), notes, reference_type ORDER_ITEM/BATCH/MANUAL, reference_id, occurred_at, recorded_by | index (stock_item_id, occurred_at); index (reference_id) where SALE; append-only |
| stock_reservations | id, stock_item_id, order_id, order_item_id, quantity, status ACTIVE/CONSUMED/RELEASED/EXPIRED, expires_at | unique (order_item_id) where ACTIVE; index (order_id); index (expires_at) where ACTIVE |
| low_stock_digests | id, fulfillment_location_id, local_date, item_count | unique (location, local_date): one digest per location per local day |
| storage_units | code, min_c, max_c (numeric(4,1), nullable = open bound) | PK code; reference data `inventory/storage-units.json` (CHILLER, FREEZER, RECEIPT) |
| cold_chain_logs | id, fulfillment_location_id, storage_unit, temperature_c numeric(4,1), is_out_of_range, recorded_at, recorded_by | FK storage_unit → storage_units; index (location, recorded_at desc) |

Cost is kept as a batch total (₹845/kg is 84.5 paise per gram, so a per-gram integer would lose precision).
Cart and order quantities are in the variant's stock unit: grams for KG variants, pieces for PIECE, pack count for PACK.

Waste reasons: SPOILED, TEMPERATURE_BREACH, DAMAGED, EXPIRED, QC_REJECTED. Sale and waste pick batches FEFO.

### cart
| Table | Columns | Constraints |
|---|---|---|
| carts | id, customer_id, guest_token_hash, fulfillment_location_id, pincode, coupon_code, status ACTIVE/CONVERTED/ABANDONED, expires_at | unique (customer_id) where ACTIVE |
| cart_items | id, cart_id, variant_id, preparation_method_id, quantity, added_at | unique (cart_id, variant_id, preparation_method_id) |

No prices are stored; the cart is priced on every read. Guest carts are allowed; checkout needs login (decided).

### promotions
| Table | Columns | Constraints |
|---|---|---|
| coupons | id, code citext, description, discount_type PERCENT/FLAT/FREE_DELIVERY, discount_value, max_discount_paise, min_order_paise, city_id, first_order_only, per_customer_limit, total_limit, used_count, valid_from, valid_to, status ACTIVE/PAUSED/EXPIRED | unique code; check used_count <= total_limit |
| coupon_scopes | coupon_id, scope_type CATEGORY/PRODUCT, scope_id | PK all |
| coupon_redemptions | id, coupon_id, customer_id, order_id, discount_paise, status APPLIED/REVERSED | unique order_id; index (coupon_id, customer_id) |
| promotions | id, title, subtitle, image_key, link_type CATEGORY/PRODUCT/COUPON, link_target, city_id, starts_at, ends_at, sort_order, is_active | |

### ordering
| Table | Columns | Constraints |
|---|---|---|
| orders | id, order_number, customer_id, city_id, fulfillment_location_id, service_area_id, status, payment_method COD/UPI_ON_DELIVERY/ONLINE, address snapshot (recipient_name, phone, line1, line2, landmark, locality, city_name, state_code, pincode, latitude, longitude), delivery_slot_id, slot_start_at, slot_end_at, substitution_preference CALL_ME/AUTO_SIMILAR/NONE, items_paise, preparation_paise, discount_paise, delivery_fee_paise, tax_paise, total_paise, final_total_paise, amount_due_paise, coupon_code, customer_notes, placed_at, confirmed_at, packed_at, delivered_at, cancelled_at, cancel_reason_code, idempotency_key | unique order_number; unique (customer_id, idempotency_key); index (fulfillment_location_id, status, slot_start_at); index (customer_id, placed_at desc) |
| order_items | id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, image_key, hsn_code, preparation_code, preparation_name, sell_unit, unit_price_paise, preparation_charge_paise, charge_basis, tax_rate_bp, yield_bp, ordered_quantity, estimated_total_paise, max_chargeable_quantity, packed_quantity, final_total_paise, status ACTIVE/SHORT_SUPPLIED/SUBSTITUTED/CANCELLED | unique (order_id, line_no) |
| order_status_history | id, order_id, from_status, to_status, actor_type CUSTOMER/STAFF/SYSTEM, actor_id, reason, changed_at | index (order_id, changed_at) |
| order_adjustments | id, order_id, order_item_id, kind WEIGHT/SHORT_SUPPLY/SUBSTITUTION/GOODWILL, amount_paise (signed), reason, created_by | index order_id |
| substitutions | id, order_item_id, proposed_variant_id, proposed_name, proposed_quantity, price_difference_paise, status PROPOSED/ACCEPTED/REJECTED/EXPIRED, expires_at, responded_at | unique (order_item_id) where PROPOSED |
| complaints | id, order_id, order_item_id, kind MISSING/DAMAGED/QUALITY/WRONG_ITEM/LATE, description, status OPEN/APPROVED/REJECTED/RESOLVED, resolution REFUND/REPLACEMENT/NONE, resolved_by, resolved_at | index (status, created_at) |
| complaint_attachments | id, complaint_id, storage_key | |
| invoices | id, order_id, invoice_number, issued_at, seller_gstin, taxable_paise, tax_paise, total_paise, pdf_storage_key | unique order_id; unique invoice_number |
| invoice_lines | id, invoice_id, line_no, description, hsn_code, quantity, unit_price_paise, taxable_paise, tax_rate_bp, tax_paise, total_paise | frozen |

Weight rule (decided): charge actual packed net weight up to `max_chargeable_quantity` = ordered × (1 + `order_policies.max_overweight_bp`, default +10%); any shortfall reduces the amount due (COD) or creates a refund (prepaid). GST invoice is issued at delivery from final amounts (decided).

### payments
| Table | Columns | Constraints |
|---|---|---|
| payments | id, order_id, method, provider CASH/FAKE/RAZORPAY/..., amount_paise, status PENDING/AUTHORIZED/CAPTURED/COLLECTED/FAILED/CANCELLED, provider_order_id, provider_payment_id, collected_by_rider_id, collected_at, failure_code, failure_message | unique (order_id) where status in (CAPTURED, COLLECTED); unique provider_payment_id |
| refunds | id, payment_id, order_id, amount_paise, reason_code WEIGHT_SHORTFALL/CANCELLATION/COMPLAINT/SHORT_SUPPLY, method ORIGINAL/UPI/BANK_TRANSFER/CASH, status REQUESTED/APPROVED/PROCESSING/SUCCEEDED/FAILED, provider_refund_id, idempotency_key, requested_by, approved_by | unique idempotency_key; total refunded <= paid (checked in Payment aggregate) |
| webhook_events | id, provider, provider_event_id, event_type, payload jsonb, signature_valid, received_at, processed_at, error | unique (provider, provider_event_id) |
| cash_settlements | id, rider_id, settlement_date, expected_paise, received_paise, difference_paise, status OPEN/SETTLED/DISPUTED, settled_by | unique (rider_id, settlement_date) |

### delivery
| Table | Columns | Constraints |
|---|---|---|
| slot_templates | id, fulfillment_location_id, day_of_week 1–7 (ISO), start_time, end_time, capacity, cutoff_minutes, is_active, xmin | check day_of_week 1–7, end_time > start_time, capacity 1–500, cutoff_minutes 0–2880; `ex_slot_templates_no_overlap` EXCLUDE USING gist (location =, day_of_week =, tsrange(start,end) &&) WHERE is_active (D22) |
| delivery_slots | id, fulfillment_location_id, slot_template_id (nullable), slot_date (city-local), start_at, end_at, cutoff_at, capacity, booked_count, status OPEN/CLOSED, closed_reason MANUAL/CLOSURE/TEMPLATE_INACTIVE, xmin | unique (location, start_at); check 0 <= booked_count <= capacity, capacity 0–500; index (location, slot_date) |
| riders | id, fulfillment_location_id, full_name, phone, is_active, xmin | unique phone where is_active |
| shipments | id, order_id, fulfillment_location_id, delivery_slot_id, rider_id, status PENDING/ASSIGNED/OUT_FOR_DELIVERY/DELIVERED/FAILED/RETURNED/CANCELLED, placed_at, packed_at, cold_box_used, dispatch_temperature_c numeric(4,1), temperature_warning, dispatched_at, delivered_at, returned_at, cancelled_at, delivery_code character(4), delivery_code_failures, cash_collected_paise, cash_mode CASH/UPI, upi_reference, cash_collected_at, xmin | unique order_id; FK delivery_slot_id, rider_id; check status, cash_mode, delivery_code_failures >= 0, cash_collected_paise >= 0; index (rider_id, status), (location, status); delivery code shown to the customer only while OUT_FOR_DELIVERY |
| delivery_attempts | id, shipment_id, attempt_no, outcome DELIVERED/CUSTOMER_UNAVAILABLE/ADDRESS_NOT_FOUND/REFUSED/OTHER, notes, attempted_at | unique (shipment_id, attempt_no) |

A job generates slots from templates 7 days ahead, skipping closures.

### notifications, audit, settings, platform
| Table | Columns | Notes |
|---|---|---|
| notifications.templates | id, code, channel SMS/EMAIL/PUSH/IN_APP, subject, body, is_active | unique (code, channel) |
| notifications.messages | id, user_id, channel, template_code, payload jsonb, status QUEUED/SENT/FAILED/READ, attempts, provider_message_id, sent_at, read_at | index (user_id, created_at desc) |
| notifications.device_tokens | id, user_id, push_token, provider FCM, platform ANDROID/IOS/WEB, last_seen_at | unique push_token |
| notifications.preferences | user_id, category ORDER_UPDATES/OFFERS, channel, enabled | PK (user_id, category, channel) |
| audit.entries | id bigint, occurred_at, actor_type, actor_id, actor_name, action, entity_type, entity_id, before jsonb, after jsonb, ip, correlation_id | append-only; index (entity_type, entity_id), (occurred_at) |
| settings.business_profile | id, legal_name, trade_name, gstin, fssai_license_no, address, support_phone, support_email | single row |
| settings.order_policies | id, city_id (null = default), reservation_minutes, max_overweight_bp, cancel_allowed_until_status, cod_max_order_paise | unique city_id (nulls not distinct) |
| settings.number_series | code ORDER/INVOICE, period, prefix, next_value | PK (code, period); row locked and incremented in the caller's transaction |
| platform.outbox | id, event_type, payload jsonb, occurred_at, correlation_id, processed_at, attempts, error | index (occurred_at) where processed_at is null |
| platform.inbox | event_id, handler, processed_at | PK both |
| platform.dead_letters | id, event_id, handler, error, failed_at | |
| platform.idempotency_keys | scope, key, request_hash, status_code, response jsonb, expires_at | PK (scope, key) |
| platform.sample_records | table_name, record_id | tracks sample data for purge |

### catalog storefront view
`catalog.storefront_offers` (SQL view, created in a migration): active product + active variant + current price + preparation charges + available stock per fulfillment location. Catalog's storefront reads query this view; it is the only cross-schema read outside Reporting.

### reporting (views only)
`daily_sales`, `product_performance`, `contribution_margin` (revenue − SALE movement cost − refunds), `waste_summary`, `repeat_customers`; materialized, refreshed by a Worker job every 15 minutes.

## 4. Transaction boundaries

| Action | One transaction | Protection |
|---|---|---|
| Place order | re-read prices → reserve stock → book slot → use coupon → order + items + history → order number → pending payment → outbox | aggregates (StockItem, DeliverySlot, Coupon) check availability and are saved with their `xmin` row version in the one `SaveChangesAsync`; a lost race throws `ConcurrentSaveException` and the whole checkout is retried (max 3) from Read; DB check constraints (`reserved <= on_hand`, `booked_count <= capacity`, `used_count <= total_limit`) are the last line; Idempotency-Key |
| Payment callback | webhook_events insert → payment → order status → outbox | unique provider_event_id |
| Weigh and pack | packed qty → capped final price → adjustments → consume reservation → SALE movements FEFO with cost → on_hand → totals → outbox | order row version; refund created by Payments handler |
| Cancel | status → release reservations → slot booked_count − 1 → reverse coupon → outbox | only before policy status |
| Refund | lock payment → check refunded ≤ paid → refund REQUESTED | provider called by Worker outside the transaction with the idempotency key |
| Receive stock | batch → RECEIPT movement → on_hand | stock item row version |
| Expire reservations (job) | release stock + slot → PAYMENT_FAILED | runs only when expired rows exist |
| Issue invoice (delivery) | lock number_series → invoice + lines → DELIVERED | gapless numbers |

Isolation: Read Committed. Decision D6 in `docs/decisions/log.md` moves pack, cancel and invoice follow-ons into event handlers.

## 5. Seed data

| Kind | Where | Rule |
|---|---|---|
| Reference (real) | `backend/data/reference/*.json` | roles, permissions, preparation methods, notification templates, tax categories; loaded every deploy, idempotent |
| Sample | `backend/data/sample/*.json` | one sample city, location, 3 service areas, ~25 products, prices, batches, slots, demo customers; `--sample` only; refused in Production; tracked in `platform.sample_records`; `--purge-sample` removes them |
