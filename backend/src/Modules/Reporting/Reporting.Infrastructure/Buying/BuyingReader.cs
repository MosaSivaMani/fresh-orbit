using System.Data.Common;
using FreshOrbit.Reporting.Application;
using FreshOrbit.Reporting.Domain;
using FreshOrbit.Reporting.Domain.Buying;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Reporting.Infrastructure.Buying;

/// <summary>
/// Live, read-only queries for the morning buying list (REP-R14) and "today's prices" (REP-R15). Like the dashboard
/// they read other modules' tables with SQL (Reporting is the only module allowed to), one statement per figure set,
/// never one per row; the rules live in <see cref="BuyingRules"/> and <see cref="PriceRules"/>.
/// </summary>
public sealed class BuyingReader(FreshOrbitDbContext db, ICurrentActor actor, IClock clock)
{
    /// <summary>The buying list for <paramref name="locationId"/> and <paramref name="deliveryDate"/> (today at the location when null).</summary>
    public async Task<BuyingListResponse> BuyingListAsync(Guid locationId, DateOnly? deliveryDate, int safetyMarginBp, CancellationToken cancellationToken)
    {
        if (safetyMarginBp is < 0 or > BuyingRules.MaxSafetyMarginBp)
        {
            throw new RefusalException(new FieldError("safetyMarginBp", ReportingErrors.ValidationFailed, "Safety margin must be between 0 and 10000 bp."));
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var location = await LocationAsync(locationId, cancellationToken);
            var date = deliveryDate ?? location.Today(clock.UtcNow);
            var (from, to) = location.DayBounds(date);
            void Bind(NpgsqlCommand c)
            {
                c.Parameters.AddWithValue("loc", locationId);
                c.Parameters.AddWithValue("from", from);
                c.Parameters.AddWithValue("to", to);
            }

            // Read: every active line of the date's confirmed orders, with the stock its reservation holds.
            var lines = await ListAsync(
                """
                SELECT oi.variant_id, oi.product_id, oi.product_name, oi.variant_name, oi.sku, oi.sell_unit, oi.stock_unit,
                       v.pack_size_grams, coalesce(oi.preparation_code, ''), coalesce(oi.preparation_name, ''), oi.yield_bp,
                       oi.ordered_quantity, oi.order_id,
                       coalesce((SELECT sum(r.quantity) FROM inventory.stock_reservations r
                                 WHERE r.order_item_id = oi.id AND r.status = 'ACTIVE'), 0)::bigint
                FROM ordering.order_items oi
                JOIN ordering.orders o ON o.id = oi.order_id
                LEFT JOIN catalog.product_variants v ON v.id = oi.variant_id
                WHERE o.fulfillment_location_id = @loc AND o.slot_start_at >= @from AND o.slot_start_at < @to
                  AND o.status IN ('CONFIRMED', 'PREPARING') AND oi.status = 'ACTIVE'
                """,
                Bind,
                r => new Line(
                    r.GetGuid(0),
                    r.GetGuid(1),
                    r.GetString(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetString(5),
                    r.GetString(6),
                    NullableInt(r, 7),
                    r.GetString(8),
                    r.GetString(9),
                    r.GetInt32(10),
                    r.GetInt32(11),
                    r.GetGuid(12),
                    r.GetInt64(13)),
                cancellationToken);
            var pending = (int)(await ListAsync(
                "SELECT count(*) FROM ordering.orders o WHERE o.fulfillment_location_id = @loc AND o.slot_start_at >= @from AND o.slot_start_at < @to AND o.status = 'PENDING_PAYMENT'",
                Bind,
                r => r.GetInt64(0),
                cancellationToken))[0];
            var variantIds = lines.Select(l => l.VariantId).Distinct().ToArray();
            var stock = (await ListAsync(
                "SELECT variant_id, on_hand::bigint, reserved::bigint FROM inventory.stock_items WHERE fulfillment_location_id = @loc AND variant_id = ANY(@variants)",
                c =>
                {
                    c.Parameters.AddWithValue("loc", locationId);
                    c.Parameters.AddWithValue("variants", variantIds);
                },
                r => (VariantId: r.GetGuid(0), Level: new StockLevel(r.GetInt64(1), r.GetInt64(2))),
                cancellationToken)).ToDictionary(s => s.VariantId, s => s.Level);

            // Decide: per variant, per preparation (REP-R14).
            var items = lines
                .GroupBy(l => l.VariantId)
                .Select(group => Item(group.ToList(), stock.GetValueOrDefault(group.Key), safetyMarginBp))
                .OrderBy(i => i.ProductName, StringComparer.Ordinal)
                .ThenBy(i => i.VariantName, StringComparer.Ordinal)
                .ThenBy(i => i.Sku, StringComparer.Ordinal)
                .ToList();
            return new BuyingListResponse(
                clock.UtcNow,
                locationId,
                location.Code,
                location.Name,
                location.TimeZone,
                date,
                safetyMarginBp,
                lines.Select(l => l.OrderId).Distinct().Count(),
                pending,
                items.Sum(i => i.WithMarginRawGrams ?? 0),
                items.Sum(i => i.ShortfallRawGrams ?? 0),
                items,
                1,
                items.Count,
                items.Count);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Today's prices at <paramref name="locationId"/> with suggestions for the targets entered on the screen (REP-R15).</summary>
    public async Task<TodaysPricesResponse> TodaysPricesAsync(
        Guid locationId,
        Guid? categoryId,
        int? targetMarginBp,
        IReadOnlyDictionary<Guid, int> categoryTargets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoryTargets);
        var errors = new List<FieldError>();
        if (targetMarginBp is < 0 or > PriceRules.MaxTargetMarginBp)
        {
            errors.Add(new FieldError("targetMarginBp", ReportingErrors.ValidationFailed, "Target margin must be between 0 and 9500 bp."));
        }

        if (categoryTargets.Values.Any(bp => bp is < 0 or > PriceRules.MaxTargetMarginBp))
        {
            errors.Add(new FieldError("categoryTargets", ReportingErrors.ValidationFailed, "Each category target must be between 0 and 9500 bp."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var location = await LocationAsync(locationId, cancellationToken);
            var now = clock.UtcNow;
            var rows = await ListAsync(
                $"""
                SELECT v.id, p.id, p.name, v.name, v.sku, p.category_id, cat.name, v.sell_unit, v.stock_unit, v.pack_size_grams,
                       cur.unit_price_paise, cur.compare_at_paise, cur.valid_from, nxt.unit_price_paise, nxt.valid_from,
                       tr.rate_bp, dp.code, dp.yield_bp, lb.id, lb.received_at, lb.total_cost_paise, lb.received_quantity
                FROM catalog.product_variants v
                JOIN catalog.products p ON p.id = v.product_id
                LEFT JOIN catalog.categories cat ON cat.id = p.category_id
                LEFT JOIN LATERAL (
                    SELECT vp.unit_price_paise, vp.compare_at_paise, vp.valid_from FROM pricing.variant_prices vp
                    WHERE vp.fulfillment_location_id = @loc AND vp.variant_id = v.id
                      AND vp.valid_from <= @now AND (vp.valid_to IS NULL OR vp.valid_to > @now)
                    ORDER BY vp.valid_from DESC LIMIT 1) cur ON TRUE
                LEFT JOIN LATERAL (
                    SELECT vp.unit_price_paise, vp.valid_from FROM pricing.variant_prices vp
                    WHERE vp.fulfillment_location_id = @loc AND vp.variant_id = v.id AND vp.valid_from > @now
                    ORDER BY vp.valid_from LIMIT 1) nxt ON TRUE
                LEFT JOIN LATERAL (
                    SELECT t.rate_bp FROM pricing.tax_rules t
                    WHERE t.tax_category_code = p.tax_category_code AND t.valid_from <= @now AND (t.valid_to IS NULL OR t.valid_to > @now)
                    ORDER BY t.valid_from DESC LIMIT 1) tr ON TRUE
                LEFT JOIN LATERAL (
                    SELECT pm.code, vpr.yield_bp FROM catalog.variant_preparations vpr
                    JOIN catalog.preparation_methods pm ON pm.id = vpr.preparation_method_id
                    WHERE vpr.variant_id = v.id AND vpr.is_default AND vpr.is_active
                    LIMIT 1) dp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT b.id, b.received_at, b.total_cost_paise, b.received_quantity FROM inventory.batches b
                    WHERE b.fulfillment_location_id = @loc AND b.variant_id = v.id
                    ORDER BY b.received_at DESC, b.id DESC LIMIT 1) lb ON TRUE
                WHERE v.is_active AND p.status <> 'ARCHIVED'{(categoryId is null ? string.Empty : " AND p.category_id = @category")}
                ORDER BY cat.name NULLS LAST, p.name, v.name, v.sku
                """,
                c =>
                {
                    c.Parameters.AddWithValue("loc", locationId);
                    c.Parameters.AddWithValue("now", now);
                    if (categoryId is { } category)
                    {
                        c.Parameters.AddWithValue("category", category);
                    }
                },
                r => PriceItem(r, targetMarginBp, categoryTargets),
                cancellationToken);
            return new TodaysPricesResponse(
                now,
                locationId,
                location.Code,
                location.Name,
                location.TimeZone,
                location.NextMidnight(now),
                targetMarginBp,
                categoryTargets,
                rows,
                1,
                rows.Count,
                rows.Count);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static BuyingListItem Item(List<Line> lines, StockLevel? stock, int safetyMarginBp)
    {
        var first = lines[0];
        var preparations = lines
            .GroupBy(l => (l.PreparationCode, l.PreparationName, l.YieldBp))
            .Select(g => new BuyingListPreparation(
                g.Key.PreparationCode,
                g.Key.PreparationName,
                g.Key.YieldBp,
                g.Select(l => l.OrderId).Distinct().Count(),
                g.Sum(l => (long)l.OrderedQuantity),
                SumOrNull(g.Select(l => l.NetGrams)),
                g.Sum(l => (long)l.Needed),
                SumOrNull(g.Select(l => l.RawGrams))))
            .OrderBy(p => p.PreparationCode, StringComparer.Ordinal)
            .ThenBy(p => p.YieldBp)
            .ToList();
        var needed = preparations.Sum(p => p.NeededQuantity);
        var raw = SumOrNull(preparations.Select(p => p.EstimatedRawGrams));
        var withMargin = BuyingRules.WithMargin(needed, safetyMarginBp);
        long? withMarginRaw = raw is { } r ? BuyingRules.WithMargin(r, safetyMarginBp) : null;
        var reservedForList = lines.Sum(l => l.ReservedQuantity);
        var reservedOther = stock is { } s ? Math.Max(0, s.Reserved - reservedForList) : 0;
        var available = BuyingRules.Available(stock?.OnHand, stock?.Reserved ?? 0, reservedForList);
        var shortfall = BuyingRules.Shortfall(withMargin, available);
        return new BuyingListItem(
            first.VariantId,
            first.ProductId,
            first.ProductName,
            first.VariantName,
            first.Sku,
            first.SellUnit,
            first.StockUnit,
            first.PackSizeGrams,
            lines.Select(l => l.OrderId).Distinct().Count(),
            preparations.Sum(p => p.OrderedQuantity),
            SumOrNull(preparations.Select(p => p.OrderedNetGrams)),
            needed,
            raw,
            withMargin,
            withMarginRaw,
            stock?.OnHand,
            reservedOther,
            available,
            shortfall,
            BuyingRules.ShortfallRawGrams(first.StockUnit, shortfall, withMargin, withMarginRaw),
            stock is null,
            preparations);
    }

    private static TodaysPriceItem PriceItem(DbDataReader r, int? screenTarget, IReadOnlyDictionary<Guid, int> categoryTargets)
    {
        var categoryId = r.IsDBNull(5) ? (Guid?)null : r.GetGuid(5);
        var sellUnit = r.GetString(7);
        var price = NullableLong(r, 10);
        var taxRate = NullableInt(r, 15);
        var yieldBp = NullableInt(r, 17);
        var batchCost = NullableLong(r, 20);
        var batchQuantity = NullableInt(r, 21);
        var cost = batchCost is { } bc && batchQuantity is { } bq ? PriceRules.CostPerSellUnitPaise(sellUnit, bc, bq, yieldBp) : null;
        int? margin = price is { } p && cost is { } c && taxRate is { } t ? PriceRules.MarginBp(PriceRules.PriceExTaxPaise(p, t), c) : null;
        int? target = categoryId is { } category && categoryTargets.TryGetValue(category, out var categoryTarget) ? categoryTarget : screenTarget;
        long? suggested = cost is { } unitCost && taxRate is { } rate && target is { } goal ? PriceRules.SuggestedPricePaise(unitCost, goal, rate) : null;

        var issues = new List<string>(4);
        if (price is null)
        {
            issues.Add("PRICE_MISSING");
        }

        if (batchCost is null)
        {
            issues.Add("BATCH_MISSING");
        }

        if (sellUnit == PriceRules.Kg && yieldBp is null)
        {
            issues.Add("YIELD_MISSING");
        }

        if (taxRate is null)
        {
            issues.Add("TAX_RULE_MISSING");
        }

        return new TodaysPriceItem(
            r.GetGuid(0),
            r.GetGuid(1),
            r.GetString(2),
            r.GetString(3),
            r.GetString(4),
            categoryId,
            r.IsDBNull(6) ? null : r.GetString(6),
            sellUnit,
            r.GetString(8),
            NullableInt(r, 9),
            price,
            NullableLong(r, 11),
            r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12),
            NullableLong(r, 13),
            r.IsDBNull(14) ? null : r.GetFieldValue<DateTimeOffset>(14),
            taxRate,
            r.IsDBNull(16) ? null : r.GetString(16),
            yieldBp,
            r.IsDBNull(18) ? null : r.GetGuid(18),
            r.IsDBNull(19) ? null : r.GetFieldValue<DateTimeOffset>(19),
            batchCost,
            batchQuantity,
            cost,
            margin,
            target,
            suggested,
            issues);
    }

    private static long? SumOrNull(IEnumerable<long?> values)
    {
        long total = 0;
        foreach (var value in values)
        {
            if (value is not { } v)
            {
                return null;
            }

            total = checked(total + v);
        }

        return total;
    }

    private static long? SumOrNull(IEnumerable<int?> values) => SumOrNull(values.Select(v => (long?)v));

    private static int? NullableInt(DbDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);

    private static long? NullableLong(DbDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetInt64(ordinal);

    /// <summary>Checks access (REP-R11: 403 <c>LOCATION_ACCESS_DENIED</c>) and reads the location's code, name and time zone.</summary>
    private async Task<LocationInfo> LocationAsync(Guid locationId, CancellationToken cancellationToken)
    {
        _ = ReportScope.Resolve(actor, locationId, null, []);
        var found = await ListAsync(
            "SELECT fl.code, fl.name, c.time_zone FROM locations.fulfillment_locations fl JOIN locations.cities c ON c.id = fl.city_id WHERE fl.id = @loc",
            c => c.Parameters.AddWithValue("loc", locationId),
            r => new LocationInfo(r.GetString(0), r.GetString(1), r.GetString(2)),
            cancellationToken);
        return found.Count == 1
            ? found[0]
            : throw new NotFoundException("The fulfillment location was not found.");
    }

    private async Task<List<T>> ListAsync<T>(string sql, Action<NpgsqlCommand> bind, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection());
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(map(reader));
        }

        return list;
    }

    private sealed record Line(
        Guid VariantId,
        Guid ProductId,
        string ProductName,
        string VariantName,
        string Sku,
        string SellUnit,
        string StockUnit,
        int? PackSizeGrams,
        string PreparationCode,
        string PreparationName,
        int YieldBp,
        int OrderedQuantity,
        Guid OrderId,
        long ReservedQuantity)
    {
        public int Needed { get; } = BuyingRules.NeededQuantity(StockUnit, OrderedQuantity, YieldBp);

        public int? NetGrams { get; } = BuyingRules.OrderedNetGrams(StockUnit, SellUnit, OrderedQuantity, PackSizeGrams);

        public int? RawGrams => BuyingRules.EstimatedRawGrams(NetGrams, YieldBp);
    }

    private sealed record StockLevel(long OnHand, long Reserved);

    /// <summary>A location's code, name and IANA time zone, with its local-day arithmetic.</summary>
    private sealed record LocationInfo(string Code, string Name, string TimeZone)
    {
        private TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

        public DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

        /// <summary>[local midnight of <paramref name="date"/>, the next local midnight) as UTC instants.</summary>
        public (DateTimeOffset From, DateTimeOffset To) DayBounds(DateOnly date) => (Midnight(date), Midnight(date.AddDays(1)));

        public DateTimeOffset NextMidnight(DateTimeOffset now) => Midnight(Today(now).AddDays(1));

        private DateTimeOffset Midnight(DateOnly date)
            => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone), TimeSpan.Zero);
    }
}
