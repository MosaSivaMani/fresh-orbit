-- REP-R3 daily_sales: one row per (business_date, fulfillment_location_id).
-- Business date = local date at the location (REP-R1); revenue from DELIVERED orders by delivered_at (REP-R2).
CREATE MATERIALIZED VIEW IF NOT EXISTS reporting.daily_sales AS
WITH loc AS (
    SELECT fl.id AS fulfillment_location_id, fl.city_id, c.time_zone
    FROM locations.fulfillment_locations fl
    JOIN locations.cities c ON c.id = fl.city_id
),
placed AS (
    SELECT (o.placed_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           count(*) AS orders_placed
    FROM ordering.orders o
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    WHERE o.placed_at IS NOT NULL AND o.status <> 'PAYMENT_FAILED'
    GROUP BY 1, 2
),
delivered AS (
    SELECT (o.delivered_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           count(*) AS orders_delivered,
           sum(coalesce(items.gross_paise, 0)) AS gross_paise,
           sum(o.discount_paise) AS discount_paise,
           sum(o.delivery_fee_paise) AS delivery_fee_paise,
           sum(o.tax_paise) AS tax_paise,
           sum(coalesce(o.final_total_paise, o.total_paise)) AS net_revenue_paise
    FROM ordering.orders o
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    LEFT JOIN (
        SELECT i.order_id, sum(coalesce(i.final_total_paise, 0)) AS gross_paise
        FROM ordering.order_items i
        GROUP BY i.order_id
    ) items ON items.order_id = o.id
    WHERE o.status = 'DELIVERED' AND o.delivered_at IS NOT NULL
    GROUP BY 1, 2
),
cancelled AS (
    SELECT (o.cancelled_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           count(*) AS orders_cancelled
    FROM ordering.orders o
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    WHERE o.status = 'CANCELLED' AND o.cancelled_at IS NOT NULL
    GROUP BY 1, 2
),
refunded AS (
    SELECT (r.succeeded_at AT TIME ZONE l.time_zone)::date AS business_date, o.fulfillment_location_id,
           sum(r.amount_paise) AS refunds_paise,
           sum(r.amount_paise) FILTER (
               WHERE r.reason_code IN ('COMPLAINT', 'GOODWILL') AND o.status = 'DELIVERED') AS post_delivery_refunds_paise
    FROM payments.refunds r
    JOIN ordering.orders o ON o.id = r.order_id
    JOIN loc l ON l.fulfillment_location_id = o.fulfillment_location_id
    WHERE r.status = 'SUCCEEDED' AND r.succeeded_at IS NOT NULL
    GROUP BY 1, 2
),
keys AS (
    SELECT business_date, fulfillment_location_id FROM placed
    UNION SELECT business_date, fulfillment_location_id FROM delivered
    UNION SELECT business_date, fulfillment_location_id FROM cancelled
    UNION SELECT business_date, fulfillment_location_id FROM refunded
)
SELECT k.business_date,
       l.city_id,
       k.fulfillment_location_id,
       coalesce(p.orders_placed, 0)::integer AS orders_placed,
       coalesce(d.orders_delivered, 0)::integer AS orders_delivered,
       coalesce(c.orders_cancelled, 0)::integer AS orders_cancelled,
       coalesce(d.gross_paise, 0)::bigint AS gross_paise,
       coalesce(d.discount_paise, 0)::bigint AS discount_paise,
       coalesce(d.delivery_fee_paise, 0)::bigint AS delivery_fee_paise,
       coalesce(d.tax_paise, 0)::bigint AS tax_paise,
       coalesce(d.net_revenue_paise, 0)::bigint AS net_revenue_paise,
       coalesce(r.refunds_paise, 0)::bigint AS refunds_paise,
       coalesce(r.post_delivery_refunds_paise, 0)::bigint AS post_delivery_refunds_paise,
       CASE WHEN coalesce(d.orders_delivered, 0) = 0 THEN NULL
            ELSE (d.net_revenue_paise / d.orders_delivered)::bigint END AS avg_order_value_paise
FROM keys k
JOIN loc l ON l.fulfillment_location_id = k.fulfillment_location_id
LEFT JOIN placed p ON p.business_date = k.business_date AND p.fulfillment_location_id = k.fulfillment_location_id
LEFT JOIN delivered d ON d.business_date = k.business_date AND d.fulfillment_location_id = k.fulfillment_location_id
LEFT JOIN cancelled c ON c.business_date = k.business_date AND c.fulfillment_location_id = k.fulfillment_location_id
LEFT JOIN refunded r ON r.business_date = k.business_date AND r.fulfillment_location_id = k.fulfillment_location_id
-- next
CREATE UNIQUE INDEX IF NOT EXISTS ux_daily_sales_grain ON reporting.daily_sales (business_date, fulfillment_location_id)
