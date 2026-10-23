-- REP-R4 product_performance: one row per (business_date, fulfillment_location_id, variant_id, preparation_code).
-- Delivered orders only, dated by delivered_at; names from the order snapshot; SALE cost linked per line (Q3:
-- reference_type = 'ORDER_ITEM', reference_id = order_items.id).
CREATE MATERIALIZED VIEW IF NOT EXISTS reporting.product_performance AS
WITH loc AS (
    SELECT fl.id AS fulfillment_location_id, c.time_zone
    FROM locations.fulfillment_locations fl
    JOIN locations.cities c ON c.id = fl.city_id
),
sale_cost AS (
    SELECT m.reference_id AS order_item_id, sum(abs(m.cost_paise)) AS cost_paise
    FROM inventory.stock_movements m
    WHERE m.kind = 'SALE' AND m.reference_type = 'ORDER_ITEM'
    GROUP BY m.reference_id
)
SELECT (o.delivered_at AT TIME ZONE l.time_zone)::date AS business_date,
       o.fulfillment_location_id,
       max(i.product_id::text)::uuid AS product_id,
       i.variant_id,
       max(i.sku) AS sku,
       max(i.product_name) AS product_name,
       max(i.variant_name) AS variant_name,
       coalesce(i.preparation_code, 'NONE') AS preparation_code,
       max(v.stock_unit) AS stock_unit,
       count(DISTINCT o.id)::integer AS orders_count,
       (count(*) FILTER (WHERE i.status = 'SHORT_SUPPLIED'))::integer AS lines_short_supplied,
       sum(i.ordered_quantity)::bigint AS ordered_quantity,
       sum(coalesce(i.packed_quantity, 0))::bigint AS packed_quantity,
       sum(least(coalesce(i.packed_quantity, 0), coalesce(i.max_chargeable_quantity, i.packed_quantity, 0)))::bigint AS charged_quantity,
       sum(coalesce(i.final_total_paise, 0))::bigint AS revenue_paise,
       sum(coalesce(s.cost_paise, 0))::bigint AS sale_cost_paise,
       (sum(coalesce(i.final_total_paise, 0)) - sum(coalesce(s.cost_paise, 0)))::bigint AS gross_margin_paise
FROM ordering.orders o
JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
JOIN ordering.order_items i ON i.order_id = o.id
LEFT JOIN sale_cost s ON s.order_item_id = i.id
LEFT JOIN catalog.product_variants v ON v.id = i.variant_id
WHERE o.status = 'DELIVERED' AND o.delivered_at IS NOT NULL
GROUP BY 1, 2, i.variant_id, coalesce(i.preparation_code, 'NONE')
-- next
CREATE UNIQUE INDEX IF NOT EXISTS ux_product_performance_grain
    ON reporting.product_performance (business_date, fulfillment_location_id, variant_id, preparation_code)
