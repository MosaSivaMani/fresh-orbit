using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Catalog.Domain.Products;

/// <summary>How a variant is sold. Stored as KG / PIECE / PACK.</summary>
public enum SellUnit
{
    Kg,
    Piece,
    Pack,
}

/// <summary>How a variant's stock is counted. Stored as GRAM / PIECE.</summary>
public enum StockUnit
{
    Gram,
    Piece,
}

/// <summary>Unit of a quantity in requests and responses: GRAM (KG variants), PIECE or PACK.</summary>
public enum QuantityUnit
{
    Gram,
    Piece,
    Pack,
}

/// <summary>A preparation offered on a variant, as requested.</summary>
/// <param name="MethodCode">Code of the preparation method (WHOLE, STEAK, …).</param>
public sealed record PreparationSpec(Guid PreparationMethodId, string MethodCode, int YieldBp, bool IsDefault);

/// <summary>The editable definition of a variant.</summary>
/// <param name="PiecesMin">Fewest pieces per serving basis (see <see cref="ServingRules"/>); null when not stated.</param>
/// <param name="PiecesMax">Most pieces per serving basis; null when not stated.</param>
/// <param name="Serves">People served per serving basis; null when not stated.</param>
public sealed record VariantSpec(
    string? Name,
    SellUnit SellUnit,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    int? MinOrder,
    int? MaxOrder,
    int? OrderStep,
    IReadOnlyList<PreparationSpec> Preparations,
    int? PiecesMin = null,
    int? PiecesMax = null,
    int? Serves = null);

/// <summary>The units a variant ends up with (CAT-R4).</summary>
public sealed record VariantUnits(SellUnit SellUnit, StockUnit StockUnit, bool IsWeightVariable, int MinOrder, int MaxOrder, int OrderStep);

/// <summary>CAT-R4 to CAT-R7: units, preparations, yield and quantity rules of a variant.</summary>
public static class VariantRules
{
    public const int NameMaxLength = 80;
    public const int DefaultKgMinOrder = 250;
    public const int DefaultKgMaxOrder = 5000;
    public const int DefaultKgOrderStep = 250;

    /// <summary>
    /// Validates <paramref name="spec"/>, adding errors keyed under <paramref name="prefix"/> (e.g. <c>variants[0].</c>),
    /// and returns the derived units (KG → GRAM stock, weight-variable; PIECE and PACK → PIECE stock).
    /// </summary>
    public static VariantUnits Validate(VariantSpec spec, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(spec.Name) || spec.Name.Trim().Length > NameMaxLength)
        {
            errors.Add(new FieldError($"{prefix}name", CatalogErrors.ValidationFailed, $"Enter a variant name of 1 to {NameMaxLength} characters."));
        }

        var stockUnit = spec.SellUnit == SellUnit.Kg ? StockUnit.Gram : StockUnit.Piece;
        switch (spec.SellUnit)
        {
            case SellUnit.Kg:
                if (spec.PackSizeGrams is not null)
                {
                    errors.Add(new FieldError($"{prefix}packSizeGrams", CatalogErrors.ValidationFailed, "A variant sold per kg has no pack size; leave packSizeGrams empty."));
                }

                break;
            case SellUnit.Piece:
                if (spec.AvgPieceGrams is not > 0)
                {
                    errors.Add(new FieldError($"{prefix}avgPieceGrams", CatalogErrors.ValidationFailed, "Enter the average piece weight in grams for a variant sold per piece."));
                }

                break;
            case SellUnit.Pack:
                if (spec.PackSizeGrams is not > 0)
                {
                    errors.Add(new FieldError($"{prefix}packSizeGrams", CatalogErrors.ValidationFailed, "Enter the net pack size in grams for a variant sold per pack."));
                }

                break;
            default:
                errors.Add(new FieldError($"{prefix}sellUnit", CatalogErrors.ValidationFailed, "Choose KG, PIECE or PACK as the sell unit."));
                break;
        }

        int min;
        int max;
        int step;
        if (spec.SellUnit == SellUnit.Kg)
        {
            min = spec.MinOrder ?? DefaultKgMinOrder;
            max = spec.MaxOrder ?? DefaultKgMaxOrder;
            step = spec.OrderStep ?? DefaultKgOrderStep;
        }
        else
        {
            if (spec.MinOrder is null || spec.MaxOrder is null || spec.OrderStep is null)
            {
                errors.Add(new FieldError($"{prefix}minOrder", CatalogErrors.ValidationFailed, "Enter minOrder, maxOrder and orderStep in pieces or packs."));
            }

            min = spec.MinOrder ?? 0;
            max = spec.MaxOrder ?? 0;
            step = spec.OrderStep ?? 0;
        }

