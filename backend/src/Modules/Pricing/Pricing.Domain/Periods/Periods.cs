using FreshOrbit.Pricing.Domain.Periods.Events;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Pricing.Domain.Periods;

/// <summary>Stored as PER_KG / PER_ITEM / FLAT (PRC-R7).</summary>
public enum ChargeBasis
{
    PerKg,
    PerItem,
    Flat,
}

/// <summary>
/// A half-open validity period <c>[ValidFrom, ValidTo)</c> (PRC-R2); <c>ValidTo</c> null = open-ended. Each row is its
/// own aggregate root so its <c>xmin</c> versions the schedule (the latest row's version is the schedule's version).
/// </summary>
public abstract class PricePeriod : AggregateRoot
{
    protected PricePeriod()
    {
    }

    protected PricePeriod(DateTimeOffset validFrom)
    {
        ValidFrom = validFrom;
    }

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset? ValidTo { get; private set; }

    public bool IsCurrentAt(DateTimeOffset at) => ValidFrom <= at && (ValidTo is null || ValidTo > at);

    public bool Contains(DateTimeOffset at) => ValidFrom < at && (ValidTo is null || ValidTo > at);

    /// <summary>Ends the period at <paramref name="at"/> (inside it).</summary>
    public void Close(DateTimeOffset at)
    {
        if (!Contains(at))
        {
            throw new InvalidOperationException($"Period {Id} [{ValidFrom:o}, {ValidTo:o}) does not contain {at:o}.");
        }

        ValidTo = at;
    }

    /// <summary>Records a bulk upload on one of the rows it wrote (the event needs an aggregate to travel with).</summary>
    public void RecordBulkApplied(Guid uploadId, int rowCount, IReadOnlyList<string> changedKeys)
        => Raise(new BulkPricesApplied(uploadId, rowCount, changedKeys));
}

/// <summary>Price of a variant at a fulfillment location (PRC-R1, PRC-R5); unit = the variant's sell unit.</summary>
public sealed class VariantPrice : PricePeriod
{
    public const long MaxUnitPricePaise = 5_000_000;

    private VariantPrice()
    {
    }

    private VariantPrice(Guid fulfillmentLocationId, Guid variantId, long unitPricePaise, long? compareAtPaise, DateTimeOffset validFrom)
        : base(validFrom)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        VariantId = variantId;
        UnitPricePaise = unitPricePaise;
        CompareAtPaise = compareAtPaise;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid VariantId { get; private set; }

    public long UnitPricePaise { get; private set; }

    public long? CompareAtPaise { get; private set; }

    /// <summary>PRC-R5 value rules, keyed under <paramref name="prefix"/>.</summary>
    public static void Validate(long unitPricePaise, long? compareAtPaise, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (unitPricePaise is < 1 or > MaxUnitPricePaise)
        {
            errors.Add(new FieldError($"{prefix}unitPricePaise", PricingErrors.ValidationFailed, "Enter a unit price between 1 and 5000000 paise."));
        }

        if (compareAtPaise is { } compareAt && compareAt <= unitPricePaise)
        {
            errors.Add(new FieldError($"{prefix}compareAtPaise", PricingErrors.ValidationFailed, "The compare-at price must be higher than the unit price, or empty."));
        }
    }

    internal static VariantPrice Open(Guid fulfillmentLocationId, Guid variantId, long unitPricePaise, long? compareAtPaise, DateTimeOffset validFrom, VariantPrice? previous)
    {
        var price = new VariantPrice(fulfillmentLocationId, variantId, unitPricePaise, compareAtPaise, validFrom);
        price.Raise(new VariantPriceScheduled(
            fulfillmentLocationId,
            variantId,
            previous is null ? null : new PriceValues(previous.UnitPricePaise, previous.CompareAtPaise),
            new PriceValues(unitPricePaise, compareAtPaise),
            validFrom));
        return price;
    }

    internal void End(DateTimeOffset at)
    {
        Close(at);
        Raise(new VariantPriceEnded(FulfillmentLocationId, VariantId, at));
    }
}

