namespace FreshOrbit.Inventory.Domain.ColdChain;

/// <summary>
/// Allowed temperature range of a storage unit (reference data <c>inventory/storage-units.json</c>, D11/D23).
/// A null bound is open: FREEZER ≤ −18.0 °C has no minimum.
/// </summary>
public sealed class StorageUnitRange
{
    public const string Receipt = "RECEIPT";

    public StorageUnitRange(string code, decimal? minC, decimal? maxC, string description)
    {
        Code = code;
        MinC = minC;
        MaxC = maxC;
        Description = description;
    }

    public string Code { get; private set; }

    public decimal? MinC { get; private set; }

    public decimal? MaxC { get; private set; }

    public string Description { get; private set; }

    public bool Contains(decimal temperatureC) => (MinC is null || temperatureC >= MinC) && (MaxC is null || temperatureC <= MaxC);

    public void Update(decimal? minC, decimal? maxC, string description)
    {
        MinC = minC;
        MaxC = maxC;
        Description = description;
    }
}
