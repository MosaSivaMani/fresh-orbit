-- REP-R6 waste_summary: one row per (business_date, fulfillment_location_id, variant_id, reason_code).
-- Dated by movement occurred_at at the location; sold quantity = SALE quantity of the same day / location / variant.
CREATE MATERIALIZED VIEW IF NOT EXISTS reporting.waste_summary AS
WITH loc AS (
    SELECT fl.id AS fulfillment_location_id, c.time_zone
    FROM locations.fulfillment_locations fl
    JOIN locations.cities c ON c.id = fl.city_id
),
moves AS (
    SELECT (m.occurred_at AT TIME ZONE l.time_zone)::date AS business_date,
           si.fulfillment_location_id, si.variant_id, m.kind, m.reason_code, m.quantity, m.cost_paise
    FROM inventory.stock_movements m
    JOIN inventory.stock_items si ON si.id = m.stock_item_id
    JOIN loc l ON l.fulfillment_location_id = si.fulfillment_location_id
    WHERE m.kind IN ('WASTE', 'SALE')
),
waste AS (
    SELECT business_date, fulfillment_location_id, variant_id, coalesce(reason_code, 'UNSPECIFIED') AS reason_code,
           count(*) AS movements_count,
           sum(abs(quantity)) AS waste_quantity,
           sum(abs(cost_paise)) AS waste_cost_paise
    FROM moves
    WHERE kind = 'WASTE'
    GROUP BY 1, 2, 3, 4
),
sold AS (
    SELECT business_date, fulfillment_location_id, variant_id, sum(abs(quantity)) AS sold_quantity
    FROM moves
    WHERE kind = 'SALE'
    GROUP BY 1, 2, 3
)
SELECT w.business_date,
       w.fulfillment_location_id,
       p.id AS product_id,
       w.variant_id,
       p.name AS product_name,
       v.name AS variant_name,
       v.stock_unit,
       w.reason_code,
       w.movements_count::integer AS movements_count,
       w.waste_quantity::bigint AS waste_quantity,
       w.waste_cost_paise::bigint AS waste_cost_paise,
       coalesce(s.sold_quantity, 0)::bigint AS sold_quantity,
       CASE WHEN w.waste_quantity + coalesce(s.sold_quantity, 0) = 0 THEN NULL
            ELSE trunc(w.waste_quantity::numeric * 10000 / (w.waste_quantity + coalesce(s.sold_quantity, 0)))::integer END AS waste_share_bp
FROM waste w
LEFT JOIN sold s ON s.business_date = w.business_date AND s.fulfillment_location_id = w.fulfillment_location_id AND s.variant_id = w.variant_id
LEFT JOIN catalog.product_variants v ON v.id = w.variant_id
LEFT JOIN catalog.products p ON p.id = v.product_id
-- next
CREATE UNIQUE INDEX IF NOT EXISTS ux_waste_summary_grain
    ON reporting.waste_summary (business_date, fulfillment_location_id, variant_id, reason_code)
