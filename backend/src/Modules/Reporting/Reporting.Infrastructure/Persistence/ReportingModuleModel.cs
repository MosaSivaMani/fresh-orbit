using FreshOrbit.Reporting.Domain;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Reporting.Infrastructure.Persistence;

/// <summary>
/// The Reporting module's objects in the <c>reporting</c> schema: the <c>refresh_state</c> table (EF) and the
/// materialized views (SQL files in <c>Persistence/Sql</c>, decision D22).
/// </summary>
public sealed class ReportingModuleModel : IModuleModel
{
    public const string SchemaName = "reporting";

    public string Schema => SchemaName;

    /// <summary>
    /// The view SQL, one statement per item. Each view is created only when all its source tables exist, so a database
    /// (or a test fixture) without those modules' tables still builds; re-running it later creates the missing views.
    /// </summary>
    public IEnumerable<string> DatabaseObjectsSql => ReportViews.All.SelectMany(view => view.CreateStatements());

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingModuleModel).Assembly);
    }
}

/// <summary>One materialized view: its SQL file and the source tables it reads.</summary>
public sealed record ReportView(string Name, string File, IReadOnlyList<string> Sources)
{
    private const string Separator = "-- next";

    public string QualifiedName => $"{ReportingModuleModel.SchemaName}.{Name}";

    public IEnumerable<string> CreateStatements()
    {
        var guard = string.Join(" AND ", Sources.Select(source => $"to_regclass('{source}') IS NOT NULL"));
        foreach (var statement in ReadSql().Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return $"""
                DO $reporting$
                BEGIN
                    IF {guard} THEN
                        EXECUTE $view${statement}$view$;
                    END IF;
                END
                $reporting$;
                """;
        }
    }

    private string ReadSql()
    {
        var assembly = typeof(ReportView).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + File, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>The materialized views in creation and refresh order.</summary>
public static class ReportViews
{
    public static readonly ReportView DailySales = new(
        "daily_sales",
        "01_daily_sales.sql",
        ["ordering.orders", "ordering.order_items", "payments.refunds", "locations.fulfillment_locations", "locations.cities"]);

    public static readonly ReportView ProductPerformance = new(
        "product_performance",
        "02_product_performance.sql",
        ["ordering.orders", "ordering.order_items", "inventory.stock_movements", "catalog.product_variants", "locations.fulfillment_locations", "locations.cities"]);

    public static readonly ReportView ContributionMargin = new(
        "contribution_margin",
        "03_contribution_margin.sql",
        ["ordering.orders", "ordering.order_items", "inventory.stock_movements", "payments.refunds", "locations.fulfillment_locations", "locations.cities"]);

    public static readonly ReportView WasteSummary = new(
        "waste_summary",
        "04_waste_summary.sql",
        ["inventory.stock_movements", "inventory.stock_items", "catalog.product_variants", "catalog.products", "locations.fulfillment_locations", "locations.cities"]);

    public static readonly ReportView CustomerDailyOrders = new(
        "customer_daily_orders",
        "05_customer_daily_orders.sql",
        ["ordering.orders", "locations.fulfillment_locations", "locations.cities"]);

    public static IReadOnlyList<ReportView> All { get; } = [DailySales, ProductPerformance, ContributionMargin, WasteSummary, CustomerDailyOrders];
}

internal sealed class RefreshStateConfiguration : IEntityTypeConfiguration<RefreshState>
{
    public void Configure(EntityTypeBuilder<RefreshState> builder)
    {
        builder.ToTable("refresh_state");
        builder.HasKey(s => s.ViewName);
        builder.Property(s => s.ViewName).HasMaxLength(63);
        builder.Property(s => s.LastError).HasMaxLength(2000);
    }
}
