using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Catalog.Domain.Preparations;

/// <summary>
/// How a product can be prepared (WHOLE, CURRY_CUT, FILLET, …). Reference data loaded from
/// <c>data/reference/catalog/preparation-methods.json</c>; read-only through the API.
/// </summary>
public sealed class PreparationMethod : Entity
{
    /// <summary>The preparation that sells as received; its yield is always 10 000 bp (CAT-R6).</summary>
    public const string WholeCode = "WHOLE";

    private PreparationMethod()
    {
        Code = Name = string.Empty;
    }

    public PreparationMethod(string code, string name, string? description, int sortOrder, int guidanceYieldBp)
    {
        Code = code;
        Name = name;
        Description = description;
        SortOrder = sortOrder;
        GuidanceYieldBp = guidanceYieldBp;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Advisory yield that pre-fills the admin screen; never used for reservations (RD-G6).</summary>
    public int GuidanceYieldBp { get; private set; }

    /// <summary>Applies the reference file's values; returns whether anything changed.</summary>
    public bool Sync(string name, string? description, int sortOrder, int guidanceYieldBp)
    {
        if (Name == name && Description == description && SortOrder == sortOrder && GuidanceYieldBp == guidanceYieldBp)
        {
            return false;
        }

        Name = name;
        Description = description;
        SortOrder = sortOrder;
        GuidanceYieldBp = guidanceYieldBp;
        return true;
    }
}