        QuantityRules.ValidateConfiguration(min, max, step, prefix, errors);
        ServingRules.Validate(spec, prefix, errors);
        ValidatePreparations(spec, prefix, errors);
        return new VariantUnits(spec.SellUnit, stockUnit, spec.SellUnit == SellUnit.Kg, min, max, step);
    }

    /// <summary>CAT-R5 and CAT-R6 for the requested preparations of an active variant.</summary>
    public static void ValidatePreparations(VariantSpec spec, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(errors);
        var preparations = spec.Preparations;
        if (preparations.Count == 0)
        {
            errors.Add(new FieldError($"{prefix}preparations", CatalogErrors.ValidationFailed, "Offer at least one preparation."));
            return;
        }

        if (preparations.Count(p => p.IsDefault) != 1)
        {
            errors.Add(new FieldError($"{prefix}preparations", CatalogErrors.DefaultPreparationRequired, "Mark exactly one preparation as the default."));
        }

        var seen = new HashSet<Guid>();
        for (var i = 0; i < preparations.Count; i++)
        {
            var preparation = preparations[i];
            var field = $"{prefix}preparations[{i}]";
            if (!seen.Add(preparation.PreparationMethodId))
            {
                errors.Add(new FieldError($"{field}.code", CatalogErrors.ValidationFailed, "Offer each preparation once."));
            }

            if (preparation.YieldBp is < 1 or > BasisPoints.Whole)
            {
                errors.Add(new FieldError($"{field}.yieldBp", CatalogErrors.YieldInvalid, "Enter a yield between 1 and 10000 basis points."));
            }
            else if (spec.SellUnit != SellUnit.Kg && preparation.YieldBp != BasisPoints.Whole)
            {
                errors.Add(new FieldError($"{field}.yieldBp", CatalogErrors.ValidationFailed, "Pieces and packs are counted, not weighed: use a yield of 10000."));
            }
            else if (preparation.MethodCode == Preparations.PreparationMethod.WholeCode && preparation.YieldBp != BasisPoints.Whole)
            {
                errors.Add(new FieldError($"{field}.yieldBp", CatalogErrors.ValidationFailed, "WHOLE is sold as received: use a yield of 10000."));
            }
        }
    }
}

/// <summary>CAT-R7: quantity rules in the variant's stock unit (grams or pieces).</summary>
public static class QuantityRules
{
    public static void ValidateConfiguration(int minOrder, int maxOrder, int orderStep, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (orderStep <= 0)
        {
            errors.Add(new FieldError($"{prefix}orderStep", CatalogErrors.ValidationFailed, "Enter an order step greater than 0."));
            return;
        }

        if (minOrder < orderStep || minOrder % orderStep != 0)
        {
            errors.Add(new FieldError($"{prefix}minOrder", CatalogErrors.ValidationFailed, "Enter a minimum order that is a multiple of the order step and at least one step."));
        }

        if (maxOrder < minOrder || maxOrder % orderStep != 0)
        {
            errors.Add(new FieldError($"{prefix}maxOrder", CatalogErrors.ValidationFailed, "Enter a maximum order that is a multiple of the order step and not below the minimum."));
        }
    }

    /// <summary>The unit a quantity of a variant sold in <paramref name="sellUnit"/> must use.</summary>
    public static QuantityUnit UnitFor(SellUnit sellUnit) => sellUnit switch
    {
        SellUnit.Kg => QuantityUnit.Gram,
        SellUnit.Piece => QuantityUnit.Piece,
        SellUnit.Pack => QuantityUnit.Pack,
        _ => throw new ArgumentOutOfRangeException(nameof(sellUnit), sellUnit, null),
    };

    /// <summary>Null when <paramref name="quantity"/> is valid; otherwise the error code (QUANTITY_*).</summary>
    public static string? Check(int quantity, QuantityUnit unit, SellUnit sellUnit, int minOrder, int maxOrder, int orderStep)
    {
        if (unit != UnitFor(sellUnit))
        {
            return CatalogErrors.QuantityUnitInvalid;
        }

        if (quantity < minOrder)
        {
            return CatalogErrors.QuantityBelowMin;
        }

        if (quantity > maxOrder)
        {
            return CatalogErrors.QuantityAboveMax;
        }

        return quantity % orderStep != 0 ? CatalogErrors.QuantityStepInvalid : null;
    }
}
