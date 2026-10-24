using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Catalog.Domain.Products;

/// <summary>Pieces, serves and net weight of a variant for one serving basis, or for an ordered quantity.</summary>
/// <param name="BasisQuantity">The quantity the facts describe, in the variant's quantity unit.</param>
/// <param name="NetWeightGrams">Net weight of that quantity; null for PIECE variants without an average piece weight.</param>
public sealed record ServingFacts(int BasisQuantity, QuantityUnit BasisUnit, int? NetWeightGrams, int? PiecesMin, int? PiecesMax, int? Serves);

/// <summary>
/// CAT-R21: what a variant contains. Pieces and serves are stated per <em>serving basis</em>: 1 kg of net weight for KG
/// variants, one pack for PACK, one piece for PIECE (a piece is one piece, so PIECE variants state no pieces range).
/// The server scales them to an ordered quantity; clients never do.
/// </summary>
public static class ServingRules
{
    public const int MaxPieces = 1000;
    public const int MaxServes = 100;

    /// <summary>The serving basis of a sell unit in its quantity unit: 1 000 g, 1 pack or 1 piece.</summary>
    public static int BasisQuantity(SellUnit sellUnit) => sellUnit == SellUnit.Kg ? Weight.GramsPerKg : 1;

    /// <summary>Field errors (keyed under <paramref name="prefix"/>) of the pieces range and serves of <paramref name="spec"/>.</summary>
    public static void Validate(VariantSpec spec, string prefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(errors);
        if (spec.PiecesMin is not null || spec.PiecesMax is not null)
        {
            if (spec.SellUnit == SellUnit.Piece)
            {
                errors.Add(new FieldError($"{prefix}piecesMin", CatalogErrors.ValidationFailed, "A variant sold per piece has no pieces range; leave piecesMin and piecesMax empty."));
            }
            else if (spec.PiecesMin is not { } min || spec.PiecesMax is not { } max)
            {
                errors.Add(new FieldError($"{prefix}piecesMin", CatalogErrors.ValidationFailed, "Enter both piecesMin and piecesMax, or leave both empty."));
            }
            else if (min is < 1 or > MaxPieces)
            {
                errors.Add(new FieldError($"{prefix}piecesMin", CatalogErrors.ValidationFailed, $"Enter piecesMin between 1 and {MaxPieces}."));
            }
            else if (max < min || max > MaxPieces)
            {
                errors.Add(new FieldError($"{prefix}piecesMax", CatalogErrors.ValidationFailed, $"Enter piecesMax between piecesMin and {MaxPieces}."));
            }
        }

        if (spec.Serves is { } serves && serves is < 1 or > MaxServes)
        {
            errors.Add(new FieldError($"{prefix}serves", CatalogErrors.ValidationFailed, $"Enter serves between 1 and {MaxServes}, or leave it empty."));
        }
    }

    /// <summary>The facts of one serving basis of the variant.</summary>
    public static ServingFacts ForBasis(SellUnit sellUnit, int? packSizeGrams, int? avgPieceGrams, int? piecesMin, int? piecesMax, int? serves)
        => ForQuantity(sellUnit, packSizeGrams, avgPieceGrams, piecesMin, piecesMax, serves, BasisQuantity(sellUnit));

    /// <summary>
    /// The facts of <paramref name="quantity"/> (grams for KG, else a count): net weight = grams, packs × pack size or
    /// pieces × average piece weight; pieces = floor(min × q / basis) to ceil(max × q / basis), at least 1; serves =
    /// floor(serves × q / basis), at least 1. A quantity of 0 has no pieces or serves.
    /// </summary>
    public static ServingFacts ForQuantity(SellUnit sellUnit, int? packSizeGrams, int? avgPieceGrams, int? piecesMin, int? piecesMax, int? serves, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        var basis = BasisQuantity(sellUnit);
        int? netWeight = sellUnit switch
        {
            SellUnit.Kg => quantity,
            SellUnit.Pack => packSizeGrams is { } pack ? checked(pack * quantity) : null,
            _ => avgPieceGrams is { } piece ? checked(piece * quantity) : null,
        };
        int? low = null;
        int? high = null;
        if (piecesMin is { } min && piecesMax is { } max && quantity > 0)
        {
            low = Math.Max(1, (int)((long)min * quantity / basis));
            high = Math.Max(low.Value, (int)(((long)max * quantity + basis - 1) / basis));
        }

        int? people = serves is { } s && quantity > 0 ? Math.Max(1, (int)((long)s * quantity / basis)) : null;
        return new ServingFacts(quantity, QuantityRules.UnitFor(sellUnit), netWeight, low, high, people);
    }
}
