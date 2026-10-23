-- REP-R5 contribution_margin: one row per (business_date, fulfillment_location_id).
-- Revenue ex tax and SALE cost of orders delivered that date; refunds = post-delivery refunds (COMPLAINT, GOODWILL on
-- delivered orders) by refund date; delivery cost 0 until a source exists (Q4). Shortfall / cancellation refunds and
-- SALE cost of orders cancelled after packing are excluded.
CREATE MATERIALIZED VIEW IF NOT EXISTS reporting.contribution_margin AS
WITH loc AS (
    SELECT fl.id AS fulfillment_location_id, c.time_zone
    FROM locations.fulfillment_locations fl
    JOIN locations.cities c ON c.id = fl.city_id
),
order_cost AS (
    SELECT i.order_id, sum(abs(m.cost_paise)) AS cost_paise
    FROM inventory.stock_movements m
    JOIN ordering.order_items i ON i.id = m.reference_id
    WHERE m.kind = 'SALE' AND m.reference_type = 'ORDER_ITEM'
    GROUP BY i.order_id
),
delivered AS (
    SELECT (o.delivered_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           sum(coalesce(o.final_total_paise, o.total_paise) - o.tax_paise) AS revenue_ex_tax_paise,
           sum(coalesce(oc.cost_paise, 0)) AS sale_cost_paise
    FROM ordering.orders o
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    LEFT JOIN order_cost oc ON oc.order_id = o.id
    WHERE o.status = 'DELIVERED' AND o.delivered_at IS NOT NULL
    GROUP BY 1, 2
),
refunded AS (
    SELECT (r.succeeded_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           sum(r.amount_paise) AS refunds_paise
    FROM payments.refunds r
    JOIN ordering.orders o ON o.id = r.order_id
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    WHERE r.status = 'SUCCEEDED' AND r.succeeded_at IS NOT NULL
      AND r.reason_code IN ('COMPLAINT', 'GOODWILL') AND o.status = 'DELIVERED'
    GROUP BY 1, 2
),
keys AS (
    SELECT business_date, fulfillment_location_id FROM delivered
    UNION SELECT business_date, fulfillment_location_id FROM refunded
),
totals AS (
    SELECT k.business_date,
           k.fulfillment_location_id,
           coalesce(d.revenue_ex_tax_paise, 0)::bigint AS revenue_ex_tax_paise,
           coalesce(d.sale_cost_paise, 0)::bigint AS sale_cost_paise,
           coalesce(r.refunds_paise, 0)::bigint AS refunds_paise,
           0::bigint AS delivery_cost_paise
    FROM keys k
    LEFT JOIN delivered d ON d.business_date = k.business_date AND d.fulfillment_location_id = k.fulfillment_location_id
    LEFT JOIN refunded r ON r.business_date = k.business_date AND r.fulfillment_location_id = k.fulfillment_location_id
)
SELECT t.business_date,
       t.fulfillment_location_id,
       t.revenue_ex_tax_paise,
       t.sale_cost_paise,
       t.refunds_paise,
       t.delivery_cost_paise,
       (t.revenue_ex_tax_paise - t.sale_cost_paise - t.refunds_paise - t.delivery_cost_paise)::bigint AS contribution_paise,
       CASE WHEN t.revenue_ex_tax_paise = 0 THEN NULL
            ELSE trunc((t.revenue_ex_tax_paise - t.sale_cost_paise - t.refunds_paise - t.delivery_cost_paise)::numeric * 10000
                       / t.revenue_ex_tax_paise)::integer END AS contribution_margin_bp
FROM totals t
-- next
CREATE UNIQUE INDEX IF NOT EXISTS ux_contribution_margin_grain ON reporting.contribution_margin (business_date, fulfillment_location_id)
