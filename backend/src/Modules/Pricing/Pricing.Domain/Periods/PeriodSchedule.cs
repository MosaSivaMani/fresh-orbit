using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Pricing.Domain.Periods;

/// <summary>
/// The outcome of scheduling a new period: the period added (null = unchanged, PRC-R5), the period closed at the start,
/// FUTURE periods removed (<c>replaceFuture</c>, PRC-R4) and the period that was current before.
/// </summary>
public sealed record PlannedChange<T>(T? Added, T? Closed, IReadOnlyList<T> Removed, T? Previous)
    where T : PricePeriod
{
    public bool Unchanged => Added is null;
}

/// <summary>PRC-R2 to PRC-R5: period arithmetic shared by prices, preparation charges and tax rules.</summary>
public static class PeriodSchedule
{
    /// <summary>A start up to this far in the past is treated as now (clock skew of the admin's device).</summary>
    public static readonly TimeSpan PastTolerance = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan MaxAhead = TimeSpan.FromDays(90);

    /// <summary>
    /// PRC-R3: omitted = now; more than 60 s ago → PRICE_IN_PAST; within the last 60 s → now; beyond now + 90 days →
    /// VALIDATION_FAILED. Errors are added under <paramref name="field"/>; the result is now when there is an error.
    /// </summary>
    public static DateTimeOffset ResolveStart(DateTimeOffset? requested, DateTimeOffset now, string field, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (requested is not { } start)
        {
            return now;
        }

        start = start.ToUniversalTime();
        if (start < now - PastTolerance)
        {
            errors.Add(new FieldError(field, PricingErrors.PriceInPast, "The start time is in the past; omit validFrom to start now."));
            return now;
        }

        if (start > now + MaxAhead)
        {
            errors.Add(new FieldError(field, PricingErrors.ValidationFailed, "Schedule at most 90 days ahead."));
            return now;
        }

        return start < now ? now : start;
    }

    /// <summary>The period containing <paramref name="start"/> and the periods starting at or after it.</summary>
    public static (T? Containing, List<T> Futures) Split<T>(IEnumerable<T> periods, DateTimeOffset start)
        where T : PricePeriod
    {
        ArgumentNullException.ThrowIfNull(periods);
        var list = periods.ToList();
        return (list.FirstOrDefault(p => p.Contains(start)), [.. list.Where(p => p.ValidFrom >= start)]);
    }

    /// <summary>Closes the containing period and removes futures (or refuses with PRICE_PERIOD_OVERLAP), then opens the new one.</summary>
    public static PlannedChange<T> Plan<T>(IReadOnlyList<T> periods, DateTimeOffset start, DateTimeOffset now, bool replaceFuture, Func<T?, bool> sameAsCurrent, Func<T?, T> open)
        where T : PricePeriod
    {
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(sameAsCurrent);
        ArgumentNullException.ThrowIfNull(open);
        var current = periods.FirstOrDefault(p => p.IsCurrentAt(now));
        var (containing, futures) = Split(periods, start);
        if (start == now && futures.Count == 0 && current is not null && sameAsCurrent(current))
        {
            return new PlannedChange<T>(null, null, [], current);
        }

        if (futures.Count == 1 && futures[0].ValidFrom == start && futures[0].ValidTo is null && sameAsCurrent(futures[0]))
        {
            // The same period was already scheduled (a repeated request): nothing to write.
            return new PlannedChange<T>(null, null, [], current);
        }

        if (futures.Count > 0 && !replaceFuture)
        {
            throw new ConflictException(PricingErrors.PricePeriodOverlap, "A future period already starts after this time; send replaceFuture=true to replace it.", "validFrom");
        }

        containing?.Close(start);
        var previous = containing ?? current;
        return new PlannedChange<T>(open(previous), containing, futures, previous);
    }

    /// <summary>PRC-R14: a change of more than 50 % up or down against the previous value.</summary>
    public static bool IsLargeChange(long? previous, long next)
        => previous is > 0 and { } before && Math.Abs(next - before) * 2 > before;
}

/// <summary>Opens new periods; kept here so the factory methods stay internal to the domain.</summary>
public static class PeriodFactory
{
    public static PlannedChange<VariantPrice> PlanPrice(
        IReadOnlyList<VariantPrice> periods,
        Guid fulfillmentLocationId,
        Guid variantId,
        long unitPricePaise,
        long? compareAtPaise,
        DateTimeOffset start,
        DateTimeOffset now,
        bool replaceFuture)
        => PeriodSchedule.Plan(
            periods,
            start,
            now,
            replaceFuture,
            current => current is not null && current.UnitPricePaise == unitPricePaise && current.CompareAtPaise == compareAtPaise,
            previous => VariantPrice.Open(fulfillmentLocationId, variantId, unitPricePaise, compareAtPaise, start, previous));

    public static PlannedChange<PreparationCharge> PlanCharge(
        IReadOnlyList<PreparationCharge> periods,
        Guid fulfillmentLocationId,
        Guid variantId,
        Guid preparationMethodId,
        string preparationCode,
        long chargePaise,
        ChargeBasis basis,
        DateTimeOffset start,
        DateTimeOffset now,
        bool replaceFuture)
        => PeriodSchedule.Plan(
            periods,
            start,
            now,
            replaceFuture,
            current => current is not null && current.ChargePaise == chargePaise && current.ChargeBasis == basis,
            _ => PreparationCharge.Open(fulfillmentLocationId, variantId, preparationMethodId, preparationCode, chargePaise, basis, start));

    public static PlannedChange<TaxRule> PlanTaxRule(IReadOnlyList<TaxRule> periods, string taxCategoryCode, int rateBp, DateTimeOffset start, DateTimeOffset now, bool replaceFuture)
    {
        if (rateBp is < 0 or > TaxRule.MaxRateBp)
        {
            throw new RefusalException(new FieldError("rateBp", PricingErrors.ValidationFailed, "Enter a rate between 0 and 2800 basis points."));
        }

        return PeriodSchedule.Plan(
            periods,
            start,
            now,
            replaceFuture,
            current => current is not null && current.RateBp == rateBp,
            _ => TaxRule.Open(taxCategoryCode, rateBp, start));
    }

    /// <summary>EndPrice: closes the CURRENT period now (stop selling).</summary>
    public static VariantPrice EndPrice(IReadOnlyList<VariantPrice> periods, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(periods);
        var current = periods.FirstOrDefault(p => p.IsCurrentAt(now))
            ?? throw new DomainException(PricingErrors.PriceNotAvailable, "There is no current price to end.");
        if (current.ValidFrom == now)
        {
            throw new DomainException(PricingErrors.ValidationFailed, "The current price started this instant; retry in a moment.");
        }

        current.End(now);
        return current;
    }
}
