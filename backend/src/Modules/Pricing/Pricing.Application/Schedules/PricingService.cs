using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Application.Common;
using FreshOrbit.Pricing.Application.Contracts;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Pricing.Application.Schedules;

/// <summary>Price, preparation charge and tax rule schedules (pricing.md §5).</summary>
public sealed class PricingService(
    IPricingRepository repository,
    ICatalogApi catalog,
    ILocationsApi locations,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor actor)
{
    public async Task<PriceScheduleResponse> GetPriceAsync(Guid locationId, Guid variantId, CancellationToken cancellationToken)
    {
        PricingRules.EnsureLocationAccess(actor, locationId);
        await RequireLocationAsync(locationId, cancellationToken);
        await RequireVariantAsync(variantId, cancellationToken);
        var periods = await repository.GetPricesAsync([locationId], [variantId], cancellationToken);
        return ToPriceResponse(locationId, variantId, Filter(periods, locationId, variantId), clock.UtcNow, false, false);
    }

    /// <summary>SetPrice (PRC-R3 to PRC-R5, PRC-R9, PRC-R15).</summary>
    public async Task<PriceScheduleResponse> SetPriceAsync(Guid locationId, Guid variantId, SchedulePrecondition precondition, SetPriceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PricingRules.EnsureLocationAccess(actor, locationId);
        var now = clock.UtcNow;
        var errors = new List<FieldError>();
        VariantPrice.Validate(request.UnitPricePaise, request.CompareAtPaise, string.Empty, errors);
        var start = PeriodSchedule.ResolveStart(request.ValidFrom, now, "validFrom", errors);
        ThrowIfAny(errors);
        await RequireLocationAsync(locationId, cancellationToken);
        var variant = await RequireSellableVariantAsync(variantId, cancellationToken);
        await RequireTaxRuleAsync(variant.TaxCategoryCode, start, "taxCategoryCode", cancellationToken);

        var periods = Filter(await repository.GetPricesAsync([locationId], [variantId], cancellationToken), locationId, variantId);
        var latest = SchedulePreconditions.Check(periods, precondition, unitOfWork.SetExpectedVersion);
        var plan = PeriodFactory.PlanPrice(periods, locationId, variantId, request.UnitPricePaise, request.CompareAtPaise, start, now, request.ReplaceFuture ?? false);
        var largeChange = PeriodSchedule.IsLargeChange(plan.Previous?.UnitPricePaise, request.UnitPricePaise);
        if (plan.Unchanged)
        {
            return ToPriceResponse(locationId, variantId, periods, now, true, false);
        }

        Apply(plan, latest);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var after = periods.Except(plan.Removed).Append(plan.Added!).ToList();
        return ToPriceResponse(locationId, variantId, after, now, false, largeChange);
    }

    /// <summary>EndPrice: stop selling now.</summary>
    public async Task<PriceScheduleResponse> EndPriceAsync(Guid locationId, Guid variantId, SchedulePrecondition precondition, CancellationToken cancellationToken)
    {
        PricingRules.EnsureLocationAccess(actor, locationId);
        var now = clock.UtcNow;
        var periods = Filter(await repository.GetPricesAsync([locationId], [variantId], cancellationToken), locationId, variantId);
        var latest = SchedulePreconditions.Check(periods, precondition, unitOfWork.SetExpectedVersion);
        var ended = PeriodFactory.EndPrice(periods, now);
        if (latest is not null && latest != ended)
        {
            repository.MarkChanged(latest);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToPriceResponse(locationId, variantId, periods, now, false, false);
    }

    public async Task<ChargeScheduleResponse> GetChargeAsync(Guid locationId, Guid variantId, string preparationCode, CancellationToken cancellationToken)
    {
        PricingRules.EnsureLocationAccess(actor, locationId);
        await RequireLocationAsync(locationId, cancellationToken);
        var variant = await RequireVariantAsync(variantId, cancellationToken);
        var preparation = RequirePreparation(variant, preparationCode);
        var periods = await LoadChargesAsync(locationId, variantId, preparation.PreparationMethodId, cancellationToken);
        return ToChargeResponse(locationId, variantId, preparation.Code, periods, clock.UtcNow, false);
    }

    /// <summary>SetPreparationCharge (PRC-R3, PRC-R4, PRC-R7, PRC-R8).</summary>
    public async Task<ChargeScheduleResponse> SetChargeAsync(Guid locationId, Guid variantId, string preparationCode, SchedulePrecondition precondition, SetChargeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PricingRules.EnsureLocationAccess(actor, locationId);
        var now = clock.UtcNow;
        await RequireLocationAsync(locationId, cancellationToken);
        var variant = await RequireSellableVariantAsync(variantId, cancellationToken);
        var preparation = RequirePreparation(variant, preparationCode);
        var errors = new List<FieldError>();
        PreparationCharge.Validate(request.ChargePaise, request.ChargeBasis, variant.SellUnit == SellUnit.Kg, string.Empty, errors);
        var start = PeriodSchedule.ResolveStart(request.ValidFrom, now, "validFrom", errors);
        ThrowIfAny(errors);

        var periods = await LoadChargesAsync(locationId, variantId, preparation.PreparationMethodId, cancellationToken);
        var latest = SchedulePreconditions.Check(periods, precondition, unitOfWork.SetExpectedVersion);
        var plan = PeriodFactory.PlanCharge(periods, locationId, variantId, preparation.PreparationMethodId, preparation.Code, request.ChargePaise, request.ChargeBasis, start, now, request.ReplaceFuture ?? false);
        if (plan.Unchanged)
        {
            return ToChargeResponse(locationId, variantId, preparation.Code, periods, now, true);
        }

        Apply(plan, latest);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToChargeResponse(locationId, variantId, preparation.Code, [.. periods.Except(plan.Removed).Append(plan.Added!)], now, false);
    }

    public async Task<IReadOnlyList<TaxCategoryRulesResponse>> ListTaxRulesAsync(CancellationToken cancellationToken)
    {
        var categories = await repository.ListTaxCategoriesAsync(cancellationToken);
        var rules = await repository.GetTaxRulesAsync([.. categories.Select(c => c.Code)], cancellationToken);
        var now = clock.UtcNow;
        return [.. categories.OrderBy(c => c.Code, StringComparer.Ordinal).Select(c => ToTaxResponse(c, [.. rules.Where(r => r.TaxCategoryCode == c.Code)], now, false))];
    }

    /// <summary>ScheduleTaxRule: needs <c>tax.manage</c> (D35), rate 0–2800 bp, PRC-R3/R4 periods. Rates are data confirmed by the CA.</summary>
    public async Task<TaxCategoryRulesResponse> ScheduleTaxRuleAsync(ScheduleTaxRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PricingRules.EnsureCanManageTax(actor);
        var now = clock.UtcNow;
        var errors = new List<FieldError>();
        var code = request.TaxCategoryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var start = PeriodSchedule.ResolveStart(request.ValidFrom, now, "validFrom", errors);
        ThrowIfAny(errors);
        var category = (await repository.ListTaxCategoriesAsync(cancellationToken)).FirstOrDefault(c => c.Code == code)
            ?? throw new RefusalException(new FieldError("taxCategoryCode", PricingErrors.ValidationFailed, "Choose a tax category from the reference list."));
        var periods = await repository.GetTaxRulesAsync([code], cancellationToken);
        var latest = SchedulePreconditions.Latest(periods);
        var plan = PeriodFactory.PlanTaxRule(periods, code, request.RateBp, start, now, request.ReplaceFuture ?? false);
        if (plan.Unchanged)
        {
            return ToTaxResponse(category, periods, now, true);
        }

        Apply(plan, latest);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToTaxResponse(category, [.. periods.Except(plan.Removed).Append(plan.Added!)], now, false);
    }

    /// <summary>ListPrices: sellable or priced variants at one location with CURRENT and next price; <paramref name="missingOnly"/> keeps unpriced ones.</summary>
    public async Task<(IReadOnlyList<PriceListItem> Items, int TotalCount)> ListPricesAsync(
        Guid? locationId,
        Guid? variantId,
        string? q,
        bool missingOnly,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (locationId is not { } location)
        {
            throw new RefusalException(new FieldError("fulfillmentLocationId", PricingErrors.ValidationFailed, "Choose the fulfillment location to list prices for."));
        }

        PricingRules.EnsureLocationAccess(actor, location);
        await RequireLocationAsync(location, cancellationToken);
        var now = clock.UtcNow;
        var ids = new HashSet<Guid>(await catalog.GetSellableVariantIdsAsync(cancellationToken));
        ids.UnionWith(await repository.GetPricedVariantIdsAsync(location, now, cancellationToken));
        if (variantId is { } only)
        {
            ids.IntersectWith([only]);
        }

        var variants = await catalog.GetVariantsAsync(ids, cancellationToken);
        var term = q?.Trim();
        var filtered = variants
            .Where(v => string.IsNullOrEmpty(term)
                || v.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || v.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || v.VariantName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var prices = await repository.GetPricesAsync([location], [.. filtered.Select(v => v.VariantId)], cancellationToken);
        var items = filtered
            .Select(v =>
            {
                var periods = Filter(prices, location, v.VariantId);
                var current = periods.FirstOrDefault(p => p.IsCurrentAt(now));
                var next = periods.Where(p => p.ValidFrom > now).MinBy(p => p.ValidFrom);
                return new PriceListItem(location, v.VariantId, v.ProductId, v.Sku, v.ProductName, v.VariantName, PricingRules.PriceUnit(v.SellUnit), ToPeriod(current), ToPeriod(next), current is null);
            })
            .Where(i => !missingOnly || i.Missing)
            .OrderBy(i => i.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .ToList();
        return ([.. items.Skip(skip).Take(take)], items.Count);
    }

    internal static void ThrowIfAny(List<FieldError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw errors.Count == 1 ? new RefusalException(errors[0]) : new RefusalException(errors);
    }

    internal static List<VariantPrice> Filter(IEnumerable<VariantPrice> prices, Guid locationId, Guid variantId)
        => [.. prices.Where(p => p.FulfillmentLocationId == locationId && p.VariantId == variantId)];

    private void Apply<T>(PlannedChange<T> plan, T? latest)
        where T : PricePeriod
    {
        foreach (var removed in plan.Removed)
        {
            repository.Remove(removed);
        }

        repository.Add(plan.Added!);
        if (latest is not null && !plan.Removed.Contains(latest) && latest != plan.Closed)
        {
            repository.MarkChanged(latest);
        }
    }

    private async Task<List<PreparationCharge>> LoadChargesAsync(Guid locationId, Guid variantId, Guid methodId, CancellationToken cancellationToken)
        => [.. (await repository.GetChargesAsync([locationId], [variantId], cancellationToken))
            .Where(c => c.FulfillmentLocationId == locationId && c.VariantId == variantId && c.PreparationMethodId == methodId)];

    private async Task RequireLocationAsync(Guid locationId, CancellationToken cancellationToken)
    {
        if ((await locations.GetFulfillmentLocationsAsync([locationId], cancellationToken)).Count == 0)
        {
            throw new NotFoundException(PricingErrors.FulfillmentLocationNotFound, "No fulfillment location has this id.");
        }
    }

    private async Task<VariantInfo> RequireVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var variants = await catalog.GetVariantsAsync([variantId], cancellationToken);
        return variants.Count > 0 ? variants[0] : throw new NotFoundException(PricingErrors.VariantNotFound, "No variant has this id.");
    }

    /// <summary>PRC-R15: DRAFT products may be priced ahead of publishing; ARCHIVED products may not.</summary>
    private async Task<VariantInfo> RequireSellableVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var variant = await RequireVariantAsync(variantId, cancellationToken);
        if (variant.ProductStatus == ProductStatus.Archived)
        {
            throw new DomainException(PricingErrors.ProductNotAvailable, "The product is archived; restore it before pricing.");
        }

        return variant;
    }

    private static PreparationInfo RequirePreparation(VariantInfo variant, string preparationCode)
    {
        var code = preparationCode.Trim().ToUpperInvariant();
        return variant.Preparations.FirstOrDefault(p => p.Code == code && p.IsActive)
            ?? throw new DomainException(PricingErrors.PreparationNotAvailable, "The preparation is not enabled on this variant.", "code");
    }

    /// <summary>PRC-R9: a price is refused when no tax rule covers its start.</summary>
    private async Task RequireTaxRuleAsync(string taxCategoryCode, DateTimeOffset at, string field, CancellationToken cancellationToken)
    {
        var rules = string.IsNullOrEmpty(taxCategoryCode) ? [] : await repository.GetTaxRulesAsync([taxCategoryCode], cancellationToken);
        if (!rules.Any(r => r.IsCurrentAt(at)))
        {
            throw new DomainException(PricingErrors.TaxRuleMissing, "No tax rule covers this time for the product's tax category; add one first.", field);
        }
    }

    private static PriceScheduleResponse ToPriceResponse(Guid locationId, Guid variantId, IReadOnlyList<VariantPrice> periods, DateTimeOffset now, bool unchanged, bool largeChange)
    {
        var latest = SchedulePreconditions.Latest(periods);
        return new PriceScheduleResponse(
            locationId,
            variantId,
            latest is null ? null : PricingRules.Version(latest.Version),
            ToPeriod(periods.FirstOrDefault(p => p.IsCurrentAt(now))),
            ToPeriod(periods.Where(p => p.ValidFrom > now).MinBy(p => p.ValidFrom)),
            [.. periods.OrderByDescending(p => p.ValidFrom).Select(p => ToPeriod(p)!)],
            unchanged,
            largeChange);
    }

    private static ChargeScheduleResponse ToChargeResponse(Guid locationId, Guid variantId, string code, IReadOnlyList<PreparationCharge> periods, DateTimeOffset now, bool unchanged)
    {
        var latest = SchedulePreconditions.Latest(periods);
        return new ChargeScheduleResponse(
            locationId,
            variantId,
            code,
            latest is null ? null : PricingRules.Version(latest.Version),
            ToPeriod(periods.FirstOrDefault(p => p.IsCurrentAt(now))),
            ToPeriod(periods.Where(p => p.ValidFrom > now).MinBy(p => p.ValidFrom)),
            [.. periods.OrderByDescending(p => p.ValidFrom).Select(p => ToPeriod(p)!)],
            unchanged);
    }

    private static TaxCategoryRulesResponse ToTaxResponse(TaxCategory category, IReadOnlyList<TaxRule> periods, DateTimeOffset now, bool unchanged)
    {
        var latest = SchedulePreconditions.Latest(periods);
        return new TaxCategoryRulesResponse(
            category.Code,
            category.Name,
            latest is null ? null : PricingRules.Version(latest.Version),
            ToPeriod(periods.FirstOrDefault(p => p.IsCurrentAt(now))),
            ToPeriod(periods.Where(p => p.ValidFrom > now).MinBy(p => p.ValidFrom)),
            [.. periods.OrderByDescending(p => p.ValidFrom).Select(p => ToPeriod(p)!)],
            unchanged);
    }

    private static PricePeriodResponse? ToPeriod(VariantPrice? p)
        => p is null ? null : new PricePeriodResponse(p.Id, p.UnitPricePaise, p.CompareAtPaise, p.ValidFrom, p.ValidTo);

    private static ChargePeriodResponse? ToPeriod(PreparationCharge? p)
        => p is null ? null : new ChargePeriodResponse(p.Id, p.ChargePaise, p.ChargeBasis, p.ValidFrom, p.ValidTo);

    private static TaxRulePeriodResponse? ToPeriod(TaxRule? p)
        => p is null ? null : new TaxRulePeriodResponse(p.Id, p.RateBp, p.ValidFrom, p.ValidTo);
}
