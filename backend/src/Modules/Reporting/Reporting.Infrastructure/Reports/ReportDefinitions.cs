using FreshOrbit.Reporting.Infrastructure.Persistence;

namespace FreshOrbit.Reporting.Infrastructure.Reports;

public enum ColumnKind
{
    Date,
    Uuid,
    Text,
    Count,
    Money,
    BasisPoints,
}

/// <summary>A report column: snake_case <paramref name="Name"/> (view column, CSV header); JSON uses camelCase.</summary>
/// <param name="Summed">Included in <c>totals</c> as the sum over all matching rows.</param>
public sealed record ReportColumn(string Name, ColumnKind Kind, bool Summed = false)
{
    public string JsonName { get; } = ToCamel(Name);

    private static string ToCamel(string snake)
    {
        var parts = snake.Split('_');
        return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}

/// <summary>A report-specific filter (gap G1): query parameter → equality on a column, or a SQL condition on <c>@value</c>.</summary>
public sealed record ReportFilter(string Parameter, ColumnKind Kind, string Condition);

/// <summary>A materialized-view report (REP-R3..R6).</summary>
public sealed record ReportDefinition(
    string Key,
    ReportView View,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<string> Grain,
    IReadOnlyList<ReportFilter> Filters,
    Action<Dictionary<string, object?>> DeriveTotals)
{
    public ReportColumn? FindColumn(string jsonName) => Columns.FirstOrDefault(c => string.Equals(c.JsonName, jsonName, StringComparison.Ordinal));
}

public static class ReportDefinitions
{
    public static readonly ReportDefinition DailySales = new(
        "daily-sales",
        ReportViews.DailySales,
        [
            new("business_date", ColumnKind.Date),
            new("city_id", ColumnKind.Uuid),
            new("fulfillment_location_id", ColumnKind.Uuid),
            new("orders_placed", ColumnKind.Count, Summed: true),
            new("orders_delivered", ColumnKind.Count, Summed: true),
            new("orders_cancelled", ColumnKind.Count, Summed: true),
            new("gross_paise", ColumnKind.Money, Summed: true),
            new("discount_paise", ColumnKind.Money, Summed: true),
            new("delivery_fee_paise", ColumnKind.Money, Summed: true),
            new("tax_paise", ColumnKind.Money, Summed: true),
            new("net_revenue_paise", ColumnKind.Money, Summed: true),
            new("refunds_paise", ColumnKind.Money, Summed: true),
            new("post_delivery_refunds_paise", ColumnKind.Money, Summed: true),
            new("avg_order_value_paise", ColumnKind.Money),
        ],
        ["business_date", "fulfillment_location_id"],
        [],
        totals => totals["avg_order_value_paise"] = Ratio(totals, "net_revenue_paise", "orders_delivered", 1));

    public static readonly ReportDefinition ProductPerformance = new(
        "product-performance",
        ReportViews.ProductPerformance,
        [
            new("business_date", ColumnKind.Date),
            new("fulfillment_location_id", ColumnKind.Uuid),
            new("product_id", ColumnKind.Uuid),
            new("variant_id", ColumnKind.Uuid),
            new("sku", ColumnKind.Text),
            new("product_name", ColumnKind.Text),
            new("variant_name", ColumnKind.Text),
            new("preparation_code", ColumnKind.Text),
            new("stock_unit", ColumnKind.Text),
            new("orders_count", ColumnKind.Count, Summed: true),
            new("lines_short_supplied", ColumnKind.Count, Summed: true),
            new("ordered_quantity", ColumnKind.Count, Summed: true),
            new("packed_quantity", ColumnKind.Count, Summed: true),
            new("charged_quantity", ColumnKind.Count, Summed: true),
            new("revenue_paise", ColumnKind.Money, Summed: true),
            new("sale_cost_paise", ColumnKind.Money, Summed: true),
            new("gross_margin_paise", ColumnKind.Money, Summed: true),
        ],
        ["business_date", "fulfillment_location_id", "variant_id", "preparation_code"],
        [
            new("variantId", ColumnKind.Uuid, "variant_id = @value"),
            new("preparationCode", ColumnKind.Text, "preparation_code = @value"),
            new("categoryId", ColumnKind.Uuid, "product_id IN (SELECT p.id FROM catalog.products p WHERE p.category_id = @value)"),
        ],
        _ => { });

    public static readonly ReportDefinition ContributionMargin = new(
        "contribution-margin",
        ReportViews.ContributionMargin,
        [
            new("business_date", ColumnKind.Date),
            new("fulfillment_location_id", ColumnKind.Uuid),
            new("revenue_ex_tax_paise", ColumnKind.Money, Summed: true),
            new("sale_cost_paise", ColumnKind.Money, Summed: true),
            new("refunds_paise", ColumnKind.Money, Summed: true),
            new("delivery_cost_paise", ColumnKind.Money, Summed: true),
            new("contribution_paise", ColumnKind.Money, Summed: true),
            new("contribution_margin_bp", ColumnKind.BasisPoints),
        ],
        ["business_date", "fulfillment_location_id"],
        [],
        totals => totals["contribution_margin_bp"] = Ratio(totals, "contribution_paise", "revenue_ex_tax_paise", 10_000));

    public static readonly ReportDefinition Waste = new(
        "waste",
        ReportViews.WasteSummary,
        [
            new("business_date", ColumnKind.Date),
            new("fulfillment_location_id", ColumnKind.Uuid),
            new("product_id", ColumnKind.Uuid),
            new("variant_id", ColumnKind.Uuid),
            new("product_name", ColumnKind.Text),
            new("variant_name", ColumnKind.Text),
            new("stock_unit", ColumnKind.Text),
            new("reason_code", ColumnKind.Text),
            new("movements_count", ColumnKind.Count, Summed: true),
            new("waste_quantity", ColumnKind.Count, Summed: true),
            new("waste_cost_paise", ColumnKind.Money, Summed: true),
            new("sold_quantity", ColumnKind.Count, Summed: true),
            new("waste_share_bp", ColumnKind.BasisPoints),
        ],
        ["business_date", "fulfillment_location_id", "variant_id", "reason_code"],
        [
            new("reasonCode", ColumnKind.Text, "reason_code = @value"),
            new("variantId", ColumnKind.Uuid, "variant_id = @value"),
        ],
        totals =>
        {
            var waste = totals["waste_quantity"] as long? ?? 0;
            var sold = totals["sold_quantity"] as long? ?? 0;
            totals["waste_share_bp"] = waste + sold == 0 ? null : waste * 10_000 / (waste + sold);
        });

    public static IReadOnlyList<ReportDefinition> All { get; } = [DailySales, ProductPerformance, ContributionMargin, Waste];

    /// <summary><c>numerator × scale / denominator</c> truncated toward zero; null when the denominator is 0.</summary>
    private static long? Ratio(Dictionary<string, object?> totals, string numerator, string denominator, long scale)
    {
        var n = totals[numerator] as long? ?? 0;
        var d = totals[denominator] as long? ?? 0;
        return d == 0 ? null : n * scale / d;
    }
}
