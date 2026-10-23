namespace FreshOrbit.Reporting.Tests;

/// <summary>
/// The seeded fixture of docs/product/reporting.md §6 (city Asia/Kolkata, location L1, business date 2026-10-23) plus
/// one delivered order at L2 for scoping. Order B is delivered at 23:30 IST (18:00 UTC) to cover T3.
/// </summary>
public static class ReportingDataset
{
    public static readonly Guid City = new("0199e000-0000-7000-8000-000000000001");
    public static readonly Guid L1 = new("0199e000-0000-7000-8000-0000000000a1");
    public static readonly Guid L2 = new("0199e000-0000-7000-8000-0000000000a2");
    public static readonly Guid CustomerX = new("0199e000-0000-7000-8000-0000000000c1");
    public static readonly Guid CustomerY = new("0199e000-0000-7000-8000-0000000000c2");
    public static readonly Guid SeerProduct = new("0199e000-0000-7000-8000-000000000101");
    public static readonly Guid SeerVariant = new("0199e000-0000-7000-8000-000000000201");
    public static readonly Guid PrawnProduct = new("0199e000-0000-7000-8000-000000000102");
    public static readonly Guid PrawnVariant = new("0199e000-0000-7000-8000-000000000202");
    public static readonly Guid CrabProduct = new("0199e000-0000-7000-8000-000000000103");
    public static readonly Guid CrabVariant = new("0199e000-0000-7000-8000-000000000203");
    public static readonly Guid Category = new("0199e000-0000-7000-8000-000000000301");

