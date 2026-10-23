using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Application.Common;
using FreshOrbit.Pricing.Application.Contracts;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Pricing.Application.Schedules;

/// <summary>
/// BulkSetPrices (PRC-R13): every row validated with PRC-R3 to PRC-R9, duplicates refused, all-or-nothing with errors
/// keyed <c>rows[i].field</c>; <c>dryRun</c> returns the diff without writing. One query per kind of data, one save.
/// </summary>
public sealed class BulkPriceService(
    IPricingRepository repository,
    ICatalogApi catalog,
    ILocationsApi locations,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor actor)
{
    public const int MaxRows = 5000;

    public async Task<BulkPriceResponse> ApplyAsync(BulkPriceRequest request, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = request.Rows ?? [];
        if (rows.Count is 0 or > MaxRows)
        {
            throw new RefusalException(new FieldError("rows", PricingErrors.ValidationFailed, $"Send 1 to {MaxRows} rows."));
        }

        var now = clock.UtcNow;
        var errors = new List<FieldError>();
        var fileStart = PeriodSchedule.ResolveStart(request.ValidFrom, now, "validFrom", errors);
        var replaceFuture = request.ReplaceFuture ?? false;

        var codes = rows.Select(r => r?.LocationCode?.Trim().ToUpperInvariant()).OfType<string>().Distinct().ToList();
        var skus = rows.Select(r => r?.Sku?.Trim().ToUpperInvariant()).OfType<string>().Distinct().ToList();
        var locationsByCode = codes.Count == 0 ? [] : (await locations.GetFulfillmentLocationsByCodesAsync(codes, cancellationToken)).ToDictionary(l => l.Code, StringComparer.Ordinal);
        var variantsBySku = skus.Count == 0 ? [] : (await catalog.GetVariantsBySkusAsync(skus, cancellationToken)).ToDictionary(v => v.Sku, StringComparer.Ordinal);

        var parsed = new List<ParsedRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = ParseRow(rows[i], i, fileStart, now, locationsByCode, variantsBySku, errors);
            if (row is not null && !seen.Add(row.Key))
            {
                errors.Add(new FieldError($"rows[{i}]", PricingErrors.ValidationFailed, "This key appears more than once in the file."));
                continue;
            }

            if (row is not null)
            {
                parsed.Add(row);
            }
        }

        var locationIds = parsed.Select(r => r.Location.Id).Distinct().ToList();
        var variantIds = parsed.Select(r => r.Variant.VariantId).Distinct().ToList();
        var prices = parsed.Any(r => r.Kind == BulkRowKind.Price) ? await repository.GetPricesAsync(locationIds, variantIds, cancellationToken) : [];
        var charges = parsed.Any(r => r.Kind == BulkRowKind.PrepCharge) ? await repository.GetChargesAsync(locationIds, variantIds, cancellationToken) : [];
        var taxCodes = parsed.Where(r => r.Kind == BulkRowKind.Price).Select(r => r.Variant.TaxCategoryCode).Where(c => c.Length > 0).Distinct().ToList();
        var taxRules = taxCodes.Count == 0 ? [] : await repository.GetTaxRulesAsync(taxCodes, cancellationToken);

        var results = new List<BulkRowResult>();
        var added = new List<PricePeriod>();
        var removed = new List<PricePeriod>();
        var changedKeys = new List<string>();
        foreach (var row in parsed)
        {
            try
            {
                var result = row.Kind == BulkRowKind.Price
                    ? PlanPrice(row, prices, taxRules, now, replaceFuture, added, removed, errors)
                    : PlanCharge(row, charges, now, replaceFuture, added, removed);
                if (result is null)
                {
                    continue;
                }

                results.Add(result);
                if (result.Status == BulkRowStatus.Changed)
                {
                    changedKeys.Add(row.Key);
                }
            }
            catch (DomainException exception)
            {
                errors.Add(new FieldError($"rows[{row.Index}].{exception.Field ?? "validFrom"}", exception.Code, exception.Message));
            }
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Guid? uploadId = null;
        if (!dryRun && added.Count > 0)
        {
            foreach (var period in removed)
            {
                repository.Remove(period);
            }

            foreach (var period in added)
            {
                repository.Add(period);
            }

            uploadId = IdGenerator.NewId();
            added[0].RecordBulkApplied(uploadId.Value, rows.Count, changedKeys);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new BulkPriceResponse(uploadId, dryRun, rows.Count, changedKeys.Count, results.Count - changedKeys.Count, results);
    }

    private ParsedRow? ParseRow(
        BulkPriceRow? row,
        int index,
        DateTimeOffset fileStart,
        DateTimeOffset now,
        Dictionary<string, FulfillmentLocationSummary> locationsByCode,
        Dictionary<string, VariantInfo> variantsBySku,
        List<FieldError> errors)
    {
        var prefix = $"rows[{index}].";
        if (row?.Kind is not { } kind)
        {
            errors.Add(new FieldError($"{prefix}kind", PricingErrors.ValidationFailed, "Use PRICE or PREP_CHARGE."));
            return null;
        }

        var before = errors.Count;
        var code = row.LocationCode?.Trim().ToUpperInvariant();
        FulfillmentLocationSummary? location = null;
        if (code is null || !locationsByCode.TryGetValue(code, out location))
        {
            errors.Add(new FieldError($"{prefix}locationCode", PricingErrors.ValidationFailed, "No fulfillment location has this code."));
        }
        else if (!actor.CanAccessLocation(location.Id))
        {
            errors.Add(new FieldError($"{prefix}locationCode", PricingErrors.LocationAccessDenied, "You do not have access to this fulfillment location."));
        }

        var sku = row.Sku?.Trim().ToUpperInvariant();
        VariantInfo? variant = null;
        if (sku is null || !variantsBySku.TryGetValue(sku, out variant))
        {
            errors.Add(new FieldError($"{prefix}sku", PricingErrors.ValidationFailed, "No variant has this SKU."));
        }
        else if (variant.ProductStatus == ProductStatus.Archived)
        {
            errors.Add(new FieldError($"{prefix}sku", PricingErrors.ProductNotAvailable, "The product is archived."));
        }

        var start = row.ValidFrom is null ? fileStart : PeriodSchedule.ResolveStart(row.ValidFrom, now, $"{prefix}validFrom", errors);
        PreparationInfo? preparation = null;
        if (kind == BulkRowKind.Price)
        {
            if (row.UnitPricePaise is not { } unitPrice)
            {
                errors.Add(new FieldError($"{prefix}unitPricePaise", PricingErrors.ValidationFailed, "Enter the unit price in paise."));
            }
            else
            {
                VariantPrice.Validate(unitPrice, row.CompareAtPaise, prefix, errors);
            }
        }
        else
        {
            var preparationCode = row.PreparationCode?.Trim().ToUpperInvariant();
            preparation = variant?.Preparations.FirstOrDefault(p => p.Code == preparationCode && p.IsActive);
            if (variant is not null && preparation is null)
            {
                errors.Add(new FieldError($"{prefix}preparationCode", PricingErrors.PreparationNotAvailable, "The preparation is not enabled on this variant."));
            }

            if (row.ChargePaise is not { } charge || row.ChargeBasis is not { } basis)
            {
                errors.Add(new FieldError($"{prefix}chargePaise", PricingErrors.ValidationFailed, "Enter the charge in paise and its basis."));
            }
            else if (variant is not null)
            {
                PreparationCharge.Validate(charge, basis, variant.SellUnit == SellUnit.Kg, prefix, errors);
            }
        }

        if (errors.Count > before)
        {
            return null;
        }

        var key = kind == BulkRowKind.Price ? $"PRICE:{location!.Code}:{variant!.Sku}" : $"PREP_CHARGE:{location!.Code}:{variant!.Sku}:{preparation!.Code}";
        return new ParsedRow(index, kind, key, location, variant, preparation, row, start);
    }

    private static BulkRowResult? PlanPrice(
        ParsedRow row,
        List<VariantPrice> prices,
        List<TaxRule> taxRules,
        DateTimeOffset now,
        bool replaceFuture,
        List<PricePeriod> added,
        List<PricePeriod> removed,
        List<FieldError> errors)
    {
        if (!taxRules.Any(r => r.TaxCategoryCode == row.Variant.TaxCategoryCode && r.IsCurrentAt(row.Start)))
        {
            errors.Add(new FieldError($"rows[{row.Index}].sku", PricingErrors.TaxRuleMissing, "No tax rule covers this time for the product's tax category."));
            return null;
        }

        var unitPrice = row.Source.UnitPricePaise!.Value;
        var periods = PricingService.Filter(prices, row.Location.Id, row.Variant.VariantId);
        var current = periods.FirstOrDefault(p => p.IsCurrentAt(now));
        var plan = PeriodFactory.PlanPrice(periods, row.Location.Id, row.Variant.VariantId, unitPrice, row.Source.CompareAtPaise, row.Start, now, replaceFuture);
        Collect(plan, added, removed);
        var previous = plan.Previous?.UnitPricePaise;
        return new BulkRowResult(
            row.Index,
            row.Kind,
            row.Location.Code,
            row.Variant.Sku,
            null,
            plan.Unchanged ? BulkRowStatus.Unchanged : BulkRowStatus.Changed,
            current is null ? null : new BulkValues(current.UnitPricePaise, current.CompareAtPaise, null, null),
            new BulkValues(unitPrice, row.Source.CompareAtPaise, null, null),
            row.Start,
            PricingRules.ChangeBp(previous, unitPrice),
            PeriodSchedule.IsLargeChange(previous, unitPrice));
    }

    private static BulkRowResult PlanCharge(ParsedRow row, List<PreparationCharge> charges, DateTimeOffset now, bool replaceFuture, List<PricePeriod> added, List<PricePeriod> removed)
    {
        var preparation = row.Preparation!;
        var charge = row.Source.ChargePaise!.Value;
        var basis = row.Source.ChargeBasis!.Value;
        var periods = charges.Where(c => c.FulfillmentLocationId == row.Location.Id && c.VariantId == row.Variant.VariantId && c.PreparationMethodId == preparation.PreparationMethodId).ToList();
        var current = periods.FirstOrDefault(p => p.IsCurrentAt(now));
        var plan = PeriodFactory.PlanCharge(periods, row.Location.Id, row.Variant.VariantId, preparation.PreparationMethodId, preparation.Code, charge, basis, row.Start, now, replaceFuture);
        Collect(plan, added, removed);
        return new BulkRowResult(
            row.Index,
            row.Kind,
            row.Location.Code,
            row.Variant.Sku,
            preparation.Code,
            plan.Unchanged ? BulkRowStatus.Unchanged : BulkRowStatus.Changed,
            current is null ? null : new BulkValues(null, null, current.ChargePaise, current.ChargeBasis),
            new BulkValues(null, null, charge, basis),
            row.Start,
            PricingRules.ChangeBp(plan.Previous?.ChargePaise, charge),
            false);
    }

    private static void Collect<T>(PlannedChange<T> plan, List<PricePeriod> added, List<PricePeriod> removed)
        where T : PricePeriod
    {
        if (plan.Added is not null)
        {
            added.Add(plan.Added);
        }

        removed.AddRange(plan.Removed);
    }

    private sealed record ParsedRow(
        int Index,
        BulkRowKind Kind,
        string Key,
        FulfillmentLocationSummary Location,
        VariantInfo Variant,
        PreparationInfo? Preparation,
        BulkPriceRow Source,
        DateTimeOffset Start);
}
