namespace FreshOrbit.Reporting.Infrastructure.Buying;

/// <summary>
/// The buying list for one location and delivery date (D369, REP-R14). Rows are variants (what is bought and stocked);
/// each lists its preparations. Quantities are in <see cref="BuyingListItem.StockUnit"/> (GRAM raw grams, PIECE pieces
/// or packs); every figure is computed by the server.
/// </summary>
/// <param name="OrdersCount">Confirmed orders (CONFIRMED or PREPARING) with a slot on the delivery date.</param>
/// <param name="PendingPaymentOrders">Online orders for the date still waiting for payment: not in the list.</param>
/// <param name="EstimatedRawGrams">Σ items' raw grams with margin (lines with known grams only).</param>
/// <param name="ShortfallRawGrams">Σ items' raw-gram shortfall (lines with known grams only).</param>
public sealed record BuyingListResponse(
    DateTimeOffset AsOf,
    Guid FulfillmentLocationId,
    string LocationCode,
    string LocationName,
    string TimeZone,
    DateOnly DeliveryDate,
    int SafetyMarginBp,
    int OrdersCount,
    int PendingPaymentOrders,
    long EstimatedRawGrams,
    long ShortfallRawGrams,
    IReadOnlyList<BuyingListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <param name="OrderedQuantity">Σ ordered quantity in the stock unit (net grams, pieces or packs).</param>
/// <param name="OrderedNetGrams">Σ net grams ordered; null for pieces.</param>
/// <param name="NeededQuantity">Σ stock the lines need (INV-R11 gross for GRAM; the count otherwise).</param>
/// <param name="EstimatedRawGrams">Σ raw grams to buy before cleaning; null for pieces.</param>
/// <param name="WithMarginQuantity">Needed quantity plus the safety margin, rounded up.</param>
/// <param name="WithMarginRawGrams">Raw grams plus the safety margin, rounded up; null for pieces.</param>
/// <param name="OnHand">Stock on hand (Inventory summary); null when the location has no stock item for the variant.</param>
/// <param name="ReservedForOtherOrders">Reserved stock held by orders not in this list.</param>
/// <param name="AvailableQuantity">On hand minus <paramref name="ReservedForOtherOrders"/>, never below zero.</param>
/// <param name="ShortfallQuantity">With-margin quantity minus available, never below zero: what to buy.</param>
/// <param name="ShortfallRawGrams">The shortfall in raw grams; null for pieces.</param>
public sealed record BuyingListItem(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string VariantName,
    string Sku,
    string SellUnit,
    string StockUnit,
    int? PackSizeGrams,
    int OrdersCount,
    long OrderedQuantity,
    long? OrderedNetGrams,
    long NeededQuantity,
    long? EstimatedRawGrams,
    long WithMarginQuantity,
    long? WithMarginRawGrams,
    long? OnHand,
    long ReservedForOtherOrders,
    long AvailableQuantity,
    long ShortfallQuantity,
    long? ShortfallRawGrams,
    bool StockItemMissing,
    IReadOnlyList<BuyingListPreparation> Preparations);

/// <param name="YieldBp">The yield the orders were placed with (order line snapshot, the one checkout reserved with).</param>
public sealed record BuyingListPreparation(
    string PreparationCode,
    string PreparationName,
    int YieldBp,
    int OrdersCount,
    long OrderedQuantity,
    long? OrderedNetGrams,
    long NeededQuantity,
    long? EstimatedRawGrams);

/// <summary>
/// Every active variant at one location with its current price, latest batch cost per sell unit, margin and a
/// suggested price (D370, REP-R15). Suggestions are never applied: staff edit prices and save them through
/// <c>POST /admin/pricing/prices/bulk</c> (PRC-R13), with <see cref="TomorrowStartsAt"/> or now as <c>validFrom</c>.
/// </summary>
/// <param name="TomorrowStartsAt">Next local midnight at the location, as a UTC instant (the "from tomorrow" start).</param>
/// <param name="TargetMarginBp">Screen-wide target margin entered by staff; null when none was given.</param>
/// <param name="CategoryTargets">Per-category targets entered on the screen (override the screen-wide target).</param>
public sealed record TodaysPricesResponse(
    DateTimeOffset AsOf,
    Guid FulfillmentLocationId,
    string LocationCode,
    string LocationName,
    string TimeZone,
    DateTimeOffset TomorrowStartsAt,
    int? TargetMarginBp,
    IReadOnlyDictionary<Guid, int> CategoryTargets,
    IReadOnlyList<TodaysPriceItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <param name="UnitPricePaise">CURRENT price per sell unit (GST included); null = not for sale here (PRC-R1).</param>
/// <param name="NextUnitPricePaise">The next FUTURE price already scheduled, if any.</param>
/// <param name="TaxRateBp">CURRENT GST rate of the product's tax category; null when no rule covers now.</param>
/// <param name="PreparationCode">Default preparation whose yield prices a KG variant.</param>
/// <param name="YieldBp">That preparation's yield (Catalog).</param>
/// <param name="LatestBatchCostPaise">Total cost of the most recently received batch at the location.</param>
/// <param name="LatestBatchQuantity">Its received quantity in the stock unit (raw grams, pieces or packs).</param>
/// <param name="CostPerUnitPaise">Cost of one sell unit net of cleaning loss (per net kg, piece or pack).</param>
/// <param name="MarginBp">Margin of the current price without GST over that cost; null when either is missing.</param>
/// <param name="TargetMarginBp">Target applied to this row (category target, else the screen target).</param>
/// <param name="SuggestedPricePaise">Price with GST earning the target, rounded up to whole rupees; never applied.</param>
/// <param name="Issues">Why figures are missing: PRICE_MISSING, BATCH_MISSING, YIELD_MISSING, TAX_RULE_MISSING.</param>
public sealed record TodaysPriceItem(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string VariantName,
    string Sku,
    Guid? CategoryId,
    string? CategoryName,
    string SellUnit,
    string StockUnit,
    int? PackSizeGrams,
    long? UnitPricePaise,
    long? CompareAtPaise,
    DateTimeOffset? PriceValidFrom,
    long? NextUnitPricePaise,
    DateTimeOffset? NextValidFrom,
    int? TaxRateBp,
    string? PreparationCode,
    int? YieldBp,
    Guid? LatestBatchId,
    DateTimeOffset? LatestBatchReceivedAt,
    long? LatestBatchCostPaise,
    int? LatestBatchQuantity,
    long? CostPerUnitPaise,
    int? MarginBp,
    int? TargetMarginBp,
    long? SuggestedPricePaise,
    IReadOnlyList<string> Issues);