    public static readonly string SeedSql = $"""
        INSERT INTO locations.cities (id, code, name, time_zone) VALUES ('{City}', 'KOC', 'Kochi', 'Asia/Kolkata');
        INSERT INTO locations.fulfillment_locations (id, city_id, code, name) VALUES
            ('{L1}', '{City}', 'L1', 'Kochi Hub'),
            ('{L2}', '{City}', 'L2', 'Kakkanad Hub');
        INSERT INTO catalog.products (id, category_id, name) VALUES
            ('{SeerProduct}', '{Category}', 'Seer Fish'),
            ('{PrawnProduct}', NULL, 'Tiger Prawns'),
            ('{CrabProduct}', NULL, 'Mud Crab');
        INSERT INTO catalog.product_variants (id, product_id, sku, name, stock_unit) VALUES
            ('{SeerVariant}', '{SeerProduct}', 'SEER-KG', 'Seer per kg', 'GRAM'),
            ('{PrawnVariant}', '{PrawnProduct}', 'PRAWN-KG', 'Prawns per kg', 'GRAM'),
            ('{CrabVariant}', '{CrabProduct}', 'CRAB-PC', 'Crab per piece', 'PIECE');
        INSERT INTO inventory.stock_items (id, fulfillment_location_id, variant_id, on_hand, reserved, low_stock_threshold) VALUES
            ('0199e000-0000-7000-8000-000000000401', '{L1}', '{SeerVariant}', 22085, 0, 2000),
            ('0199e000-0000-7000-8000-000000000402', '{L1}', '{PrawnVariant}', 1500, 600, 2000),
            ('0199e000-0000-7000-8000-000000000403', '{L1}', '{CrabVariant}', 10, 0, 2),
            ('0199e000-0000-7000-8000-000000000404', '{L2}', '{SeerVariant}', 100, 0, 2000);

        -- Order A (FO-260001, COD, customer X): delivered 2026-10-23 14:00 IST.
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method, slot_start_at,
            items_paise, preparation_paise, discount_paise, delivery_fee_paise, tax_paise, total_paise, final_total_paise, placed_at, delivered_at)
        VALUES ('0199e000-0000-7000-8000-00000000a000', 'FO-260001', '{CustomerX}', '{City}', '{L1}', 'DELIVERED', 'COD', '2026-10-23T07:30:00Z',
            249591, 5460, 10000, 0, 0, 255000, 245051, '2026-10-23T04:30:00Z', '2026-10-23T08:30:00Z');
        INSERT INTO ordering.order_items (id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, preparation_code,
            ordered_quantity, max_chargeable_quantity, packed_quantity, final_total_paise) VALUES
            ('0199e000-0000-7000-8000-00000000a001', '0199e000-0000-7000-8000-00000000a000', 1, '{SeerProduct}', '{SeerVariant}', 'Seer Fish', 'Seer per kg', 'SEER-KG', 'STEAK', 500, 550, 540, 72306),
            ('0199e000-0000-7000-8000-00000000a002', '0199e000-0000-7000-8000-00000000a000', 2, '{PrawnProduct}', '{PrawnVariant}', 'Tiger Prawns', 'Prawns per kg', 'PRAWN-KG', 'CLEANED', 500, 550, 580, 52745),
            ('0199e000-0000-7000-8000-00000000a003', '0199e000-0000-7000-8000-00000000a000', 3, '{CrabProduct}', '{CrabVariant}', 'Mud Crab', 'Crab per piece', 'CRAB-PC', 'WHOLE', 2, 2, 2, 130000);

        -- Order B (ONLINE, customer Y): seer short by weight, delivered 2026-10-23 23:30 IST (18:00 UTC).
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method, slot_start_at,
            items_paise, preparation_paise, discount_paise, delivery_fee_paise, tax_paise, total_paise, final_total_paise, placed_at, delivered_at)
        VALUES ('0199e000-0000-7000-8000-00000000b000', 'FO-260002', '{CustomerY}', '{City}', '{L1}', 'DELIVERED', 'ONLINE', '2026-10-23T16:30:00Z',
            64950, 2000, 0, 0, 0, 66950, 60255, '2026-10-23T05:00:00Z', '2026-10-23T18:00:00Z');
        INSERT INTO ordering.order_items (id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, preparation_code,
            ordered_quantity, max_chargeable_quantity, packed_quantity, final_total_paise) VALUES
            ('0199e000-0000-7000-8000-00000000b001', '0199e000-0000-7000-8000-00000000b000', 1, '{SeerProduct}', '{SeerVariant}', 'Seer Fish', 'Seer per kg', 'SEER-KG', 'STEAK', 500, 550, 450, 60255);

        -- Order C (ONLINE) captured then cancelled at CONFIRMED; Order D (COD) placed and cancelled.
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method, slot_start_at,
            total_paise, final_total_paise, placed_at, cancelled_at) VALUES
            ('0199e000-0000-7000-8000-00000000c000', 'FO-260003', '0199e000-0000-7000-8000-0000000000c3', '{City}', '{L1}', 'CANCELLED', 'ONLINE', '2026-10-23T12:30:00Z', 47950, NULL, '2026-10-23T05:30:00Z', '2026-10-23T06:00:00Z'),
            ('0199e000-0000-7000-8000-00000000d000', 'FO-260004', '0199e000-0000-7000-8000-0000000000c4', '{City}', '{L1}', 'CANCELLED', 'COD', '2026-10-23T12:30:00Z', 30000, NULL, '2026-10-23T06:30:00Z', '2026-10-23T07:00:00Z');

        -- Customer X history: delivered 2026-09-10.
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method,
            total_paise, final_total_paise, placed_at, delivered_at) VALUES
            ('0199e000-0000-7000-8000-00000000e000', 'FO-250999', '{CustomerX}', '{City}', '{L1}', 'DELIVERED', 'COD', 50000, 50000, '2026-09-10T04:00:00Z', '2026-09-10T09:00:00Z');
        INSERT INTO ordering.order_items (id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, preparation_code,
            ordered_quantity, max_chargeable_quantity, packed_quantity, final_total_paise) VALUES
            ('0199e000-0000-7000-8000-00000000e001', '0199e000-0000-7000-8000-00000000e000', 1, '{SeerProduct}', '{SeerVariant}', 'Seer Fish', 'Seer per kg', 'SEER-KG', 'CURRY_CUT', 500, 550, 500, 50000);

        -- Order F at L2 (scoping): delivered 2026-10-23; its name tests CSV formula neutralisation.
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method,
            total_paise, final_total_paise, placed_at, delivered_at) VALUES
            ('0199e000-0000-7000-8000-00000000f000', 'FO-260005', '0199e000-0000-7000-8000-0000000000c5', '{City}', '{L2}', 'DELIVERED', 'COD', 99999, 99999, '2026-10-23T04:00:00Z', '2026-10-23T09:00:00Z');
        INSERT INTO ordering.order_items (id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, preparation_code,
            ordered_quantity, max_chargeable_quantity, packed_quantity, final_total_paise) VALUES
            ('0199e000-0000-7000-8000-00000000f001', '0199e000-0000-7000-8000-00000000f000', 1, '{SeerProduct}', '{SeerVariant}', '=HYPERLINK("http://x")', 'Seer per kg', 'SEER-KG', 'WHOLE', 1000, 1100, 1000, 99999);

        -- SALE movements per line (reference ORDER_ITEM) and the SEER waste.
        INSERT INTO inventory.stock_movements (id, stock_item_id, kind, quantity, cost_paise, reason_code, reference_type, reference_id, occurred_at) VALUES
            ('0199e000-0000-7000-8000-000000000501', '0199e000-0000-7000-8000-000000000401', 'SALE', -772, -67164, NULL, 'ORDER_ITEM', '0199e000-0000-7000-8000-00000000a001', '2026-10-23T06:00:00Z'),
            ('0199e000-0000-7000-8000-000000000502', '0199e000-0000-7000-8000-000000000402', 'SALE', -967, -58020, NULL, 'ORDER_ITEM', '0199e000-0000-7000-8000-00000000a002', '2026-10-23T06:00:00Z'),
            ('0199e000-0000-7000-8000-000000000503', '0199e000-0000-7000-8000-000000000403', 'SALE', -2, -80000, NULL, 'ORDER_ITEM', '0199e000-0000-7000-8000-00000000a003', '2026-10-23T06:00:00Z'),
            ('0199e000-0000-7000-8000-000000000504', '0199e000-0000-7000-8000-000000000401', 'SALE', -643, -55941, NULL, 'ORDER_ITEM', '0199e000-0000-7000-8000-00000000b001', '2026-10-23T10:00:00Z'),
            ('0199e000-0000-7000-8000-000000000505', '0199e000-0000-7000-8000-000000000401', 'WASTE', -1000, -87000, 'SPOILED', NULL, NULL, '2026-10-23T12:00:00Z');

        -- Refunds: B shortfall + complaint, C cancellation (all SUCCEEDED 2026-10-23 IST).
        INSERT INTO payments.refunds (id, order_id, fulfillment_location_id, amount_paise, reason_code, status, succeeded_at) VALUES
            ('0199e000-0000-7000-8000-000000000601', '0199e000-0000-7000-8000-00000000b000', '{L1}', 6695, 'WEIGHT_SHORTFALL', 'SUCCEEDED', '2026-10-23T11:00:00Z'),
            ('0199e000-0000-7000-8000-000000000602', '0199e000-0000-7000-8000-00000000b000', '{L1}', 5000, 'COMPLAINT', 'SUCCEEDED', '2026-10-23T17:00:00Z'),
            ('0199e000-0000-7000-8000-000000000603', '0199e000-0000-7000-8000-00000000c000', '{L1}', 47950, 'CANCELLATION', 'SUCCEEDED', '2026-10-23T06:30:00Z'),
            ('0199e000-0000-7000-8000-000000000604', '0199e000-0000-7000-8000-00000000a000', '{L1}', 1500, 'GOODWILL', 'REQUESTED', NULL);

        INSERT INTO ordering.complaints (id, order_id, status) VALUES
            ('0199e000-0000-7000-8000-000000000701', '0199e000-0000-7000-8000-00000000a000', 'OPEN');
        INSERT INTO payments.reconciliation_issues (id, kind, status, fulfillment_location_id) VALUES
            ('0199e000-0000-7000-8000-000000000801', 'CASH_DIFFERENCE', 'OPEN', '{L1}'),
            ('0199e000-0000-7000-8000-000000000802', 'WEBHOOK_ERROR', 'OPEN', NULL);
        """;
}
