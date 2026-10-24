using System.Globalization;
using FreshOrbit.Pricing.Application.Contracts;
using FreshOrbit.Pricing.Application.Schedules;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Pricing.Infrastructure.Seeding;

/// <summary>
/// Sample <c>pricing.json</c> (order 40): placeholder tax rules first (PRC-R9), skipping any category that already has
/// a CURRENT rule so real rates are never replaced (D58), then the file's <c>bulk</c> body as-is through
/// <see cref="BulkPriceService"/> (all-or-nothing, PRC-R13; rows equal to the current price are unchanged).
/// </summary>
public sealed partial class PricingSampleDataLoader(
    PricingService pricing,
    BulkPriceService bulk,
    FreshOrbitDbContext db,
    ILogger<PricingSampleDataLoader> logger) : ISampleDataLoader
{
    public int Order => 40;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<PricingFile>(context, "pricing.json", cancellationToken);

        var listed = await pricing.ListTaxRulesAsync(cancellationToken);
        var current = listed.Where(r => r.Current is not null).Select(r => r.TaxCategoryCode).ToHashSet(StringComparer.Ordinal);
        var versions = listed.ToDictionary(r => r.TaxCategoryCode, r => r.Version, StringComparer.Ordinal);
        var insertedRules = 0;
        foreach (var rule in file.TaxRules.Where(r => !current.Contains(r.TaxCategoryCode)))
        {
            var version = versions.GetValueOrDefault(rule.TaxCategoryCode);
            var precondition = new SchedulePrecondition(version is null ? null : uint.Parse(version, CultureInfo.InvariantCulture), version is null);
            var scheduled = await pricing.ScheduleTaxRuleAsync(new ScheduleTaxRuleRequest(rule.TaxCategoryCode, rule.RateBp, null, false), precondition, cancellationToken);
            var period = scheduled.Current ?? scheduled.Next ?? throw new InvalidOperationException($"Tax rule {rule.TaxCategoryCode} was scheduled but cannot be read.");
            context.Track<TaxRule>(db, period.Id);
            insertedRules++;
        }

        await db.SaveChangesAsync(cancellationToken);

        var request = file.Bulk ?? throw new InvalidDataException("pricing.json: 'bulk' is missing.");
        var pricesBefore = await db.Set<VariantPrice>().AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        var chargesBefore = await db.Set<PreparationCharge>().AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        var result = await bulk.ApplyAsync(request, dryRun: false, cancellationToken);
        var newPrices = (await db.Set<VariantPrice>().AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken)).Except(pricesBefore).ToList();
        var newCharges = (await db.Set<PreparationCharge>().AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken)).Except(chargesBefore).ToList();
        context.Track<VariantPrice>(db, newPrices);
        context.Track<PreparationCharge>(db, newCharges);
        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedRules, file.TaxRules.Count - insertedRules, newPrices.Count, newCharges.Count, result.UnchangedCount);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample pricing: {Rules} tax rules inserted, {RulesSkipped} skipped (a current rule exists); {Prices} prices and {Charges} preparation charges inserted, {Unchanged} rows unchanged")]
    private partial void LogLoaded(int rules, int rulesSkipped, int prices, int charges, int unchanged);

    private sealed record PricingFile(IReadOnlyList<TaxRuleRow> TaxRules, BulkPriceRequest? Bulk);

    private sealed record TaxRuleRow(string TaxCategoryCode, int RateBp);
}
