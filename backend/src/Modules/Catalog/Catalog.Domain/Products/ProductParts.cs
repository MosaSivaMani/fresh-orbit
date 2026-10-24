using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Catalog.Domain.Products;

/// <summary>Stored as DRAFT / ACTIVE / ARCHIVED.</summary>
public enum ProductStatus
{
    Draft,
    Active,
    Archived,
}

/// <summary>Stored as WILD / FARMED.</summary>
public enum SourceType
{
    Wild,
    Farmed,
}

/// <summary>A sellable variant of a product (size, grade). Changes only through <see cref="Product"/>.</summary>
public sealed class ProductVariant : Entity
{
    private readonly List<VariantPreparation> _preparations = [];

    private ProductVariant()
    {
        Sku = Name = string.Empty;
    }

    internal ProductVariant(Guid productId, string sku)
    {
        ProductId = productId;
        Sku = sku;
        Name = string.Empty;
    }

    public Guid ProductId { get; private set; }

    /// <summary>CAT-R8: unique, immutable (a variant is active from the moment it is added).</summary>
    public string Sku { get; private set; }

    public string Name { get; private set; }

    public SellUnit SellUnit { get; private set; }

    public StockUnit StockUnit { get; private set; }

    public int? PackSizeGrams { get; private set; }

    public int? AvgPieceGrams { get; private set; }

    public int MinOrder { get; private set; }

    public int MaxOrder { get; private set; }

    public int OrderStep { get; private set; }

    /// <summary>Fewest pieces per serving basis (1 kg net for KG, one pack for PACK; never for PIECE).</summary>
    public int? PiecesMin { get; private set; }

    /// <summary>Most pieces per serving basis.</summary>
    public int? PiecesMax { get; private set; }

    /// <summary>People served per serving basis (1 kg net for KG, one pack, one piece).</summary>
    public int? Serves { get; private set; }

    public bool IsWeightVariable { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<VariantPreparation> Preparations => _preparations;

    public VariantPreparation? DefaultPreparation => _preparations.FirstOrDefault(p => p.IsActive && p.IsDefault);

    internal VariantSpec ToSpec(Func<Guid, string> methodCode) => new(
        Name,
        SellUnit,
        PackSizeGrams,
        AvgPieceGrams,
        MinOrder,
        MaxOrder,
        OrderStep,
        [.. _preparations.Where(p => p.IsActive).Select(p => new PreparationSpec(p.PreparationMethodId, methodCode(p.PreparationMethodId), p.YieldBp, p.IsDefault))],
        PiecesMin,
        PiecesMax,
        Serves);

    /// <summary>Applies an already validated spec; returns whether the active preparations changed.</summary>
    internal bool Apply(VariantSpec spec, VariantUnits units, bool isActive)
    {
        Name = spec.Name!.Trim();
        SellUnit = units.SellUnit;
        StockUnit = units.StockUnit;
        IsWeightVariable = units.IsWeightVariable;
        PackSizeGrams = spec.SellUnit == SellUnit.Pack ? spec.PackSizeGrams : null;
        AvgPieceGrams = spec.AvgPieceGrams;
        MinOrder = units.MinOrder;
        MaxOrder = units.MaxOrder;
        OrderStep = units.OrderStep;
        PiecesMin = spec.PiecesMin;
        PiecesMax = spec.PiecesMax;
        Serves = spec.Serves;
        IsActive = isActive;
        return ReplacePreparations(spec.Preparations);
    }

    private bool ReplacePreparations(IReadOnlyList<PreparationSpec> requested)
    {
        var changed = false;
        foreach (var existing in _preparations)
        {
            var match = requested.FirstOrDefault(r => r.PreparationMethodId == existing.PreparationMethodId);
            changed |= match is null ? existing.Deactivate() : existing.Set(match.YieldBp, match.IsDefault);
        }

        foreach (var added in requested.Where(r => _preparations.All(p => p.PreparationMethodId != r.PreparationMethodId)))
        {
            _preparations.Add(new VariantPreparation(Id, added.PreparationMethodId, added.YieldBp, added.IsDefault));
            changed = true;
        }

        return changed;
    }
}

/// <summary>A preparation offered on a variant with its yield (CAT-R5, CAT-R6). Never deleted, only deactivated.</summary>
public sealed class VariantPreparation
{
    private VariantPreparation()
    {
    }

    internal VariantPreparation(Guid variantId, Guid preparationMethodId, int yieldBp, bool isDefault)
    {
        VariantId = variantId;
        PreparationMethodId = preparationMethodId;
        YieldBp = yieldBp;
        IsDefault = isDefault;
        IsActive = true;
    }

    public Guid VariantId { get; private set; }

    public Guid PreparationMethodId { get; private set; }

    /// <summary>Net weight after preparation as basis points of raw weight.</summary>
    public int YieldBp { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    internal bool Set(int yieldBp, bool isDefault)
    {
        if (IsActive && YieldBp == yieldBp && IsDefault == isDefault)
        {
            return false;
        }

        YieldBp = yieldBp;
        IsDefault = isDefault;
        IsActive = true;
        return true;
    }

    internal bool Deactivate()
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        IsDefault = false;
        return true;
    }
}

/// <summary>A product image; the bytes live in file storage under <see cref="StorageKey"/> (CAT-R16).</summary>
public sealed class ProductImage : Entity
{
    public const int AltTextMaxLength = 200;

    private ProductImage()
    {
        StorageKey = AltText = string.Empty;
    }

    internal ProductImage(Guid id, Guid productId, string altText, int width, int height, int sortOrder)
        : base(id)
    {
        ProductId = productId;
        StorageKey = KeyPrefix(productId, id);
        AltText = altText;
        Width = width;
        Height = height;
        SortOrder = sortOrder;
    }

    public Guid ProductId { get; private set; }

    /// <summary>Key prefix <c>products/{productId}/{imageId}</c>; renditions are <c>{prefix}/{width}.webp</c>, the upload <c>{prefix}/original</c>.</summary>
    public string StorageKey { get; private set; }

    public string AltText { get; private set; }

    /// <summary>Width and height of the largest rendition.</summary>
    public int Width { get; private set; }

    public int Height { get; private set; }

    public int SortOrder { get; internal set; }

    public static string KeyPrefix(Guid productId, Guid imageId) => $"products/{productId:D}/{imageId:D}";
}
