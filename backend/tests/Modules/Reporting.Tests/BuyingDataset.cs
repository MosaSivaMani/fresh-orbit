using static FreshOrbit.Reporting.Tests.ReportingDataset;

namespace FreshOrbit.Reporting.Tests;

/// <summary>
/// Buying list and "today's prices" fixture (REP-R14, REP-R15) at its own location L3, so the report fixture of
/// <see cref="ReportingDataset"/> is untouched: no order here is delivered or placed on 2026-10-23, and every stock
/// item keeps on hand above reserved (no extra low-stock row on the dashboard).
/// Delivery date 2026-11-06 (IST): slot 07:00 IST = 01:30 UTC.
/// </summary>
public static class BuyingDataset
{
    public static readonly Guid L3 = new("0199e000-0000-7000-8000-0000000000a3");
    public static readonly Guid SeaFish = new("0199e000-0000-7000-8000-000000000302");
    public static readonly Guid PomfretProduct = new("0199e000-0000-7000-8000-000000001101");
    public static readonly Guid PomfretVariant = new("0199e000-0000-7000-8000-000000001201");
    public static readonly Guid SeerPackProduct = new("0199e000-0000-7000-8000-000000001102");
    public static readonly Guid SeerPackVariant = new("0199e000-0000-7000-8000-000000001202");
    public static readonly Guid LobsterProduct = new("0199e000-0000-7000-8000-000000001103");
    public static readonly Guid LobsterVariant = new("0199e000-0000-7000-8000-000000001203");
    public static readonly Guid ArchivedVariant = new("0199e000-0000-7000-8000-000000001204");
    public static readonly Guid PomfretStock = new("0199e000-0000-7000-8000-000000001401");

    private const string CurryCut = "0199e000-0000-7000-8000-000000001501";
    private const string Whole = "0199e000-0000-7000-8000-000000001502";
    private const string Steak = "0199e000-0000-7000-8000-000000001503";
    private const string LobsterStock = "0199e000-0000-7000-8000-000000001402";

