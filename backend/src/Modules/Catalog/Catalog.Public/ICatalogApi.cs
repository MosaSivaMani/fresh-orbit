namespace FreshOrbit.Catalog.Public;

/// <summary>What other modules may ask the Catalog module.</summary>
public interface ICatalogApi
{
    /// <summary>The requested variants that exist, active or not (missing ids are simply absent).</summary>
    Task<IReadOnlyList<VariantInfo>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);
}

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

/// <param name="MinOrder">Minimum quantity in <paramref name="StockUnit"/> (grams or pieces); likewise MaxOrder and OrderStep.</param>
public sealed record VariantInfo(
    Guid VariantId,
    Guid ProductId,
    Guid CategoryId,
    string ProductName,
    string ProductSlug,
    string VariantName,
    string Sku,
    string? ImageKey,
    string HsnCode,
    string TaxCategoryCode,
    SellUnit SellUnit,
    StockUnit StockUnit,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    int MinOrder,
    int MaxOrder,
    int OrderStep,
    bool IsWeightVariable,
    bool IsActive,
    IReadOnlyList<PreparationInfo> Preparations);

/// <param name="YieldBp">Net weight after preparation as basis points of raw weight (6500 = 65 %).</param>
public sealed record PreparationInfo(Guid PreparationMethodId, string Code, string Name, int YieldBp, bool IsDefault, bool IsActive);