/// <summary>Charge for a preparation of a variant at a location (PRC-R7, PRC-R8); 0 must be explicit.</summary>
public sealed class PreparationCharge : PricePeriod
{
    public const long MaxChargePaise = 5_000_000;

    private PreparationCharge()
    {
        PreparationCode = string.Empty;
    }

    private PreparationCharge(Guid fulfillmentLocationId, Guid variantId, Guid preparationMethodId, string preparationCode, long chargePaise, ChargeBasis chargeBasis, DateTimeOffset validFrom)
        : base(validFrom)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        VariantId = variantId;
        PreparationMethodId = preparationMethodId;
        PreparationCode = preparationCode;
        ChargePaise = chargePaise;
        ChargeBasis = chargeBasis;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid PreparationMethodId { get; private set; }

    /// <summary>Code of the preparation method when the row was written (for audit and bulk diffs).</summary>
    public string PreparationCode { get; private set; }

    public long ChargePaise { get; private set; }

    public ChargeBasis ChargeBasis { get; private set; }

    /// <summary>PRC-R7: PER_KG only for KG variants, PER_ITEM only for PIECE/PACK; FLAT for any.</summary>
    public static void Validate(long chargePaise, ChargeBasis basis, bool soldByWeight, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (chargePaise is < 0 or > MaxChargePaise)
        {
            errors.Add(new FieldError($"{prefix}chargePaise", PricingErrors.ValidationFailed, "Enter a charge between 0 and 5000000 paise."));
        }

        if (!Enum.IsDefined(basis)
            || (basis == ChargeBasis.PerKg && !soldByWeight)
            || (basis == ChargeBasis.PerItem && soldByWeight))
        {
            errors.Add(new FieldError($"{prefix}chargeBasis", PricingErrors.ValidationFailed, soldByWeight
                ? "Use PER_KG or FLAT for a variant sold by weight."
                : "Use PER_ITEM or FLAT for a variant sold by piece or pack."));
        }
    }

    internal static PreparationCharge Open(Guid fulfillmentLocationId, Guid variantId, Guid preparationMethodId, string preparationCode, long chargePaise, ChargeBasis basis, DateTimeOffset validFrom)
    {
        var charge = new PreparationCharge(fulfillmentLocationId, variantId, preparationMethodId, preparationCode, chargePaise, basis, validFrom);
        charge.Raise(new PreparationChargeScheduled(fulfillmentLocationId, variantId, preparationCode, chargePaise, basis, validFrom));
        return charge;
    }
}

/// <summary>GST rate of a tax category for a period (PRC-R9); rates are data confirmed by the CA.</summary>
public sealed class TaxRule : PricePeriod
{
    public const int MaxRateBp = 2800;

    private TaxRule()
    {
        TaxCategoryCode = string.Empty;
    }

    private TaxRule(string taxCategoryCode, int rateBp, DateTimeOffset validFrom)
        : base(validFrom)
    {
        TaxCategoryCode = taxCategoryCode;
        RateBp = rateBp;
    }

    public string TaxCategoryCode { get; private set; }

    public int RateBp { get; private set; }

    internal static TaxRule Open(string taxCategoryCode, int rateBp, DateTimeOffset validFrom)
    {
        var rule = new TaxRule(taxCategoryCode, rateBp, validFrom);
        rule.Raise(new TaxRuleScheduled(taxCategoryCode, rateBp, validFrom));
        return rule;
    }
}

/// <summary>A tax category code (reference data, <c>pricing/tax-categories.json</c>).</summary>
public sealed class TaxCategory : Entity
{
    private TaxCategory()
    {
        Code = Name = string.Empty;
    }

    public TaxCategory(string code, string name, string? appliesTo)
    {
        Code = code;
        Name = name;
        AppliesTo = appliesTo;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? AppliesTo { get; private set; }

    public bool Sync(string name, string? appliesTo)
    {
        if (Name == name && AppliesTo == appliesTo)
        {
            return false;
        }

        Name = name;
        AppliesTo = appliesTo;
        return true;
    }
}