    public static readonly string SeedSql = $"""
        INSERT INTO locations.fulfillment_locations (id, city_id, code, name) VALUES ('{L3}', '{City}', 'L3', 'Harbour Hub');
        INSERT INTO catalog.categories (id, name) VALUES ('{SeaFish}', 'Sea fish'), ('{Category}', 'Premium fish');
        INSERT INTO catalog.preparation_methods (id, code, name) VALUES
            ('{CurryCut}', 'CURRY_CUT', 'Curry cut'), ('{Whole}', 'WHOLE', 'Whole'), ('{Steak}', 'STEAK', 'Steaks');
        INSERT INTO catalog.products (id, category_id, name, status, tax_category_code) VALUES
            ('{PomfretProduct}', '{SeaFish}', 'Silver Pomfret', 'ACTIVE', 'FRESH_FISH'),
            ('{SeerPackProduct}', '{Category}', 'Seer Fish Steaks', 'ACTIVE', 'FRESH_FISH'),
            ('{LobsterProduct}', NULL, 'Rock Lobster', 'DRAFT', NULL),
            ('0199e000-0000-7000-8000-000000001104', '{SeaFish}', 'Old Sardine', 'ARCHIVED', 'FRESH_FISH');
        INSERT INTO catalog.product_variants (id, product_id, sku, name, stock_unit, sell_unit, pack_size_grams) VALUES
            ('{PomfretVariant}', '{PomfretProduct}', 'POMF-KG', 'Pomfret per kg', 'GRAM', 'KG', NULL),
            ('{SeerPackVariant}', '{SeerPackProduct}', 'SEER-STEAK-500', 'Steaks 500 g net', 'PIECE', 'PACK', 500),
            ('{LobsterVariant}', '{LobsterProduct}', 'LOBSTER-PC', 'Lobster per piece', 'PIECE', 'PIECE', NULL),
            ('{ArchivedVariant}', '0199e000-0000-7000-8000-000000001104', 'SARDINE-KG', 'Sardine per kg', 'GRAM', 'KG', NULL);
        INSERT INTO catalog.variant_preparations (variant_id, preparation_method_id, yield_bp, is_default) VALUES
            ('{PomfretVariant}', '{CurryCut}', 6500, true),
            ('{PomfretVariant}', '{Whole}', 10000, false),
            ('{SeerPackVariant}', '{Steak}', 7000, true),
            ('{LobsterVariant}', '{Whole}', 10000, true);

        -- Stock at L3: Pomfret 5 300 g on hand, 5 233 g reserved (O1, O2, O4 pending, O5 next day); lobster 10, 3 reserved.
        INSERT INTO inventory.stock_items (id, fulfillment_location_id, variant_id, on_hand, reserved, low_stock_threshold) VALUES
            ('{PomfretStock}', '{L3}', '{PomfretVariant}', 5300, 5233, 0),
            ('{LobsterStock}', '{L3}', '{LobsterVariant}', 10, 3, 0);

        -- O1 CONFIRMED: pomfret curry cut 500 g (gross 770), whole 1 000 g (gross 1 000), 2 seer packs.
        -- O2 PREPARING: pomfret curry cut 1 000 g (gross 1 539), 3 lobsters, a cancelled line.
        -- O3 CANCELLED, O4 PENDING_PAYMENT (same date), O5 CONFIRMED for the next day: not in the list.
        INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, payment_method, slot_start_at,
            total_paise, placed_at) VALUES
            ('0199e000-0000-7000-8000-00000000b100', 'FO-261001', '0199e000-0000-7000-8000-0000000000c6', '{City}', '{L3}', 'CONFIRMED', 'COD', '2026-11-06T01:30:00Z', 100000, '2026-11-05T10:00:00Z'),
            ('0199e000-0000-7000-8000-00000000b200', 'FO-261002', '0199e000-0000-7000-8000-0000000000c7', '{City}', '{L3}', 'PREPARING', 'COD', '2026-11-06T04:30:00Z', 100000, '2026-11-05T11:00:00Z'),
            ('0199e000-0000-7000-8000-00000000b300', 'FO-261003', '0199e000-0000-7000-8000-0000000000c8', '{City}', '{L3}', 'CANCELLED', 'COD', '2026-11-06T01:30:00Z', 100000, '2026-11-05T12:00:00Z'),
            ('0199e000-0000-7000-8000-00000000b400', 'FO-261004', '0199e000-0000-7000-8000-0000000000c9', '{City}', '{L3}', 'PENDING_PAYMENT', 'ONLINE', '2026-11-06T01:30:00Z', 100000, '2026-11-05T13:00:00Z'),
            ('0199e000-0000-7000-8000-00000000b500', 'FO-261005', '0199e000-0000-7000-8000-0000000000ca', '{City}', '{L3}', 'CONFIRMED', 'COD', '2026-11-06T18:30:00Z', 100000, '2026-11-05T14:00:00Z');
        INSERT INTO ordering.order_items (id, order_id, line_no, product_id, variant_id, product_name, variant_name, sku, preparation_code,
            preparation_name, sell_unit, stock_unit, yield_bp, ordered_quantity, status) VALUES
            ('0199e000-0000-7000-8000-00000000b101', '0199e000-0000-7000-8000-00000000b100', 1, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'CURRY_CUT', 'Curry cut', 'KG', 'GRAM', 6500, 500, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b102', '0199e000-0000-7000-8000-00000000b100', 2, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'WHOLE', 'Whole', 'KG', 'GRAM', 10000, 1000, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b103', '0199e000-0000-7000-8000-00000000b100', 3, '{SeerPackProduct}', '{SeerPackVariant}', 'Seer Fish Steaks', 'Steaks 500 g net', 'SEER-STEAK-500', 'STEAK', 'Steaks', 'PACK', 'PIECE', 7000, 2, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b201', '0199e000-0000-7000-8000-00000000b200', 1, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'CURRY_CUT', 'Curry cut', 'KG', 'GRAM', 6500, 1000, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b202', '0199e000-0000-7000-8000-00000000b200', 2, '{LobsterProduct}', '{LobsterVariant}', 'Rock Lobster', 'Lobster per piece', 'LOBSTER-PC', 'WHOLE', 'Whole', 'PIECE', 'PIECE', 10000, 3, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b203', '0199e000-0000-7000-8000-00000000b200', 3, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'WHOLE', 'Whole', 'KG', 'GRAM', 10000, 250, 'CANCELLED'),
            ('0199e000-0000-7000-8000-00000000b301', '0199e000-0000-7000-8000-00000000b300', 1, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'CURRY_CUT', 'Curry cut', 'KG', 'GRAM', 6500, 2000, 'CANCELLED'),
            ('0199e000-0000-7000-8000-00000000b401', '0199e000-0000-7000-8000-00000000b400', 1, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'CURRY_CUT', 'Curry cut', 'KG', 'GRAM', 6500, 500, 'ACTIVE'),
            ('0199e000-0000-7000-8000-00000000b501', '0199e000-0000-7000-8000-00000000b500', 1, '{PomfretProduct}', '{PomfretVariant}', 'Silver Pomfret', 'Pomfret per kg', 'POMF-KG', 'CURRY_CUT', 'Curry cut', 'KG', 'GRAM', 6500, 750, 'ACTIVE');
        INSERT INTO inventory.stock_reservations (id, stock_item_id, order_id, order_item_id, quantity, status) VALUES
            ('0199e000-0000-7000-8000-000000001601', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b100', '0199e000-0000-7000-8000-00000000b101', 770, 'ACTIVE'),
            ('0199e000-0000-7000-8000-000000001602', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b100', '0199e000-0000-7000-8000-00000000b102', 1000, 'ACTIVE'),
            ('0199e000-0000-7000-8000-000000001603', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b200', '0199e000-0000-7000-8000-00000000b201', 1539, 'ACTIVE'),
            ('0199e000-0000-7000-8000-000000001604', '{LobsterStock}', '0199e000-0000-7000-8000-00000000b200', '0199e000-0000-7000-8000-00000000b202', 3, 'ACTIVE'),
            ('0199e000-0000-7000-8000-000000001605', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b300', '0199e000-0000-7000-8000-00000000b301', 3077, 'RELEASED'),
            ('0199e000-0000-7000-8000-000000001606', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b400', '0199e000-0000-7000-8000-00000000b401', 770, 'ACTIVE'),
            ('0199e000-0000-7000-8000-000000001607', '{PomfretStock}', '0199e000-0000-7000-8000-00000000b500', '0199e000-0000-7000-8000-00000000b501', 1154, 'ACTIVE');

        -- Prices at L3 (GST 5 % for the arithmetic): pomfret ₹900/kg until 2026-11-07 00:00 IST, then ₹950; seer pack ₹650.
        INSERT INTO pricing.tax_rules (id, tax_category_code, rate_bp, valid_from) VALUES
            ('0199e000-0000-7000-8000-000000001701', 'FRESH_FISH', 500, '2026-01-01T00:00:00Z');
        INSERT INTO pricing.variant_prices (id, fulfillment_location_id, variant_id, unit_price_paise, compare_at_paise, valid_from, valid_to) VALUES
            ('0199e000-0000-7000-8000-000000001801', '{L3}', '{PomfretVariant}', 90000, NULL, '2026-11-01T00:00:00Z', '2026-11-06T18:30:00Z'),
            ('0199e000-0000-7000-8000-000000001802', '{L3}', '{PomfretVariant}', 95000, NULL, '2026-11-06T18:30:00Z', NULL),
            ('0199e000-0000-7000-8000-000000001803', '{L3}', '{SeerPackVariant}', 65000, 70000, '2026-11-01T00:00:00Z', NULL),
            ('0199e000-0000-7000-8000-000000001804', '{L1}', '{LobsterVariant}', 120000, NULL, '2026-11-01T00:00:00Z', NULL);

        -- Batches at L3: the latest pomfret batch is 10 000 g for ₹4 550 (₹700 per net kg at 6 500 bp); seer 20 packs for ₹10 000.
        INSERT INTO inventory.batches (id, fulfillment_location_id, variant_id, received_at, received_quantity, total_cost_paise) VALUES
            ('0199e000-0000-7000-8000-000000001901', '{L3}', '{PomfretVariant}', '2026-11-04T01:00:00Z', 10000, 300000),
            ('0199e000-0000-7000-8000-000000001902', '{L3}', '{PomfretVariant}', '2026-11-05T01:00:00Z', 10000, 455000),
            ('0199e000-0000-7000-8000-000000001903', '{L3}', '{SeerPackVariant}', '2026-11-05T01:00:00Z', 20, 1000000),
            ('0199e000-0000-7000-8000-000000001904', '{L1}', '{LobsterVariant}', '2026-11-05T01:00:00Z', 10, 500000);
        """;
}
