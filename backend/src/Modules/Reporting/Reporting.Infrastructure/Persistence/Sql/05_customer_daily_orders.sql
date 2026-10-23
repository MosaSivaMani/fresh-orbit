-- REP-R7 customer_daily_orders (backs repeat-customers): one row per (business_date, fulfillment_location_id, customer_id).
CREATE MATERIALIZED VIEW IF NOT EXISTS reporting.customer_daily_orders AS
SELECT (o.delivered_at AT TIME ZONE c.time_zone)::date AS business_date,
       o.fulfillment_location_id,
       fl.city_id,
       o.customer_id,
       count(*)::integer AS delivered_orders,
       sum(coalesce(o.final_total_paise, o.total_paise))::bigint AS net_revenue_paise
FROM ordering.orders o
JOIN locations.fulfillment_locations fl ON fl.id = o.fulfillment_location_id
JOIN locations.cities c ON c.id = fl.city_id
WHERE o.status = 'DELIVERED' AND o.delivered_at IS NOT NULL
GROUP BY 1, 2, 3, 4
-- next
CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_daily_orders_grain
    ON reporting.customer_daily_orders (business_date, fulfillment_location_id, customer_id)
-- next
CREATE INDEX IF NOT EXISTS ix_customer_daily_orders_customer ON reporting.customer_daily_orders (customer_id, business_date)
